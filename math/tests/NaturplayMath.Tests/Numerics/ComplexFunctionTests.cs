using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
using NaturplayMath.Serialization;
using NaturplayMath.Calculation;
using Xunit;
namespace NaturplayMath.Tests.Numerics;

public class ComplexFunctionTests
{
    [Fact]
    public async Task ComplexTextPipelinePreservesExactInputsAndBranches()
    {
        Assert.Equal("-1 + 0i", (await CalculationEngine.CalculateComplexAsync("exp(i*pi)")).Text);
        Assert.Equal("0 + 1i", CalculationEngine.CalculateComplex("sqrt(-1)").Text);
        Assert.Equal("-2 + 0i", CalculationEngine.CalculateComplex("root(-8,3)").Text);
        Assert.Equal("1 + 2i", CalculationEngine.CalculateComplex("Ans", variables: new Dictionary<string, ComplexExpression>
        { ["Ans"] = ComplexExpression.Constant(new Rational(1), new Rational(2)) }).Text);
        var log = CalculationEngine.CalculateComplex("ln(-1)", new(new NumberFormat(30)));
        Assert.Equal("0", log.RealText);
        Assert.Equal("3.141592653589793238462643383279", log.ImaginaryText);
        Assert.Equal(log.Expression, MathJson.DeserializeComplex(MathJson.Serialize(log.Expression)));
        var projected = ExpressionParser.ParseComplex("re(1+i*ln(x))");
        Assert.Throws<MathDomainException>(() => projected.Real.Substitute(new Dictionary<string, Expression> { ["x"] = Expression.Integer(0) }));
    }
    [Fact]
    public void GeneralComplexPowerKeepsItsZeroExtensionUnderSubstitution()
    {
        var power = ExpressionParser.ParseComplex("(1+2i)^(2+3i)");
        var result = CalculationEngine.CalculateComplex(power, new(new NumberFormat(30, trimTrailingZeros: false)));
        Assert.Equal("-0.015132672422722735301598237776", result.RealText);
        Assert.Equal("-0.179867483913334931157605480864", result.ImaginaryText);
        Assert.Equal(power, MathJson.DeserializeComplex(MathJson.Serialize(power)));
        var symbolic = ExpressionParser.ParseComplex("(x+yi)^sqrt(2)", variables: new Dictionary<string, ComplexExpression>
        { ["yi"] = new(Expression.Integer(0), Expression.Symbol("y")) });
        var zero = symbolic.Substitute(new Dictionary<string, Expression> { ["x"] = Expression.Integer(0), ["y"] = Expression.Integer(0) });
        var enclosure = zero.Approximate(30);
        Assert.Equal(RealInterval.Exact(Rational.Zero), enclosure.Real);
        Assert.Equal(RealInterval.Exact(Rational.Zero), enclosure.Imaginary);
    }
    [Theory]
    [InlineData("sqrt", "1.272019649514068964252422461737", "0.786151377757423286069558585842")]
    [InlineData("ln", "0.804718956217050187300379666613", "1.107148717794090503017065460178")]
    [InlineData("exp", "-1.131204383756813638431255255510", "2.471726672004818927616930893551")]
    [InlineData("sin", "3.165778513216168146740734617191", "1.959601041421605897070352049989")]
    [InlineData("cos", "2.032723007019665529436343448499", "-3.051897799151800057512115686895")]
    [InlineData("tan", "0.033812826079896690284370559725", "1.014793616146633568117054175417")]
    [InlineData("asin", "0.427078586392476125480646883318", "1.528570919480998161272456184793")]
    [InlineData("acos", "1.143717740402420493750674808320", "-1.528570919480998161272456184793")]
    [InlineData("atan", "1.338972522294493561124193575909", "0.402359478108525093650189833306")]
    [InlineData("asinh", "1.469351744368185273255844317361", "1.063440023577752056189491997089")]
    [InlineData("acosh", "1.528570919480998161272456184793", "1.143717740402420493750674808320")]
    [InlineData("atanh", "0.173286795139986327354308030364", "1.178097245096172464423491268729")]
    public void PrincipalComplexFunctionsMatchIndependentReference(string function, string real, string imaginary)
    {
        var z = ComplexExpression.Constant(new Rational(1), new Rational(2));
        var result = function switch
        {
            "sqrt" => z.Sqrt(),
            "ln" => z.Ln(),
            "exp" => z.Exp(),
            "sin" => z.Sin(),
            "cos" => z.Cos(),
            "tan" => z.Tan(),
            "asin" => z.Asin(),
            "acos" => z.Acos(),
            "atan" => z.Atan(),
            "asinh" => z.Asinh(),
            "acosh" => z.Acosh(),
            _ => z.Atanh()
        };
        var enclosure = result.Approximate(50);
        Assert.True(enclosure.Real.TryFormat(new NumberFormat(30, trimTrailingZeros: false), out var realText));
        Assert.True(enclosure.Imaginary.TryFormat(new NumberFormat(30, trimTrailingZeros: false), out var imaginaryText));
        Assert.Equal(real, realText); Assert.Equal(imaginary, imaginaryText);
        Assert.Equal(result.Real, MathJson.DeserializeExpression(MathJson.Serialize(result.Real)));
        Assert.Equal(result.Imaginary, MathJson.DeserializeExpression(MathJson.Serialize(result.Imaginary)));
    }

    [Fact]
    public void ComplexExactValuesAndPrincipalBranchCutsArePreserved()
    {
        Assert.Equal(ComplexExpression.Integer(-1), ComplexExpression.I * ComplexExpression.I);
        Assert.Equal(ComplexExpression.I, ComplexExpression.Integer(-1).Sqrt());
        Assert.Equal(ComplexExpression.Zero, ComplexExpression.Zero.Sqrt());
        Assert.Equal(ComplexExpression.Integer(-1), (ComplexExpression.I * new ComplexExpression(Expression.Pi)).Exp());
        var upper = ComplexExpression.Constant(new Rational(-1), new Rational(1, 1000)).Ln().Approximate(40);
        var lower = ComplexExpression.Constant(new Rational(-1), new Rational(-1, 1000)).Ln().Approximate(40);
        Assert.True(upper.Imaginary.Lower.Sign > 0); Assert.True(lower.Imaginary.Upper.Sign < 0);
        Assert.Throws<MathDomainException>(() => ComplexExpression.Zero.Ln());
    }
}
