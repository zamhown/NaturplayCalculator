using NaturplayMath.Numbers;
using Xunit;

namespace NaturplayMath.Tests.Numerics;

internal static class ReferenceAssert
{
    // Only the independent, ordinary-precision oracle is floating point. The library's
    // enclosure width is checked separately using exact arithmetic.
    internal static void Near(double expected, RealInterval actual, int digits = 20)
    {
        Assert.True(actual.Width <= new Rational(1, System.Numerics.BigInteger.Pow(10, digits)));
        var observed = (double)actual.Midpoint.Numerator / (double)actual.Midpoint.Denominator;
        Assert.True(double.IsFinite(observed));
        Assert.InRange(Math.Abs(observed - expected), 0, 2e-12 * Math.Max(1, Math.Abs(expected)));
    }
}
