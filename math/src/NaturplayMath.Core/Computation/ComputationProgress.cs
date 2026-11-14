using System.Numerics;

namespace NaturplayMath.Computation;

public enum ComputationStage { Preparing, Simplifying, Evaluating, Refining, Formatting, Completed, Cancelled, Failed }

/// <summary>Observed work, not a guessed overall percentage. Work totals refer to the current algorithm.</summary>
public sealed record ComputationProgress(
    Guid OperationId,
    ComputationStage Stage,
    string? Detail,
    int? WorkingPrecisionBits,
    BigInteger CompletedWork,
    BigInteger? TotalWork,
    BigInteger Operations,
    TimeSpan Elapsed);
