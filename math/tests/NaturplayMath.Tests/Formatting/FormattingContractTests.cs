using System.Numerics;
using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
using Xunit;

namespace NaturplayMath.Tests.Formatting;

public class FormattingContractTests
{
    public static IEnumerable<object[]> Nodes()
    {
        yield return ["Constant", "-2/3", @"-\frac{2}{3}"];
        yield return ["Symbol", "x_1", @"\mathrm{x\_1}"];
        yield return ["Pi", "pi", @"\pi"];
        yield return ["Sum", "(x_1 + 2)", @"\left(\mathrm{x\_1} + 2\right)"];
        yield return ["Product", "(x_1 * 2)", @"\left(\mathrm{x\_1}\cdot 2\right)"];
        yield return ["Power", "(x_1)^(3)", @"\left(\mathrm{x\_1}\right)^{3}"];
        yield return ["Root", "root(x_1, 3)", @"\sqrt[3]{\mathrm{x\_1}}"];
        yield return ["GeneralPower", "(x_1)^(2)", @"\left(\mathrm{x\_1}\right)^{2}"];
        yield return ["Sqrt", "sqrt(x_1)", @"\sqrt{\mathrm{x\_1}}"];
        yield return ["Abs", "abs(x_1)", @"\left|\mathrm{x\_1}\right|"];
        yield return ["Asin", "asin(x_1)", @"\arcsin\left(\mathrm{x\_1}\right)"];
        yield return ["Atan", "atan(x_1)", @"\arctan\left(\mathrm{x\_1}\right)"];
        yield return ["Asinh", "asinh(x_1)", @"\operatorname{asinh}\left(\mathrm{x\_1}\right)"];
        yield return ["Floor", "floor(x_1)", @"\left\lfloor \mathrm{x\_1}\right\rfloor"];
        yield return ["Ceiling", "ceiling(x_1)", @"\left\lceil \mathrm{x\_1}\right\rceil"];
        yield return ["Factorial", "factorial(x_1)", @"\left(\mathrm{x\_1}\right)!"];
        yield return ["DoubleFactorial", "doublefactorial(x_1)", @"\left(\mathrm{x\_1}\right)!!"];
        yield return ["Truncate", "truncate(x_1)", @"\operatorname{truncate}\left(\mathrm{x\_1}\right)"];
        foreach (string name in new[] { "Ln", "Exp", "Sin", "Cos", "Tanh" })
            yield return [name, name.ToLowerInvariant() + "(x_1)", "\\" + name.ToLowerInvariant() + @"\left(\mathrm{x\_1}\right)"];
        foreach (string name in new[] { "Choose", "Permute", "Atan2", "ComplexRootReal", "ComplexRootImag" })
            yield return [name, name.ToLowerInvariant() + "(x_1, 2)", @"\operatorname{" + name.ToLowerInvariant() + @"}\left(\mathrm{x\_1}, 2\right)"];
        foreach (string name in new[] { "ComplexPowerReal", "ComplexPowerImag" })
            yield return [name, name.ToLowerInvariant() + "(x_1, 2, 3, 4)", @"\operatorname{" + name.ToLowerInvariant() + @"}\left(\mathrm{x\_1}, 2, 3, 4\right)"];
    }

    [Theory]
    [MemberData(nameof(Nodes))]
    public void EveryExpressionNodeHasAnUnambiguousTextAndTexForm(string name, string text, string tex)
    {
        var kind = Enum.Parse<NodeKind>(name);
        var x = Node.Symbol("x_1"); var two = Node.Constant(new Rational(2));
        var node = kind switch
        {
            NodeKind.Constant => Node.Constant(new Rational(-2, 3)), NodeKind.Symbol => x, NodeKind.Pi => Node.Make(kind),
            NodeKind.Power => Node.Power(x, 3), NodeKind.Root => Node.Root(x, 3),
            NodeKind.Sum or NodeKind.Product or NodeKind.GeneralPower or NodeKind.Choose or NodeKind.Permute or NodeKind.Atan2 or NodeKind.ComplexRootReal or NodeKind.ComplexRootImag => Node.Make(kind, x, two),
            NodeKind.ComplexPowerReal or NodeKind.ComplexPowerImag => Node.Make(kind, x, two, Node.Constant(new Rational(3)), Node.Constant(new Rational(4))),
            _ => Node.Make(kind, x)
        };
        var expression = new Expression(node, []);
        Assert.Equal(text, ExpressionFormatter.Format(expression));
        Assert.Equal(tex, ExpressionFormatter.ToTeX(expression));
    }

    [Theory]
    [InlineData("1/x", " != 0", @"\ne 0")]
    [InlineData("sqrt(x)", " >= 0", @"\ge 0")]
    [InlineData("ln(x)", " > 0", ">0")]
    [InlineData("fact(x)", " is defined", @"\text{ is defined}")]
    public void FormattingMakesDomainRestrictionsVisible(string text, string condition, string texCondition)
    {
        var expression = ExpressionParser.Parse(text);
        Assert.Contains(" where ", ExpressionFormatter.Format(expression));
        Assert.EndsWith(condition, ExpressionFormatter.Format(expression));
        Assert.Contains(@"\quad\text{where } ", ExpressionFormatter.ToTeX(expression));
        Assert.EndsWith(texCondition, ExpressionFormatter.ToTeX(expression));
        Assert.EndsWith(condition, Assert.Single(expression.Conditions).ToString());
    }

    [Theory]
    [InlineData("2.50", "2", "2", "3", "2")]
    [InlineData("3.50", "3", "3", "4", "4")]
    [InlineData("2.51", "2", "2", "3", "3")]
    [InlineData("2.49", "2", "2", "3", "2")]
    [InlineData("-2.50", "-2", "-3", "-2", "-2")]
    [InlineData("-3.50", "-3", "-4", "-3", "-4")]
    [InlineData("-2.51", "-2", "-3", "-2", "-3")]
    [InlineData("-2.49", "-2", "-3", "-2", "-2")]
    [InlineData("2", "2", "2", "2", "2")]
    [InlineData("-2", "-2", "-2", "-2", "-2")]
    [InlineData("0", "0", "0", "0", "0")]
    public void DirectedRoundingAndNearestEvenHandleEverySignAndTie(string input, string trunc, string floor, string ceil, string even)
    {
        var value = NumberParser.ParseExact(input);
        foreach (var (mode, expected) in new[] { (RoundingMode.TowardZero, trunc), (RoundingMode.Floor, floor), (RoundingMode.Ceiling, ceil), (RoundingMode.NearestEven, even) })
            Assert.Equal(expected, NumberFormatter.Format(value, new(0, rounding: mode, trimTrailingZeros: false)));
    }

    [Fact]
    public void RadixFormattingPreservesPaddingAndCarriesAcrossThePoint()
    {
        Assert.Equal("0.5", NumberFormatter.Format(new Rational(1, 2)));
        Assert.Equal("0.0000", NumberFormatter.Format(Rational.Zero, new(4, trimTrailingZeros: false)));
        Assert.Equal("-2.000", NumberFormatter.Format(new Rational(-2), new(3, trimTrailingZeros: false)));
        Assert.Equal("2", NumberFormatter.Format(new Rational(1999, 1000), new(2, rounding: RoundingMode.NearestEven)));
        Assert.Equal("0.12", NumberFormatter.Format(new Rational(12, 100), new(5)));
        Assert.Equal("0.00120", NumberFormatter.Format(new Rational(12, 10000), new(5, trimTrailingZeros: false)));
        Assert.Equal("[1][0].[0][0]", NumberFormatter.Format(new Rational(100), new(2, 100, trimTrailingZeros: false)));
        Assert.Equal("[0]", NumberFormatter.Format(Rational.Zero, new(5, 100)));
        Assert.Equal("[0].[0][1]", NumberFormatter.Format(new Rational(1, 10000), new(4, 100)));
        Assert.Equal(@"\frac{2}{3}", NumberFormatter.ToTeX(new Rational(2, 3)));
        Assert.Equal("-7", NumberFormatter.ToTeX(new Rational(-7)));
        Assert.Throws<ArgumentNullException>(() => ExpressionFormatter.Format(null!));
        Assert.Throws<ArgumentNullException>(() => ExpressionFormatter.ToTeX(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NumberFormat(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NumberFormat(radix: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NumberFormat(rounding: (RoundingMode)99));
    }
}
