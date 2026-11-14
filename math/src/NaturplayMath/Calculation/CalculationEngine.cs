using NaturplayMath.Formatting;
using NaturplayMath.Numbers;
using NaturplayMath.Numerics;
using NaturplayMath.Symbolics;
using NaturplayMath.Text;
namespace NaturplayMath.Calculation;

/// <summary>The exact expression remains reusable when display precision, base, or rounding changes.</summary>
public sealed record CalculationResult(Expression Expression, RealInterval Enclosure, string Text, NumberFormat Format);
public sealed record ComplexCalculationResult(ComplexExpression Expression, ComplexEnclosure Enclosure, string RealText, string ImaginaryText, NumberFormat Format)
{
    public string Text => ImaginaryText.StartsWith('-') ? RealText + " - " + ImaginaryText[1..] + "i" : RealText + " + " + ImaginaryText + "i";
}

public sealed record CalculationOptions
{
    public NumberFormat Format { get; }
    public ExpressionParseOptions Parsing { get; }
    public int MaxRefinements { get; }
    public CalculationOptions(NumberFormat? format = null, ExpressionParseOptions? parsing = null, int maxRefinements = 16)
    {
        if (maxRefinements < 1) throw new ArgumentOutOfRangeException(nameof(maxRefinements));
        Format = format ?? new(); Parsing = parsing ?? new(); MaxRefinements = maxRefinements;
    }
}

/// <summary>One cancellable, budgeted session from parsing through certified display digits.</summary>
public static class CalculationEngine
{
    public static CalculationResult Calculate(string input, CalculationOptions? options = null,
        IReadOnlyDictionary<string, Expression>? variables = null, ComputationContext? context = null)
        => Run(c => ExpressionParser.Parse(input, (options ?? new()).Parsing, c), options ?? new(), variables, context);
    public static CalculationResult Calculate(Expression expression, CalculationOptions? options = null,
        IReadOnlyDictionary<string, Expression>? variables = null, ComputationContext? context = null)
        => Run(_ => expression, options ?? new(), variables, context);
    public static Task<CalculationResult> CalculateAsync(string input, CalculationOptions? options = null,
        IReadOnlyDictionary<string, Expression>? variables = null, ComputationContext? context = null, CancellationToken cancellationToken = default)
    {
        var snapshot = variables?.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        return RunAsync(c => Calculate(input, options, snapshot, c), context, cancellationToken);
    }
    public static Task<CalculationResult> CalculateAsync(Expression expression, CalculationOptions? options = null,
        IReadOnlyDictionary<string, Expression>? variables = null, ComputationContext? context = null, CancellationToken cancellationToken = default)
    {
        var snapshot = variables?.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        return RunAsync(c => Calculate(expression, options, snapshot, c), context, cancellationToken);
    }
    private static async Task<T> RunAsync<T>(Func<ComputationContext, T> calculate,
        ComputationContext? context, CancellationToken cancellationToken)
    {
        context ??= ComputationContext.Default;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellationToken);
        var effective = new ComputationContext(linked.Token, context.MaxOperations, context.Progress, context.ProgressInterval);
        return await Task.Run(() => calculate(effective), linked.Token).ConfigureAwait(false);
    }
    private static CalculationResult Run(Func<ComputationContext, Expression> parse, CalculationOptions options,
        IReadOnlyDictionary<string, Expression>? variables, ComputationContext? context)
    {
        context ??= ComputationContext.Default;
        var session = context.Start(); var scoped = context.Bind(session);
        try
        {
            var expression = parse(scoped);
            session.Report(ComputationStage.Simplifying, "exact substitution and simplification", force: true);
            if (variables is not null) expression = expression.Substitute(variables, scoped);
            var (interval, text) = EvaluateDisplay(expression, options, session);
            session.Report(ComputationStage.Completed, "certified display result", force: true);
            return new(expression, interval, text, options.Format);
        }
        catch (OperationCanceledException) { session.Report(ComputationStage.Cancelled, force: true); throw; }
        catch { session.Report(ComputationStage.Failed, force: true); throw; }
    }
    private static (RealInterval Interval, string Text) EvaluateDisplay(Expression expression, CalculationOptions options, ComputationSession session)
    {
        var scoped = session.BoundContext;
        int digits = checked(options.Format.FractionalDigits + 8);
        for (int attempt = 0; attempt < options.MaxRefinements; attempt++)
        {
            session.Check();
            var interval = NumericalEvaluator.EvaluateCore(expression,
                () => new Rational(1, IntegerMath.Pow(options.Format.Radix, digits, session)), options.MaxRefinements, null, session);
            session.Report(ComputationStage.Formatting, "certifying displayed digits", force: true);
            if (interval.TryFormat(options.Format, out var text, scoped))
            {
                session.Check();
                return (interval, text);
            }
            digits = checked(digits + Math.Max(16, digits / 2));
            session.Report(ComputationStage.Refining, "rounding boundary needs more precision", force: true);
        }
        throw new InsufficientPrecisionException("The value remains on a display-rounding boundary. Simplify the expression or increase MaxRefinements.");
    }
    public static ComplexCalculationResult CalculateComplex(string input, CalculationOptions? options = null,
        IReadOnlyDictionary<string, ComplexExpression>? variables = null, ComputationContext? context = null)
    {
        options ??= new();
        return RunComplex(c => ExpressionParser.ParseComplex(input, options.Parsing, variables, c), options, context);
    }
    public static ComplexCalculationResult CalculateComplex(ComplexExpression expression, CalculationOptions? options = null, ComputationContext? context = null)
        => RunComplex(_ => expression, options ?? new(), context);
    public static Task<ComplexCalculationResult> CalculateComplexAsync(string input, CalculationOptions? options = null,
        IReadOnlyDictionary<string, ComplexExpression>? variables = null, ComputationContext? context = null, CancellationToken cancellationToken = default)
    {
        var snapshot = variables?.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        return RunAsync(c => CalculateComplex(input, options, snapshot, c), context, cancellationToken);
    }
    public static Task<ComplexCalculationResult> CalculateComplexAsync(ComplexExpression expression, CalculationOptions? options = null,
        ComputationContext? context = null, CancellationToken cancellationToken = default)
        => RunAsync(c => CalculateComplex(expression, options, c), context, cancellationToken);
    private static ComplexCalculationResult RunComplex(Func<ComputationContext, ComplexExpression> parse, CalculationOptions options, ComputationContext? context)
    {
        context ??= ComputationContext.Default;
        var session = context.Start();
        try
        {
            var expression = parse(context.Bind(session));
            var real = EvaluateDisplay(expression.Real, options, session);
            var imaginary = EvaluateDisplay(expression.Imaginary, options, session);
            session.Report(ComputationStage.Completed, "certified complex display result", force: true);
            return new(expression, new(real.Interval, imaginary.Interval), real.Text, imaginary.Text, options.Format);
        }
        catch (OperationCanceledException) { session.Report(ComputationStage.Cancelled, force: true); throw; }
        catch { session.Report(ComputationStage.Failed, force: true); throw; }
    }
    /// <summary>Writes a completed, certified result in bounded chunks; never exposes unverified leading digits.</summary>
    public static async Task WriteAsync(CalculationResult result, TextWriter destination, int chunkSize = 8192, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result); ArgumentNullException.ThrowIfNull(destination);
        if (chunkSize < 1) throw new ArgumentOutOfRangeException(nameof(chunkSize));
        for (int offset = 0; offset < result.Text.Length; offset += Math.Min(chunkSize, result.Text.Length - offset))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await destination.WriteAsync(result.Text.AsMemory(offset, Math.Min(chunkSize, result.Text.Length - offset)), cancellationToken).ConfigureAwait(false);
        }
    }
}
