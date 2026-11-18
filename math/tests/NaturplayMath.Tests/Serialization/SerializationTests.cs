using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using NaturplayMath.Computation;
using NaturplayMath.Numbers;
using NaturplayMath.Serialization;
using NaturplayMath.Symbolics;
using Xunit;
namespace NaturplayMath.Tests.Serialization;

public class SerializationTests
{
    [Fact]
    public void NormalizationIsStableAcrossCombinedOperationsAndStorage()
    {
        var x = Expression.Symbol("x"); var y = Expression.Symbol("y");
        var seeds = new[] { x, y, x.Abs(), x.Sqrt(), y.Sqrt(), x.Pow(2), x + y, x - y, x.Pow(-1), Expression.Integer(-2), Expression.Integer(3) };
        foreach (var a in seeds) foreach (var b in seeds)
        {
            foreach (var expression in new[] { a + b, a * b, a - b, x * a * b, a.Pow(2) * b, a / b, (a * b).Pow(-1) })
                Assert.Equal(expression, MathJson.DeserializeExpression(MathJson.Serialize(expression)));
        }
    }
    [Fact]
    public void ExpressionRoundTripPreservesConditionsAndArbitraryPrecision()
    {
        var x = Expression.Symbol("x");
        var expression = (x / x) + Expression.Integer(2).Sqrt() + Expression.Constant(new Rational(BigInteger.Parse("1234567890123456789012345678901234567890"), 97));
        var json = MathJson.Serialize(expression); var restored = MathJson.DeserializeExpression(json);
        Assert.Equal(expression, restored); Assert.Equal(expression.GetHashCode(), restored.GetHashCode());
        Assert.Throws<MathDomainException>(() => restored.Approximate(3, variables: new Dictionary<string, Rational> { ["x"] = Rational.Zero }));
        Assert.Equal(expression.Approximate(5, variables: new Dictionary<string, Rational> { ["x"] = Rational.One }),
            restored.Approximate(5, variables: new Dictionary<string, Rational> { ["x"] = Rational.One }));
    }
    [Fact]
    public void IntervalRoundTripKeepsBothBoundsAndItsDistinctType()
    {
        var interval = Expression.Integer(2).Sqrt().Approximate(20);
        var json = MathJson.Serialize(interval);
        Assert.Equal(interval, MathJson.DeserializeInterval(json));
        Assert.Throws<FormatException>(() => MathJson.DeserializeExpression(json));
        Assert.Throws<FormatException>(() => MathJson.DeserializeInterval(MathJson.Serialize(Expression.Integer(2))));
    }
    [Fact]
    public void DeserializationReconstructsRequiredDomainsEvenWhenNotSupplied()
    {
        var json = "{\"version\":1,\"type\":\"expression\",\"root\":{\"kind\":\"Ln\",\"arguments\":[{\"kind\":\"Symbol\",\"name\":\"x\"}]},\"conditions\":[]}";
        var expression = MathJson.DeserializeExpression(json);
        Assert.Single(expression.Conditions);
        Assert.Throws<MathDomainException>(() => expression.Approximate(3, variables: new Dictionary<string, Rational> { ["x"] = new(-1) }));
    }
    [Fact]
    public void UnsupportedVersionsAndInvalidIntervalsAreRejected()
    {
        Assert.Throws<FormatException>(() => MathJson.DeserializeExpression(MathJson.Serialize(Expression.Integer(1)).Replace("\"version\":1", "\"version\":2")));
        Assert.Throws<ArgumentException>(() => MathJson.DeserializeInterval("{\"version\":1,\"type\":\"interval\",\"lower\":\"2\",\"upper\":\"1\"}"));
        Assert.Throws<DivideByZeroException>(() => MathJson.DeserializeInterval("{\"version\":1,\"type\":\"interval\",\"lower\":\"1/0\",\"upper\":\"1\"}"));
    }
    [Fact]
    public void StorageUsesOneCancellableBudgetAcrossAllNodesAndDigits()
    {
        var x = Expression.Symbol("x"); var expression = x.Pow(123) + x.Ln(); var json = MathJson.Serialize(expression);
        Assert.Throws<CalculationBudgetExceededException>(() => MathJson.DeserializeExpression(json, new ComputationContext(maxOperations: 5)));
        Assert.Throws<CalculationBudgetExceededException>(() => MathJson.Serialize(expression, new ComputationContext(maxOperations: 3)));
    }
}
