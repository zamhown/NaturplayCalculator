using NaturplayMath.Calculation;
using NaturplayMath.Formatting;
using NaturplayMath.Numbers;
using NaturplayMath.Serialization;
using NaturplayMath.Symbolics;
using Xunit;

namespace NaturplayMath.Tests.Symbolics;

public class WorkbenchBoundaryTests
{
    [Theory]
    [InlineData("matrix(row(1),row(2))+matrix(row(3),row(4))", "4", "6")]
    [InlineData("matrix(row(1),row(2))-matrix(row(3),row(4))", "-2", "-2")]
    [InlineData("2*matrix(row(1),row(2))", "2", "4")]
    [InlineData("matrix(row(1),row(2))*2", "2", "4")]
    [InlineData("matrix(row(1),row(2))/2", "1/2", "1")]
    [InlineData("-matrix(row(1),row(2))", "-1", "-2")]
    public void MatrixArithmeticRetainsShape(string input, string first, string second)
    {
        var matrix = MathEngine.Evaluate(input).Value.Matrix!;
        Assert.Equal(2, matrix.Rows); Assert.Equal(1, matrix.Columns);
        Assert.Equal(ComplexExpression.Constant(NumberParser.ParseExact(first)), matrix[0, 0]);
        Assert.Equal(ComplexExpression.Constant(NumberParser.ParseExact(second)), matrix[1, 0]);
    }

    [Fact]
    public void MatrixFunctionsStatisticsAndTransformsUseExactKernels()
    {
        Assert.Equal(ComplexExpression.Integer(11), MathEngine.Evaluate("row(1,2)*transpose(row(3,4))").Value.Matrix![0, 0]);
        Assert.Equal(ComplexExpression.Integer(8), MathEngine.Evaluate("matrix(row(2))^3").Value.Matrix![0, 0]);
        Assert.Equal(ComplexExpression.Constant(new Rational(1, 2)), MathEngine.Evaluate("inverse(matrix(row(2)))").Value.Matrix![0, 0]);
        Assert.Equal(-ComplexExpression.I, MathEngine.Evaluate("adjoint(row(i))").Value.Matrix![0, 0]);
        Assert.Equal(ComplexExpression.Integer(2), MathEngine.Evaluate("mean(row(1,2,3))").Value.Scalar);
        Assert.Equal(ComplexExpression.One, MathEngine.Evaluate("samplevariance(1,2,3)").Value.Scalar);
        Assert.Equal(ComplexExpression.One, MathEngine.Evaluate("correlation(row(1,2),row(3,4))").Value.Scalar);
        Assert.Equal(ComplexExpression.Constant(new Rational(1, 4)), MathEngine.Evaluate("covariance(row(1,2),row(3,4))").Value.Scalar);
        Assert.Equal(ComplexExpression.Constant(new Rational(1, 4)), MathEngine.Evaluate("Cov(row(1,2),row(3,4))").Value.Scalar);
        Assert.Equal(ComplexExpression.One, MathEngine.Evaluate("rho(row(1,2),row(3,4))").Value.Scalar);
        Assert.Equal(0, MathEngine.Evaluate("matrix()").Value.Matrix!.Rows);
        Assert.Equal(ExpressionParser.Parse("x^2+2*x+1"), MathEngine.Evaluate("expand((x+1)^2)").Value.Scalar!.Real);
        Assert.Equal(ExpressionParser.Parse("2*sqrt(2)"), MathEngine.Evaluate("radicals(sqrt(8))").Value.Scalar!.Real);
        Assert.Equal(ComplexExpression.Integer(3), MathEngine.Evaluate("+3").Value.Scalar);
    }

    [Theory]
    [InlineData("row(matrix())")]
    [InlineData("matrix(1)")]
    [InlineData("matrix(row(1),row(1,2))")]
    [InlineData("matrix(matrix(row(1),row(2)))")]
    [InlineData("1+row(2)")]
    [InlineData("row(1)^i")]
    [InlineData("row(1)^x")]
    [InlineData("row(1)^(1/2)")]
    [InlineData("row(1)mod2")]
    [InlineData("abs(row(1))")]
    [InlineData("samplevariance(1)")]
    public void InvalidTypesAreRejectedInsteadOfCoerced(string input)
        => Assert.Throws<MathDomainException>(() => MathEngine.Evaluate(input));

    [Theory]
    [InlineData("det()")]
    [InlineData("transpose(1,2)")]
    [InlineData("covariance(row(1))")]
    [InlineData("expand(1,2)")]
    public void ArityIsValidated(string input) => Assert.Throws<ArgumentException>(() => MathEngine.Evaluate(input));

    [Fact]
    public void InvalidArityIsRejectedEvenWhenArgumentsAreUnevaluated()
    {
        Assert.Throws<ArgumentException>(() => MathEngine.Evaluate("rank(F(x),1)"));
        Assert.Throws<ArgumentException>(() => MathEngine.Evaluate("sin(F(x),1)"));
        Assert.Throws<InsufficientPrecisionException>(() => MathEngine.Approximate(MathEngine.Evaluate("rank(F(x))").Value));
    }

    [Theory]
    [InlineData("2x")]
    [InlineData("radix(2.5,\"1\")")]
    [InlineData("radix(2,\"1)")]
    [InlineData("radix(2,1)")]
    [InlineData("\"1\"")]
    public void CanonicalSyntaxReportsInvalidInput(string input) => Assert.Throws<ExpressionSyntaxException>(() => MathEngine.Parse(input));

    [Fact]
    public void TypedTermsAreImmutableVisitableAndLosslesslySerializable()
    {
        var x = MathExpression.Symbol("x");
        var arguments = new[] { x }; var call = MathExpression.Call("F", arguments); arguments[0] = MathExpression.Symbol("y");
        Assert.Equal("x", call.Arguments[0].Name);
        var replacement = call.Rewrite(t => t.Kind == MathExpressionKind.Symbol ? MathExpression.From(Expression.Integer(3)) : t);
        Assert.Equal(ComplexExpression.Integer(3), replacement.Arguments[0].Scalar);
        foreach (var expression in new[] { x, call, replacement, MathEngine.Evaluate("row(x,i)").Value })
        {
            var restored = MathJson.DeserializeMathExpression(MathJson.Serialize(expression));
            Assert.Equal(expression, restored); Assert.Equal(expression.GetHashCode(), restored.GetHashCode());
            Assert.Equal(expression.ToString(), restored.ToString()); Assert.True(expression.Equals((object)restored));
            Assert.False(expression.Equals(null)); Assert.False(expression.Equals((object)1));
        }
        Assert.Throws<InvalidOperationException>(() => call.Rewrite(_ => null!));
        Assert.Throws<ArgumentNullException>(() => call.Rewrite(null!));
        Assert.Throws<ArgumentNullException>(() => MathExpression.Call("f", null!));
        Assert.Throws<ArgumentNullException>(() => MathExpression.Call("f", new MathExpression[] { null! }));
        Assert.Throws<ArgumentNullException>(() => MathExpression.From((ComplexExpression)null!));
        Assert.Throws<ArgumentNullException>(() => MathExpression.From((ExpressionMatrix)null!));
        Assert.Throws<ArgumentException>(() => MathExpression.Call(" "));
        Assert.Throws<ArgumentException>(() => MathExpression.Call("bad{"));
        Assert.Throws<ArgumentNullException>(() => MathJson.Serialize((MathExpression)null!));
        Assert.Throws<FormatException>(() => MathJson.DeserializeMathExpression("{\"version\":1,\"type\":\"term\",\"kind\":\"alien\"}"));
    }

    [Fact]
    public void SubstitutionIsSimultaneousAndConditionalRankStaysSymbolic()
    {
        var term = MathEngine.Parse("x+y").Expression;
        var variables = new Dictionary<string, MathExpression> { ["x"] = MathExpression.Symbol("y"), ["y"] = MathExpression.From(Expression.Integer(2)) };
        Assert.Equal(ExpressionParser.Parse("y+2"), MathEngine.Evaluate(term, variables: variables).Value.Scalar!.Real);
        var matrix = MathEngine.Evaluate("row(x,y)").Value;
        Assert.Equal(ComplexExpression.Integer(2), MathEngine.Evaluate(matrix, variables: variables).Value.Matrix![0, 1]);
        Assert.Equal(new[] { "x" }, MathEngine.Evaluate(matrix, variables: new Dictionary<string, MathExpression> { ["y"] = MathExpression.From(Expression.Integer(2)) }).Value.FreeSymbols);
        Assert.Throws<MathDomainException>(() => MathEngine.Evaluate(matrix, variables: new Dictionary<string, MathExpression> { ["x"] = MathExpression.From(ComplexExpression.I) }));
        Assert.Equal(MathExpressionKind.Call, MathEngine.Evaluate("rank(row(x))").Value.Kind);
        Assert.Equal(MathExpressionKind.Call, MathEngine.Evaluate("1+F(x)").Value.Kind);
        Assert.Equal(ComplexExpression.One, MathEngine.Evaluate("rank(row(x))", variables: new Dictionary<string, MathExpression> { ["x"] = MathExpression.From(Expression.Integer(2)) }).Value.Scalar);
        var traced = MathEngine.Evaluate("(1+2)*3", new(maxSteps: 1));
        Assert.Single(traced.Steps); Assert.Equal("+", traced.Steps[0].Rule);
        Assert.Equal(MathExpressionKind.Call, traced.Steps[0].Before.Kind);
        Assert.Equal(ComplexExpression.Integer(3), traced.Steps[0].After.Scalar);
    }

    [Fact]
    public async Task NumericResultsStreamMatricesAndKeepCancellationAndErrorsObservable()
    {
        var exact = MathEngine.Evaluate("matrix(row(1,i),row(2,3))").Value;
        var result = await MathEngine.ApproximateAsync(exact);
        Assert.Equal(2, result.Rows); Assert.Equal(2, result.Columns);
        using var output = new StringWriter(); await MathEngine.WriteAsync(result, output, 1);
        Assert.Equal("(1, 0 + (1)i; 2, 3)", output.ToString());
        Assert.Throws<InsufficientPrecisionException>(() => MathEngine.Approximate(MathEngine.Evaluate("F(x)").Value));
        Assert.Throws<UnboundSymbolException>(() => MathEngine.Approximate(MathEngine.Evaluate("x").Value));
        var variables = new Dictionary<string, MathExpression> { ["x"] = MathExpression.From(Expression.Integer(2)) };
        Assert.Equal(ComplexExpression.Integer(3), (await MathEngine.EvaluateAsync("x+1", variables: variables)).Value.Scalar);
        Assert.Equal("3", (await MathEngine.CalculateAsync("x+1", variables: variables)).Cells[0].RealText);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MathEngine.ApproximateAsync(exact, cancellationToken: cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MathEngine.WriteAsync(result, output, cancellationToken: cancelled.Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => MathEngine.WriteAsync(result, output, 0));
        await Assert.ThrowsAsync<ArgumentNullException>(() => MathEngine.WriteAsync(null!, output));
        await Assert.ThrowsAsync<ArgumentNullException>(() => MathEngine.WriteAsync(result, null!));
        Assert.Throws<ArgumentNullException>(() => MathEngine.Evaluate((MathExpression)null!));
        Assert.Throws<ArgumentNullException>(() => MathEngine.Evaluate((MathDocument)null!));
        Assert.Throws<ArgumentNullException>(() => MathEngine.Approximate(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvaluationOptions((InputSyntax)9));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvaluationOptions(maxSteps: -1));
        Assert.Throws<ExpressionSyntaxException>(() => MathEngine.Parse("1[open", new(syntax: InputSyntax.Calculator)));
    }

    [Fact]
    public async Task TermVisitorsTaskCancellationAndPresentationHaveExplicitContracts()
    {
        var symbol = MathExpression.Symbol("x"); var call = MathExpression.Call("F", symbol);
        Assert.Equal(new[] { "x" }, symbol.FreeSymbols);
        Assert.Equal(new[] { "x" }, call.FreeSymbols);
        Assert.Equal(call, call.Rewrite(t => t, new()));
        Assert.Empty(MathEngine.Evaluate(MathExpression.Call("F", MathExpression.From(Expression.Integer(1))), new(maxSteps: 10)).Steps);
        Assert.Equal(ComplexExpression.Integer(2), MathEngine.Evaluate("E(1,3)").Value.Scalar);
        Assert.Equal(ComplexExpression.One, MathEngine.Evaluate("Dx(1,3)").Value.Scalar);
        Assert.Equal(ComplexExpression.Integer(2), MathEngine.Evaluate(MathEngine.Parse("2"), new()).Value.Scalar);
        Assert.Equal(ComplexExpression.One, (await MathEngine.EvaluateAsync("1")).Value.Scalar);
        using var cancelled = new CancellationTokenSource();
        var events = new List<ComputationProgress>();
        Assert.Throws<OperationCanceledException>(() => MathEngine.Evaluate("1+2", context: new(cancelled.Token, progress: new CancelOnSimplify(cancelled, events))));
        Assert.Contains(events, e => e.Stage == ComputationStage.Cancelled);
        var value = MathEngine.Evaluate("matrix(row(1,2),row(3,4))").Value;
        Assert.Contains("3", MathExpressionFormatter.Format(value));
        Assert.Contains("bmatrix", MathExpressionFormatter.ToTeX(value));
        Assert.Equal("F(x)", MathExpressionFormatter.Format(call));
        Assert.Equal("x", MathExpressionFormatter.Format(symbol));
        Assert.Contains("where", MathExpressionFormatter.Format(MathEngine.Evaluate("x/x").Value));
        Assert.Equal(ExpressionFormatter.Format(ExpressionParser.Parse("x/x")), MathExpressionFormatter.Format(MathEngine.Evaluate("x/x").Value));
        Assert.Contains("i", MathExpressionFormatter.Format(MathEngine.Evaluate("1+i").Value));
        Assert.Contains("operatorname", MathExpressionFormatter.ToTeX(call));
        Assert.Contains("mathrm", MathExpressionFormatter.ToTeX(symbol));
        Assert.Contains("i", MathExpressionFormatter.ToTeX(MathEngine.Evaluate("1+i").Value));
        Assert.Equal("1", MathExpressionFormatter.ToTeX(MathEngine.Evaluate("1").Value));
        Assert.Throws<ArgumentNullException>(() => MathExpressionFormatter.Format(null!));
        Assert.Throws<ArgumentNullException>(() => MathExpressionFormatter.ToTeX(null!));
        using var matrixOutput = new StringWriter();
        await MathEngine.WriteAsync(await MathEngine.ApproximateAsync(MathEngine.Parse("row(1,2)").Expression), matrixOutput);
        Assert.Equal("(1, 2)", matrixOutput.ToString());
    }
    private sealed class CancelOnSimplify(CancellationTokenSource cancellation, List<ComputationProgress> events) : IProgress<ComputationProgress>
    {
        public void Report(ComputationProgress progress)
        {
            events.Add(progress);
            if (progress.Stage == ComputationStage.Simplifying) cancellation.Cancel();
        }
    }

    [Theory]
    [InlineData("cos(60)", "0.5")]
    [InlineData("tan(45)", "1")]
    [InlineData("asin(0)", "0")]
    [InlineData("acos(1)", "0")]
    [InlineData("atan(0)", "0")]
    [InlineData("arcsin(0)", "0")]
    [InlineData("arccos(1)", "0")]
    [InlineData("arctan(0)", "0")]
    public async Task AnglesAreDesugaredWithoutGlobalSettings(string input, string expected)
    {
        var result = await MathEngine.CalculateAsync(input, new(10), new(angleUnit: AngleUnit.Degrees));
        Assert.Equal(expected, result.Cells[0].RealText);
    }

    [Fact]
    public void CalculatorLexicalBoundariesAreExplicit()
    {
        var options = new EvaluationOptions(syntax: InputSyntax.Calculator);
        Assert.Throws<ExpressionSyntaxException>(() => MathEngine.Parse("2^", options));
        Assert.Equal(ComplexExpression.Integer(8), MathEngine.Evaluate("2^3", options).Value.Scalar);
        Assert.Equal(ComplexExpression.Integer(3), MathEngine.Evaluate("1+2", options).Value.Scalar);
        Assert.Equal(new[] { "_x" }, MathEngine.Evaluate("_x", options).Value.FreeSymbols);
        Assert.Contains("i", MathExpressionFormatter.Format(MathExpression.From(new ComplexExpression(Expression.Integer(1), Expression.Symbol("y")))));
    }
}
