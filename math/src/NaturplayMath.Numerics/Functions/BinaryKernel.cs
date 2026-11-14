using System.Numerics;
using NaturplayMath.Numbers;
namespace NaturplayMath.Numerics;

internal sealed partial class BinaryKernel
{
    internal int Precision { get; }
    internal BigInteger Scale { get; }
    private readonly ComputationSession session;
    private BinaryInterval? pi, logTwo;
    internal BinaryInterval One => new(Scale, Scale, Precision);
    internal BinaryInterval Zero => new(0, 0, Precision);
    internal BinaryInterval From(Rational value) => BinaryInterval.From(value, Precision);
    internal BinaryKernel(int precision, ComputationSession session)
    { Precision = precision; Scale = BigInteger.One << precision; this.session = session; }
    internal BinaryInterval Sqrt(BinaryInterval value)
    {
        session.Check();
        if (value.Upper.Sign < 0) throw new MathDomainException("A real square root requires a nonnegative argument.");
        if (value.Lower.Sign < 0) throw new RefinementNeededException();
        return RootProduct(value, One);
    }
    private BinaryInterval RootProduct(BinaryInterval a, BinaryInterval b)
    {
        var lowProduct = a.Lower * b.Lower; var highProduct = a.Upper * b.Upper;
        var lower = IntegerMath.SqrtFloor(lowProduct, session);
        BigInteger upper;
        if (lower.Sign > 0)
        {
            // Concavity: sqrt(L^2+d) <= L+d/(2L). For narrow input intervals this
            // avoids computing a second full integer square root and remains an outward bound.
            upper = lower + BinaryInterval.CeilingDiv(highProduct - lower * lower, 2 * lower);
        }
        else
        {
            upper = IntegerMath.SqrtFloor(highProduct, session);
            if (upper * upper != highProduct) upper++;
        }
        return new(lower, upper, Precision);
    }
    internal BinaryInterval Pow(BinaryInterval value, BigInteger exponent)
    {
        session.Check();
        if (exponent.Sign <= 0 && value.ContainsZero) throw new RefinementNeededException();
        if (exponent.Sign < 0) { value = value.Reciprocal(); exponent = -exponent; }
        var result = One;
        while (exponent > 0)
        {
            session.Check();
            if (!exponent.IsEven) result *= value;
            exponent >>= 1;
            if (exponent > 0)
            {
                var l = value.Lower * value.Lower; var u = value.Upper * value.Upper;
                value = new(value.ContainsZero ? 0 : BigInteger.Min(l, u) >> Precision,
                    -((-BigInteger.Max(l, u)) >> Precision), Precision);
            }
        }
        return result;
    }
    internal BinaryInterval Root(BinaryInterval value, BigInteger degree)
    {
        if (degree.IsOne) return value;
        if (degree == 2) return Sqrt(value);
        if (degree.IsEven && value.Upper.Sign < 0) throw new MathDomainException("An even real root requires a nonnegative argument.");
        if (degree.IsEven && value.Lower.Sign < 0) throw new RefinementNeededException();
        BinaryInterval Point(BigInteger units)
        {
            session.Check();
            if (units.IsZero) return Zero;
            if (units.Sign < 0) return -Point(-units);
            if (degree <= 16)
            {
                var scaled = units << checked(Precision * ((int)degree - 1));
                var lower = IntegerMath.RootFloor(scaled, degree, session);
                var upper = IntegerMath.Pow(lower, degree, session) == scaled ? lower : lower + 1;
                return new(lower, upper, Precision);
            }
            return Exp(Ln(new BinaryInterval(units, units, Precision)).Divide(degree));
        }
        var low = Point(value.Lower);
        if (value.Lower == value.Upper) return low;
        var high = Point(value.Upper);
        return new(low.Lower, high.Upper, Precision);
    }
    internal BinaryInterval Pi()
    {
        if (pi.HasValue) return pi.Value;
        // The absolute Chudnovsky term ratio is < 2^-40, and the first term is < 2^24.
        int terms = checked((Precision + 64) / 40 + 1);
        int done = 0;
        (BigInteger P, BigInteger Q, BigInteger T) Split(int a, int b)
        {
            session.Check();
            if (b - a == 1)
            {
                done++;
                session.Report(ComputationStage.Evaluating, "pi: binary splitting", Precision, done, terms);
                if (a == 0) return (1, 1, 13591409);
                BigInteger k = a;
                var p = (6 * k - 5) * (2 * k - 1) * (6 * k - 1);
                var q = k * k * k * BigInteger.Parse("10939058860032000");
                var t = p * (13591409 + 545140134 * k);
                return (p, q, a % 2 == 0 ? t : -t);
            }
            int m = a + (b - a) / 2;
            var left = Split(a, m); var right = Split(m, b);
            return (left.P * right.P, left.Q * right.Q, left.T * right.Q + left.P * right.T);
        }
        var sum = Split(0, terms);
        var denominator = new BinaryInterval(BinaryInterval.FloorDiv(sum.T << Precision, sum.Q) - 1,
            BinaryInterval.CeilingDiv(sum.T << Precision, sum.Q) + 1, Precision);
        pi = Sqrt(From(new Rational(10005))).Times(426880) / denominator;
        return pi.Value;
    }
    internal BinaryInterval Ln(BinaryInterval value)
    {
        session.Check();
        if (value.Upper.Sign <= 0) throw new MathDomainException("A real logarithm requires a positive argument.");
        if (value.Lower.Sign <= 0) throw new RefinementNeededException();
        if (value.Lower == Scale && value.Upper == Scale) return Zero;
        var midpoint = (value.Lower + value.Upper) / 2;
        var central = LnPoint(midpoint);
        if (value.Lower == value.Upper) return central;
        // ln'(t)=1/t <=1/lower on the whole input interval.
        var radius = BigInteger.Max(midpoint - value.Lower, value.Upper - midpoint);
        var error = BinaryInterval.CeilingDiv(radius << Precision, value.Lower);
        return central.Widen(error);
    }
    private BinaryInterval LnPoint(BigInteger units)
    {
        if (units == Scale) return Zero;
        // Choose log2(y) near Precision/3 to balance b^2 truncation and relative rounding of b.
        // K=pi/(2*AGM(1,b)) satisfies K*(1-b*b) <= ln(y) <= K (DLMF 19.12.1).
        long exponent = units.GetBitLength() - Precision - 1;
        long m = (long)Precision / 3 + 32 - exponent;
        var numerator = Scale * Scale * 4;
        var denominator = units;
        if (m >= 0) denominator = Shift(denominator, m); else numerator = Shift(numerator, -m);
        var b = new BinaryInterval(BinaryInterval.FloorDiv(numerator, denominator), BinaryInterval.CeilingDiv(numerator, denominator), Precision);
        return LogFromComplement(b) - LogTwo().Times(new BigInteger(m));
    }
    private BigInteger Shift(BigInteger value, long bits)
    {
        while (bits > int.MaxValue) { session.Check(); value <<= int.MaxValue; bits -= int.MaxValue; }
        return value << (int)bits;
    }
    private BinaryInterval LogTwo()
    {
        if (logTwo.HasValue) return logTwo.Value;
        int m = checked(Precision / 3 + 32);
        var b = new BinaryInterval(Scale >> (m - 2), Scale >> (m - 2), Precision);
        logTwo = LogFromComplement(b).Divide(m);
        return logTwo.Value;
    }
    private BinaryInterval LogFromComplement(BinaryInterval b)
    {
        if (b.Lower.Sign <= 0) throw new RefinementNeededException();
        var a = One; var g = b;
        var previousGap = a.Upper - g.Lower;
        int iteration = 0;
        while (true)
        {
            session.Check();
            session.Report(ComputationStage.Evaluating, "ln: arithmetic-geometric mean", Precision, iteration++);
            var nextA = (a + g).Divide(2);
            var nextG = RootProduct(a, g);
            a = nextA; g = nextG;
            var gap = a.Upper - g.Lower;
            if (gap <= 8 || gap >= previousGap) break;
            previousGap = gap;
        }
        var mean = new BinaryInterval(g.Lower, a.Upper, Precision);
        var k = Pi() / mean.Times(2);
        var lower = k * (One - b * b);
        return new(lower.Lower, k.Upper, Precision);
    }
}
