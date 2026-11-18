using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
namespace NaturplayMath.Numerics;

/// <summary>A rectangular enclosure, with independent exact bounds on real and imaginary parts.</summary>
public sealed record ComplexEnclosure(RealInterval Real, RealInterval Imaginary);

public static class ComplexNumericalExtensions
{
    public static ComplexEnclosure Approximate(this ComplexExpression expression, int fractionalDigits,
        System.Numerics.BigInteger? radix = null, IReadOnlyDictionary<string, Rational>? variables = null, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        if (fractionalDigits < 0) throw new ArgumentOutOfRangeException(nameof(fractionalDigits));
        var basis = radix ?? 10;
        if (basis < 2) throw new ArgumentOutOfRangeException(nameof(radix));
        var session = (context ?? ComputationContext.Default).Start();
        try
        {
            Rational Target() => new(1, IntegerMath.Pow(basis, fractionalDigits, session));
            var real = NumericalEvaluator.EvaluateCore(expression.Real, Target, 16, variables, session);
            var imaginary = NumericalEvaluator.EvaluateCore(expression.Imaginary, Target, 16, variables, session);
            session.Report(ComputationStage.Completed, "certified complex enclosure", force: true);
            return new(real, imaginary);
        }
        catch (OperationCanceledException) { session.Report(ComputationStage.Cancelled, force: true); throw; }
        catch { session.Report(ComputationStage.Failed, force: true); throw; }
    }
    public static async Task<ComplexEnclosure> ApproximateAsync(this ComplexExpression expression, int fractionalDigits,
        System.Numerics.BigInteger? radix = null, IReadOnlyDictionary<string, Rational>? variables = null,
        ComputationContext? context = null, CancellationToken cancellationToken = default)
    {
        var snapshot = variables?.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        context ??= ComputationContext.Default;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellationToken);
        var effective = new ComputationContext(linked.Token, context.MaxOperations, context.Progress, context.ProgressInterval);
        return await Task.Run(() => expression.Approximate(fractionalDigits, radix, snapshot, effective), linked.Token).ConfigureAwait(false);
    }
}
