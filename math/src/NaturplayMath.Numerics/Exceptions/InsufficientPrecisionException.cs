namespace NaturplayMath.Exceptions;

public sealed class InsufficientPrecisionException : ArithmeticException
{
    public InsufficientPrecisionException(string message) : base(message) { }
}
