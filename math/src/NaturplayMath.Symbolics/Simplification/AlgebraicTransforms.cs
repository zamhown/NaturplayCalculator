using System.Numerics;
using NaturplayMath.Numbers;

namespace NaturplayMath.Symbolics;

/// <summary>Explicit, exact transformations. None are implicitly run by numerical evaluation.</summary>
public static class AlgebraicTransforms
{
    public static Expression Expand(this Expression expression, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var session = (context ?? ComputationContext.Default).Start();
        Node Product(Node a, Node b)
        {
            session.Check();
            if (a.Kind == NodeKind.Sum) return ExpressionNormalizer.Add(a.Children.Select(c => Product(c, b)), session);
            if (b.Kind == NodeKind.Sum) return ExpressionNormalizer.Add(b.Children.Select(c => Product(a, c)), session);
            return ExpressionNormalizer.Multiply(new[] { a, b }, session);
        }
        Node ExpandNode(Node node)
        {
            if (node.Kind == NodeKind.Product) return node.Children.Aggregate(Node.Constant(Rational.One), Product);
            if (node.Kind == NodeKind.Power && node.Exponent.Sign > 0)
            {
                var result = Node.Constant(Rational.One); var factor = node.Children[0]; var exponent = node.Exponent;
                while (exponent > 0)
                {
                    session.Check();
                    if (!exponent.IsEven) result = Product(result, factor);
                    exponent >>= 1;
                    if (exponent > 0) factor = Product(factor, factor);
                }
                return result;
            }
            return node;
        }
        return Transform(expression, ExpandNode, session);
    }

    /// <summary>Extracts square factors by bounded trial division and an exact perfect-square check.</summary>
    public static Expression SimplifyRadicals(this Expression expression, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var session = (context ?? ComputationContext.Default).Start();
        Node Radical(Node node)
        {
            if (node.Kind != NodeKind.Sqrt || node.Children[0].Kind != NodeKind.Constant) return node;
            var value = node.Children[0].Value;
            var remaining = value.Numerator * value.Denominator;
            BigInteger outside = 1;
            for (BigInteger p = 2; p <= 1000 && p * p <= remaining; p++)
            {
                session.Check();
                var square = p * p;
                while (remaining % square == 0) { session.Check(); remaining /= square; outside *= p; }
            }
            var root = IntegerMath.SqrtFloor(remaining, session);
            if (root * root == remaining) { outside *= root; remaining = 1; }
            return ExpressionNormalizer.Multiply(new[] { Node.Constant(new Rational(outside, value.Denominator)),
                ExpressionNormalizer.Sqrt(Node.Constant(new Rational(remaining)), session) }, session);
        }
        return Transform(expression, Radical, session);
    }

    private static Expression Transform(Expression expression, Func<Node, Node> rule, ComputationSession session)
    {
        var cache = new Dictionary<Node, Node>();
        Node Visit(Node node)
        {
            session.Check();
            if (cache.TryGetValue(node, out var result)) return result;
            var children = node.Children.Select(Visit).ToArray();
            var rebuilt = node.Kind switch
            {
                NodeKind.Sum => ExpressionNormalizer.Add(children, session),
                NodeKind.Product => ExpressionNormalizer.Multiply(children, session),
                NodeKind.Power => ExpressionNormalizer.Pow(children[0], node.Exponent, session),
                NodeKind.Root => ExpressionNormalizer.Root(children[0], node.Exponent, session),
                NodeKind.Constant or NodeKind.Symbol or NodeKind.Pi => node,
                _ => Node.Make(node.Kind, children)
            };
            result = rule(rebuilt); cache.Add(node, result); return result;
        }
        var root = Visit(expression.Root);
        return new(root, expression.Conditions.Concat(Expression.WithIntrinsicConditions(root).Conditions));
    }

    /// <summary>Cancel the polynomial GCD in Q(variable). Original domain restrictions are retained.</summary>
    public static Expression Cancel(this Expression expression, string variable, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var symbol = Expression.Symbol(variable);
        var session = (context ?? ComputationContext.Default).Start();
        var algebra = new PolynomialArithmetic(session);
        (SortedDictionary<BigInteger, Rational> N, SortedDictionary<BigInteger, Rational> D) Read(Node node)
        {
            session.Check();
            var one = algebra.Constant(Rational.One);
            if (node.Kind == NodeKind.Constant) return (algebra.Constant(node.Value), one);
            if (node.Kind == NodeKind.Symbol && node.Name == variable) return (new() { [1] = Rational.One }, one);
            if (node.Kind is NodeKind.Sum or NodeKind.Product)
            {
                var n = algebra.Constant(node.Kind == NodeKind.Sum ? Rational.Zero : Rational.One); var d = one;
                foreach (var child in node.Children)
                {
                    var b = Read(child);
                    n = node.Kind == NodeKind.Sum ? algebra.Add(algebra.Multiply(n, b.D), algebra.Multiply(b.N, d)) : algebra.Multiply(n, b.N);
                    d = algebra.Multiply(d, b.D);
                }
                return (n, d);
            }
            if (node.Kind == NodeKind.Power)
            {
                var value = Read(node.Children[0]); var power = BigInteger.Abs(node.Exponent);
                var n = algebra.Pow(value.N, power); var d = algebra.Pow(value.D, power);
                return node.Exponent.Sign < 0 ? (d, n) : (n, d);
            }
            throw new ArgumentException("Cancel requires a rational function with rational coefficients in the specified variable.", nameof(expression));
        }
        var fraction = Read(expression.Root);
        var a = fraction.N; var b = fraction.D;
        while (b.Count != 0) { session.Check(); var remainder = algebra.Divide(a, b).Remainder; a = b; b = remainder; }
        var numerator = algebra.Divide(fraction.N, a).Quotient;
        var denominator = algebra.Divide(fraction.D, a).Quotient;
        Expression ToExpression(SortedDictionary<BigInteger, Rational> polynomial)
        {
            var value = Expression.Integer(0);
            foreach (var term in polynomial)
            {
                session.Check();
                var coefficient = Expression.Constant(term.Value);
                value = value.Add(term.Key.IsZero ? coefficient : coefficient.Multiply(symbol.Pow(term.Key, session.BoundContext), session.BoundContext), session.BoundContext);
            }
            return value;
        }
        var result = ToExpression(numerator).Divide(ToExpression(denominator), session.BoundContext);
        return new(result.Root, expression.Conditions.Concat(result.Conditions));
    }

    private sealed class PolynomialArithmetic(ComputationSession session)
    {
        internal SortedDictionary<BigInteger, Rational> Constant(Rational value)
            => value.IsZero ? new() : new() { [0] = value };
        private void AddTerm(SortedDictionary<BigInteger, Rational> target, BigInteger power, Rational value)
        {
            session.Check();
            target.TryGetValue(power, out var previous);
            var sum = previous + value;
            if (sum.IsZero) target.Remove(power); else target[power] = sum;
        }
        internal SortedDictionary<BigInteger, Rational> Add(SortedDictionary<BigInteger, Rational> a, SortedDictionary<BigInteger, Rational> b)
        {
            var result = new SortedDictionary<BigInteger, Rational>(a);
            foreach (var term in b) AddTerm(result, term.Key, term.Value);
            return result;
        }
        internal SortedDictionary<BigInteger, Rational> Multiply(SortedDictionary<BigInteger, Rational> a, SortedDictionary<BigInteger, Rational> b)
        {
            var result = new SortedDictionary<BigInteger, Rational>();
            foreach (var x in a) foreach (var y in b) AddTerm(result, x.Key + y.Key, x.Value * y.Value);
            return result;
        }
        internal SortedDictionary<BigInteger, Rational> Pow(SortedDictionary<BigInteger, Rational> value, BigInteger power)
        {
            var result = Constant(Rational.One);
            while (power > 0)
            {
                session.Check();
                if (!power.IsEven) result = Multiply(result, value);
                power >>= 1;
                if (power > 0) value = Multiply(value, value);
            }
            return result;
        }
        internal (SortedDictionary<BigInteger, Rational> Quotient, SortedDictionary<BigInteger, Rational> Remainder) Divide(
            SortedDictionary<BigInteger, Rational> a, SortedDictionary<BigInteger, Rational> b)
        {
            var quotient = new SortedDictionary<BigInteger, Rational>(); var remainder = new SortedDictionary<BigInteger, Rational>(a);
            var leading = b.Last();
            while (remainder.Count != 0 && remainder.Last().Key >= leading.Key)
            {
                session.Check();
                var term = remainder.Last(); var power = term.Key - leading.Key; var coefficient = term.Value / leading.Value;
                AddTerm(quotient, power, coefficient);
                foreach (var divisor in b) AddTerm(remainder, divisor.Key + power, -coefficient * divisor.Value);
            }
            return (quotient, remainder);
        }
    }
}
