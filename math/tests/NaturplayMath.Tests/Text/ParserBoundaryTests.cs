using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
using Xunit;

namespace NaturplayMath.Tests.Text;

public class ParserBoundaryTests
{
    [Theory]
    [InlineData("1)")] [InlineData("?")] [InlineData("1@2")]
    [InlineData("sin 2")] [InlineData("sin()")] [InlineData("()")]
    [InlineData("2+")] [InlineData("2/")] [InlineData("2,")]
    [InlineData("[123")] [InlineData("[12x]")] [InlineData("[]")]
    [InlineData("[1 2]")]
    [InlineData("0x")] [InlineData(".")] [InlineData("1..2")]
    [InlineData("1+//nothing")]
    public void InvalidExpressionsReportPositionsInBothDomains(string input)
    {
        var real = Assert.Throws<ExpressionSyntaxException>(() => ExpressionParser.Parse(input));
        var complex = Assert.Throws<ExpressionSyntaxException>(() => ExpressionParser.ParseComplex(input));
        Assert.InRange(real.Position, 0, input.Length);
        Assert.InRange(complex.Position, 0, input.Length);
    }

    [Theory]
    [InlineData("+2", "2")]
    [InlineData("2×3−4÷2", "4")]
    [InlineData("0o17", "15")]
    [InlineData("0Xf", "15")]
    [InlineData("1E+3", "1000")]
    [InlineData("1e-3", "1/1000")]
    [InlineData("1e3", "1000")]
    [InlineData("1.25 // comment\n + .75", "2")]
    [InlineData("2+3 // trailing", "5")]
    [InlineData("1%2", "1")]
    [InlineData("3!", "6")]
    [InlineData("3!!", "3")]
    public void ExactLexicalFormsPreserveTheirValue(string input, string expected)
    {
        Assert.Equal(Expression.Constant(NumberParser.ParseExact(expected)), ExpressionParser.Parse(input));
        Assert.Equal(ComplexExpression.Constant(NumberParser.ParseExact(expected)), ExpressionParser.ParseComplex(input));
    }

    [Fact]
    public void IdentifiersAreNotConfusedWithOperatorsDigitsOrScientificNotation()
    {
        foreach (string identifier in new[] { "mod_value", "ncrank", "npr_x", "yrooted", "_x1" })
        {
            // A numeric suffix is allowed after a named infix operator, but an
            // underscore or letter means an identifier.
            Assert.Equal(Expression.Symbol(identifier), ExpressionParser.Parse(identifier));
        }
        Assert.Equal(Expression.Integer(175), ExpressionParser.Parse("Af", new(16)));
        Assert.Equal(Expression.Symbol("AG"), ExpressionParser.Parse("AG", new(16)));
        Assert.Equal(Expression.Symbol("A_"), ExpressionParser.Parse("A_", new(16)));
        Assert.Equal(Expression.Pi, ExpressionParser.Parse("pi", new(16)));
        Assert.Equal(Expression.Integer(1).Exp(), ExpressionParser.Parse("e", new(16)));
        Assert.Equal(Expression.Integer(1).Exp(), ExpressionParser.Parse("1e"));
        Assert.Equal(Expression.Integer(1).Exp() + Expression.Integer(1), ExpressionParser.Parse("1e + 1e0"));
        Assert.Equal(Expression.Integer(14), ExpressionParser.Parse("0xE"));
        Assert.Equal(Expression.Symbol("A"), ExpressionParser.Parse("A", new(100)));
        Assert.Equal(Expression.Integer(999), ExpressionParser.Parse("[9][99]", new(100)));
        Assert.Throws<ExpressionSyntaxException>(() => ExpressionParser.Parse("((1))", new(maxDepth: 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExpressionParseOptions(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExpressionParseOptions(angleUnit: (AngleUnit)7));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExpressionParseOptions(maxDepth: 0));
        Assert.Throws<ArgumentNullException>(() => ExpressionParser.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => ExpressionParser.ParseComplex(null!));
        Assert.Throws<ExpressionSyntaxException>(() => ExpressionParser.Parse("10", new(100)));
        Assert.Throws<ExpressionSyntaxException>(() => ExpressionParser.Parse("mod"));
        Assert.Throws<MathDomainException>(() => ExpressionParser.ParseComplex("floor(z)", variables: new Dictionary<string, ComplexExpression>
        { ["z"] = new(Expression.Integer(0), Expression.Symbol("y")) }));
    }

    [Theory]
    [InlineData("[", 100)] [InlineData("[]", 100)] [InlineData("[1", 100)]
    [InlineData("[1x]", 100)] [InlineData("[100]", 100)] [InlineData("1", 100)]
    [InlineData("[1 2]", 100)]
    [InlineData("2", 2)] [InlineData("?", 10)] [InlineData("[1]..[2]", 100)]
    public void NumericDigitsMustBeValidForTheirRadix(string input, int radix)
        => Assert.Throws<FormatException>(() => NumberParser.Parse(input, radix));

    [Fact]
    public void NumericParsingRejectsFractionalFractionComponentsAndAcceptsSigns()
    {
        Assert.Throws<FormatException>(() => NumberParser.ParseExact("1.5/2"));
        Assert.Throws<FormatException>(() => NumberParser.ParseExact("1/2.5"));
        Assert.Throws<FormatException>(() => NumberParser.Parse(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => NumberParser.Parse("1", 1));
        Assert.Equal(new Rational(1, 2), NumberParser.Parse(" +.5 "));
        Assert.Equal(new Rational(-3, 2), NumberParser.ParseExact("-3/2", new()));
        Assert.Equal(new Rational(-15), NumberParser.Parse("-f", 16, new ComputationContext()));
    }
}
