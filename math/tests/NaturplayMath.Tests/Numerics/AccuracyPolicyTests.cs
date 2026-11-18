using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
using Xunit;

namespace NaturplayMath.Tests.Numerics;

public class AccuracyPolicyTests
{
    [Fact]
    public void AbsoluteErrorTargetIsIndependentOfDisplayAndBudget()
    {
        var expression = Expression.Integer(2).Sqrt();
        var target = new Rational(1, IntegerMath.Pow(10, 40));
        var context = new ComputationContext(maxOperations: 1000);
        var options = new ApproximationOptions(target);
        var result = NumericalEvaluator.Evaluate(expression, options, context: context);
        Assert.True(result.Width <= target);
        Assert.True(result.Lower * result.Lower <= new Rational(2));
        Assert.True(result.Upper * result.Upper >= new Rational(2));
        Assert.Equal("[1, 2]", result.FormatBounds(0));
        Assert.Equal(result, NumericalEvaluator.Evaluate(expression, options, context: context));
        Assert.False(expression.TryGetRational(out _));
    }

    [Fact]
    public void ExactSubstitutionFinishesBeforeAnyApproximation()
    {
        var x = Expression.Symbol("x");
        var expression = x.Sqrt() * Expression.Integer(2).Sqrt();
        var result = NumericalEvaluator.Evaluate(expression, new ApproximationOptions(new Rational(1, 100)),
            new Dictionary<string, Rational> { ["x"] = new Rational(8) });
        Assert.True(result.IsExact);
        Assert.Equal(new Rational(4), result.Lower);
    }

    [Fact]
    public void ErrorTargetsMustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ApproximationOptions(Rational.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ApproximationOptions(new Rational(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ApproximationOptions(Rational.One, maxRefinements: 0));
    }
}
