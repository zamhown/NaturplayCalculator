using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
using NaturplayMath.Tests.Numerics;
using Xunit;

namespace NaturplayMath.Tests.Symbolics;

public class NormalizerBoundaryTests
{
    [Fact]
    public void TrigonometricTermsCombineOnlyWhenTheirCoefficientsAgree()
    {
        var x = Expression.Symbol("x");
        var vars = new Dictionary<string, Rational> { ["x"] = new(1) };
        ReferenceAssert.Near(Math.Sin(1) * Math.Sin(1) + 1, (x.Sin().Pow(2) + Expression.Integer(1)).Approximate(20, variables: vars));
        var unequal = x.Sin().Pow(2) + Expression.Integer(2) * x.Cos().Pow(2);
        Assert.False(unequal.TryGetRational(out _));
        ReferenceAssert.Near(Math.Sin(1) * Math.Sin(1) + 2 * Math.Cos(1) * Math.Cos(1), unequal.Approximate(20, variables: vars));
    }

    [Fact]
    public void MultiplicationNormalizesUnreducedPowersRootsAndRepeatedFactors()
    {
        var session = new ComputationContext().Start();
        var x = Node.Symbol("x");
        Assert.Equal(Node.Constant(Rational.One).Key, ExpressionNormalizer.Multiply([Node.Power(x, 0)], session).Key);
        Assert.Equal(Node.Constant(new Rational(8)).Key,
            ExpressionNormalizer.Multiply([Node.Power(Node.Root(Node.Constant(new Rational(8)), 3), 3)], session).Key);
        Assert.Equal(Node.Constant(Rational.Zero).Key,
            ExpressionNormalizer.Multiply([Node.Make(NodeKind.Sqrt, Node.Constant(Rational.Zero))], session).Key);
        var product = ExpressionParser.Parse("abs(x)*sqrt(x)*sqrt(x)");
        var expected = Expression.Symbol("x").Pow(2);
        Assert.Equal(expected.Root.Key, product.Root.Key);
        Assert.Single(product.Conditions);
        var value = product.Substitute(new Dictionary<string, Expression> { ["x"] = Expression.Integer(4) });
        Assert.Equal(Expression.Integer(16), value);
        Assert.Equal(Expression.Symbol("x").Pow(2), Expression.Symbol("x").Pow(2).Abs());
    }

    [Fact]
    public void KnownSignPredicatesAreConservativeAtZeroAndForUnknownSymbols()
    {
        var zero = Node.Constant(Rational.Zero); var negative = Node.Constant(new Rational(-1)); var x = Node.Symbol("x");
        Assert.False(ExpressionNormalizer.IsPositive(Node.Power(zero, 2)));
        Assert.True(ExpressionNormalizer.IsPositive(Node.Power(negative, 2)));
        Assert.False(ExpressionNormalizer.IsNonnegative(negative));
        Assert.True(ExpressionNormalizer.IsNonnegative(zero));
        Assert.True(ExpressionNormalizer.IsNonnegative(Node.Make(NodeKind.Abs, x)));
        Assert.True(ExpressionNormalizer.IsNonnegative(Node.Make(NodeKind.Sqrt, x)));
        Assert.True(ExpressionNormalizer.IsNonnegative(Node.Power(x, 2)));
        Assert.False(ExpressionNormalizer.IsNonnegative(Node.Power(x, 3)));
        Assert.False(ExpressionNormalizer.IsNonnegative(x));
        Assert.True(ExpressionNormalizer.IsNonnegative(Node.Make(NodeKind.Pi)));
    }

    [Fact]
    public void DomainSignInferenceTraversesNestedFunctionsAndKeepsUnprovedConditions()
    {
        var x = Node.Symbol("x"); var one = Node.Constant(Rational.One); var zero = Node.Constant(Rational.Zero);
        var absX = Node.Make(NodeKind.Abs, x);
        var negativeOddPower = new DomainCondition(Node.Power(x, -3), DomainRequirement.Nonnegative);
        var required = Assert.Single(DomainNormalizer.Normalize([negativeOddPower]));
        Assert.Equal(DomainRequirement.Positive, required.Requirement); Assert.Equal(x.Key, required.ArgumentNode.Key);
        var positiveOddPower = Assert.Single(DomainNormalizer.Normalize([new(Node.Power(x, 3), DomainRequirement.Positive)]));
        Assert.Equal(DomainRequirement.Positive, positiveOddPower.Requirement); Assert.Equal(x.Key, positiveOddPower.ArgumentNode.Key);
        var nonnegativeOddPower = Assert.Single(DomainNormalizer.Normalize([new(Node.Power(x, 3), DomainRequirement.Nonnegative)]));
        Assert.Equal(DomainRequirement.Nonnegative, nonnegativeOddPower.Requirement); Assert.Equal(x.Key, nonnegativeOddPower.ArgumentNode.Key);

        // abs(x)+1 and sqrt(x)+1 are positive on their domains, including x=0.
        foreach (var nonnegative in new[] { absX, Node.Make(NodeKind.Abs, zero), Node.Make(NodeKind.Sqrt, x), Node.Power(zero, 2), Node.Make(NodeKind.Exp, x), Node.Make(NodeKind.Pi) })
        {
            var sum = Node.Make(NodeKind.Sum, nonnegative, one);
            var conditions = DomainNormalizer.Normalize([new(x, DomainRequirement.Nonnegative), new(sum, DomainRequirement.Positive)]);
            Assert.Equal(x.Key, Assert.Single(conditions).ArgumentNode.Key);
        }
        var positiveSqrt = Node.Make(NodeKind.Sum, Node.Make(NodeKind.Sqrt, x), one);
        Assert.Equal(x.Key, Assert.Single(DomainNormalizer.Normalize([new(x, DomainRequirement.Positive), new(positiveSqrt, DomainRequirement.Positive)])).ArgumentNode.Key);
        var ambiguous = Node.Make(NodeKind.Sum, absX, Node.Constant(new Rational(-1)));
        Assert.Single(DomainNormalizer.Normalize([new(ambiguous, DomainRequirement.Positive)]));

        // Contradictory assumptions must not vacuously erase a downstream guard.
        var minusX = Node.Make(NodeKind.Product, Node.Constant(new Rational(-1)), x);
        var downstream = Node.Make(NodeKind.Sum, Node.Make(NodeKind.Abs, minusX), Node.Constant(new Rational(-1)));
        var retained = DomainNormalizer.Normalize([new(x, DomainRequirement.Positive), new(minusX, DomainRequirement.Positive), new(downstream, DomainRequirement.NonZero)]);
        Assert.Contains(retained, c => c.ArgumentNode.Key == downstream.Key);
    }

    [Fact]
    public void NonIntegralLogarithmicBaseDoesNotTriggerUnboundedExactPowerSearch()
    {
        var expression = Expression.Constant(new Rational(3, 2)).Log(Expression.Constant(new Rational(5, 4)));
        Assert.False(expression.TryGetRational(out _));
        ReferenceAssert.Near(Math.Log(1.5, 1.25), expression.Approximate(20));
    }
}
