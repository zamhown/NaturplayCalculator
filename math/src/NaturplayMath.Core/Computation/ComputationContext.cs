using System;
using System.Threading;
namespace NaturplayMath.Computation;

/// <summary>Options for one operation, never attached to a mathematical value.</summary>
public sealed class ComputationContext
{
    private ComputationSession? sharedSession;
    public static ComputationContext Default { get; } = new();
    public CancellationToken CancellationToken { get; }
    public int? MaxOperations { get; }
    public IProgress<ComputationProgress>? Progress { get; }
    public TimeSpan ProgressInterval { get; }
    public ComputationContext(CancellationToken cancellationToken = default, int? maxOperations = null,
        IProgress<ComputationProgress>? progress = null, TimeSpan? progressInterval = null)
    {
        if (maxOperations <= 0) throw new ArgumentOutOfRangeException(nameof(maxOperations));
        CancellationToken = cancellationToken; MaxOperations = maxOperations;
        Progress = progress;
        ProgressInterval = progressInterval ?? TimeSpan.FromMilliseconds(100);
        if (ProgressInterval < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(progressInterval));
    }
    internal ComputationSession Start() { var session = sharedSession ?? new ComputationSession(this); session.Check(); return session; }
    // A bound copy stays inside a single synchronous operation; public contexts remain safely reusable.
    internal ComputationContext Bind(ComputationSession session) => new(CancellationToken, MaxOperations, Progress, ProgressInterval) { sharedSession = session };
}
