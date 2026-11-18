using NaturplayMath.Calculation;
using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
using Xunit;

namespace NaturplayMath.Tests.Numerics;

public class AsyncAndFailureTests
{
    private sealed class Observer(Action<ComputationProgress> report) : IProgress<ComputationProgress>
    { public void Report(ComputationProgress value) => report(value); }

    [Fact]
    public async Task EveryAsyncEntryPointSupportsExactVariableSubstitution()
    {
        var x = Expression.Symbol("x");
        var variables = new Dictionary<string, Rational> { ["x"] = new(3, 2) };
        var expressions = new Dictionary<string, Expression> { ["x"] = Expression.Constant(variables["x"]) };
        var context = new ComputationContext();
        var options = new ApproximationOptions(new Rational(1, 1000));
        Assert.Equal(RealInterval.Exact(new Rational(3, 2)), await NumericalEvaluator.EvaluateAsync(x, options, variables, context));
        Assert.Equal(RealInterval.Exact(Rational.One), await NumericalEvaluator.EvaluateAsync(Expression.Integer(1), options));
        Assert.Equal(RealInterval.Exact(new Rational(3, 2)), await x.ApproximateAsync(20, variables: variables, context: context));
        Assert.Equal("1.5", (await CalculationEngine.CalculateAsync(x, variables: expressions, context: context)).Text);
        Assert.Equal("1", (await CalculationEngine.CalculateAsync(Expression.Integer(1))).Text);
        Assert.Equal("1.5", (await CalculationEngine.CalculateAsync("x", variables: expressions)).Text);
        var complex = new ComplexExpression(x, -x);
        var enclosure = await complex.ApproximateAsync(20, 10, variables, context);
        Assert.Equal(RealInterval.Exact(new Rational(3, 2)), enclosure.Real);
        Assert.Equal(RealInterval.Exact(new Rational(-3, 2)), enclosure.Imaginary);
        Assert.Equal(RealInterval.Exact(Rational.One), (await ComplexExpression.One.ApproximateAsync(20)).Real);
        Assert.Equal("0 - 1i", (await CalculationEngine.CalculateComplexAsync(-ComplexExpression.I, context: context)).Text);
        Assert.Equal("1 + 0i", (await CalculationEngine.CalculateComplexAsync(ComplexExpression.One)).Text);
        Assert.Equal("0 - 1i", (await CalculationEngine.CalculateComplexAsync("z", variables: new Dictionary<string, ComplexExpression> { ["z"] = -ComplexExpression.I })).Text);
    }

    [Theory]
    [InlineData("complex")]
    [InlineData("matrix")]
    [InlineData("rank")]
    [InlineData("complex-display")]
    public void CancellationDuringEvaluationEmitsOnlyOneTerminalCancellation(string entryPoint)
    {
        using var stop = new CancellationTokenSource();
        var progress = new List<ComputationProgress>();
        var context = new ComputationContext(stop.Token, progress: new Observer(p =>
        {
            progress.Add(p);
            if (p.Stage == ComputationStage.Evaluating) stop.Cancel();
        }), progressInterval: TimeSpan.Zero);
        var z = new ComplexExpression(Expression.Pi);
        var matrix = new ExpressionMatrix(new[,] { { z } });
        var options = new ApproximationOptions(new Rational(1, 100));
        Assert.Throws<OperationCanceledException>(() =>
        {
            switch (entryPoint)
            {
                case "complex": z.Approximate(20, context: context); break;
                case "matrix": matrix.Approximate(options, context: context); break;
                case "rank": matrix.RankCertified(options, context: context); break;
                default: CalculationEngine.CalculateComplex(z, context: context); break;
            }
        });
        Assert.Equal(ComputationStage.Cancelled, progress[^1].Stage);
        Assert.Single(progress, p => p.Stage == ComputationStage.Cancelled);
        Assert.DoesNotContain(progress, p => p.Stage is ComputationStage.Completed or ComputationStage.Failed);
        Assert.Single(progress.Select(p => p.OperationId).Distinct());
    }

    [Theory]
    [InlineData("complex")]
    [InlineData("matrix")]
    [InlineData("rank")]
    [InlineData("complex-display")]
    public void EvaluationFailureEmitsFailureInsteadOfCompletion(string entryPoint)
    {
        var progress = new List<ComputationProgress>();
        var context = new ComputationContext(progress: new Observer(progress.Add), progressInterval: TimeSpan.Zero);
        var z = new ComplexExpression(Expression.Symbol("missing"));
        var matrix = new ExpressionMatrix(new[,] { { z } });
        var options = new ApproximationOptions(new Rational(1, 100));
        Assert.Throws<UnboundSymbolException>(() =>
        {
            switch (entryPoint)
            {
                case "complex": z.Approximate(20, context: context); break;
                case "matrix": matrix.Approximate(options, context: context); break;
                case "rank": matrix.RankCertified(options, context: context); break;
                default: CalculationEngine.CalculateComplex(z, context: context); break;
            }
        });
        Assert.Equal(ComputationStage.Failed, progress[^1].Stage);
        Assert.DoesNotContain(progress, p => p.Stage == ComputationStage.Completed);
    }

    [Fact]
    public async Task WritingRejectsInvalidChunksAndObservesCancellation()
    {
        var result = CalculationEngine.Calculate("1/3");
        using var writer = new StringWriter();
        await Assert.ThrowsAsync<ArgumentNullException>(() => CalculationEngine.WriteAsync(null!, writer));
        await Assert.ThrowsAsync<ArgumentNullException>(() => CalculationEngine.WriteAsync(result, null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CalculationEngine.WriteAsync(result, writer, 0));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CalculationEngine.WriteAsync(result, writer, cancellationToken: new(true)));
        Assert.Equal("", writer.ToString());
        await CalculationEngine.WriteAsync(result, writer, 1);
        Assert.Equal(result.Text, writer.ToString());
    }

    [Fact]
    public void InvalidExecutionOptionsAreRejectedBeforeCalculating()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ComputationContext(maxOperations: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ComputationContext(progressInterval: TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CalculationOptions(maxRefinements: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Expression.Pi.Approximate(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Expression.Pi.Approximate(1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Expression.Pi.Approximate(1, maxRefinements: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ComplexExpression.I.Approximate(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ComplexExpression.I.Approximate(1, 1));
        Assert.Throws<ArgumentNullException>(() => ComplexNumericalExtensions.Approximate(null!, 1));
    }

    [Fact]
    public void ProgressPreservesStagePrecisionWorkAndTaskIdentity()
    {
        var reports = new List<ComputationProgress>();
        var session = new ComputationContext(progress: new Observer(reports.Add), progressInterval: TimeSpan.FromDays(1)).Start();
        session.Report(ComputationStage.Evaluating, "test algorithm", 1234, 7, 13, true);
        var report = Assert.Single(reports);
        Assert.NotEqual(Guid.Empty, report.OperationId);
        Assert.Equal(ComputationStage.Evaluating, report.Stage);
        Assert.Equal("test algorithm", report.Detail);
        Assert.Equal(1234, report.WorkingPrecisionBits);
        Assert.Equal(new System.Numerics.BigInteger(7), report.CompletedWork);
        Assert.Equal(new System.Numerics.BigInteger(13), report.TotalWork);
        Assert.True(report.Operations >= 1); Assert.True(report.Elapsed >= TimeSpan.Zero);
        session.Report(ComputationStage.Completed, force: true);
        Assert.Equal(report.OperationId, reports[^1].OperationId);
        Assert.Null(reports[^1].TotalWork);
    }

    [Fact]
    public void DisplayRoundingRefinesUntilCertifiedOrFailsWithoutGuessing()
    {
        var progress = new List<ComputationProgress>();
        var context = new ComputationContext(progress: new Observer(progress.Add));
        var closeToZero = Expression.Integer(2).Sqrt() - Expression.Constant(NumberParser.ParseExact("1.4142135623730950488016887242096980785696"));
        var result = CalculationEngine.Calculate(closeToZero, new(new NumberFormat(0, rounding: RoundingMode.Floor), maxRefinements: 3), context: context);
        Assert.Equal("0", result.Text);
        Assert.Contains(progress, p => p.Stage == ComputationStage.Refining && p.Detail == "rounding boundary needs more precision");
        Assert.Throws<InsufficientPrecisionException>(() => CalculationEngine.Calculate(closeToZero,
            new(new NumberFormat(0, rounding: RoundingMode.Floor), maxRefinements: 1)));
    }
}
