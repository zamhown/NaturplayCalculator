using System.Numerics;
namespace NaturplayMath.Numbers;

/// <summary>Exact discrete operations with arbitrary-size integer arguments.</summary>
public static class Combinatorics
{
    public static BigInteger Factorial(BigInteger n, ComputationContext? context = null)
        => Factorial(n, false, (context ?? ComputationContext.Default).Start());
    public static BigInteger DoubleFactorial(BigInteger n, ComputationContext? context = null)
        => Factorial(n, true, (context ?? ComputationContext.Default).Start());
    internal static BigInteger Factorial(BigInteger n, bool twice, ComputationSession session)
    {
        if (n.Sign < 0) throw new MathDomainException("Factorial requires a nonnegative integer.");
        var count = twice ? (n + 1) / 2 : n;
        return Product(twice && n.IsEven ? 2 : 1, twice ? 2 : 1, count, session);
    }
    public static BigInteger Choose(BigInteger n, BigInteger k, ComputationContext? context = null)
        => Choose(n, k, (context ?? ComputationContext.Default).Start());
    internal static BigInteger Choose(BigInteger n, BigInteger k, ComputationSession session)
    {
        Validate(n, k);
        k = BigInteger.Min(k, n - k);
        return Product(n - k + 1, 1, k, session) / Product(1, 1, k, session);
    }
    public static BigInteger Permute(BigInteger n, BigInteger k, ComputationContext? context = null)
        => Permute(n, k, (context ?? ComputationContext.Default).Start());
    internal static BigInteger Permute(BigInteger n, BigInteger k, ComputationSession session)
    {
        Validate(n, k);
        return Product(n - k + 1, 1, k, session);
    }
    private static void Validate(BigInteger n, BigInteger k)
    {
        if (n.Sign < 0 || k.Sign < 0 || k > n) throw new MathDomainException("Combinatorics requires integers with 0 <= k <= n.");
    }
    private static BigInteger Product(BigInteger first, BigInteger step, BigInteger count, ComputationSession session)
    {
        BigInteger done = 0;
        BigInteger Split(BigInteger start, BigInteger length)
        {
            session.Check();
            if (length <= 32)
            {
                BigInteger product = 1;
                for (BigInteger i = 0; i < length; i++) { session.Check(); product *= start + i * step; }
                done += length;
                session.Report(ComputationStage.Simplifying, "exact integer product", work: done, total: count);
                return product;
            }
            var half = length / 2;
            return Split(start, half) * Split(start + half * step, length - half);
        }
        return Split(first, count);
    }
}
