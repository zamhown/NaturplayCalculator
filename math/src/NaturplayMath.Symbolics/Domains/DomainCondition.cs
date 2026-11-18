namespace NaturplayMath.Symbolics;

public sealed class DomainCondition
{
    internal Node ArgumentNode { get; }
    public DomainRequirement Requirement { get; }
    public Expression Argument => Expression.WithIntrinsicConditions(ArgumentNode);
    internal string Key => Requirement + ":" + ArgumentNode.Key;
    internal DomainCondition(Node argument, DomainRequirement requirement) { ArgumentNode = argument; Requirement = requirement; }
    public override string ToString() => ArgumentNode.Key + (Requirement switch
    { DomainRequirement.NonZero => " != 0", DomainRequirement.Nonnegative => " >= 0", DomainRequirement.Defined => " is defined", _ => " > 0" });
}

