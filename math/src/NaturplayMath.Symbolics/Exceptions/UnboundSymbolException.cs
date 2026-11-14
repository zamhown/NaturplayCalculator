namespace NaturplayMath.Exceptions;

public sealed class UnboundSymbolException : InvalidOperationException
{
    public UnboundSymbolException(string name) : base("No value was supplied for symbol " + name + ".") { }
}
