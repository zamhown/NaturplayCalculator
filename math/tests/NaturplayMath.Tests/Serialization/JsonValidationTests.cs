using System.Text.Json.Nodes;
using NaturplayMath.Numbers;
using NaturplayMath.Serialization;
using NaturplayMath.Symbolics;
using Xunit;

namespace NaturplayMath.Tests.Serialization;

public class JsonValidationTests
{
    [Theory]
    [InlineData("sin(x)")] [InlineData("cos(x)")] [InlineData("exp(x)")] [InlineData("atan(x)")]
    [InlineData("asin(x)")] [InlineData("asinh(x)")] [InlineData("tanh(x)")]
    [InlineData("root(x,3)")] [InlineData("root(x,4)")] [InlineData("x^y")]
    [InlineData("floor(x)")] [InlineData("ceil(x)")] [InlineData("trunc(x)")]
    [InlineData("fact(x)")] [InlineData("dfact(x)")] [InlineData("choose(x,y)")] [InlineData("permute(x,y)")]
    public void EverySymbolicFunctionRoundTripsBeforeAndAfterSubstitution(string input)
    {
        var expression = ExpressionParser.Parse(input);
        var restored = MathJson.DeserializeExpression(MathJson.Serialize(expression, new()), new());
        Assert.Equal(expression, restored);
        // Substitution exercises the node reconstruction path independently of
        // deserialization. Stay within the common domain (including asin).
        var variables = new Dictionary<string, Expression> { ["x"] = Expression.Integer(1), ["y"] = Expression.Integer(1) };
        Assert.Equal(expression.Substitute(variables), restored.Substitute(variables));
    }

    [Theory]
    [InlineData("sqrt(x+i)")] [InlineData("arg(x+i)")] [InlineData("(x+i)^(y+i)")]
    public void ComplexNodesRoundTripWithTheirJointDomains(string input)
    {
        var expression = ExpressionParser.ParseComplex(input);
        var restored = MathJson.DeserializeComplex(MathJson.Serialize(expression, new()), new());
        Assert.Equal(expression, restored);
        var values = new Dictionary<string, Expression> { ["x"] = Expression.Integer(-3), ["y"] = Expression.Integer(2) };
        Assert.Equal(expression.Substitute(values), restored.Substitute(values));
    }

    [Fact]
    public void MalformedHeaderEnumsNullableStringsAndArityAreRejected()
    {
        var source = JsonNode.Parse(MathJson.Serialize(Expression.Symbol("x")))!;
        void Invalid(Action<JsonNode> change)
        {
            var candidate = source.DeepClone(); change(candidate);
            Assert.Throws<FormatException>(() => MathJson.DeserializeExpression(candidate.ToJsonString()));
        }
        Invalid(n => n["type"] = null);
        Invalid(n => n["root"]!["kind"] = null);
        Invalid(n => n["root"]!["kind"] = "Unknown");
        Invalid(n => n["root"]!["kind"] = "999");
        Invalid(n => n["root"]!["name"] = null);
        foreach (string? requirement in new string?[] { null, "Unknown", "999" })
            Invalid(n => n["conditions"] = new JsonArray(new JsonObject { ["requirement"] = requirement, ["argument"] = source["root"]!.DeepClone() }));
        foreach (string name in Enum.GetNames<NodeKind>().Except(["Constant", "Symbol"]))
        {
            Invalid(n => n["root"] = new JsonObject { ["kind"] = name, ["arguments"] = name == "Pi" ? new JsonArray(source["root"]!.DeepClone()) : new JsonArray() });
        }
        foreach (string? value in new string?[] { null, "", "-", "1/2/3", "a", ":", "+1" })
            Invalid(n => n["root"] = new JsonObject { ["kind"] = "Constant", ["value"] = value });
        foreach (string kind in new[] { "Power", "Root" })
            Invalid(n => n["root"] = new JsonObject { ["kind"] = kind, ["exponent"] = null, ["arguments"] = new JsonArray(source["root"]!.DeepClone()) });
        foreach (string degree in new[] { "0", "-1" })
            Invalid(n => n["root"] = new JsonObject { ["kind"] = "Root", ["exponent"] = degree, ["arguments"] = new JsonArray(source["root"]!.DeepClone()) });
    }

    [Fact]
    public void IntervalAndMatrixReadersValidateDimensionsAndBounds()
    {
        var interval = JsonNode.Parse(MathJson.Serialize(RealInterval.Exact(Rational.One)))!;
        Assert.Equal(RealInterval.Exact(Rational.One), MathJson.DeserializeInterval(interval.ToJsonString(), new()));
        foreach (string endpoint in new[] { "lower", "upper" })
        {
            var candidate = interval.DeepClone(); candidate[endpoint] = null;
            Assert.Throws<FormatException>(() => MathJson.DeserializeInterval(candidate.ToJsonString()));
        }
        foreach (var (rows, columns) in new[] { (-1, 0), (0, -1), (2, 3) })
        {
            var matrix = new JsonObject { ["version"] = 1, ["type"] = "matrix", ["rows"] = rows, ["columns"] = columns, ["entries"] = new JsonArray() };
            Assert.Throws<FormatException>(() => MathJson.DeserializeMatrix(matrix.ToJsonString()));
        }
        var empty = new ExpressionMatrix(new ComplexExpression[0, 3]);
        var restored = MathJson.DeserializeMatrix(MathJson.Serialize(empty, new()), new());
        Assert.Equal(0, restored.Rows); Assert.Equal(3, restored.Columns);
        Assert.Throws<ArgumentNullException>(() => MathJson.Serialize((Expression)null!));
        Assert.Throws<ArgumentNullException>(() => MathJson.Serialize((ComplexExpression)null!));
        Assert.Throws<ArgumentNullException>(() => MathJson.Serialize((ExpressionMatrix)null!));
    }
}
