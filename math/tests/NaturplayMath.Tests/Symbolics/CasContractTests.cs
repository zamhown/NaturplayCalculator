using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
using Xunit;

namespace NaturplayMath.Tests.Symbolics;

public class CasContractTests
{
    [Theory]
    [InlineData(-1, "1")]
    [InlineData(-3, "-1")]
    [InlineData(-5, "1/3")]
    [InlineData(-7, "-1/15")]
    public void NegativeOddDoubleFactorialsAreExact(int n, string expected)
    {
        var value = NumberParser.ParseExact(expected);
        Assert.Equal(Expression.Constant(value), Expression.Integer(n).DoubleFactorial());
        Assert.Equal(value, Combinatorics.DoubleFactorial(n));
    }

    [Fact]
    public void ComplexCovarianceUsesHermitianConvention()
    {
        var x = new[] { ComplexExpression.I, -ComplexExpression.I };
        var y = new[] { ComplexExpression.One, -ComplexExpression.One };
        Assert.Equal(ComplexExpression.One, SymbolicStatistics.Variance(x));
        Assert.Equal(ComplexExpression.I, SymbolicStatistics.Covariance(x, y));
        Assert.Equal(-ComplexExpression.I, SymbolicStatistics.Covariance(y, x));
    }

    [Fact]
    public void ExplicitTransformsPreserveDomainsAndLeaveConstructionCompact()
    {
        var square = ExpressionParser.Parse("(a+b)^2");
        Assert.Equal("Power", square.Head);
        Assert.Equal(new[] { "a", "b" }, square.FreeSymbols);
        Assert.Single(square.Arguments);
        Assert.Equal(new System.Numerics.BigInteger(2), square.IntegerExponent);
        Assert.Null(square.SymbolName); Assert.Null(square.ConstantValue);
        Assert.Equal("a", Expression.Symbol("a").SymbolName);
        Assert.Equal(new Rational(2), Expression.Integer(2).ConstantValue);
        Assert.Null(Expression.Pi.IntegerExponent);
        Assert.Equal(new System.Numerics.BigInteger(3), Expression.Symbol("a").NthRoot(3).IntegerExponent);
        Assert.Equal(ExpressionParser.Parse("a^2+2*a*b+b^2"), square.Expand());
        var quotient = ExpressionParser.Parse("(x^2-1)/(x-1)").Cancel("x");
        Assert.Equal(ExpressionParser.Parse("x+1").Root.Key, quotient.Root.Key);
        Assert.Throws<MathDomainException>(() => quotient.Substitute(new Dictionary<string, Expression> { ["x"] = Expression.Integer(1) }));
        Assert.Equal(Expression.Integer(4), quotient.Substitute(new Dictionary<string, Expression> { ["x"] = Expression.Integer(3) }));
        Assert.Equal(ExpressionParser.Parse("2*sqrt(2)"), ExpressionParser.Parse("sqrt(8)").SimplifyRadicals());
        Assert.Equal(ExpressionParser.Parse("sqrt(2)/2"), ExpressionParser.Parse("sqrt(1/2)").SimplifyRadicals());
    }

    [Fact]
    public void TransformsAreBudgetedAndInspectSymbolsInGuards()
    {
        Assert.Equal(new[] { "x" }, ExpressionParser.Parse("x/x").FreeSymbols);
        Assert.Throws<CalculationBudgetExceededException>(() => ExpressionParser.Parse("(a+b)^100").Expand(new(maxOperations: 20)));
        var transcendental = ExpressionParser.Parse("sin(x)");
        Assert.Throws<ArgumentException>(() => transcendental.Cancel("x"));
    }

    [Fact]
    public void SharedSubtermsAndExplicitTransformBoundariesRemainExact()
    {
        var x = Expression.Symbol("x"); var shared = x.Sin() + x.Cos();
        Assert.Equal(new[] { "x" }, shared.FreeSymbols);
        Assert.Equal(shared, shared.Expand());
        Assert.Equal(ExpressionParser.Parse("x^2+x"), ExpressionParser.Parse("x*(x+1)").Expand());
        Assert.Equal(ExpressionParser.Parse("1/x"), ExpressionParser.Parse("1/x").Expand());
        Assert.Equal(Expression.Pi, Expression.Pi.Expand());
        Assert.Equal(x.NthRoot(3), x.NthRoot(3).Expand());
        var perfect = ExpressionParser.Parse("sqrt((x+1)^2-x^2-2*x)").Expand().SimplifyRadicals();
        Assert.Equal(Expression.Integer(1), perfect.Substitute(new Dictionary<string, Expression> { ["x"] = Expression.Integer(3) }));
        var largeRadical = Expression.Integer(2 * 1009L * 1009L).Sqrt();
        Assert.Equal(largeRadical, largeRadical.SimplifyRadicals());
        Assert.Equal(Expression.Integer(0), Expression.Integer(0).Cancel("x", new()));
        Assert.Equal(ExpressionParser.Parse("x+1").Root.Key, ExpressionParser.Parse("(x^2-1)/(x-1)").Cancel("x", new()).Root.Key);
        Assert.Throws<ArgumentException>(() => x.Cancel("y"));
        Assert.Throws<ArgumentNullException>(() => AlgebraicTransforms.Expand(null!));
        Assert.Throws<ArgumentNullException>(() => AlgebraicTransforms.Cancel(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => AlgebraicTransforms.SimplifyRadicals(null!));
    }
}
