using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
using Xunit;

namespace NaturplayMath.Tests.Numerics;

public class ElementaryKernelTests
{
    [Fact]
    public void HugeAnglesAndTinyLogarithmArgumentsRequestSufficientWorkingPrecision()
    {
        var huge = IntegerMath.Pow(10, 1000);
        var sine = Expression.Integer(huge).Sin().Approximate(70);
        Assert.True(sine.TryFormat(new NumberFormat(50, trimTrailingZeros: false), out var text));
        Assert.Equal("0.65335979821036985694809946803976857426591654081540", text);
        var cancellation = Expression.Constant(new Rational(1, huge)).Ln() + Expression.Integer(1000) * Expression.Integer(10).Ln();
        Assert.True(cancellation.Approximate(50).Contains(Rational.Zero));
    }
    [Theory]
    [InlineData("pi", "3.14159265358979323846264338327950288419716939937510")]
    [InlineData("sin", "0.84147098480789650665250232163029899962256306079837")]
    [InlineData("cos", "0.54030230586813971740093660744297660373231042061792")]
    [InlineData("lnsin", "-0.17260374626909167851341097586390906984010840889640")]
    public void ElementaryValuesMatchIndependentReferenceDigits(string name, string expected)
    {
        var one = Expression.Integer(1);
        var expression = name switch { "pi" => Expression.Pi, "sin" => one.Sin(), "cos" => one.Cos(), _ => one.Sin().Ln() };
        var interval = expression.Approximate(80);
        Assert.True(interval.TryFormat(new NumberFormat(50, trimTrailingZeros: false), out var text));
        Assert.Equal(expected, text);
    }

    [Fact]
    public void TrigonometricQuadrantsSimplifyExactly()
    {
        for (int k = -8; k <= 8; k++)
        {
            var angle = Expression.Constant(new Rational(k, 2)) * Expression.Pi;
            Assert.True(angle.Sin().TryGetRational(out var sine));
            Assert.True(angle.Cos().TryGetRational(out var cosine));
            Assert.Equal(Rational.One, sine * sine + cosine * cosine);
            if (k % 2 == 0) Assert.True(sine.IsZero); else Assert.True(cosine.IsZero);
        }
        Assert.Throws<DivideByZeroException>(() => (Expression.Pi / Expression.Integer(2)).Tan());
    }

    [Fact]
    public void PeriodReductionAndComposedIntervalsPreserveIdentities()
    {
        var angle = Expression.Integer(12345);
        var sin = angle.Sin().Approximate(40); var cos = angle.Cos().Approximate(40);
        Assert.True((sin.Pow(2) + cos.Pow(2)).Contains(Rational.One));
        var negative = (-angle).Sin().Approximate(40);
        Assert.True((negative + sin).Contains(Rational.Zero));
        var periodic = (angle + Expression.Pi * Expression.Integer(200)).Sin().Approximate(40);
        Assert.True(periodic.Lower <= sin.Upper && sin.Lower <= periodic.Upper);
    }

    [Fact]
    public void RecursiveIntegerSqrtPreservesExactSquareBounds()
    {
        var root = IntegerMath.Pow(10, 5000) + 12345;
        Assert.Equal(root, IntegerMath.SqrtFloor(root * root));
        Assert.Equal(root - 1, IntegerMath.SqrtFloor(root * root - 1));
        Assert.Equal(root, IntegerMath.SqrtFloor(root * root + root));
    }
}
