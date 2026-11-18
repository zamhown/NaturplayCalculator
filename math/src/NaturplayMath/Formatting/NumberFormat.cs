using System;
using System.Numerics;
using System.Text;
using NaturplayMath.Computation;
using NaturplayMath.Numbers;
namespace NaturplayMath.Formatting;

public sealed record NumberFormat
{
    public BigInteger Radix { get; }
    public int FractionalDigits { get; }
    public RoundingMode Rounding { get; }
    public bool TrimTrailingZeros { get; }
    public NumberFormat(int fractionalDigits = 10, BigInteger? radix = null, RoundingMode rounding = RoundingMode.TowardZero, bool trimTrailingZeros = true)
    {
        if (fractionalDigits < 0) throw new ArgumentOutOfRangeException(nameof(fractionalDigits));
        var basis = radix ?? 10;
        if (basis < 2) throw new ArgumentOutOfRangeException(nameof(radix));
        if (!Enum.IsDefined(rounding)) throw new ArgumentOutOfRangeException(nameof(rounding));
        FractionalDigits = fractionalDigits; Radix = basis; Rounding = rounding; TrimTrailingZeros = trimTrailingZeros;
    }
}
