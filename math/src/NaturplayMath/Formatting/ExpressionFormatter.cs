using System;
using System.Linq;
using NaturplayMath.Symbolics;
namespace NaturplayMath.Formatting;

public static class ExpressionFormatter
{
    public static string Format(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return FormatNode(expression.Root) + (expression.Conditions.Count == 0 ? "" : " where " + string.Join(", ", expression.Conditions.Select(FormatCondition)));
    }
    private static string FormatCondition(DomainCondition condition) => FormatNode(condition.ArgumentNode) + (condition.Requirement switch
    { DomainRequirement.NonZero => " != 0", DomainRequirement.Nonnegative => " >= 0", DomainRequirement.Defined => " is defined", _ => " > 0" });
    internal static string FormatNode(Node node) => node.Kind switch
    {
        NodeKind.Constant => node.Value.ToString(),
        NodeKind.Symbol => node.Name,
        NodeKind.Pi => "pi",
        NodeKind.Sum => "(" + string.Join(" + ", node.Children.Select(FormatNode)) + ")",
        NodeKind.Product => "(" + string.Join(" * ", node.Children.Select(FormatNode)) + ")",
        NodeKind.Power => "(" + FormatNode(node.Children[0]) + ")^(" + node.Exponent + ")",
        NodeKind.Root => "root(" + FormatNode(node.Children[0]) + ", " + node.Exponent + ")",
        NodeKind.GeneralPower => "(" + FormatNode(node.Children[0]) + ")^(" + FormatNode(node.Children[1]) + ")",
        NodeKind.Choose or NodeKind.Permute or NodeKind.Atan2 or NodeKind.ComplexRootReal or NodeKind.ComplexRootImag or NodeKind.ComplexPowerReal or NodeKind.ComplexPowerImag => node.Kind.ToString().ToLowerInvariant() + "(" + string.Join(", ", node.Children.Select(FormatNode)) + ")",
        _ => node.Kind.ToString().ToLowerInvariant() + "(" + FormatNode(node.Children[0]) + ")"
    };
    public static string ToTeX(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return Tex(expression.Root) + (expression.Conditions.Count == 0 ? "" : @"\quad\text{where } " + string.Join(@",\;", expression.Conditions.Select(c => Tex(c.ArgumentNode)
            + (c.Requirement switch { DomainRequirement.NonZero => @"\ne 0", DomainRequirement.Nonnegative => @"\ge 0", DomainRequirement.Defined => @"\text{ is defined}", _ => ">0" }))));
    }
    private static string Tex(Node node) => node.Kind switch
    {
        NodeKind.Constant => NumberFormatter.ToTeX(node.Value),
        NodeKind.Pi => @"\pi",
        NodeKind.Symbol => @"\mathrm{" + node.Name.Replace("_", @"\_") + "}",
        NodeKind.Sum => @"\left(" + string.Join(" + ", node.Children.Select(Tex)) + @"\right)",
        NodeKind.Product => @"\left(" + string.Join(@"\cdot ", node.Children.Select(Tex)) + @"\right)",
        NodeKind.Power => @"\left(" + Tex(node.Children[0]) + @"\right)^{" + node.Exponent + "}",
        NodeKind.Sqrt => @"\sqrt{" + Tex(node.Children[0]) + "}",
        NodeKind.Root => @"\sqrt[" + node.Exponent + "]{" + Tex(node.Children[0]) + "}",
        NodeKind.GeneralPower => @"\left(" + Tex(node.Children[0]) + @"\right)^{" + Tex(node.Children[1]) + "}",
        NodeKind.Asin => @"\arcsin\left(" + Tex(node.Children[0]) + @"\right)",
        NodeKind.Atan => @"\arctan\left(" + Tex(node.Children[0]) + @"\right)",
        NodeKind.Asinh => @"\operatorname{asinh}\left(" + Tex(node.Children[0]) + @"\right)",
        NodeKind.Abs => @"\left|" + Tex(node.Children[0]) + @"\right|",
        NodeKind.Floor => @"\left\lfloor " + Tex(node.Children[0]) + @"\right\rfloor",
        NodeKind.Ceiling => @"\left\lceil " + Tex(node.Children[0]) + @"\right\rceil",
        NodeKind.Factorial or NodeKind.DoubleFactorial => @"\left(" + Tex(node.Children[0]) + @"\right)" + (node.Kind == NodeKind.Factorial ? "!" : "!!"),
        NodeKind.Choose or NodeKind.Permute or NodeKind.Truncate or NodeKind.Atan2 or NodeKind.ComplexRootReal or NodeKind.ComplexRootImag or NodeKind.ComplexPowerReal or NodeKind.ComplexPowerImag => @"\operatorname{" + node.Kind.ToString().ToLowerInvariant() + @"}\left(" + string.Join(", ", node.Children.Select(Tex)) + @"\right)",
        _ => "\\" + node.Kind.ToString().ToLowerInvariant() + @"\left(" + Tex(node.Children[0]) + @"\right)"
    };
}
