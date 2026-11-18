namespace NaturplayMath.Symbolics;

public enum MathExpressionKind { Scalar, Matrix, Symbol, Call }

/// <summary>
/// Immutable, unevaluated mathematical term. Calls can remain symbolic, including matrix operations.
/// Scalar leaves use real symbols; bind complex or matrix variables before lowering a symbol to a scalar.
/// </summary>
public sealed class MathExpression : IEquatable<MathExpression>
{
    public MathExpressionKind Kind { get; }
    public ComplexExpression? Scalar { get; }
    public ExpressionMatrix? Matrix { get; }
    public string Name { get; }
    public IReadOnlyList<MathExpression> Arguments { get; }
    private readonly string key;
    private MathExpression(MathExpressionKind kind, ComplexExpression? scalar, ExpressionMatrix? matrix, string name, MathExpression[] arguments)
    {
        Kind = kind; Scalar = scalar; Matrix = matrix; Name = name; Arguments = Array.AsReadOnly(arguments);
        key = kind + ":" + name.Length + ":" + name + "(" + (kind switch
        {
            MathExpressionKind.Scalar => scalar!.Real.ToString() + ";" + scalar.Imaginary.ToString(),
            MathExpressionKind.Matrix => matrix!.Rows + "," + matrix.Columns + ":" + string.Join(";", Entries().Select(v => v.Real.ToString() + ";" + v.Imaginary.ToString())),
            _ => string.Join(";", arguments.Select(a => a.key))
        }) + ")";
    }
    public static MathExpression From(Expression value) => From(new ComplexExpression(value));
    public static MathExpression From(ComplexExpression value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(MathExpressionKind.Scalar, value, null, "", []);
    }
    public static MathExpression From(ExpressionMatrix value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(MathExpressionKind.Matrix, null, value, "", []);
    }
    public static MathExpression Symbol(string name)
    {
        _ = Expression.Symbol(name);
        return new(MathExpressionKind.Symbol, null, null, name, []);
    }
    public static MathExpression Call(string name, params MathExpression[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name is not ("+" or "-" or "*" or "/" or "^")) _ = Expression.Symbol(name);
        ArgumentNullException.ThrowIfNull(arguments);
        foreach (var argument in arguments) ArgumentNullException.ThrowIfNull(argument);
        return new(MathExpressionKind.Call, null, null, name, (MathExpression[])arguments.Clone());
    }
    internal IEnumerable<ComplexExpression> Entries()
    {
        if (Kind == MathExpressionKind.Scalar) { yield return Scalar!; yield break; }
        for (int row = 0; row < Matrix!.Rows; row++) for (int column = 0; column < Matrix.Columns; column++) yield return Matrix[row, column];
    }
    public IReadOnlyList<string> FreeSymbols => Array.AsReadOnly((Kind switch
    {
        MathExpressionKind.Symbol => new[] { Name },
        MathExpressionKind.Call => Arguments.SelectMany(a => a.FreeSymbols),
        _ => Entries().SelectMany(v => v.Real.FreeSymbols.Concat(v.Imaginary.FreeSymbols))
    }).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());

    /// <summary>Bottom-up visitor. Returns a new term; callbacks cannot mutate the original.</summary>
    public MathExpression Rewrite(Func<MathExpression, MathExpression> rule, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var session = (context ?? ComputationContext.Default).Start();
        MathExpression Visit(MathExpression term)
        {
            session.Check();
            var rebuilt = term.Kind == MathExpressionKind.Call ? Call(term.Name, term.Arguments.Select(Visit).ToArray()) : term;
            return rule(rebuilt) ?? throw new InvalidOperationException("A rewrite must return an expression.");
        }
        return Visit(this);
    }
    public bool Equals(MathExpression? other) => other is not null && key == other.key;
    public override bool Equals(object? obj) => obj is MathExpression other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(key);
    public override string ToString() => key;
}
