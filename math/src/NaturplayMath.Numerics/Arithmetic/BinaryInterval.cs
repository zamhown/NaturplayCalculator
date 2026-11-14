using System.Numerics;
using NaturplayMath.Numbers;
namespace NaturplayMath.Numerics;

// Endpoints are integers divided by 2^Precision. Every discarded bit is rounded outward.
internal readonly record struct BinaryInterval(BigInteger Lower, BigInteger Upper, int Precision)
{
    internal BigInteger Scale => BigInteger.One << Precision;
    internal bool ContainsZero => Lower.Sign <= 0 && Upper.Sign >= 0;
    internal BigInteger Width => Upper - Lower;
    internal RealInterval ToRational() => new(new Rational(Lower, Scale), new Rational(Upper, Scale));
    internal static BinaryInterval From(Rational value, int precision)
    {
        var scaled = value.Numerator << precision;
        return new(FloorDiv(scaled, value.Denominator), CeilingDiv(scaled, value.Denominator), precision);
    }
    internal static BigInteger FloorDiv(BigInteger n, BigInteger d)
    {
        if (d.Sign < 0) { n = -n; d = -d; }
        var q = BigInteger.DivRem(n, d, out var r);
        return r.Sign < 0 ? q - 1 : q;
    }
    internal static BigInteger CeilingDiv(BigInteger n, BigInteger d) => -FloorDiv(-n, d);
    private static void Same(BinaryInterval a, BinaryInterval b)
    {
        if (a.Precision != b.Precision) throw new InvalidOperationException("Mismatched internal precision.");
    }
    public static BinaryInterval operator +(BinaryInterval a, BinaryInterval b)
    { Same(a, b); return new(a.Lower + b.Lower, a.Upper + b.Upper, a.Precision); }
    public static BinaryInterval operator -(BinaryInterval a) => new(-a.Upper, -a.Lower, a.Precision);
    public static BinaryInterval operator -(BinaryInterval a, BinaryInterval b) => a + -b;
    public static BinaryInterval operator *(BinaryInterval a, BinaryInterval b)
    {
        Same(a, b);
        var p = a.Lower * b.Lower; var q = a.Lower * b.Upper;
        var r = a.Upper * b.Lower; var s = a.Upper * b.Upper;
        var lower = BigInteger.Min(BigInteger.Min(p, q), BigInteger.Min(r, s));
        var upper = BigInteger.Max(BigInteger.Max(p, q), BigInteger.Max(r, s));
        return new(lower >> a.Precision, -((-upper) >> a.Precision), a.Precision);
    }
    internal BinaryInterval Times(BigInteger factor) => factor.Sign >= 0
        ? new(Lower * factor, Upper * factor, Precision) : new(Upper * factor, Lower * factor, Precision);
    internal BinaryInterval Divide(BigInteger divisor)
    {
        if (divisor.IsZero) throw new DivideByZeroException();
        return divisor.Sign > 0 ? new(FloorDiv(Lower, divisor), CeilingDiv(Upper, divisor), Precision) : -Divide(-divisor);
    }
    internal BinaryInterval Reciprocal()
    {
        if (ContainsZero) throw new RefinementNeededException();
        var squareScale = BigInteger.One << checked(2 * Precision);
        return new(FloorDiv(squareScale, Upper), CeilingDiv(squareScale, Lower), Precision);
    }
    public static BinaryInterval operator /(BinaryInterval a, BinaryInterval b) => a * b.Reciprocal();
    internal BinaryInterval Widen(BigInteger units) => new(Lower - units, Upper + units, Precision);
    internal BinaryInterval Abs() => Lower.Sign >= 0 ? this : Upper.Sign <= 0 ? -this
        : new(BigInteger.Zero, BigInteger.Max(-Lower, Upper), Precision);
    internal BinaryInterval Intersect(BigInteger lower, BigInteger upper)
    {
        var l = BigInteger.Max(Lower, lower); var u = BigInteger.Min(Upper, upper);
        if (l > u) throw new InvalidOperationException("An internal enclosure became empty.");
        return new(l, u, Precision);
    }
}
internal sealed class RefinementNeededException(int? requiredPrecision = null) : Exception
{
    internal int? RequiredPrecision { get; } = requiredPrecision;
}
