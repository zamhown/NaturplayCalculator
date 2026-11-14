using System.Numerics;
namespace NaturplayMath.Numerics;

internal sealed partial class BinaryKernel
{
    internal BinaryInterval Atan(BinaryInterval value)
    {
        var middle = (value.Lower + value.Upper) / 2;
        var radius = BigInteger.Max(middle - value.Lower, value.Upper - middle);
        return AtanPoint(middle).Widen(radius); // |atan'| <= 1 globally.
    }
    private BinaryInterval AtanPoint(BigInteger units)
    {
        if (units.IsZero) return Zero;
        if (units.Sign < 0) return -AtanPoint(-units);
        var x = new BinaryInterval(units, units, Precision);
        if (units > Scale) return Pi().Divide(2) - AtanSmall(x.Reciprocal());
        return AtanSmall(x);
    }
    private BinaryInterval AtanSmall(BinaryInterval value)
    {
        BigInteger y = 0;
        int oldPrecision = 0;
        for (int bits = Math.Min(64, Precision); ; bits = (int)Math.Min((long)bits * 2, Precision))
        {
            session.Check();
            var local = bits == Precision ? this : new BinaryKernel(bits, session);
            int shift = Precision - bits;
            var x = new BinaryInterval(value.Lower >> shift, -((-value.Upper) >> shift), bits);
            y <<= bits - oldPrecision;
            for (int iteration = 0; iteration < 4; iteration++)
            {
                session.Report(ComputationStage.Evaluating, "atan: precision-doubling Newton iteration", bits, iteration, 4);
                var pair = local.SinCos(new BinaryInterval(y, y, bits));
                var tangent = pair.Sin / pair.Cos;
                var correction = (x - tangent) / (local.One + tangent * tangent);
                y += (correction.Lower + correction.Upper) / 2;
                y = BigInteger.Max(-local.Scale, BigInteger.Min(local.Scale, y));
            }
            oldPrecision = bits;
            if (bits == Precision) break;
        }
        var finalPair = SinCos(new BinaryInterval(y, y, Precision));
        var residual = value - finalPair.Sin / finalPair.Cos;
        var error = BigInteger.Max(BigInteger.Abs(residual.Lower), BigInteger.Abs(residual.Upper));
        // y lies in [-1,1], strictly inside the principal atan branch. The inverse is 1-Lipschitz.
        return new BinaryInterval(y, y, Precision).Widen(error);
    }
    internal BinaryInterval Asin(BinaryInterval value)
    {
        if (value.Upper < -Scale || value.Lower > Scale) throw new MathDomainException("A real arcsine requires an argument in [-1,1].");
        if (value.Lower < -Scale || value.Upper > Scale) throw new RefinementNeededException();
        BinaryInterval Point(BigInteger units)
        {
            if (units == Scale) return Pi().Divide(2);
            if (units == -Scale) return -Pi().Divide(2);
            var x = new BinaryInterval(units, units, Precision);
            return Atan(x / Sqrt(One - x * x));
        }
        var lower = Point(value.Lower);
        if (value.Lower == value.Upper) return lower;
        var upper = Point(value.Upper);
        return new(lower.Lower, upper.Upper, Precision);
    }
    internal BinaryInterval Asinh(BinaryInterval value)
    {
        BinaryInterval Point(BigInteger units)
        {
            if (units.Sign < 0) return -Point(-units);
            var x = new BinaryInterval(units, units, Precision);
            return Ln(x + Sqrt(x * x + One));
        }
        var lower = Point(value.Lower);
        if (value.Lower == value.Upper) return lower;
        var upper = Point(value.Upper);
        return new(lower.Lower, upper.Upper, Precision);
    }
    internal BinaryInterval Tanh(BinaryInterval value)
    {
        BinaryInterval Point(BigInteger units)
        {
            if (units.Sign < 0) return -Point(-units);
            var e = ExpPoint(-2 * units);
            return (One - e) / (One + e);
        }
        var lower = Point(value.Lower);
        if (value.Lower == value.Upper) return lower;
        var upper = Point(value.Upper);
        return new(lower.Lower, upper.Upper, Precision);
    }
}
