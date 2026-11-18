using System;
using System.Collections.Generic;
using System.Numerics;
using NaturplayMath.Computation;
using NaturplayMath.Formatting;
using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
using Xunit;
namespace NaturplayMath.Tests.Numerics;

public class ApproximationTests
{
    private static Expression N(BigInteger value) => Expression.Integer(value);
    [Theory]
    [InlineData(0, 10u)]
    [InlineData(1, 10u)]
    [InlineData(30, 10u)]
    [InlineData(100, 10u)]
    [InlineData(60, 2u)]
    [InlineData(30, 16u)]
    [InlineData(30, 36u)]
    public void SqrtBoundsAreProvenByExactSquaring(int digits, uint radix)
    {
        var result = N(2).Sqrt().Approximate(digits, radix);
        Assert.False(result.IsExact); Assert.True(result.Lower * result.Lower <= new Rational(2));
        Assert.True(result.Upper * result.Upper >= new Rational(2));
        Assert.True(result.Width <= new Rational(1, IntegerMath.Pow(radix, digits)));
        Assert.True(result.AbsoluteError == result.Width / new Rational(2));
    }
    [Fact]
    public void IncreasingRequestedAccuracyUsesOriginalSymbolicValue()
    {
        var expression = N(2).Sqrt(); var loose = expression.Approximate(1); var tight = expression.Approximate(50);
        Assert.True(tight.Width < loose.Width);
        Assert.Equal("[1.4, 1.5]", tight.FormatBounds(1));
        Assert.True(tight.TryFormat(new NumberFormat(30), out var text));
        Assert.Equal("1.414213562373095048801688724209", text);
        Assert.False(loose.TryFormat(new NumberFormat(50), out _));
    }
    [Fact]
    public void RationalAndSymbolicallyCancelledValuesRemainExactAtEvaluation()
    {
        var third = Expression.Constant(new Rational(1, 3));
        Assert.Equal(new Rational(1, 3), third.Approximate(0).Lower); Assert.True(third.Approximate(0).IsExact);
        var root = N(2).Sqrt(); Assert.Equal(new Rational(2), (root * root).Approximate(0).Lower);
        Assert.True((root * root).Approximate(0).IsExact);
        Assert.True((root / N(8).Sqrt()).TryGetRational(out var ratio)); Assert.Equal(new Rational(1, 2), ratio);
    }
    [Fact]
    public void AnExactAnswerDoesNotAllocateUnusedDecimalPlaces()
    {
        Assert.Equal(new Rational(1, 3), Expression.Constant(new Rational(1, 3)).Approximate(int.MaxValue).Lower);
        Assert.Equal("123", NumberFormatter.Format(new Rational(123), new NumberFormat(int.MaxValue)));
    }
    [Theory]
    [InlineData("2", "0.6931471805599453094172321214581765680755")]
    [InlineData("0.5", "-0.6931471805599453094172321214581765680755")]
    [InlineData("10", "2.3025850929940456840179914546843642076011")]
    [InlineData("1.0001", "0.0000999950003333083353331666809511310634")]
    public void LogarithmAgreesWithIndependentReferenceDigits(string input, string expected)
    {
        var result = Expression.Constant(NumberParser.Parse(input)).Ln().Approximate(40);
        Assert.True(result.TryFormat(new NumberFormat(40), out var text)); Assert.Equal(expected, text);
    }
    [Theory]
    [InlineData("1", "2.7182818284590452353602874713526624977572")]
    [InlineData("-1", "0.3678794411714423215955237701614608674458")]
    [InlineData("2", "7.3890560989306502272304274605750078131803")]
    public void ExponentialAgreesWithIndependentReferenceDigits(string input, string expected)
    {
        var result = Expression.Constant(NumberParser.Parse(input)).Exp().Approximate(40);
        Assert.True(result.TryFormat(new NumberFormat(40), out var text)); Assert.Equal(expected, text);
    }
    [Fact]
    public void ExponentialBoundsContainAnIndependentlyBoundedFactorialSeries()
    {
        var result = N(1).Exp().Approximate(25);
        BigInteger factorial = 1; Rational lower = Rational.One;
        for (BigInteger i = 1; i <= 80; i++) { factorial *= i; lower += new Rational(1, factorial); }
        // For terms after 1/80!, geometric ratio <=1/81 bounds the positive tail.
        var upper = lower + new Rational(1, factorial * 80);
        Assert.True(result.Lower <= lower); Assert.True(result.Upper >= upper);
    }
    [Fact]
    public void LogarithmsRespectMultiplicativeIdentityWithinCertifiedEnclosures()
    {
        var sum = N(2).Ln().Approximate(25) + N(3).Ln().Approximate(25);
        var direct = N(6).Ln().Approximate(25);
        Assert.True(sum.Lower <= direct.Upper && direct.Lower <= sum.Upper);
    }
    [Fact]
    public void SevereCancellationRefinesInsteadOfDividingByUncertainZero()
    {
        var denominator = N(2).Sqrt() - Expression.Constant(NumberParser.Parse("1.4142135623730950488016887242096980785696718753769"));
        var expression = N(1) / denominator;
        Assert.Throws<InsufficientPrecisionException>(() => expression.Approximate(3, maxRefinements: 1));
        var value = expression.Approximate(3);
        Assert.True(value.Lower > new Rational(BigInteger.Parse("10000000000000000")));
        Assert.True(value.Width <= new Rational(1, 1000));
        // Algebraic rationalization provides an independent enclosure.
        var c = NumberParser.ParseExact("1.4142135623730950488016887242096980785696718753769");
        var reference = (N(2).Sqrt() + Expression.Constant(c)) / Expression.Constant(new Rational(2) - c * c);
        var alternative = reference.Approximate(3);
        Assert.True(value.Lower <= alternative.Upper && alternative.Lower <= value.Upper);
    }
    [Fact]
    public void IntervalArithmeticPropagatesEnclosuresThroughAllSigns()
    {
        for (int a = -3; a <= 3; a++) for (int b = a; b <= 3; b++)
            for (int c = -3; c <= 3; c++) for (int d = c; d <= 3; d++)
            {
                var left = new RealInterval(new Rational(a), new Rational(b));
                var right = new RealInterval(new Rational(c), new Rational(d));
                foreach (var x in new[] { left.Lower, left.Midpoint, left.Upper })
                    foreach (var y in new[] { right.Lower, right.Midpoint, right.Upper })
                    {
                        Assert.True((left + right).Contains(x + y)); Assert.True((left - right).Contains(x - y));
                        Assert.True((left * right).Contains(x * y));
                        if (!right.ContainsZero) Assert.True((left / right).Contains(x / y));
                    }
            }
    }
    [Fact]
    public void IntervalsNeverPretendOverlapsProveNumericalEquality()
    {
        var interval = new RealInterval(new Rational(1), new Rational(2));
        Assert.True(interval.Equals(interval)); Assert.Equal(IntervalOrder.Undetermined, interval.Compare(interval));
        Assert.Equal(IntervalOrder.Less, interval.Compare(RealInterval.Exact(new Rational(3))));
        Assert.Equal(IntervalOrder.Greater, interval.Compare(RealInterval.Exact(new Rational(0))));
        Assert.Equal(IntervalOrder.Equal, RealInterval.Exact(Rational.One).Compare(RealInterval.Exact(Rational.One)));
        Assert.Throws<DivideByZeroException>(() => new RealInterval(new Rational(-1), Rational.One).Reciprocal());
        Assert.Throws<ArgumentException>(() => new RealInterval(new Rational(2), Rational.One));
    }
    [Fact]
    public void IntervalPowersHandleParitySignsAndZero()
    {
        var interval = new RealInterval(new Rational(-2), new Rational(3));
        Assert.Equal(new RealInterval(Rational.Zero, new Rational(9)), interval.Pow(2));
        Assert.Equal(new RealInterval(new Rational(-8), new Rational(27)), interval.Pow(3));
        Assert.Equal(new RealInterval(Rational.Zero, new Rational(3)), interval.Abs());
        Assert.Throws<MathDomainException>(() => interval.Pow(-1));
        Assert.Equal(new RealInterval(new Rational(1, 9), new Rational(1, 4)), new RealInterval(new Rational(-3), new Rational(-2)).Pow(-2));
    }
    [Fact]
    public void FormattingRoundsBoundsOutwardAndSharesTheOperationBudget()
    {
        var interval = new RealInterval(new Rational(-4, 3), new Rational(1, 3));
        Assert.Equal("[-1.34, 0.34]", interval.FormatBounds(2));
        Assert.Throws<CalculationBudgetExceededException>(() => interval.FormatBounds(100, context: new ComputationContext(maxOperations: 50)));
    }
}
