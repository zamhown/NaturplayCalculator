using System.Numerics;
namespace NaturplayMath.Numerics;

internal sealed partial class BinaryKernel
{
    internal (BinaryInterval Real, BinaryInterval Imaginary) ComplexPow(BinaryInterval real, BinaryInterval imaginary, BinaryInterval p, BinaryInterval q)
    {
        var norm = Pow(real, 2) + Pow(imaginary, 2);
        if (norm.Upper.IsZero)
        {
            if (q.Lower.IsZero && q.Upper.IsZero && p.Lower.Sign > 0) return (Zero, Zero);
            if (!q.ContainsZero || p.Upper.Sign <= 0) throw new MathDomainException("Zero to a complex power requires a positive real exponent.");
            throw new RefinementNeededException();
        }
        var logRadius = Ln(norm).Divide(2);
        var argument = Atan2(imaginary, real);
        var magnitude = Exp(p * logRadius - q * argument);
        var angle = SinCos(q * logRadius + p * argument);
        return (magnitude * angle.Cos, magnitude * angle.Sin);
    }
    internal BinaryInterval Atan2(BinaryInterval y, BinaryInterval x)
    {
        if (y.Lower.IsZero && y.Upper.IsZero)
        {
            if (x.Lower.Sign > 0) return Zero;
            if (x.Upper.Sign < 0) return Pi();
            if (x.Width.IsZero) throw new MathDomainException("The complex argument is undefined at zero.");
            throw new RefinementNeededException();
        }
        if (y.Lower.Sign > 0) return Pi().Divide(2) - Atan(x / y);
        if (y.Upper.Sign < 0) return -Pi().Divide(2) - Atan(x / y);
        if (x.Lower.Sign > 0) return Atan(y / x);
        if (x.Upper.Sign < 0 && y.Lower.Sign >= 0) return Pi() + Atan(y / x);
        // A rectangle crossing the negative real axis crosses the principal branch cut.
        throw new RefinementNeededException();
    }
    internal (BinaryInterval Real, BinaryInterval Imaginary) ComplexSqrt(BinaryInterval real, BinaryInterval imaginary)
    {
        var radius = Sqrt(Pow(real, 2) + Pow(imaginary, 2));
        var positive = (radius + real).Divide(2);
        positive = positive.Intersect(0, BigInteger.Max(0, positive.Upper));
        var u = Sqrt(positive);
        if (u.Lower.Sign > 0) return (u, imaginary / u.Times(2));
        var negative = (radius - real).Divide(2);
        negative = negative.Intersect(0, BigInteger.Max(0, negative.Upper));
        var v = Sqrt(negative);
        if (imaginary.Lower.Sign >= 0) return (u, v);
        if (imaginary.Upper.Sign < 0) return (u, -v);
        throw new RefinementNeededException();
    }
}
