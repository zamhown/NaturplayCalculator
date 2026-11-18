using NaturplayMath.Numbers;
namespace NaturplayMath.Formatting;

public static class IntervalFormatter
{
    /// <summary>Outward rounding preserves the enclosure even at fewer display digits.</summary>
    public static string FormatBounds(this RealInterval interval, int fractionalDigits = 10, System.Numerics.BigInteger? radix = null, ComputationContext? context = null)
    {
        var session = (context ?? ComputationContext.Default).Start();
        return "[" + NumberFormatter.Format(interval.Lower, new NumberFormat(fractionalDigits, radix, RoundingMode.Floor), session)
            + ", " + NumberFormatter.Format(interval.Upper, new NumberFormat(fractionalDigits, radix, RoundingMode.Ceiling), session) + "]";
    }
    /// <summary>Returns digits only when both endpoints round to the same displayed number.</summary>
    public static bool TryFormat(this RealInterval interval, NumberFormat format, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? text, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(format);
        var session = (context ?? ComputationContext.Default).Start();
        if (interval.IsExact) { text = NumberFormatter.Format(interval.Lower, format, session); return true; }
        var scale = IntegerMath.Pow(format.Radix, format.FractionalDigits, session);
        var lower = NumberFormatter.RoundToUnits(interval.Lower, scale, format.Rounding);
        session.Check();
        var upper = NumberFormatter.RoundToUnits(interval.Upper, scale, format.Rounding);
        text = lower == upper ? NumberFormatter.FormatUnits(lower, scale, format, session) : null;
        return text is not null;
    }
}
