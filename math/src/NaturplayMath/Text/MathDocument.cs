using NaturplayMath.Symbolics;
using NaturplayMath.Numbers;

namespace NaturplayMath.Text;

public enum InputSyntax { Canonical, Calculator }

/// <summary>Source metadata belongs to a document, not to the identity of its mathematical value.</summary>
public sealed record MathDocument(string Source, MathExpression Expression, IReadOnlyList<string> Comments, ExpressionParseOptions Parsing);

internal sealed class MathExpressionDomain(ExpressionParseOptions options) : IExpressionDomain<MathExpression>
{
    public MathExpression Number(Rational value) => MathExpression.From(Expression.Constant(value));
    public MathExpression Symbol(string name) => MathExpression.Symbol(name);
    public MathExpression Negate(MathExpression value) => MathExpression.Call("negate", value);
    public MathExpression Binary(string operation, MathExpression left, MathExpression right) => MathExpression.Call(operation, left, right);
    public MathExpression Function(string name, IReadOnlyList<MathExpression> args, int position)
    {
        var lower = name.ToLowerInvariant();
        if (options.AngleUnit == AngleUnit.Degrees && args.Count == 1)
        {
            var radians = MathExpression.From(Expression.Pi / Expression.Integer(180));
            if (lower is "sin" or "cos" or "tan") return MathExpression.Call(lower + "rad", Binary("*", args[0], radians));
            if (lower is "asin" or "acos" or "atan" or "arcsin" or "arccos" or "arctan")
                return Binary("/", MathExpression.Call(lower + "rad", args[0]), radians);
        }
        return MathExpression.Call(name, args.ToArray());
    }
}
