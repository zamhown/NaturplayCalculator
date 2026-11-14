using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
using System.Numerics;
namespace NaturplayMath.Numerics;

/// <summary>Opt-in numerical operations, kept outside the exact symbolic assembly.</summary>
public static class NumericalExtensions
{
    public static Task<RealInterval> ApproximateAsync(this Expression expression, int fractionalDigits, BigInteger? radix = null,
        IReadOnlyDictionary<string, Rational>? variables = null, ComputationContext? context = null,
        CancellationToken cancellationToken = default, int maxRefinements = 16)
    {
        var snapshot = variables?.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        return NumericalEvaluator.RunAsync(effective => expression.Approximate(fractionalDigits, radix, snapshot, effective, maxRefinements),
            context, cancellationToken);
    }
    public static RealInterval Approximate(this Expression expression, int fractionalDigits, BigInteger? radix = null,
        IReadOnlyDictionary<string, Rational>? variables = null, ComputationContext? context = null, int maxRefinements = 16)
    {
        ArgumentNullException.ThrowIfNull(expression);
        if (fractionalDigits < 0) throw new ArgumentOutOfRangeException(nameof(fractionalDigits));
        radix ??= 10;
        if (radix < 2) throw new ArgumentOutOfRangeException(nameof(radix));
        if (maxRefinements < 1) throw new ArgumentOutOfRangeException(nameof(maxRefinements));
        var session = (context ?? ComputationContext.Default).Start();
        return NumericalEvaluator.Evaluate(expression, () => new Rational(1, IntegerMath.Pow(radix.Value, fractionalDigits, session)),
            maxRefinements, variables, session);
    }
}
