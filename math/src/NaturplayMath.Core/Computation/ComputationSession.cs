using System.Diagnostics;
using System.Numerics;
namespace NaturplayMath.Computation;

internal sealed class ComputationSession
{
    private readonly ComputationContext context;
    private int? remaining;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Guid operationId = Guid.NewGuid();
    private TimeSpan lastReport;
    private ComputationStage stage = ComputationStage.Preparing;
    private string? detail;
    private int? precision;
    private BigInteger work, operations;
    private BigInteger? total;
    internal ComputationSession(ComputationContext context) { this.context = context; remaining = context.MaxOperations; }
    internal ComputationContext BoundContext => context.Bind(this);
    internal void Check()
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        operations++;
        if (remaining is int n)
        {
            if (n == 0) throw new CalculationBudgetExceededException();
            remaining = n - 1;
        }
        Publish(false);
    }
    internal void Report(ComputationStage stage, string? detail = null, int? precision = null,
        BigInteger work = default, BigInteger? total = null, bool force = false)
    {
        this.stage = stage; this.detail = detail; this.precision = precision; this.work = work; this.total = total;
        Publish(force);
    }
    private void Publish(bool force)
    {
        if (context.Progress is null) return;
        var elapsed = clock.Elapsed;
        if (!force && elapsed - lastReport < context.ProgressInterval) return;
        lastReport = elapsed;
        context.Progress.Report(new(operationId, stage, detail, precision, work, total, operations, elapsed));
    }
}
