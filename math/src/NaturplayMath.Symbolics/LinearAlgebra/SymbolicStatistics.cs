namespace NaturplayMath.Symbolics;

/// <summary>Population moments, preserving NaturplayCalculator's non-conjugate complex covariance convention.</summary>
public static class SymbolicStatistics
{
    public static ComplexExpression Mean(IReadOnlyList<ComplexExpression> values, ComputationContext? context = null)
    {
        Validate(values);
        var session = (context ?? ComputationContext.Default).Start();
        var sum = ComplexExpression.Zero;
        foreach (var value in values) { session.Check(); sum += value; }
        return sum / ComplexExpression.Integer(values.Count);
    }
    public static ComplexExpression Variance(IReadOnlyList<ComplexExpression> values, ComputationContext? context = null)
        => Covariance(values, values, context);
    public static ComplexExpression Covariance(IReadOnlyList<ComplexExpression> x, IReadOnlyList<ComplexExpression> y, ComputationContext? context = null)
    {
        Validate(x); Validate(y);
        if (x.Count != y.Count) throw new ArgumentException("Samples must have equal length.");
        context ??= ComputationContext.Default;
        var session = context.Start(); var scoped = context.Bind(session);
        var mx = Mean(x, scoped); var my = Mean(y, scoped); var sum = ComplexExpression.Zero;
        for (int i = 0; i < x.Count; i++) { session.Check(); sum += (x[i] - mx) * (y[i] - my); }
        return sum / ComplexExpression.Integer(x.Count);
    }
    public static ComplexExpression Correlation(IReadOnlyList<ComplexExpression> x, IReadOnlyList<ComplexExpression> y, ComputationContext? context = null)
        => Covariance(x, y, context) / (Variance(x, context).Sqrt() * Variance(y, context).Sqrt());
    private static void Validate(IReadOnlyList<ComplexExpression> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0) throw new ArgumentException("A sample must not be empty.", nameof(values));
        foreach (var value in values) ArgumentNullException.ThrowIfNull(value);
    }
}
