using NaturplayMath.Calculation;
using NaturplayMath.Numbers;
using NaturplayMath.Serialization;
using NaturplayMath.Symbolics;
using Xunit;

namespace NaturplayMath.Tests.Symbolics;

public class WorkbenchTests
{
    [Fact]
    public void ParsingIsUnevaluatedAndOneFacadeHandlesScalarMatrixAndSymbols()
    {
        var parsed = MathEngine.Parse("1/0");
        Assert.Equal(MathExpressionKind.Call, parsed.Expression.Kind);
        Assert.Throws<DivideByZeroException>(() => MathEngine.Evaluate(parsed));
        var symbolic = MathEngine.Evaluate("x+1");
        Assert.Equal(new[] { "x" }, symbolic.Value.FreeSymbols);
        Assert.Equal(ExpressionParser.Parse("x+1"), symbolic.Value.Scalar!.Real);
        var matrix = MathEngine.Evaluate("transpose(matrix(row(1,2),row(3,4)))").Value.Matrix!;
        Assert.Equal(ComplexExpression.Integer(3), matrix[0, 1]);
        Assert.Equal(ComplexExpression.Integer(-2), MathEngine.Evaluate("det(matrix(row(1,2),row(3,4)))").Value.Scalar);
        Assert.Equal(ComplexExpression.Constant(new Rational(2, 3)), MathEngine.Evaluate("variance(1,2,3)").Value.Scalar);
    }

    [Theory]
    [InlineData("1[2]", "1")]
    [InlineData("sin30", "1/2")]
    [InlineData("√4", "2")]
    [InlineData("Ex(1,2,3)", "2")]
    [InlineData("D(1,2,3)", "2/3")]
    public void CalculatorSyntaxIsAnExplicitFrontend(string input, string expected)
    {
        var result = MathEngine.Evaluate(input, new(syntax: InputSyntax.Calculator, angleUnit: AngleUnit.Degrees));
        Assert.Equal(ComplexExpression.Constant(NumberParser.ParseExact(expected)), result.Value.Scalar);
    }

    [Fact]
    public void CanonicalNamesAreNeverSilentlySplitAndCallsRemainCalls()
    {
        Assert.Equal(new[] { "bc" }, MathEngine.Evaluate("bc").Value.FreeSymbols);
        Assert.Equal(ComplexExpression.Integer(6), MathEngine.Evaluate("bc", new(syntax: InputSyntax.Calculator),
            new Dictionary<string, MathExpression> { ["b"] = MathExpression.From(Expression.Integer(2)), ["c"] = MathExpression.From(Expression.Integer(3)) }).Value.Scalar);
        var unknown = MathEngine.Evaluate("f(x)").Value;
        Assert.Equal(MathExpressionKind.Call, unknown.Kind);
        Assert.Equal("f", unknown.Name);
        Assert.Equal(ComplexExpression.One, MathEngine.Evaluate("rank(2)").Value.Scalar);
        Assert.Throws<ExpressionSyntaxException>(() => MathEngine.Parse("1[2]"));
        Assert.Equal(ComplexExpression.Constant(new Rational(12345006, 1000)), MathEngine.Evaluate("radix(1000,\"[12][345].[6]\")").Value.Scalar);
    }

    [Fact]
    public void ExactValuesSurviveStorageAndCanBeUsedAsAnswers()
    {
        var exact = MathEngine.Evaluate("9007199254740993+1/3").Value;
        var restored = MathJson.DeserializeMathExpression(MathJson.Serialize(exact));
        var answer = MathEngine.Evaluate("Ans-9007199254740993", variables: new Dictionary<string, MathExpression> { ["Ans"] = restored });
        Assert.Equal(ComplexExpression.Constant(new Rational(1, 3)), answer.Value.Scalar);
        var guarded = MathEngine.Evaluate("x/x").Value;
        var back = MathJson.DeserializeMathExpression(MathJson.Serialize(guarded));
        Assert.Equal(new[] { "x" }, back.FreeSymbols);
        Assert.Throws<MathDomainException>(() => MathEngine.Evaluate(back, variables: new Dictionary<string, MathExpression> { ["x"] = MathExpression.From(Expression.Integer(0)) }));
    }

    [Fact]
    public async Task AsyncDisplayIsSeparateFromSymbolicValuesAndReportsProgress()
    {
        var events = new List<ComputationProgress>();
        var exact = MathEngine.Evaluate("ln(sin(1))").Value;
        var result = await MathEngine.ApproximateAsync(exact, new(40), context: new(progress: new Capture(events)));
        Assert.StartsWith("-0.172603746269091678513410975863", result.Cells[0].RealText);
        Assert.Contains(events, e => e.Stage == ComputationStage.Completed);
        Assert.Equal(exact, result.Value);
        Assert.Equal("0.5", (await MathEngine.ApproximateAsync(MathEngine.Evaluate("1/3").Value, new(1, radix: 16))).Cells[0].RealText);
    }
    private sealed class Capture(List<ComputationProgress> events) : IProgress<ComputationProgress>
    { public void Report(ComputationProgress value) => events.Add(value); }

    [Fact]
    public void DocumentsRetainAngleSettingsAndNamedOperators()
    {
        var degrees = new EvaluationOptions(syntax: InputSyntax.Calculator, angleUnit: AngleUnit.Degrees);
        var document = MathEngine.Parse("sin30[angle] // note", degrees);
        Assert.Equal(ComplexExpression.Constant(new Rational(1, 2)), MathEngine.Evaluate(document).Value.Scalar);
        Assert.Equal(new[] { "angle", " note" }, document.Comments);
        Assert.Equal(ComplexExpression.Integer(10), MathEngine.Evaluate("5nCr2", degrees).Value.Scalar);
        Assert.Equal(ComplexExpression.Integer(20), MathEngine.Evaluate("5nPr2", degrees).Value.Scalar);
        Assert.Equal(ComplexExpression.Integer(2), MathEngine.Evaluate("8yroot3", degrees).Value.Scalar);
        Assert.Equal(ComplexExpression.One, MathEngine.Evaluate("5mod2", degrees).Value.Scalar);
        Assert.Equal(ComplexExpression.Integer(3), MathEngine.Evaluate("(1,2,,3,4)^T", degrees).Value.Matrix![0, 1]);
        Assert.Equal("F", MathEngine.Evaluate("F(x)").Value.Name);
        Assert.Equal(ComplexExpression.Integer(6), MathEngine.Evaluate("x(2)", degrees,
            new Dictionary<string, MathExpression> { ["x"] = MathExpression.From(Expression.Integer(3)) }).Value.Scalar);
        Assert.Equal(ComplexExpression.Integer(2), MathEngine.Evaluate("Ex2", degrees).Value.Scalar);
    }

    [Fact]
    public async Task ConveniencePipelineHasOneTaskAndOnlyOneCompletedEvent()
    {
        var events = new List<ComputationProgress>();
        var result = await MathEngine.CalculateAsync("(1/3)*3", new(20), context: new(progress: new Capture(events)));
        Assert.Equal("1", result.Cells[0].RealText);
        Assert.Single(events, e => e.Stage == ComputationStage.Completed);
        Assert.Single(events.Select(e => e.OperationId).Distinct());
        using var output = new StringWriter();
        await MathEngine.WriteAsync(result, output, chunkSize: 2);
        Assert.Equal("1", output.ToString());
    }

    [Fact]
    public async Task DeferredOperationsRetainAngleMeaningAndUseCertifiedRank()
    {
        var document = MathEngine.Parse("sin(rank(row(pi)))", new(angleUnit: AngleUnit.Degrees));
        var pending = MathEngine.Evaluate(document).Value;
        var stored = MathJson.DeserializeMathExpression(MathJson.Serialize(pending));
        var events = new List<ComputationProgress>();
        var actual = await MathEngine.ApproximateAsync(stored, new(30), new(progress: new Capture(events)));
        Assert.StartsWith("0.017452406437283512819418978516", actual.Cells[0].RealText);
        Assert.Single(events, e => e.Stage == ComputationStage.Completed);
        var rank = await MathEngine.ApproximateAsync(MathEngine.Evaluate("rank(row(pi))").Value);
        Assert.Equal("1", rank.Cells[0].RealText);
        Assert.Equal("1", (await MathEngine.ApproximateAsync(MathEngine.Evaluate("rank(pi)").Value)).Cells[0].RealText);
        var degreeLiteral = MathEngine.Parse("sin30", new(syntax: InputSyntax.Calculator, angleUnit: AngleUnit.Degrees)).Expression;
        Assert.Equal(ComplexExpression.Constant(new Rational(1, 2)), MathEngine.Evaluate(MathJson.DeserializeMathExpression(MathJson.Serialize(degreeLiteral))).Value.Scalar);
    }
}
