using System.Numerics;
using NaturplayMath.Numbers;
using Xunit;

namespace NaturplayMath.Tests.Numerics;

public class KernelBoundaryTests
{
    private static BinaryKernel Kernel(int bits = 256) => new(bits, new ComputationContext().Start());
    private static void ContainsReference(BinaryInterval interval, double value)
    {
        var bounds = interval.ToRational();
        double low = (double)bounds.Lower.Numerator / (double)bounds.Lower.Denominator;
        double high = (double)bounds.Upper.Numerator / (double)bounds.Upper.Denominator;
        double tolerance = Math.Max(1, Math.Abs(value)) * 2e-12;
        Assert.True(low <= value + tolerance && high >= value - tolerance, $"[{low:R}, {high:R}] does not enclose {value:R}");
    }

    [Fact]
    public void ReducedSeriesAndChunkedShiftsHaveExplicitBoundaryContracts()
    {
        var k = Kernel();
        Assert.Equal(BigInteger.Zero, k.Shift(BigInteger.Zero, (long)int.MaxValue + 1));
        Assert.Equal(new BigInteger(96), k.Shift(new BigInteger(3), 5));
        Assert.Equal(k.Zero, k.Series(0, 4, BinaryKernel.SeriesKind.Sin));
        Assert.Equal(k.One, k.Series(0, 4, BinaryKernel.SeriesKind.Cos));
        Assert.Equal(k.One, k.Series(0, 4, BinaryKernel.SeriesKind.Exp));
        Assert.Throws<InvalidOperationException>(() => k.Series(-1, 4, BinaryKernel.SeriesKind.Sin));
        Assert.Throws<InvalidOperationException>(() => k.Series(17, 4, BinaryKernel.SeriesKind.Cos));
        for (int n = 1; n <= 16; n++)
        {
            ReferenceAssert.Near(Math.Sin(n / 16.0), k.Series(n, 4, BinaryKernel.SeriesKind.Sin).ToRational());
            ReferenceAssert.Near(Math.Cos(n / 16.0), k.Series(n, 4, BinaryKernel.SeriesKind.Cos).ToRational());
            ReferenceAssert.Near(Math.Exp(n / 16.0), k.Series(n, 4, BinaryKernel.SeriesKind.Exp).ToRational());
        }
    }

    [Fact]
    public void SquareRootAndPowersHandleZeroCrossingAndDirectedEndpoints()
    {
        var k = Kernel();
        Assert.Equal(k.Zero, k.Sqrt(k.Zero));
        var small = k.Sqrt(new(0, 3, k.Precision));
        Assert.Equal(BigInteger.Zero, small.Lower);
        Assert.True(small.Upper * small.Upper >= 3 * k.Scale);
        Assert.True((small.Upper - 1) * (small.Upper - 1) < 3 * k.Scale);
        Assert.Throws<MathDomainException>(() => k.Sqrt(-k.One));
        Assert.Throws<RefinementNeededException>(() => k.Sqrt(new(-1, 1, k.Precision)));
        Assert.Throws<RefinementNeededException>(() => k.Pow(k.Zero, 0));
        Assert.Throws<RefinementNeededException>(() => k.Pow(new(-1, 1, k.Precision), -1));
        Assert.Equal(k.One, k.Pow(k.One, 0));
        Assert.Equal(k.From(new Rational(1, 8)), k.Pow(k.From(new Rational(2)), -3));
        var square = k.Pow(new(-2 * k.Scale, k.Scale, k.Precision), 2);
        Assert.Equal(BigInteger.Zero, square.Lower); Assert.Equal(4 * k.Scale, square.Upper);
        Assert.Equal(k.One, k.Root(k.One, 1));
        Assert.Equal(k.One, k.Root(k.One, 2));
        Assert.Throws<MathDomainException>(() => k.Root(-k.One, 4));
        Assert.Throws<RefinementNeededException>(() => k.Root(new(-1, 1, k.Precision), 4));
        Assert.Equal(k.Zero, k.Root(k.Zero, 3));
        Assert.Equal(k.One.Times(-2), k.Root(k.One.Times(-8), 3));
        var interval = k.Root(new(-8 * k.Scale, 27 * k.Scale, k.Precision), 3);
        Assert.Equal(-2 * k.Scale, interval.Lower); Assert.Equal(3 * k.Scale, interval.Upper);
        ReferenceAssert.Near(Math.Pow(2, 1.0 / 17), k.Root(k.One.Times(2), 17).ToRational());
    }

    [Fact]
    public void LogarithmRejectsInvalidDomainAndHandlesExtremeBinaryScale()
    {
        var k = Kernel();
        Assert.Throws<MathDomainException>(() => k.Ln(k.Zero));
        Assert.Throws<MathDomainException>(() => k.Ln(-k.One));
        Assert.Throws<RefinementNeededException>(() => k.Ln(new(0, k.Scale, k.Precision)));
        Assert.Equal(k.Zero, k.Ln(k.One));
        // This interval's midpoint is exactly one: the logarithmic sensitivity
        // bound must include both sides even when the central value simplifies.
        var nearOne = k.Ln(new(k.Scale - 1, k.Scale + 1, k.Precision));
        Assert.True(nearOne.Lower.Sign < 0 && nearOne.Upper.Sign > 0);
        var enormous = k.From(new Rational(BigInteger.One << 300));
        ReferenceAssert.Near(300 * Math.Log(2), k.Ln(enormous).ToRational());
        Assert.Throws<RefinementNeededException>(() => Kernel(8).Ln(BinaryInterval.From(new Rational(2), 8)));
        var pi = k.Pi(); Assert.Equal(pi, k.Pi());
        ReferenceAssert.Near(Math.PI, pi.ToRational());
    }

    [Fact]
    public void InverseFunctionsAndExponentialEncloseWholeInputIntervals()
    {
        var k = Kernel();
        Assert.Throws<MathDomainException>(() => k.Asin(k.One.Times(2)));
        Assert.Throws<MathDomainException>(() => k.Asin(k.One.Times(-2)));
        Assert.Throws<RefinementNeededException>(() => k.Asin(new(-k.Scale - 1, 0, k.Precision)));
        Assert.Throws<RefinementNeededException>(() => k.Asin(new(0, k.Scale + 1, k.Precision)));
        ReferenceAssert.Near(Math.PI / 2, k.Asin(k.One).ToRational());
        ReferenceAssert.Near(-Math.PI / 2, k.Asin(-k.One).ToRational());
        var input = new BinaryInterval(-k.Scale / 4, k.Scale / 2, k.Precision);
        var asin = k.Asin(input); var asinh = k.Asinh(input); var tanh = k.Tanh(input); var exp = k.Exp(input);
        for (int i = -4; i <= 8; i++)
        {
            double x = i / 16.0;
            ContainsReference(asin, Math.Asin(x)); ContainsReference(asinh, Math.Asinh(x));
            ContainsReference(tanh, Math.Tanh(x)); ContainsReference(exp, Math.Exp(x));
        }
        Assert.Equal(k.Zero, k.Atan(k.Zero));
        Assert.Equal(k.One, k.Exp(k.Zero));
        ReferenceAssert.Near(Math.Atan(-2), k.Atan(k.One.Times(-2)).ToRational());
        var tiny = k.Exp(k.One.Times(-1000));
        Assert.Equal(BigInteger.Zero, tiny.Lower); Assert.Equal(BigInteger.One, tiny.Upper);
        Assert.Throws<RefinementNeededException>(() => k.SinCos(new(-k.Scale, k.Scale, k.Precision)));
        var shortKernel = Kernel(8);
        Assert.Throws<RefinementNeededException>(() => shortKernel.SinCos(shortKernel.From(new Rational(BigInteger.One << 100))));
    }

    [Fact]
    public void Atan2RespectsAxesQuadrantsAndUncertainBranchCuts()
    {
        var k = Kernel();
        Assert.Equal(k.Zero, k.Atan2(k.Zero, k.One));
        Assert.Equal(k.Pi(), k.Atan2(k.Zero, -k.One));
        Assert.Throws<MathDomainException>(() => k.Atan2(k.Zero, k.Zero));
        Assert.Throws<RefinementNeededException>(() => k.Atan2(k.Zero, new(-1, 1, k.Precision)));
        foreach (int y in new[] { -2, 2 }) foreach (int x in new[] { -3, 0, 3 })
            ReferenceAssert.Near(Math.Atan2(y, x), k.Atan2(k.One.Times(y), k.One.Times(x)).ToRational());
        var crossing = new BinaryInterval(-k.Scale / 10, k.Scale / 10, k.Precision);
        var nearPositive = k.Atan2(crossing, k.One);
        ContainsReference(nearPositive, Math.Atan2(-.1, 1)); ContainsReference(nearPositive, Math.Atan2(.1, 1));
        var touchingUpperCut = k.Atan2(new(0, k.Scale / 10, k.Precision), -k.One);
        ContainsReference(touchingUpperCut, Math.PI); ContainsReference(touchingUpperCut, Math.Atan2(.1, -1));
        Assert.Throws<RefinementNeededException>(() => k.Atan2(crossing, -k.One));
        Assert.Throws<RefinementNeededException>(() => k.Atan2(crossing, new(-1, 1, k.Precision)));
    }

    [Fact]
    public void ComplexSquareRootHandlesImaginarySignAndTheOrigin()
    {
        var k = Kernel();
        var zero = k.ComplexSqrt(k.Zero, k.Zero);
        Assert.Equal(k.Zero, zero.Real); Assert.Equal(k.Zero, zero.Imaginary);
        var imaginary = k.ComplexSqrt(-k.One, k.Zero);
        Assert.Equal(k.Zero, imaginary.Real); Assert.Equal(k.One, imaginary.Imaginary);
        // At a coarse precision, u can enclose zero despite an imaginary input
        // below the cut; use the alternate square-root formula with the right sign.
        var lower = k.ComplexSqrt(-k.One, new(-2, -1, k.Precision));
        Assert.True(lower.Real.Lower.Sign >= 0); Assert.True(lower.Imaginary.Upper.Sign < 0);
        Assert.Throws<RefinementNeededException>(() => k.ComplexSqrt(-k.One, new(-1, 1, k.Precision)));
        var normal = k.ComplexSqrt(k.One, k.One.Times(-2));
        var reference = Complex.Sqrt(new Complex(1, -2));
        ReferenceAssert.Near(reference.Real, normal.Real.ToRational());
        ReferenceAssert.Near(reference.Imaginary, normal.Imaginary.ToRational());
    }

    [Fact]
    public void ComplexZeroPowerRequiresAProvenPositiveRealExponent()
    {
        var k = Kernel();
        Assert.Equal((k.Zero, k.Zero), k.ComplexPow(k.Zero, k.Zero, k.One, k.Zero));
        Assert.Throws<MathDomainException>(() => k.ComplexPow(k.Zero, k.Zero, k.One, k.One));
        Assert.Throws<MathDomainException>(() => k.ComplexPow(k.Zero, k.Zero, -k.One, k.Zero));
        Assert.Throws<RefinementNeededException>(() => k.ComplexPow(k.Zero, k.Zero, new(0, 1, k.Precision), k.Zero));
        Assert.Throws<RefinementNeededException>(() => k.ComplexPow(k.Zero, k.Zero, k.One, new(0, 1, k.Precision)));
        Assert.Throws<RefinementNeededException>(() => k.ComplexPow(k.Zero, k.Zero, k.One, new(-1, 1, k.Precision)));
    }
}
