using System.Numerics;
using NaturplayMath.Numbers;
using Xunit;

namespace NaturplayMath.Tests.Numerics;

public class BinaryIntervalTests
{
    [Fact]
    public void DirectedDivisionBracketsExactQuotientsForBothDivisorSigns()
    {
        for (int n = -30; n <= 30; n++) for (int d = -9; d <= 9; d++)
            if (d != 0)
            {
                var q = new Rational(n, d);
                var lower = BinaryInterval.FloorDiv(n, d); var upper = BinaryInterval.CeilingDiv(n, d);
                Assert.True(new Rational(lower) <= q && q <= new Rational(upper));
                Assert.Equal(q.IsInteger ? BigInteger.Zero : BigInteger.One, upper - lower);
            }
        Assert.Throws<DivideByZeroException>(() => BinaryInterval.FloorDiv(1, 0));
    }

    [Fact]
    public void LowPrecisionOperationsEncloseExactRationalArithmetic()
    {
        for (int bits = 0; bits <= 8; bits++) for (int a = -4; a <= 4; a++) for (int b = -4; b <= 4; b++)
        {
            var x = new Rational(a, 3); var y = new Rational(b, 5);
            var ix = BinaryInterval.From(x, bits); var iy = BinaryInterval.From(y, bits);
            Assert.True(ix.ToRational().Contains(x));
            Assert.True((ix + iy).ToRational().Contains(x + y));
            Assert.True((ix - iy).ToRational().Contains(x - y));
            Assert.True((ix * iy).ToRational().Contains(x * y));
            Assert.True(ix.Abs().ToRational().Contains(x.Abs()));
            Assert.True(ix.Times(b).ToRational().Contains(x * new Rational(b)));
            if (b != 0) Assert.True(ix.Divide(b).ToRational().Contains(x / new Rational(b)));
            if (!iy.ContainsZero) Assert.True((ix / iy).ToRational().Contains(x / y));
            Assert.Equal(ix.Width + 4, ix.Widen(2).Width);
        }
        var crossing = new BinaryInterval(-3, 5, 4);
        Assert.Equal(new BinaryInterval(0, 5, 4), crossing.Abs());
        Assert.Equal(new BinaryInterval(-2, 4, 4), crossing.Intersect(-2, 4));
        Assert.Equal(crossing, crossing.Intersect(-10, 10));
        Assert.Throws<InvalidOperationException>(() => crossing.Intersect(6, 7));
        Assert.Throws<RefinementNeededException>(() => crossing.Reciprocal());
        Assert.Throws<DivideByZeroException>(() => crossing.Divide(0));
        Assert.Throws<InvalidOperationException>(() => crossing + new BinaryInterval(1, 2, 3));
        Assert.Throws<InvalidOperationException>(() => crossing * new BinaryInterval(1, 2, 3));
    }
}
