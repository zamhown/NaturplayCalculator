using System.Numerics;
using NaturplayMath.Numbers;
using Xunit;

namespace NaturplayMath.Tests.Numbers;

public class ExactBoundaryTests
{
    [Fact]
    public void IntegerRootsBracketEverySmallNonnegativeInteger()
    {
        var context = new ComputationContext();
        Assert.Equal(new BigInteger(81), IntegerMath.Pow(3, 4, context));
        Assert.Equal(new BigInteger(9), IntegerMath.SqrtFloor(81, context));
        Assert.Equal(new BigInteger(3), IntegerMath.RootFloor(81, 4, context));
        for (int degree = 1; degree <= 12; degree++)
            for (int value = 0; value <= 200; value++)
            {
                var root = IntegerMath.RootFloor(value, degree);
                Assert.True(BigInteger.Pow(root, degree) <= value);
                Assert.True(BigInteger.Pow(root + 1, degree) > value);
            }
        Assert.Throws<ArgumentOutOfRangeException>(() => IntegerMath.Pow(2, -1));
        Assert.Throws<MathDomainException>(() => IntegerMath.SqrtFloor(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => IntegerMath.RootFloor(-1, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => IntegerMath.RootFloor(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => IntegerMath.RootFloor(2, -1));
    }

    [Fact]
    public void CombinatoricsMatchesPascalTriangleAndRepeatedProducts()
    {
        BigInteger factorial = 1;
        BigInteger[] row = [1];
        for (int n = 0; n <= 80; n++)
        {
            if (n > 0) factorial *= n;
            Assert.Equal(factorial, Combinatorics.Factorial(n));
            BigInteger doubleFactorial = 1;
            for (int j = n; j > 0; j -= 2) doubleFactorial *= j;
            Assert.Equal(doubleFactorial, Combinatorics.DoubleFactorial(n));
            BigInteger permutation = 1;
            for (int k = 0; k <= n; k++)
            {
                if (k > 0) permutation *= n - k + 1;
                Assert.Equal(permutation, Combinatorics.Permute(n, k));
                Assert.Equal(row[k], Combinatorics.Choose(n, k));
            }
            var next = new BigInteger[row.Length + 1];
            for (int k = 0; k < row.Length; k++) { next[k] += row[k]; next[k + 1] += row[k]; }
            row = next;
        }
        Assert.Throws<MathDomainException>(() => Combinatorics.Factorial(-1));
        Assert.Throws<MathDomainException>(() => Combinatorics.DoubleFactorial(-2));
        foreach (var (n, k) in new[] { (-1, 0), (3, -1), (3, 4) })
        {
            Assert.Throws<MathDomainException>(() => Combinatorics.Choose(n, k));
            Assert.Throws<MathDomainException>(() => Combinatorics.Permute(n, k));
        }
        var context = new ComputationContext(maxOperations: 1000);
        Assert.Equal(new BigInteger(120), Combinatorics.Factorial(5, context));
        Assert.Equal(new BigInteger(48), Combinatorics.DoubleFactorial(6, context));
        Assert.Equal(new BigInteger(10), Combinatorics.Choose(5, 2, context));
        Assert.Equal(new BigInteger(20), Combinatorics.Permute(5, 2, context));
    }

    [Fact]
    public void RationalEqualityIncludesTheDenominatorAndType()
    {
        Rational r = new BigInteger(7);
        Assert.True(r.Equals((object)new Rational(14, 2)));
        Assert.False(r.Equals(null));
        Assert.False(r.Equals("7"));
        Assert.False(new Rational(1, 2).Equals((object)new Rational(1, 3)));
        Assert.False(new Rational(1, 2).Equals((object)new Rational(2, 3)));
        Assert.False(new Rational(1, 2).IsOne);
        Assert.False(new Rational(2).IsOne);
        Assert.True(Rational.One.IsOne);
        Assert.Equal(new Rational(4), new Rational(4).Abs());
        Assert.Equal(new BigInteger(3), new Rational(7, 3).Ceiling());
        Assert.Equal(new BigInteger(2), new Rational(7, 3).Floor());
    }

    [Fact]
    public void IntervalArithmeticContainsEveryEndpointCombination()
    {
        for (int a = -2; a <= 2; a++) for (int b = a; b <= 2; b++)
            for (int c = -2; c <= 2; c++) for (int d = c; d <= 2; d++)
            {
                var x = new RealInterval(new(a), new(b));
                var y = new RealInterval(new(c), new(d));
                foreach (var p in new[] { new Rational(a), new Rational(b) })
                    foreach (var q in new[] { new Rational(c), new Rational(d) })
                    {
                        Assert.True((x + y).Contains(p + q));
                        Assert.True((x - y).Contains(p - q));
                        Assert.True((x * y).Contains(p * q));
                        if (!y.ContainsZero) Assert.True((x / y).Contains(p / q));
                    }
                Assert.True(x.Abs().Contains(new Rational(a).Abs()));
                Assert.True(x.Abs().Contains(new Rational(b).Abs()));
                Assert.True(x.Pow(2).Contains(new Rational(a * a)));
                Assert.True(x.Pow(3).Contains(new Rational(b * b * b)));
                if (!x.ContainsZero)
                {
                    Assert.Equal(RealInterval.Exact(Rational.One), x.Pow(0));
                    Assert.True(x.Pow(-2).Contains(new Rational(1, a * a)));
                }
            }
        var point = RealInterval.Exact(new Rational(2));
        var range = new RealInterval(new(2), new(3));
        Assert.Equal(IntervalOrder.Equal, point.Compare(point));
        Assert.Equal(IntervalOrder.Undetermined, point.Compare(range));
        Assert.Equal(IntervalOrder.Undetermined, range.Compare(point));
        Assert.Equal(IntervalOrder.Undetermined, range.Compare(new RealInterval(new(1), new(4))));
        Assert.Equal(RealInterval.Exact(new Rational(4)), point.Pow(2, new ComputationContext()));
        Assert.Equal(IntervalOrder.Less, point.Compare(RealInterval.Exact(new Rational(4))));
        Assert.Equal(IntervalOrder.Greater, point.Compare(RealInterval.Exact(Rational.One)));
        Assert.True(range.Equals((object)new RealInterval(new(2), new(3))));
        Assert.False(range.Equals(null)); Assert.False(range.Equals("range"));
        Assert.False(range.Equals(point)); Assert.False(range.Equals(RealInterval.Exact(Rational.One)));
        Assert.Equal(range.GetHashCode(), new RealInterval(new(2), new(3)).GetHashCode());
        Assert.Equal("[2, 3]", range.ToString()); Assert.Equal("2", point.ToString());
        Assert.False(range.Contains(Rational.Zero)); Assert.False(range.Contains(new Rational(4)));
        Assert.Throws<ArgumentException>(() => new RealInterval(new(1), new(0)));
        Assert.Throws<DivideByZeroException>(() => new RealInterval(new(-1), new(1)).Reciprocal());
        Assert.Throws<MathDomainException>(() => new RealInterval(new(-1), new(1)).Pow(0));
        Assert.Throws<MathDomainException>(() => RealInterval.Exact(Rational.Zero).Pow(-1));
    }
}
