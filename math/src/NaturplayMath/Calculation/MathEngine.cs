using NaturplayMath.Formatting;
using NaturplayMath.Numbers;
using NaturplayMath.Numerics;
using NaturplayMath.Symbolics;
using NaturplayMath.Text;

namespace NaturplayMath.Calculation;

public sealed record EvaluationOptions
{
    public InputSyntax Syntax { get; }
    public ExpressionParseOptions Parsing { get; }
    public int MaxSteps { get; }
    public EvaluationOptions(InputSyntax syntax = InputSyntax.Canonical, AngleUnit angleUnit = AngleUnit.Radians,
        int maxSteps = 0, ExpressionParseOptions? parsing = null)
    {
        if (!Enum.IsDefined(syntax)) throw new ArgumentOutOfRangeException(nameof(syntax));
        if (maxSteps < 0) throw new ArgumentOutOfRangeException(nameof(maxSteps));
        Syntax = syntax; Parsing = parsing ?? new(angleUnit: angleUnit); MaxSteps = maxSteps;
    }
}

public sealed record MathStep(string Rule, MathExpression Before, MathExpression After);
public sealed record MathResult(MathExpression Value, IReadOnlyList<MathStep> Steps, IReadOnlyList<string> Comments);
public sealed record MathApproximation(MathExpression Value, IReadOnlyList<ComplexCalculationResult> Cells, int Rows, int Columns, bool IsMatrix);

/// <summary>Parse without evaluation, evaluate exactly, then request certified digits independently.</summary>
public static class MathEngine
{
    public static MathDocument Parse(string input, EvaluationOptions? options = null, ComputationContext? context = null)
    {
        options ??= new();
        return ExpressionParser.ParseDocument(input, options.Parsing, options.Syntax, context ?? ComputationContext.Default);
    }
    public static MathResult Evaluate(string input, EvaluationOptions? options = null,
        IReadOnlyDictionary<string, MathExpression>? variables = null, ComputationContext? context = null)
    {
        return Execute(scoped =>
        {
            var document = Parse(input, options, scoped);
            return EvaluateCore(document.Expression, options, variables, scoped) with { Comments = document.Comments };
        }, context);
    }
    public static MathResult Evaluate(MathDocument document, EvaluationOptions? options = null,
        IReadOnlyDictionary<string, MathExpression>? variables = null, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Evaluate(document.Expression, options ?? new(parsing: document.Parsing), variables, context) with { Comments = document.Comments };
    }
    public static MathResult Evaluate(MathExpression expression, EvaluationOptions? options = null,
        IReadOnlyDictionary<string, MathExpression>? variables = null, ComputationContext? context = null)
        => Execute(scoped => EvaluateCore(expression, options, variables, scoped), context);

    private static MathResult EvaluateCore(MathExpression expression, EvaluationOptions? options,
        IReadOnlyDictionary<string, MathExpression>? variables, ComputationContext context)
    {
        ArgumentNullException.ThrowIfNull(expression);
        options ??= new();
        var session = context.Start(); var scoped = context.Bind(session);
        // Angle units are desugared by the parser. Mathematical terms always use radians.
        var domain = new ComplexExpressionDomain(new(), scoped, null);
        var steps = new List<MathStep>();
        MathExpression Visit(MathExpression term, bool substitute)
        {
            session.Check();
            if (term.Kind == MathExpressionKind.Symbol)
            {
                if (substitute && variables is not null && variables.TryGetValue(term.Name, out var value)) return Visit(value, false);
                return MathExpression.From(domain.Symbol(term.Name));
            }
            if (term.Kind is MathExpressionKind.Scalar or MathExpressionKind.Matrix)
            {
                if (!substitute || variables is null) return term;
                var replacements = new Dictionary<string, Expression>(StringComparer.Ordinal);
                foreach (var name in term.FreeSymbols)
                    if (variables.TryGetValue(name, out var value))
                    {
                        var replacement = Visit(value, false);
                        var scalar = RequireScalar(replacement);
                        if (!scalar.IsReal) throw new MathDomainException("A lowered real symbol requires a real replacement. Bind complex variables in the parsed document.");
                        replacements.Add(name, scalar.Real);
                    }
                return MapScalars(term, v => v.Substitute(replacements, scoped));
            }
            var args = term.Arguments.Select(a => Visit(a, substitute)).ToArray();
            var result = Apply(term.Name, args, domain, scoped);
            if (steps.Count < options.MaxSteps && !result.Equals(term)) steps.Add(new(term.Name, term, result));
            return result;
        }
        session.Report(ComputationStage.Simplifying, "exact symbolic evaluation", force: true);
        var value = Visit(expression, true);
        return new(value, steps.AsReadOnly(), Array.Empty<string>());
    }

    private static ComplexExpression RequireScalar(MathExpression term)
        => term.Scalar ?? throw new MathDomainException("This operation requires scalar arguments.");
    private static ExpressionMatrix RequireMatrix(MathExpression term)
        => term.Matrix ?? throw new MathDomainException("This operation requires a matrix.");
    private static MathExpression MapScalars(MathExpression term, Func<ComplexExpression, ComplexExpression> map)
    {
        if (term.Kind == MathExpressionKind.Scalar) return MathExpression.From(map(term.Scalar!));
        var matrix = term.Matrix!; var values = new ComplexExpression[matrix.Rows, matrix.Columns];
        for (int row = 0; row < matrix.Rows; row++) for (int column = 0; column < matrix.Columns; column++) values[row, column] = map(matrix[row, column]);
        return MathExpression.From(new ExpressionMatrix(values));
    }
    private static void Arity(string name, MathExpression[] args, int count)
    {
        if (args.Length != count) throw new ArgumentException($"{name} requires {count} argument(s).");
    }
    private static MathExpression Apply(string name, MathExpression[] args, ComplexExpressionDomain domain, ComputationContext context)
    {
        var session = context.Start();
        var originalName = name; name = name.ToLowerInvariant();
        if (ExpressionParser.IsScalarFunction(name)) Arity(name, args, ExpressionParser.ScalarArity(name));
        else if (name is "transpose" or "adjoint" or "det" or "inverse" or "rank" or "expand" or "radicals" or "negate") Arity(name, args, 1);
        else if (name is "covariance" or "cov" or "correlation" or "rho" or "+" or "-" or "*" or "/" or "^" or "yroot") Arity(name, args, 2);
        if (args.Any(a => a.Kind is MathExpressionKind.Call or MathExpressionKind.Symbol)) return MathExpression.Call(originalName, args);
        if (name is "row" or "matrix")
        {
            if (name == "row")
            {
                var entries = new ComplexExpression[1, args.Length];
                for (int i = 0; i < args.Length; i++) entries[0, i] = RequireScalar(args[i]);
                return MathExpression.From(new ExpressionMatrix(entries));
            }
            int columns = args.Length == 0 ? 0 : RequireMatrix(args[0]).Columns;
            var values = new ComplexExpression[args.Length, columns];
            for (int row = 0; row < args.Length; row++)
            {
                session.Check(); var source = RequireMatrix(args[row]);
                if (source.Rows != 1 || source.Columns != columns) throw new MathDomainException("Matrix rows must have the same length.");
                for (int column = 0; column < columns; column++) values[row, column] = source[0, column];
            }
            return MathExpression.From(new ExpressionMatrix(values));
        }
        if (name is "mean" or "ex" or "e" or "variance" or "dx" or "d" or "samplevariance")
        {
            var samples = args.Length == 1 && args[0].Kind == MathExpressionKind.Matrix ? args[0].Entries().ToArray() : args.Select(RequireScalar).ToArray();
            var value = name is "mean" or "ex" or "e" ? SymbolicStatistics.Mean(samples, context) : SymbolicStatistics.Variance(samples, context);
            if (name == "samplevariance")
            {
                if (samples.Length < 2) throw new MathDomainException("Sample variance requires at least two observations.");
                value = value * ComplexExpression.Integer(samples.Length) / ComplexExpression.Integer(samples.Length - 1);
            }
            return MathExpression.From(value);
        }
        if (name is "covariance" or "cov" or "correlation" or "rho")
        {
            var x = RequireMatrix(args[0]); var y = RequireMatrix(args[1]);
            var a = MathExpression.From(x).Entries().ToArray(); var b = MathExpression.From(y).Entries().ToArray();
            return MathExpression.From(name is "covariance" or "cov" ? SymbolicStatistics.Covariance(a, b, context) : SymbolicStatistics.Correlation(a, b, context));
        }
        if (name is "transpose" or "adjoint" or "det" or "inverse" or "rank")
        {
            var matrix = args[0].Matrix ?? new ExpressionMatrix(new[,] { { RequireScalar(args[0]) } });
            if (name == "rank")
            {
                try { return MathExpression.From(Expression.Integer(matrix.Rank(context))); }
                catch (UndeterminedRankException) { return MathExpression.Call("rank", args); }
            }
            return name switch
            {
                "transpose" => MathExpression.From(matrix.Transpose()),
                "adjoint" => MathExpression.From(matrix.Transpose(conjugate: true)),
                "det" => MathExpression.From(matrix.Determinant(context)),
                _ => MathExpression.From(matrix.Inverse(context))
            };
        }
        if (name is "expand" or "radicals")
        {
            Expression Transform(Expression value) => name == "expand" ? value.Expand(context) : value.SimplifyRadicals(context);
            return MapScalars(args[0], v => new(Transform(v.Real), Transform(v.Imaginary)));
        }
        if (name == "negate")
        { return MapScalars(args[0], v => -v); }
        if (name is "+" or "-" or "*" or "/" or "^" or "mod" or "ncr" or "npr" or "yroot")
        {
            var a = args[0]; var b = args[1];
            if (a.Kind == MathExpressionKind.Scalar && b.Kind == MathExpressionKind.Scalar) return MathExpression.From(domain.Binary(name, a.Scalar!, b.Scalar!));
            return name switch
            {
                "+" => MathExpression.From(RequireMatrix(a).Add(RequireMatrix(b), context)),
                "-" => MathExpression.From(RequireMatrix(a).Add(RequireMatrix(b).Scale(ComplexExpression.Integer(-1), context), context)),
                "*" when a.Kind == MathExpressionKind.Scalar => MathExpression.From(RequireMatrix(b).Scale(a.Scalar!, context)),
                "*" when b.Kind == MathExpressionKind.Scalar => MathExpression.From(RequireMatrix(a).Scale(b.Scalar!, context)),
                "*" => MathExpression.From(RequireMatrix(a).Multiply(RequireMatrix(b), context)),
                "/" => MathExpression.From(RequireMatrix(a).Scale(ComplexExpression.One / RequireScalar(b), context)),
                "^" => MathExpression.From(RequireMatrix(a).Pow(Integer(RequireScalar(b)), context)),
                _ => throw new MathDomainException("This operator is not defined for matrices.")
            };
        }
        if (!ExpressionParser.IsScalarFunction(name)) return MathExpression.Call(originalName, args);
        return MathExpression.From(domain.Function(name, args.Select(RequireScalar).ToArray(), 0));
    }
    private static System.Numerics.BigInteger Integer(ComplexExpression value)
    {
        if (!value.IsReal || !value.Real.TryGetRational(out var rational) || !rational.IsInteger) throw new MathDomainException("An exact integer is required.");
        return rational.Numerator;
    }

    public static MathApproximation Approximate(MathExpression value, NumberFormat? format = null, ComputationContext? context = null)
        => Execute(scoped => ApproximateCore(value, format, scoped), context);

    private static MathApproximation ApproximateCore(MathExpression value, NumberFormat? format, ComputationContext context)
    {
        ArgumentNullException.ThrowIfNull(value);
        var session = context.Start();
        var ready = EvaluateCore(value, null, null, context).Value;
        var options = new CalculationOptions(format);
        if (ready.Kind == MathExpressionKind.Call)
        {
            var domain = new ComplexExpressionDomain(new(), context, null);
            ready = ready.Rewrite(term =>
            {
                if (term.Kind != MathExpressionKind.Call) return term;
                if (term.Name.Equals("rank", StringComparison.OrdinalIgnoreCase)
                    && term.Arguments[0].Kind is MathExpressionKind.Scalar or MathExpressionKind.Matrix)
                {
                    var argument = term.Arguments[0];
                    var matrix = argument.Matrix ?? new ExpressionMatrix(new[,] { { argument.Scalar! } });
                    var target = new Rational(1, IntegerMath.Pow(options.Format.Radix, checked(options.Format.FractionalDigits + 8), context));
                    return MathExpression.From(Expression.Integer(MatrixNumericalExtensions.RankCore(matrix, new(target), null, session)));
                }
                return Apply(term.Name, term.Arguments.ToArray(), domain, context);
            }, context);
        }
        if (ready.Kind is not (MathExpressionKind.Scalar or MathExpressionKind.Matrix))
            throw new InsufficientPrecisionException("An unevaluated symbolic operation has no certified numeric value.");
        var cells = new List<ComplexCalculationResult>();
        foreach (var entry in ready.Entries())
        {
            session.Check();
            var real = CalculationEngine.EvaluateDisplay(entry.Real, options, session);
            var imaginary = CalculationEngine.EvaluateDisplay(entry.Imaginary, options, session);
            cells.Add(new(entry, new(real.Interval, imaginary.Interval), real.Text, imaginary.Text, options.Format));
        }
        return new(value, cells.AsReadOnly(), ready.Matrix?.Rows ?? 1, ready.Matrix?.Columns ?? 1, ready.Kind == MathExpressionKind.Matrix);
    }
    private static T Execute<T>(Func<ComputationContext, T> operation, ComputationContext? context)
    {
        context ??= ComputationContext.Default;
        var session = context.Start();
        try
        {
            var result = operation(context.Bind(session));
            session.Report(ComputationStage.Completed, force: true);
            return result;
        }
        catch (OperationCanceledException) { session.Report(ComputationStage.Cancelled, force: true); throw; }
        catch { session.Report(ComputationStage.Failed, force: true); throw; }
    }
    public static Task<MathApproximation> ApproximateAsync(MathExpression value, NumberFormat? format = null,
        ComputationContext? context = null, CancellationToken cancellationToken = default)
        => CalculationEngine.RunAsync(c => Approximate(value, format, c), context, cancellationToken);
    public static Task<MathResult> EvaluateAsync(string input, EvaluationOptions? options = null,
        IReadOnlyDictionary<string, MathExpression>? variables = null, ComputationContext? context = null, CancellationToken cancellationToken = default)
    {
        var snapshot = variables?.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        return CalculationEngine.RunAsync(c => Evaluate(input, options, snapshot, c), context, cancellationToken);
    }
    public static Task<MathApproximation> CalculateAsync(string input, NumberFormat? format = null, EvaluationOptions? options = null,
        IReadOnlyDictionary<string, MathExpression>? variables = null, ComputationContext? context = null, CancellationToken cancellationToken = default)
    {
        var snapshot = variables?.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        MathApproximation Calculate(ComputationContext scoped)
        {
            var document = Parse(input, options, scoped);
            var exact = EvaluateCore(document.Expression, options, snapshot, scoped);
            return ApproximateCore(exact.Value, format, scoped);
        }
        return CalculationEngine.RunAsync(c => Execute(Calculate, c), context, cancellationToken);
    }
    /// <summary>Streams certified digits without building a display AST or concatenating a whole matrix.</summary>
    public static async Task WriteAsync(MathApproximation result, TextWriter destination, int chunkSize = 8192, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result); ArgumentNullException.ThrowIfNull(destination);
        if (chunkSize < 1) throw new ArgumentOutOfRangeException(nameof(chunkSize));
        async Task Write(string text)
        {
            for (int offset = 0; offset < text.Length; offset += Math.Min(chunkSize, text.Length - offset))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await destination.WriteAsync(text.AsMemory(offset, Math.Min(chunkSize, text.Length - offset)), cancellationToken).ConfigureAwait(false);
            }
        }
        bool matrix = result.IsMatrix;
        if (matrix) await Write("(").ConfigureAwait(false);
        for (int i = 0; i < result.Cells.Count; i++)
        {
            if (i != 0) await Write(i % result.Columns == 0 ? "; " : ", ").ConfigureAwait(false);
            var cell = result.Cells[i];
            await Write(cell.RealText).ConfigureAwait(false);
            if (!cell.Expression.IsReal)
            {
                await Write(" + (").ConfigureAwait(false);
                await Write(cell.ImaginaryText).ConfigureAwait(false);
                await Write(")i").ConfigureAwait(false);
            }
        }
        if (matrix) await Write(")").ConfigureAwait(false);
    }
}
