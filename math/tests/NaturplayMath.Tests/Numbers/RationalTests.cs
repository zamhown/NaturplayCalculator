using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using NaturplayMath.Numbers;
using NaturplayMath.Computation;
using NaturplayMath.Formatting;
using Xunit;
namespace NaturplayMath.Tests.Numbers;

public class RationalTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-0")]
    [InlineData("000.000")]
    [InlineData("+0")]
    [InlineData("0/-123")]
    public void ZeroIsCanonical(string text)
    {
        var value = NumberParser.ParseExact(text);
        Assert.Equal(default, value); Assert.Equal(BigInteger.One, value.Denominator);
        Assert.Equal(Rational.Zero.GetHashCode(), value.GetHashCode());
    }
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-")]
    [InlineData(".")]
    [InlineData("1.2.3")]
    [InlineData("1.23X")]
    [InlineData("NaN")]
    [InlineData("1/2/3")]
    [InlineData("1/1.1")]
    public void InvalidInputIsRejected(string text) => Assert.Throws<FormatException>(() => NumberParser.ParseExact(text));
    [Fact]
    public void FractionsAreNormalizedAndSigned()
    {
        Assert.Equal(new Rational(-2, 3), new Rational(14, -21));
        Assert.Equal(new Rational(2, 3), new Rational(-14, -21));
        Assert.Throws<DivideByZeroException>(() => new Rational(0, 0));
        Assert.Throws<DivideByZeroException>(() => new Rational(1) / Rational.Zero);
        Assert.Throws<DivideByZeroException>(() => new Rational(1) % Rational.Zero);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void PrecisionOnlyAffectsDisplay(int digits)
    {
        var third = new Rational(1, 3);
        NumberFormatter.Format(third, new NumberFormat(digits));
        Assert.Equal(Rational.One, third * new Rational(3));
        var input = NumberParser.ParseExact("1.12345678901234567890123456789");
        NumberFormatter.Format(input, new NumberFormat(digits));
        Assert.Equal("112345678901234567890123456789/100000000000000000000000000000", input.ToString());
        Assert.Equal(input, NumberParser.ParseExact(input.ToString()));
    }
    [Fact]
    public void IntegerAndExponentHaveNoMachineWidthCeiling()
    {
        var huge = BigInteger.Parse("10000000000000000000000000000000000000000000000000000000000000000");
        Assert.Equal(Rational.One, new Rational(-1).Pow(huge));
        Assert.Equal(new Rational(-1), new Rational(-1).Pow(huge + 1));
        Assert.Equal(new Rational(1, 8), new Rational(2).Pow(-3));
        Assert.Equal(BigInteger.One, IntegerMath.Pow(-1, huge));
        Assert.Equal(new Rational(huge, huge + 1), NumberParser.ParseExact(new Rational(huge, huge + 1).ToString()));
        Assert.Throws<MathDomainException>(() => Rational.Zero.Pow(0));
        Assert.Throws<MathDomainException>(() => IntegerMath.Pow(0, 0));
    }
    [Fact]
    public void MoreThanTenThousandDigitsRemainExact()
    {
        var text = "1" + new string('0', 10001) + "1";
        var value = NumberParser.ParseExact(text);
        Assert.Equal(text, value.ToString());
        Assert.Equal(Rational.One, (value + Rational.One) - value);
        var fraction = NumberFormatter.Format(new Rational(1, 3), new NumberFormat(10001));
        Assert.Equal("0." + new string('3', 10001), fraction);
    }
    [Theory]
    [InlineData(2u)]
    [InlineData(3u)]
    [InlineData(8u)]
    [InlineData(10u)]
    [InlineData(16u)]
    [InlineData(32u)]
    [InlineData(36u)]
    public void RadixRoundTripIsIndependentOfValue(uint radix)
    {
        var value = NumberParser.ParseExact("-1234567890123456789012345678901234567890");
        Assert.Equal(value, NumberParser.Parse(NumberFormatter.Format(value, new NumberFormat(0, radix)), radix));
        var fraction = new Rational(17, new BigInteger(radix) * radix * radix);
        Assert.Equal(fraction, NumberParser.Parse(NumberFormatter.Format(fraction, new NumberFormat(3, radix)), radix));
    }
    [Theory]
    [InlineData("2.5", "2")]
    [InlineData("3.5", "4")]
    [InlineData("-2.5", "-2")]
    [InlineData("-3.5", "-4")]
    public void RoundToEvenHandlesSignsAndTies(string value, string expected) =>
        Assert.Equal(expected, NumberFormatter.Format(NumberParser.ParseExact(value), new NumberFormat(0, rounding: RoundingMode.NearestEven)));
    [Fact]
    public void TruncationFloorCeilingAndSignedRemainderAreDistinct()
    {
        var x = new Rational(-7, 3);
        Assert.Equal(new BigInteger(-2), x.Truncate()); Assert.Equal(new BigInteger(-3), x.Floor()); Assert.Equal(new BigInteger(-2), x.Ceiling());
        Assert.Equal("-2.3", NumberFormatter.Format(x, new NumberFormat(1)));
        Assert.Equal("-2.4", NumberFormatter.Format(x, new NumberFormat(1, rounding: RoundingMode.Floor)));
        Assert.Equal("-2.3", NumberFormatter.Format(x, new NumberFormat(1, rounding: RoundingMode.Ceiling)));
        Assert.Equal("0", NumberFormatter.Format(new Rational(-1, 100), new NumberFormat(0)));
        for (int a = -12; a <= 12; a++) for (int b = -7; b <= 7; b++)
        {
            if (b == 0) continue;
            var left = new Rational(a, 3); var right = new Rational(b, 5); var rem = left % right;
            Assert.Equal(left, new Rational((left / right).Truncate()) * right + rem);
            Assert.True(rem.IsZero || rem.Sign == left.Sign); Assert.True(rem.Abs() < right.Abs());
        }
    }
    [Fact]
    public void ArithmeticMatchesIndependentIntegerCrossProducts()
    {
        for (int a = -13; a <= 13; a++) for (int b = 1; b <= 9; b++)
            for (int c = -8; c <= 8; c++)
            {
                BigInteger an = a, ad = b, bn = c, bd = 11;
                var x = new Rational(an, ad); var y = new Rational(bn, bd);
                var sum = x + y; var product = x * y;
                Assert.Equal((an * bd + bn * ad) * sum.Denominator, sum.Numerator * ad * bd);
                Assert.Equal(an * bn * product.Denominator, product.Numerator * ad * bd);
                Assert.Equal(x, (x + y) - y);
                if (c != 0) Assert.Equal(x, (x / y) * y);
            }
    }
    [Fact]
    public void IntegerAlgorithmsCoverZeroSignsAndLargeSquares()
    {
        Assert.Equal(new BigInteger(6), IntegerMath.Gcd(-18, 24)); Assert.Equal(BigInteger.Zero, IntegerMath.Gcd(0, 0));
        Assert.Equal(new BigInteger(72), IntegerMath.Lcm(-18, 24)); Assert.Equal(BigInteger.Zero, IntegerMath.Lcm(0, 0));
        var n = IntegerMath.Pow(10, 300) + 123;
        Assert.Equal(n, IntegerMath.SqrtFloor(n * n)); Assert.Equal(n - 1, IntegerMath.SqrtFloor(n * n - 1));
        Assert.Equal(n, IntegerMath.SqrtFloor(n * n + n));
    }
    [Fact]
    public void ValuesAndHashesAreIndependentOfContextsAndFormatting()
    {
        var value = new Rational(1, 3); var map = new Dictionary<Rational, string> { [value] = "preserved" };
        var context = new ComputationContext(maxOperations: 30);
        Assert.Equal(value, value.Pow(1, context));
        Assert.Throws<CalculationBudgetExceededException>(() => NumberFormatter.Format(value, new NumberFormat(100), context));
        Assert.Equal(value, value.Pow(1, context));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => value.Pow(2, new ComputationContext(cts.Token)));
        Assert.Throws<OperationCanceledException>(() => IntegerMath.Lcm(0, 0, new ComputationContext(cts.Token)));
        Assert.Equal("preserved", map[new Rational(2, 6)]); Assert.Equal(new Rational(1, 9), value.Pow(2));
    }
}
