using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
namespace NaturplayMath.Text;

internal interface IExpressionDomain<T>
{
    T Number(Rational value);
    T Symbol(string name);
    T Negate(T value);
    T Binary(string operation, T left, T right);
    T Function(string name, IReadOnlyList<T> args, int position);
}

internal sealed class RealExpressionDomain(ExpressionParseOptions options, ComputationContext context) : IExpressionDomain<Expression>
{
    public Expression Number(Rational value) => Expression.Constant(value);
    public Expression Symbol(string name) => name.ToLowerInvariant() switch
    { "pi" or "π" => Expression.Pi, "e" => Expression.Integer(1).Exp(), _ => Expression.Symbol(name) };
    public Expression Negate(Expression value) => value.Negate(context);
    public Expression Binary(string operation, Expression left, Expression right) => operation switch
    {
        "+" => left.Add(right, context),
        "-" => left.Subtract(right, context),
        "*" => left.Multiply(right, context),
        "/" => left.Divide(right, context),
        "^" => left.Pow(right, context),
        "mod" => left.Remainder(right),
        "ncr" => left.Choose(right, context),
        "npr" => left.Permute(right, context),
        "yroot" => left.Pow(Expression.Integer(1).Divide(right, context), context),
        _ => throw new InvalidOperationException("Unknown operator.")
    };
    public Expression Function(string name, IReadOnlyList<Expression> args, int position)
    {
        int arity = name is "root" or "pow" or "choose" or "permute" or "ncr" or "npr" or "mod" or "log" ? 2 : 1;
        if (args.Count != arity) throw new ExpressionSyntaxException($"{name} requires {arity} argument(s)", position);
        var x = args[0];
        Expression ToRadians(Expression value) => options.AngleUnit == AngleUnit.Degrees ? value.Multiply(Expression.Pi, context).Divide(Expression.Integer(180), context) : value;
        Expression FromRadians(Expression value) => options.AngleUnit == AngleUnit.Degrees ? value.Multiply(Expression.Integer(180), context).Divide(Expression.Pi, context) : value;
        return name switch
        {
            "sqrt" => x.Sqrt(context),
            "root" => x.Pow(Expression.Integer(1).Divide(args[1], context), context),
            "pow" => x.Pow(args[1], context),
            "exp" => x.Exp(),
            "ln" => x.Ln(),
            "lg" => x.Log(Expression.Integer(10), context),
            "log" => x.Log(args[1], context),
            "abs" => x.Abs(),
            "sin" => ToRadians(x).Sin(),
            "cos" => ToRadians(x).Cos(),
            "tan" => ToRadians(x).Tan(),
            "sinrad" => x.Sin(),
            "cosrad" => x.Cos(),
            "tanrad" => x.Tan(),
            "asin" or "arcsin" => FromRadians(x.Asin()),
            "acos" or "arccos" => FromRadians(x.Acos()),
            "atan" or "arctan" => FromRadians(x.Atan()),
            "asinrad" or "arcsinrad" => x.Asin(),
            "acosrad" or "arccosrad" => x.Acos(),
            "atanrad" or "arctanrad" => x.Atan(),
            "sinh" => x.Sinh(),
            "cosh" => x.Cosh(),
            "tanh" => x.Tanh(),
            "asinh" or "arsh" => x.Asinh(),
            "acosh" or "arch" => x.Acosh(),
            "atanh" or "arth" => x.Atanh(),
            "fact" or "factorial" => x.Factorial(context),
            "dfact" or "doublefactorial" => x.DoubleFactorial(context),
            "floor" => x.Floor(),
            "ceil" or "ceiling" => x.Ceiling(),
            "trunc" or "truncate" => x.Truncate(),
            "choose" or "ncr" => x.Choose(args[1], context),
            "permute" or "npr" => x.Permute(args[1], context),
            "mod" => x.Remainder(args[1]),
            "conj" or "re" => x,
            "im" => x.Multiply(Expression.Integer(0), context),
            "arg" => Expression.Integer(0).Atan2(x),
            _ => throw new ExpressionSyntaxException("Unknown function", position)
        };
    }
}

internal sealed class ComplexExpressionDomain(ExpressionParseOptions options, ComputationContext context,
    IReadOnlyDictionary<string, ComplexExpression>? variables) : IExpressionDomain<ComplexExpression>
{
    private readonly RealExpressionDomain realDomain = new(options, context);
    public ComplexExpression Number(Rational value) => ComplexExpression.Constant(value);
    public ComplexExpression Symbol(string name)
    {
        if (variables is not null && variables.TryGetValue(name, out var value)) return value;
        return name == "i" ? ComplexExpression.I : new(realDomain.Symbol(name));
    }
    public ComplexExpression Negate(ComplexExpression value) => -value;
    private static Expression Real(ComplexExpression value)
    {
        if (value.Imaginary.Root.Kind != NodeKind.Constant || !value.Imaginary.Root.Value.IsZero)
            throw new MathDomainException("This operation requires a real argument.");
        return new(value.Real.Root, value.Real.Conditions.Concat(value.Imaginary.Conditions));
    }
    public ComplexExpression Binary(string operation, ComplexExpression left, ComplexExpression right) => operation switch
    {
        "+" => left + right,
        "-" => left - right,
        "*" => left * right,
        "/" => left / right,
        "^" => left.Pow(right, context),
        "yroot" => Root(left, right),
        _ => new(realDomain.Binary(operation, Real(left), Real(right)))
    };
    private ComplexExpression Root(ComplexExpression value, ComplexExpression degree)
    {
        if (value.IsReal && degree.IsReal && degree.Real.TryGetRational(out var n) && n.IsInteger && !n.Numerator.IsEven)
        {
            if (n.Sign > 0) return new(value.Real.NthRoot(n.Numerator, context));
            return new(Expression.Integer(1).Divide(value.Real.NthRoot(-n.Numerator, context), context));
        }
        return value.Pow(ComplexExpression.One / degree, context);
    }
    public ComplexExpression Function(string name, IReadOnlyList<ComplexExpression> args, int position)
    {
        int arity = name is "root" or "pow" or "choose" or "permute" or "ncr" or "npr" or "mod" or "log" ? 2 : 1;
        if (args.Count != arity) throw new ExpressionSyntaxException($"{name} requires {arity} argument(s)", position);
        var x = args[0];
        ComplexExpression ToRadians(ComplexExpression value) => options.AngleUnit == AngleUnit.Degrees
            ? value * new ComplexExpression(Expression.Pi) / ComplexExpression.Integer(180) : value;
        ComplexExpression FromRadians(ComplexExpression value) => options.AngleUnit == AngleUnit.Degrees
            ? value * ComplexExpression.Integer(180) / new ComplexExpression(Expression.Pi) : value;
        return name switch
        {
            "sqrt" => x.Sqrt(),
            "root" => Root(x, args[1]),
            "pow" => x.Pow(args[1], context),
            "exp" => x.Exp(),
            "ln" => x.Ln(),
            "lg" => x.Ln() / ComplexExpression.Integer(10).Ln(),
            "log" => x.Ln() / args[1].Ln(),
            "abs" => new(x.Abs()),
            "sin" => ToRadians(x).Sin(),
            "cos" => ToRadians(x).Cos(),
            "tan" => ToRadians(x).Tan(),
            "sinrad" => x.Sin(),
            "cosrad" => x.Cos(),
            "tanrad" => x.Tan(),
            "asin" or "arcsin" => FromRadians(x.Asin()),
            "acos" or "arccos" => FromRadians(x.Acos()),
            "atan" or "arctan" => FromRadians(x.Atan()),
            "asinrad" or "arcsinrad" => x.Asin(),
            "acosrad" or "arccosrad" => x.Acos(),
            "atanrad" or "arctanrad" => x.Atan(),
            "sinh" => x.Sinh(),
            "cosh" => x.Cosh(),
            "tanh" => x.Tanh(),
            "asinh" or "arsh" => x.Asinh(),
            "acosh" or "arch" => x.Acosh(),
            "atanh" or "arth" => x.Atanh(),
            "conj" => x.Conjugate(),
            "arg" => new(x.Argument()),
            "re" => new(x.Real),
            "im" => new(x.Imaginary),
            _ => new(realDomain.Function(name, args.Select(Real).ToArray(), position))
        };
    }
}
