using System.Numerics;
using NaturplayMath.Numbers;
namespace NaturplayMath.Text;

public static class NumberParser
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public static Rational ParseExact(string text, ComputationContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new FormatException("A number is required.");
        var session = (context ?? ComputationContext.Default).Start();
        var parts = text.Split('/');
        if (parts.Length == 1) return Parse(text, 10, session);
        if (parts.Length != 2) throw new FormatException("Expected an integer, decimal, or fraction.");
        var numerator = Parse(parts[0], 10, session);
        var denominator = Parse(parts[1], 10, session);
        if (!numerator.IsInteger || !denominator.IsInteger) throw new FormatException("Fraction components must be integers.");
        return new Rational(numerator.Numerator, denominator.Numerator);
    }
    /// <summary>Parses ordinary digits in bases 2..36; any larger base uses decimal digit tokens such as [12][345].[6].</summary>
    public static Rational Parse(string text, BigInteger? radix = null, ComputationContext? context = null)
        => Parse(text, radix ?? 10, (context ?? ComputationContext.Default).Start());
    internal static Rational Parse(string text, BigInteger radix, ComputationSession session)
    {
        if (radix < 2) throw new ArgumentOutOfRangeException(nameof(radix));
        if (string.IsNullOrWhiteSpace(text)) throw new FormatException("A number is required.");
        text = text.Trim();
        int index = 0, sign = 1, fractionalDigits = 0;
        bool point = false;
        if (text[0] is '+' or '-') { sign = text[0] == '-' ? -1 : 1; index++; }
        var digits = new List<BigInteger>();
        for (; index < text.Length; index++)
        {
            session.Check();
            if (text[index] == '.' && !point) { point = true; continue; }
            BigInteger digit;
            if (text[index] == '[')
            {
                int start = ++index;
                while (index < text.Length && text[index] is >= '0' and <= '9') { session.Check(); index++; }
                if (start == index || index == text.Length || text[index] != ']') throw new FormatException("Expected a decimal digit in brackets.");
                digit = Parse(text[start..index], 10, session).Numerator;
            }
            else
            {
                if (radix > 36) throw new FormatException("Bases above 36 require bracketed decimal digits, for example [12][34].");
                digit = Alphabet.IndexOf(char.ToUpperInvariant(text[index]));
            }
            if (digit.Sign < 0 || digit >= radix) throw new FormatException("Invalid numeric digit for this base.");
            digits.Add(digit);
            if (point) fractionalDigits++;
        }
        if (digits.Count == 0) throw new FormatException("A number requires digits.");
        var powers = new Dictionary<int, BigInteger>();
        BigInteger Scale(int length)
        {
            if (!powers.TryGetValue(length, out var scale)) powers[length] = scale = IntegerMath.Pow(radix, length, session);
            return scale;
        }
        BigInteger Read(int start, int length)
        {
            session.Check();
            if (length <= 32)
            {
                BigInteger result = 0;
                for (int i = start; i < start + length; i++) result = result * radix + digits[i];
                return result;
            }
            int half = length / 2;
            return Read(start, half) * Scale(length - half) + Read(start + half, length - half);
        }
        return new Rational(sign * Read(0, digits.Count), Scale(fractionalDigits));
    }
}
