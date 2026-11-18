using System;
using System.Numerics;
using NaturplayMath.Computation;
namespace NaturplayMath.Numbers;

public static class IntegerMath
{
    public static BigInteger Pow(BigInteger value, BigInteger exponent, ComputationContext? context = null) => Pow(value, exponent, (context ?? ComputationContext.Default).Start());
    internal static BigInteger Pow(BigInteger value, BigInteger exponent, ComputationSession session)
    {
        if (exponent.Sign < 0) throw new ArgumentOutOfRangeException(nameof(exponent));
        if (value.IsZero && exponent.IsZero) throw new MathDomainException("Zero to the zeroth power is undefined.");
        var result = BigInteger.One;
        while (!exponent.IsZero)
        {
            session.Check();
            if (!exponent.IsEven) result *= value;
            exponent >>= 1;
            if (!exponent.IsZero) value *= value;
        }
        return result;
    }
    public static BigInteger SqrtFloor(BigInteger value, ComputationContext? context = null) => SqrtFloor(value, (context ?? ComputationContext.Default).Start());
    internal static BigInteger SqrtFloor(BigInteger value, ComputationSession session)
    {
        session.Check();
        if (value.Sign < 0) throw new MathDomainException("A real square root requires a nonnegative argument.");
        if (value <= BigInteger.One) return value;
        var length = value.GetBitLength();
        BigInteger root;
        if (length > 2048 && length <= int.MaxValue)
        {
            // An upper seed from half as many significant bits needs only a few full-size Newton steps.
            int shift = (int)((length + 3) / 4);
            root = (SqrtFloor(value >> (2 * shift), session) + 1) << shift;
        }
        else root = Pow(2, (new BigInteger(length) + 1) / 2, session);
        while (true)
        {
            session.Check();
            var next = (root + value / root) / 2;
            if (next >= root) return root;
            root = next;
        }
    }
    public static BigInteger Gcd(BigInteger a, BigInteger b, ComputationContext? context = null)
    {
        var session = (context ?? ComputationContext.Default).Start();
        a = BigInteger.Abs(a); b = BigInteger.Abs(b);
        while (!b.IsZero) { session.Check(); (a, b) = (b, a % b); }
        return a;
    }
    /// <summary>Largest integer r with r^degree &lt;= value, for nonnegative value and positive degree.</summary>
    public static BigInteger RootFloor(BigInteger value, BigInteger degree, ComputationContext? context = null)
        => RootFloor(value, degree, (context ?? ComputationContext.Default).Start());
    internal static BigInteger RootFloor(BigInteger value, BigInteger degree, ComputationSession session)
    {
        session.Check();
        if (value.Sign < 0 || degree.Sign <= 0) throw new ArgumentOutOfRangeException(value.Sign < 0 ? nameof(value) : nameof(degree));
        if (degree.IsOne || value <= 1) return value;
        if (degree == 2) return SqrtFloor(value, session);
        var bits = new BigInteger(value.GetBitLength());
        if (degree >= bits) return BigInteger.One;
        var root = Pow(2, (bits + degree - 1) / degree, session);
        while (true)
        {
            session.Check();
            var next = ((degree - 1) * root + value / Pow(root, degree - 1, session)) / degree;
            if (next >= root) return root;
            root = next;
        }
    }
    public static BigInteger Lcm(BigInteger a, BigInteger b, ComputationContext? context = null)
    {
        var gcd = Gcd(a, b, context);
        return gcd.IsZero ? BigInteger.Zero : BigInteger.Abs(a / gcd * b);
    }
}
