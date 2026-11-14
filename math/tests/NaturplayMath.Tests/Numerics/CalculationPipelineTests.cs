using System.Collections.Concurrent;
using System.Numerics;
using NaturplayMath.Calculation;
using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
using Xunit;

namespace NaturplayMath.Tests.Numerics;

public class CalculationPipelineTests
{
    [Theory]
    [InlineData("(1/3)*3", "1")]
    [InlineData("1.23456789e-12 * 10^20", "123456789")]
    [InlineData("-2^2 + 2^3^2", "508")]
    [InlineData("8/2(2+2)", "1")]
    [InlineData("root(-8,3)", "-2")]
    [InlineData("256 yroot 2 yroot 2", "4")]
    [InlineData("sin(pi/6)", "0.5")]
    [InlineData("sin(2)^2+cos(2)^2", "1")]
    [InlineData("1^x", "1")]
    [InlineData("lg(1000)+log(1/8,2)", "0")]
    [InlineData("20! / 19!", "20")]
    [InlineData("10 nCr 3 + 10 nPr 2 + 6!!", "258")]
    [InlineData("10nCr3+10nPr2+6!!", "258")]
    [InlineData("floor(pi)! + ceil(pi)!!", "14")]
    [InlineData("mod(-7,3)", "-1")]
    [InlineData("0xFF + 0b1", "256")]
    public void CalculatorSyntaxProducesExactResults(string input, string expected)
        => Assert.Equal(expected, CalculationEngine.Calculate(input).Text);

    [Fact]
    public void DisplaySettingsNeverDiscardInputOrIntermediateDigits()
    {
        var result = CalculationEngine.Calculate("1.23456789123456789+1/3", new(new NumberFormat(2)));
        Assert.Equal("1.56", result.Text);
        Assert.Equal("1.56790122456790122333", CalculationEngine.Calculate(result.Expression, new(new NumberFormat(20))).Text);
        Assert.Equal("1.91", CalculationEngine.Calculate(result.Expression, new(new NumberFormat(2, 16))).Text);
    }

    [Fact]
    public void AngleModeAndExplicitRadianAliasesAgree()
    {
        var options = new CalculationOptions(parsing: new(angleUnit: AngleUnit.Degrees));
        Assert.Equal("1", CalculationEngine.Calculate("sin(30)+cos(60)", options).Text);
        Assert.Equal("90", CalculationEngine.Calculate("arcsin(1)", options).Text);
        Assert.Equal("1", CalculationEngine.Calculate("sinrad(pi/2)", options).Text);
    }

    [Fact]
    public void ArbitraryRadicesRoundTripWithoutMachineIntegerLimits()
    {
        var radix = IntegerMath.Pow(10, 100) + 267;
        var value = new Rational((radix - 1) * radix * radix + 123 * radix + 456, radix);
        var text = NumberFormatter.Format(value, new NumberFormat(1, radix));
        Assert.Equal($"[{radix - 1}][123].[456]", text);
        Assert.Equal(value, NumberParser.Parse(text, radix));
        Assert.Equal(text, CalculationEngine.Calculate(text, new(new NumberFormat(1, radix), new(radix))).Text);
        for (int b = 2; b <= 36; b++)
        {
            var r = new Rational(123456789, IntegerMath.Pow(b, 20));
            Assert.Equal(r, NumberParser.Parse(NumberFormatter.Format(r, new NumberFormat(20, b)), b));
        }
    }

    private sealed class Observer(Action<ComputationProgress> callback) : IProgress<ComputationProgress>
    { public void Report(ComputationProgress value) => callback(value); }

    [Fact]
    public async Task AsyncPipelineUsesOneSessionAndCertifiesCompleteOutput()
    {
        var progress = new ConcurrentQueue<ComputationProgress>();
        var result = await CalculationEngine.CalculateAsync("ln(sin(1))", new(new NumberFormat(1000, trimTrailingZeros: false)),
            context: new(progress: new Observer(progress.Enqueue), progressInterval: TimeSpan.Zero));
        Assert.Equal(1003, result.Text.Length);
        Assert.StartsWith("-0.172603746269091678513410975863909", result.Text);
        Assert.Single(progress.Select(p => p.OperationId).Distinct());
        Assert.Equal(1, progress.Count(p => p.Stage == ComputationStage.Completed));
        Assert.Equal(ComputationStage.Completed, progress.Last().Stage);
        Assert.Contains(progress, p => p.Stage == ComputationStage.Formatting);
        using var writer = new StringWriter();
        await CalculationEngine.WriteAsync(result, writer, 17);
        Assert.Equal(result.Text, writer.ToString());
    }

    [Fact]
    public async Task CancellationDuringFormattingAndSharedBudgetAreEnforced()
    {
        using var stop = new CancellationTokenSource();
        var stages = new List<ComputationStage>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CalculationEngine.CalculateAsync("1/3", context: new(progress: new Observer(p =>
        {
            stages.Add(p.Stage);
            if (p.Stage == ComputationStage.Formatting) stop.Cancel();
        })), cancellationToken: stop.Token));
        Assert.DoesNotContain(ComputationStage.Completed, stages);
        Assert.Equal(ComputationStage.Cancelled, stages.Last());
        Assert.Throws<CalculationBudgetExceededException>(() => CalculationEngine.Calculate("10000!", context: new(maxOperations: 100)));
    }

    [Theory]
    [InlineData("sin(")]
    [InlineData("1..2")]
    [InlineData("pow(2)")]
    [InlineData("1+)")]
    public void SyntaxErrorsHaveSourcePositions(string input)
        => Assert.True(Assert.Throws<ExpressionSyntaxException>(() => ExpressionParser.Parse(input)).Position >= 0);
}
