using System.Numerics;
namespace NaturplayMath.Text;

public enum AngleUnit { Radians, Degrees }

public sealed record ExpressionParseOptions
{
    public BigInteger InputRadix { get; }
    public AngleUnit AngleUnit { get; }
    public int MaxDepth { get; }
    public ExpressionParseOptions(BigInteger? inputRadix = null, AngleUnit angleUnit = AngleUnit.Radians, int maxDepth = 512)
    {
        InputRadix = inputRadix ?? 10;
        if (InputRadix < 2) throw new ArgumentOutOfRangeException(nameof(inputRadix));
        if (!Enum.IsDefined(angleUnit)) throw new ArgumentOutOfRangeException(nameof(angleUnit));
        if (maxDepth < 1) throw new ArgumentOutOfRangeException(nameof(maxDepth));
        AngleUnit = angleUnit; MaxDepth = maxDepth;
    }
}

public sealed class ExpressionSyntaxException : FormatException
{
    public int Position { get; }
    public ExpressionSyntaxException(string message, int position) : base($"{message} (position {position}).") => Position = position;
}
