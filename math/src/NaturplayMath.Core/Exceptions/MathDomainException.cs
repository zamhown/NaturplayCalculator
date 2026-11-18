namespace NaturplayMath.Exceptions;

public sealed class MathDomainException : ArithmeticException
{
    public MathDomainException(string message) : base(message) { }
}
