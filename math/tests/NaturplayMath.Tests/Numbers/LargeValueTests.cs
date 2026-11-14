using System.Numerics;
using NaturplayMath.Numbers;
using Xunit;

namespace NaturplayMath.Tests.Numbers;

public class LargeValueTests
{
    [Fact]
    public void OneHundredThousandDigitsSurviveInputArithmeticAndOutput()
    {
        var digits = new string('9', 100000);
        var value = NumberParser.ParseExact(digits);
        Assert.Equal(digits, value.ToString());
        Assert.Equal(digits, NumberFormatter.Format(value, new NumberFormat(0)));

        var next = value + Rational.One;
        Assert.Equal("1" + new string('0', 100000), next.ToString());
        Assert.Equal(Rational.One, next - value);
        Assert.Equal(BigInteger.One, value.Denominator);

        var fraction = value / next;
        Assert.Equal(value, fraction * next);
        Assert.Equal(value.Numerator, fraction.Numerator);
        Assert.Equal(next.Numerator, fraction.Denominator);
    }

    [Fact]
    public void ScalingNeverTurnsSmallExactValuesIntoZero()
    {
        var scale = new Rational(IntegerMath.Pow(10, 10001));
        var tiny = Rational.One / scale;
        Assert.Equal("0", NumberFormatter.Format(tiny, new NumberFormat(0)));
        Assert.False(tiny.IsZero);
        Assert.Equal(Rational.One, tiny * scale);
    }
}
