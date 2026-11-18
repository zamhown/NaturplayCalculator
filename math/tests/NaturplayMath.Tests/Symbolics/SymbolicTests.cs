using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using NaturplayMath.Computation;
using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
using Xunit;
namespace NaturplayMath.Tests.Symbolics;

public class SymbolicTests
{
    private static Expression N(BigInteger n) => Expression.Integer(n);
    private static void Exact(Expression expression, Rational expected)
    { Assert.True(expression.TryGetRational(out var value), expression.ToString()); Assert.Equal(expected, value); }
    [Fact]
    public void IrrationalFactorsCancelBeforeApproximation()
    {
        var root = N(2).Sqrt();
        Assert.False(root.TryGetRational(out _));
        Exact(root * root, new Rational(2)); Exact(root.Pow(2), new Rational(2));
        Exact(root - root, Rational.Zero); Exact(root * N(8).Sqrt(), new Rational(4));
        Exact(N(2).Sqrt() / N(2).Sqrt(), Rational.One);
    }
    [Fact]
    public void ExactRootsAndTranscendentalIdentitiesStayExact()
    {
        Exact(Expression.Constant(new Rational(4, 9)).Sqrt(), new Rational(2, 3));
        Exact(N(1).Ln(), Rational.Zero); Exact(N(0).Exp(), Rational.One);
        Exact(N(7).Ln().Exp(), new Rational(7)); Exact(N(-123).Exp().Ln(), new Rational(-123));
        var x = Expression.Symbol("x"); Assert.Equal(x, x.Exp().Ln());
    }
    [Fact]
    public void CanonicalSumsProductsAndPowersAreImmutable()
    {
        var x = Expression.Symbol("x"); var y = Expression.Symbol("y");
        var map = new Dictionary<Expression, string> { [x] = "x" };
        Assert.Equal(x + y, y + x); Assert.Equal(x * y, y * x);
        Assert.Equal(N(2) * x, x + x); Exact(x - x, Rational.Zero);
        Exact((x + y) - (x + y), Rational.Zero);
        Assert.Equal(x.Pow(5), x.Pow(2) * x.Pow(3)); Assert.Equal(x.Pow(6), x.Pow(2).Pow(3));
        Assert.Equal("x", map[Expression.Symbol("x")]);
    }
    [Fact]
    public void CancellationPreservesDomainInsteadOfInventingDefinedValues()
    {
        var x = Expression.Symbol("x"); var ratio = x / x;
        Assert.False(ratio.TryGetRational(out _)); Assert.Single(ratio.Conditions);
        Assert.Contains("x != 0", ExpressionFormatter.Format(ratio));
        Exact(ratio.Substitute(new Dictionary<string, Expression> { ["x"] = N(7) }), Rational.One);
        Assert.Throws<MathDomainException>(() => ratio.Substitute(new Dictionary<string, Expression> { ["x"] = N(0) }));
        var zero = N(0) * x.Ln(); Assert.False(zero.TryGetRational(out _));
        Assert.Throws<MathDomainException>(() => zero.Approximate(10, variables: new Dictionary<string, Rational> { ["x"] = new(-1) }));
        Assert.Throws<UnboundSymbolException>(() => zero.Approximate(10));
        Assert.True(zero.Approximate(10, variables: new Dictionary<string, Rational> { ["x"] = new(2) }).IsExact);
    }
    [Fact]
    public void SquareRootOfSquareIsAbsoluteValue()
    {
        var x = Expression.Symbol("x"); var root = x.Pow(2).Sqrt();
        Assert.Equal(x.Abs(), root);
        Exact(root.Substitute(new Dictionary<string, Expression> { ["x"] = N(-3) }), new Rational(3));
        var product = x.Sqrt() * x.Sqrt();
        Assert.Throws<MathDomainException>(() => product.Substitute(new Dictionary<string, Expression> { ["x"] = N(-3) }));
        Exact(product.Substitute(new Dictionary<string, Expression> { ["x"] = N(0) }), Rational.Zero);
    }
    [Fact]
    public void RemovedSubexpressionsStillEnforceTheirOriginalDomains()
    {
        var x = Expression.Symbol("x"); var expr = (x.Sqrt() - x.Sqrt()) + (x.Ln() - x.Ln());
        Assert.Single(expr.Conditions); Assert.Equal(DomainRequirement.Positive, expr.Conditions[0].Requirement);
        Assert.Throws<MathDomainException>(() => expr.Substitute(new Dictionary<string, Expression> { ["x"] = N(0) }));
        var replacement = Expression.Symbol("y").Ln();
        var substituted = (x / x).Substitute(new Dictionary<string, Expression> { ["x"] = replacement });
        Assert.Throws<MathDomainException>(() => substituted.Approximate(5, variables: new Dictionary<string, Rational> { ["y"] = Rational.One }));
    }
    [Fact]
    public void SubstitutionSimplifiesBeforeComputingNumericalFunctions()
    {
        var x = Expression.Symbol("x"); var expr = x.Sqrt() * N(2).Sqrt();
        Exact(expr.Substitute(new Dictionary<string, Expression> { ["x"] = N(8) }), new Rational(4));
        var result = expr.Approximate(0, variables: new Dictionary<string, Rational> { ["x"] = new(8) });
        Assert.True(result.IsExact); Assert.Equal(new Rational(4), result.Lower);
    }
    [Fact]
    public void InspectingAConditionDoesNotLoseTheArgumentsDomain()
    {
        var x = Expression.Symbol("x");
        var inverse = N(1) / x.Ln();
        var condition = System.Linq.Enumerable.Single(inverse.Conditions, c => c.Requirement == DomainRequirement.NonZero);
        var zero = N(0) * condition.Argument;
        Assert.Throws<MathDomainException>(() => zero.Substitute(new Dictionary<string, Expression> { ["x"] = N(-1) }));
    }
    [Fact]
    public void UndefinedConstantsAndUnboundSymbolsFailClearly()
    {
        Assert.Throws<MathDomainException>(() => N(-1).Sqrt()); Assert.Throws<MathDomainException>(() => N(0).Ln());
        Assert.Throws<MathDomainException>(() => N(-1).Ln()); Assert.Throws<MathDomainException>(() => N(0).Pow(0));
        Assert.Throws<DivideByZeroException>(() => N(1) / N(0));
        Assert.Throws<UnboundSymbolException>(() => Expression.Symbol("x").Approximate(3));
        Assert.Throws<ArgumentException>(() => Expression.Symbol("x + 1"));
    }
    [Fact]
    public void CancellationAndBudgetsNeverPoisonExpressionValues()
    {
        var expression = N(2).Sqrt() + N(3).Sqrt(); var hash = expression.GetHashCode();
        using var source = new CancellationTokenSource(); source.Cancel();
        Assert.Throws<OperationCanceledException>(() => expression.Approximate(10, context: new ComputationContext(source.Token)));
        Assert.Throws<CalculationBudgetExceededException>(() => expression.Approximate(10, context: new ComputationContext(maxOperations: 3)));
        Assert.Equal(hash, expression.GetHashCode()); Assert.True(expression.Approximate(10).Lower > new Rational(3));
    }
}
