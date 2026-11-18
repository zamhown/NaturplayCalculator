using NaturplayMath.Numbers;
namespace NaturplayMath.Numerics;

/// <summary>Numerical accuracy and refinement policy; never changes an exact value or display settings.</summary>
public sealed record ApproximationOptions
{
    public Rational AbsoluteTolerance { get; }
    public int MaxRefinements { get; }
    public ApproximationOptions(Rational absoluteTolerance, int maxRefinements = 16)
    {
        if (absoluteTolerance.Sign <= 0) throw new ArgumentOutOfRangeException(nameof(absoluteTolerance));
        if (maxRefinements < 1) throw new ArgumentOutOfRangeException(nameof(maxRefinements));
        AbsoluteTolerance = absoluteTolerance;
        MaxRefinements = maxRefinements;
    }
}
