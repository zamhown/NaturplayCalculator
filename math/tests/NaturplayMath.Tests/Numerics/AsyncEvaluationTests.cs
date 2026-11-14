using System.Collections.Concurrent;
using NaturplayMath.Symbolics;
using Xunit;

namespace NaturplayMath.Tests.Numerics;

public class AsyncEvaluationTests
{
    private sealed class Observer(Action<ComputationProgress> action) : IProgress<ComputationProgress>
    {
        public void Report(ComputationProgress value) => action(value);
    }

    [Fact]
    public async Task AsyncEvaluationReportsRealWorkAndFinalSuccess()
    {
        var events = new ConcurrentQueue<ComputationProgress>();
        var context = new ComputationContext(progress: new Observer(events.Enqueue), progressInterval: TimeSpan.Zero);
        var expression = Expression.Integer(1).Sin().Ln();
        var value = await expression.ApproximateAsync(100, context: context);
        Assert.True(value.Upper.Sign < 0);
        Assert.Equal(ComputationStage.Completed, events.Last().Stage);
        Assert.Contains(events, e => e.TotalWork > 0 && e.CompletedWork > 0);
        Assert.Contains(events, e => e.WorkingPrecisionBits > 0);
        Assert.Single(events.Select(e => e.OperationId).Distinct());
        var sequence = events.ToArray();
        for (int i = 1; i < sequence.Length; i++)
        {
            Assert.True(sequence[i].Elapsed >= sequence[i - 1].Elapsed);
            Assert.True(sequence[i].Operations >= sequence[i - 1].Operations);
        }
    }

    [Fact]
    public async Task CancellationDuringWorkDoesNotReportSuccessOrPoisonValues()
    {
        using var cancellation = new CancellationTokenSource();
        var events = new ConcurrentQueue<ComputationProgress>();
        var context = new ComputationContext(progress: new Observer(p =>
        {
            events.Enqueue(p);
            if (p.Stage == ComputationStage.Evaluating) cancellation.Cancel();
        }));
        var expression = Expression.Integer(1).Sin().Ln();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => expression.ApproximateAsync(1000000,
            context: context, cancellationToken: cancellation.Token));
        Assert.DoesNotContain(events, e => e.Stage == ComputationStage.Completed);
        Assert.Equal(ComputationStage.Cancelled, events.Last().Stage);
        Assert.True((await expression.ApproximateAsync(20)).Upper.Sign < 0);
    }

    [Fact]
    public async Task ConcurrentCallsHaveIndependentSessions()
    {
        var events = new ConcurrentQueue<ComputationProgress>();
        var context = new ComputationContext(progress: new Observer(events.Enqueue));
        var expression = Expression.Integer(1).Sin().Ln();
        var results = await Task.WhenAll(expression.ApproximateAsync(40, context: context), expression.ApproximateAsync(40, context: context));
        Assert.Equal(results[0], results[1]);
        Assert.Equal(2, events.Where(e => e.Stage == ComputationStage.Completed).Select(e => e.OperationId).Distinct().Count());
    }
}
