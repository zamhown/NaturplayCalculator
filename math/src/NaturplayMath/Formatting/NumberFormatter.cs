using System.Numerics;
using System.Text;
using System.Globalization;
using NaturplayMath.Numbers;
namespace NaturplayMath.Formatting;

public static class NumberFormatter
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public static string Format(Rational value, NumberFormat? format = null, ComputationContext? context = null)
    {
        format ??= new NumberFormat();
        var session = (context ?? ComputationContext.Default).Start();
        session.Report(ComputationStage.Formatting, "radix conversion", force: true);
        return Format(value, format, session);
    }
    internal static string Format(Rational value, NumberFormat format, ComputationSession session)
    {
        if (value.IsInteger && format.TrimTrailingZeros)
            return (value.Sign < 0 ? "-" : "") + Digits(BigInteger.Abs(value.Numerator), format.Radix, session);
        var scale = IntegerMath.Pow(format.Radix, format.FractionalDigits, session);
        return FormatUnits(RoundToUnits(value, scale, format.Rounding), scale, format, session);
    }
    internal static string FormatUnits(BigInteger units, BigInteger scale, NumberFormat format, ComputationSession session)
    {
        var magnitude = BigInteger.Abs(units);
        var integer = BigInteger.DivRem(magnitude, scale, out var fraction);
        var text = Digits(integer, format.Radix, session);
        if (format.FractionalDigits > 0)
        {
            var tail = Digits(fraction, format.Radix, session, format.FractionalDigits, format.TrimTrailingZeros);
            if (tail.Length > 0) text += "." + tail;
        }
        return units.Sign < 0 ? "-" + text : text;
    }
    internal static BigInteger RoundToUnits(Rational value, BigInteger scale, RoundingMode rounding)
    {
        var q = BigInteger.DivRem(value.Numerator * scale, value.Denominator, out var remainder);
        if (rounding == RoundingMode.Floor && remainder.Sign < 0) return q - 1;
        if (rounding == RoundingMode.Ceiling && remainder.Sign > 0) return q + 1;
        var twice = 2 * BigInteger.Abs(remainder);
        return rounding == RoundingMode.NearestEven && (twice > value.Denominator || twice == value.Denominator && !q.IsEven) ? q + value.Sign : q;
    }
    private static string Digits(BigInteger value, BigInteger radix, ComputationSession session, int minimumDigits = 1, bool trimEnd = false)
    {
        // Divide-and-conquer radix conversion avoids a full large-integer division for every digit.
        var powers = new List<BigInteger> { radix };
        int capacity = 1;
        while (value >= powers[^1] || capacity < minimumDigits)
        {
            session.Check();
            powers.Add(powers[^1] * powers[^1]);
            capacity = checked(capacity * 2);
        }
        var digits = new BigInteger[capacity];
        int completed = 0;
        void Write(BigInteger number, int level, int offset, int length)
        {
            session.Check();
            if (length <= 32)
            {
                for (int i = offset + length - 1; i >= offset; i--)
                {
                    session.Check(); number = BigInteger.DivRem(number, radix, out var digit);
                    digits[i] = digit;
                }
                completed += length;
                session.Report(ComputationStage.Formatting, "radix conversion", work: completed, total: capacity);
                return;
            }
            var high = BigInteger.DivRem(number, powers[level - 1], out var low);
            Write(high, level - 1, offset, length / 2);
            Write(low, level - 1, offset + length / 2, length / 2);
        }
        Write(value, powers.Count - 1, 0, capacity);
        int first = 0;
        while (first < digits.Length - minimumDigits && digits[first].IsZero) first++;
        int end = digits.Length;
        if (trimEnd) while (end > first && digits[end - 1].IsZero) end--;
        if (radix <= 36)
        {
            var chars = new char[end - first];
            for (int i = first; i < end; i++) { session.Check(); chars[i - first] = Alphabet[(int)digits[i]]; }
            return new string(chars);
        }
        var builder = new StringBuilder();
        for (int i = first; i < end; i++) { session.Check(); builder.Append('[').Append(digits[i].ToString(CultureInfo.InvariantCulture)).Append(']'); }
        return builder.ToString();
    }
    public static string ToTeX(Rational value) => value.Denominator.IsOne ? value.ToString()
        : (value.Sign < 0 ? "-" : "") + @"\frac{" + BigInteger.Abs(value.Numerator) + "}{" + value.Denominator + "}";
}
