using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
using Xunit;

namespace NaturplayMath.Tests.Numerics;

public class CertificationBoundaryTests
{
    [Theory]
    [InlineData("choose(floor(pi),2)", 3)]
    [InlineData("permute(floor(pi),2)", 6)]
    [InlineData("dfact(floor(pi))", 3)]
    [InlineData("fact(floor(pi))", 6)]
    [InlineData("trunc(pi)", 3)]
    [InlineData("trunc(-pi)", -3)]
    [InlineData("ceil(pi)", 4)]
    [InlineData("0^sqrt(2)", 0)]
    public void CertifiedIntermediateIntegersRemainExact(string input, int expected)
        => Assert.Equal(RealInterval.Exact(new Rational(expected)), ExpressionParser.Parse(input).Approximate(20));

    [Theory]
    [InlineData("fact(sqrt(2))")]
    [InlineData("sqrt(ln(0.5))")]
    [InlineData("ln(ln(0.5))")]
    [InlineData("1/floor(ln(2))")]
    [InlineData("0^(-sqrt(2))")]
    public void NumericDomainViolationsAreRejectedEvenWhenNotKnownSymbolically(string input)
        => Assert.Throws<MathDomainException>(() => ExpressionParser.Parse(input).Approximate(20));

    [Theory]
    [InlineData("floor(exp(1)*exp(-1))")]
    [InlineData("fact(exp(1)*exp(-1))")]
    [InlineData("sqrt(sin(1)*sin(2)+cos(1)*cos(2)-cos(1))")]
    public void UnresolvedIntegerAndDomainBoundariesRefineWithoutGuessing(string input)
        => Assert.Throws<InsufficientPrecisionException>(() => ExpressionParser.Parse(input).Approximate(20, maxRefinements: 3));

    [Fact]
    public void AmbiguousPositiveAndNonzeroBoundsRequireRefinement()
    {
        var almostZero = ExpressionParser.Parse("sin(1)*sin(2)+cos(1)*cos(2)-cos(1)");
        var nonnegative = Node.Make(NodeKind.Abs, almostZero.Root);
        var power = new Expression(Node.Make(NodeKind.GeneralPower, nonnegative, Expression.Integer(2).Sqrt().Root), []);
        Assert.Throws<InsufficientPrecisionException>(() => power.Approximate(0, maxRefinements: 1));
        var reciprocal = new Expression(Node.Power(nonnegative, -1), []);
        Assert.Throws<InsufficientPrecisionException>(() => reciprocal.Approximate(0, maxRefinements: 1));
        var invalid = new Expression(Node.Make((NodeKind)999), []);
        Assert.Throws<InvalidOperationException>(() => invalid.Approximate(1));
    }

    [Fact]
    public void RankCannotTreatAnUnprovedSmallRealOrImaginaryPartAsZero()
    {
        // This identity is zero, but it is outside the simplifier's proof rules.
        // Enclosing zero with a small error is not a proof of exact rank zero.
        var unresolved = ExpressionParser.Parse("sin(1)*sin(2)+cos(1)*cos(2)-cos(1)");
        var options = new ApproximationOptions(new Rational(1, 1000));
        foreach (var value in new[] { new ComplexExpression(unresolved), new ComplexExpression(Expression.Integer(0), unresolved) })
            Assert.Throws<UndeterminedRankException>(() => new ExpressionMatrix(new[,] { { value } }).RankCertified(options));
    }
}
