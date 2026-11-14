using System.Numerics;
using NaturplayMath.Numbers;
namespace NaturplayMath.Symbolics;

internal static class ExpressionNormalizer
{
    internal static Node Add(IEnumerable<Node> nodes, ComputationSession session)
    {
        var terms = new SortedDictionary<string, (Node Node, Rational Coefficient)>(StringComparer.Ordinal);
        Rational constant = Rational.Zero;
        void Collect(Node node)
        {
            session.Check();
            if (node.Kind == NodeKind.Sum) { foreach (var child in node.Children) Collect(child); return; }
            if (node.Kind == NodeKind.Constant) { constant += node.Value; return; }
            var coefficient = Rational.One;
            var basis = node;
            if (node.Kind == NodeKind.Product && node.Children[0].Kind == NodeKind.Constant)
            {
                coefficient = node.Children[0].Value;
                basis = ProductNode(node.Children.Skip(1).ToArray());
            }
            if (basis.Kind == NodeKind.Sum)
            {
                foreach (var child in basis.Children) Collect(Multiply(new[] { Node.Constant(coefficient), child }, session));
                return;
            }
            if (terms.TryGetValue(basis.Key, out var old)) coefficient += old.Coefficient;
            terms[basis.Key] = (basis, coefficient);
        }
        foreach (var node in nodes) Collect(node);
        foreach (var term in terms.Values.ToArray())
        {
            if (term.Node.Kind != NodeKind.Power || term.Node.Exponent != 2 || term.Node.Children[0].Kind != NodeKind.Sin) continue;
            var cosine = Node.Power(Node.Make(NodeKind.Cos, term.Node.Children[0].Children[0]), 2);
            if (!terms.TryGetValue(cosine.Key, out var partner) || partner.Coefficient != term.Coefficient) continue;
            constant += term.Coefficient;
            terms.Remove(term.Node.Key); terms.Remove(cosine.Key);
        }
        var result = new List<Node>();
        if (!constant.IsZero) result.Add(Node.Constant(constant));
        foreach (var term in terms.Values)
            if (!term.Coefficient.IsZero) result.Add(term.Coefficient.IsOne ? term.Node
                : Multiply(new[] { Node.Constant(term.Coefficient), term.Node }, session));
        result.Sort((a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
        return result.Count switch { 0 => Node.Constant(Rational.Zero), 1 => result[0], _ => Node.Make(NodeKind.Sum, result.ToArray()) };
    }
    internal static Node Multiply(IEnumerable<Node> nodes, ComputationSession session)
    {
        var factors = new SortedDictionary<string, (Node Node, BigInteger Exponent)>(StringComparer.Ordinal);
        var roots = new List<Node>();
        Rational coefficient = Rational.One;
        void Collect(Node node, BigInteger exponent)
        {
            session.Check();
            if (exponent.IsZero) return;
            if (node.Kind == NodeKind.Constant) { coefficient *= node.Value.Pow(exponent, session); return; }
            if (node.Kind == NodeKind.Product) { foreach (var child in node.Children) Collect(child, exponent); return; }
            if (node.Kind == NodeKind.Power) { Collect(node.Children[0], exponent * node.Exponent); return; }
            if (node.Kind == NodeKind.Sqrt) { roots.Add(Pow(node.Children[0], exponent, session)); return; }
            if (factors.TryGetValue(node.Key, out var old)) exponent += old.Exponent;
            factors[node.Key] = (node, exponent);
        }
        foreach (var node in nodes) Collect(node, BigInteger.One);
        if (coefficient.IsZero) return Node.Constant(Rational.Zero);
        if (roots.Count > 0)
        {
            var root = Sqrt(roots.Count == 1 ? roots[0] : Multiply(roots, session), session);
            if (root.Kind == NodeKind.Constant) coefficient *= root.Value;
            else
            {
                var exponent = BigInteger.One;
                if (factors.TryGetValue(root.Key, out var old)) exponent += old.Exponent;
                factors[root.Key] = (root, exponent);
            }
        }
        var result = new List<Node>();
        bool regroup = false;
        foreach (var factor in factors.Values)
        {
            if (factor.Exponent.IsZero) continue;
            var value = Pow(factor.Node, factor.Exponent, session);
            if (value.Kind == NodeKind.Constant) coefficient *= value.Value;
            else
            {
                // Reducing abs(x)^2 to x^2 can expose another existing factor x.
                var original = factor.Exponent.IsOne ? factor.Node : Node.Power(factor.Node, factor.Exponent);
                regroup |= value.Key != original.Key;
                result.Add(value);
            }
        }
        if (coefficient.IsZero) return Node.Constant(Rational.Zero);
        if (!coefficient.IsOne || result.Count == 0) result.Insert(0, Node.Constant(coefficient));
        return regroup ? Multiply(result, session) : ProductNode(result.ToArray());
    }
    private static Node ProductNode(Node[] nodes) => nodes.Length == 1 ? nodes[0] : Node.Make(NodeKind.Product, nodes);
    internal static Node Pow(Node node, BigInteger exponent, ComputationSession session)
    {
        session.Check();
        if (node.Kind == NodeKind.Constant) return Node.Constant(node.Value.Pow(exponent, session));
        if (exponent.IsZero) return Node.Constant(Rational.One);
        if (exponent.IsOne) return node;
        if (node.Kind == NodeKind.Power) return Pow(node.Children[0], node.Exponent * exponent, session);
        if (node.Kind == NodeKind.Sqrt && exponent.IsEven) return Pow(node.Children[0], exponent / 2, session);
        if (node.Kind == NodeKind.Root && exponent % node.Exponent == 0) return Pow(node.Children[0], exponent / node.Exponent, session);
        if (node.Kind == NodeKind.Abs && exponent.IsEven) return Pow(node.Children[0], exponent, session);
        if (node.Kind == NodeKind.Product) return Multiply(node.Children.Select(c => Pow(c, exponent, session)), session);
        return Node.Power(node, exponent);
    }
    internal static Node Sqrt(Node node, ComputationSession session)
    {
        session.Check();
        if (node.Kind == NodeKind.Constant)
        {
            if (node.Value.Sign < 0) throw new MathDomainException("A real square root requires a nonnegative argument.");
            var n = IntegerMath.SqrtFloor(node.Value.Numerator, session);
            var d = IntegerMath.SqrtFloor(node.Value.Denominator, session);
            if (n * n == node.Value.Numerator && d * d == node.Value.Denominator) return Node.Constant(new Rational(n, d));
        }
        if (node.Kind == NodeKind.Power && node.Exponent.IsEven) return Abs(Pow(node.Children[0], node.Exponent / 2, session));
        return Node.Make(NodeKind.Sqrt, node);
    }
    internal static Node Abs(Node node)
    {
        if (node.Kind == NodeKind.Constant) return Node.Constant(node.Value.Abs());
        if (IsNonnegative(node)) return node;
        return Node.Make(NodeKind.Abs, node);
    }
    internal static Node ComplexPart(NodeKind kind, Node a, Node b, ComputationSession session)
    {
        if (kind == NodeKind.Atan2 && a.Kind == NodeKind.Constant && b.Kind == NodeKind.Constant)
        {
            if (a.Value.IsZero && b.Value.IsZero) throw new MathDomainException("The complex argument is undefined at zero.");
            if (a.Value.IsZero) return b.Value.Sign > 0 ? Node.Constant(Rational.Zero) : Node.Make(NodeKind.Pi);
            if (b.Value.IsZero) return Multiply(new[] { Node.Constant(new Rational(a.Value.Sign, 2)), Node.Make(NodeKind.Pi) }, session);
        }
        if (kind is NodeKind.ComplexRootReal or NodeKind.ComplexRootImag && a.Kind == NodeKind.Constant && b.Kind == NodeKind.Constant)
        {
            var radius = Sqrt(Node.Constant(a.Value * a.Value + b.Value * b.Value), session);
            if (radius.Kind == NodeKind.Constant)
            {
                var imaginary = kind == NodeKind.ComplexRootImag;
                var part = Sqrt(Node.Constant((radius.Value + (imaginary ? -a.Value : a.Value)) / new Rational(2)), session);
                return imaginary && b.Value.Sign < 0 ? Multiply(new[] { Node.Constant(new Rational(-1)), part }, session) : part;
            }
        }
        return Node.Make(kind, a, b);
    }
    internal static Node ComplexPower(NodeKind kind, Node[] args, ComputationSession session)
    {
        if (args[0].Kind == NodeKind.Constant && args[0].Value.IsOne && args[1].Kind == NodeKind.Constant && args[1].Value.IsZero)
            return Node.Constant(kind == NodeKind.ComplexPowerReal ? Rational.One : Rational.Zero);
        if (args.All(a => a.Kind == NodeKind.Constant))
        {
            var basis = ComplexExpression.Constant(args[0].Value, args[1].Value);
            if (args[3].Value.IsZero)
            {
                if (args[2].Value.IsInteger)
                {
                    var result = basis.Pow(args[2].Value.Numerator, session.BoundContext);
                    return kind == NodeKind.ComplexPowerReal ? result.Real.Root : result.Imaginary.Root;
                }
                if (basis.IsZero && args[2].Value.Sign > 0) return Node.Constant(Rational.Zero);
            }
            if (basis.IsZero) throw new MathDomainException("Zero to a complex power requires a positive real exponent.");
        }
        return Node.Make(kind, args);
    }
    internal static Node Discrete(NodeKind kind, Node[] args, ComputationSession session)
    {
        session.Check();
        if (args.Any(a => a.Kind != NodeKind.Constant)) return Node.Make(kind, args);
        var a = args[0].Value;
        if (kind is NodeKind.Floor or NodeKind.Ceiling or NodeKind.Truncate)
            return Node.Constant(new Rational(kind == NodeKind.Floor ? a.Floor() : kind == NodeKind.Ceiling ? a.Ceiling() : a.Truncate()));
        if (args.Any(a => !a.Value.IsInteger)) throw new MathDomainException("This operation requires integer arguments.");
        var n = a.Numerator;
        var result = kind switch
        {
            NodeKind.Factorial => Combinatorics.Factorial(n, false, session),
            NodeKind.DoubleFactorial => Combinatorics.Factorial(n, true, session),
            NodeKind.Choose => Combinatorics.Choose(n, args[1].Value.Numerator, session),
            NodeKind.Permute => Combinatorics.Permute(n, args[1].Value.Numerator, session),
            _ => throw new InvalidOperationException("Unknown discrete operation.")
        };
        return Node.Constant(new Rational(result));
    }
    internal static Node Root(Node node, BigInteger degree, ComputationSession session)
    {
        session.Check();
        if (degree.IsOne) return node;
        if (degree == 2) return Sqrt(node, session);
        if (node.Kind == NodeKind.Constant)
        {
            if (node.Value.Sign < 0 && degree.IsEven) throw new MathDomainException("An even real root requires a nonnegative argument.");
            var n = IntegerMath.RootFloor(BigInteger.Abs(node.Value.Numerator), degree, session);
            var d = IntegerMath.RootFloor(node.Value.Denominator, degree, session);
            if (IntegerMath.Pow(n, degree, session) == BigInteger.Abs(node.Value.Numerator) && IntegerMath.Pow(d, degree, session) == node.Value.Denominator)
                return Node.Constant(new Rational(node.Value.Sign * n, d));
        }
        if (node.Kind == NodeKind.Power && node.Exponent % degree == 0)
        {
            var result = Pow(node.Children[0], node.Exponent / degree, session);
            return degree.IsEven ? Abs(result) : result;
        }
        return Node.Root(node, degree);
    }
    internal static Node GeneralPower(Node basis, Node exponent, ComputationSession session)
    {
        if (exponent.Kind == NodeKind.Constant)
            return Pow(Root(basis, exponent.Value.Denominator, session), exponent.Value.Numerator, session);
        if (basis.Kind == NodeKind.Constant && basis.Value.IsOne) return basis;
        return Node.Make(NodeKind.GeneralPower, basis, exponent);
    }
    internal static Node Ln(Node node)
    {
        if (node.Kind == NodeKind.Constant && node.Value.IsOne) return Node.Constant(Rational.Zero);
        return node.Kind == NodeKind.Exp ? node.Children[0] : Node.Make(NodeKind.Ln, node);
    }
    internal static Node Exp(Node node)
    {
        if (node.Kind == NodeKind.Constant && node.Value.IsZero) return Node.Constant(Rational.One);
        return node.Kind == NodeKind.Ln ? node.Children[0] : Node.Make(NodeKind.Exp, node);
    }
    internal static Node Trig(Node node, bool cosine)
    {
        if (node.Kind == NodeKind.Constant && node.Value.IsZero) return Node.Constant(cosine ? Rational.One : Rational.Zero);
        if (!cosine && node.Kind == NodeKind.Asin) return node.Children[0];
        Rational? multiple = node.Kind == NodeKind.Pi ? Rational.One : null;
        if (node.Kind == NodeKind.Product && node.Children.Count == 2 && node.Children[0].Kind == NodeKind.Constant && node.Children[1].Kind == NodeKind.Pi)
            multiple = node.Children[0].Value;
        if (multiple is Rational m && (m * new Rational(12)).IsInteger)
        {
            var k = (m * new Rational(12)).Numerator % 24;
            if (k.Sign < 0) k += 24;
            if (cosine) k = (k + 6) % 24;
            int sign = k > 12 ? -1 : 1;
            if (k > 12) k -= 12;
            if (k > 6) k = 12 - k;
            var session = ComputationContext.Default.Start();
            Node Root(int n) => Sqrt(Node.Constant(new Rational(n)), session);
            var value = (int)k switch
            {
                0 => Node.Constant(Rational.Zero),
                2 => Node.Constant(new Rational(1, 2)),
                6 => Node.Constant(Rational.One),
                3 => Multiply(new[] { Node.Constant(new Rational(1, 2)), Root(2) }, session),
                4 => Multiply(new[] { Node.Constant(new Rational(1, 2)), Root(3) }, session),
                _ => Multiply(new[] { Node.Constant(new Rational(1, 4)), Add(new[] { Root(6),
                    Multiply(new[] { Node.Constant(new Rational(k == 1 ? -1 : 1)), Root(2) }, session) }, session) }, session)
            };
            return sign < 0 ? Multiply(new[] { Node.Constant(new Rational(-1)), value }, session) : value;
        }
        return Node.Make(cosine ? NodeKind.Cos : NodeKind.Sin, node);
    }
    internal static Node Inverse(Node node, NodeKind kind)
    {
        if (node.Kind == NodeKind.Constant && node.Value.IsZero) return Node.Constant(Rational.Zero);
        if (node.Kind == NodeKind.Constant && node.Value.Abs().IsOne && kind is NodeKind.Atan or NodeKind.Asin)
        {
            var fraction = new Rational(node.Value.Sign, kind == NodeKind.Atan ? 4 : 2);
            return Node.Make(NodeKind.Product, Node.Constant(fraction), Node.Make(NodeKind.Pi));
        }
        return Node.Make(kind, node);
    }
    internal static bool IsPositive(Node node) => node.Kind is NodeKind.Exp or NodeKind.Pi || node.Kind == NodeKind.Constant && node.Value.Sign > 0
        || node.Kind is NodeKind.Sqrt or NodeKind.Abs && IsPositive(node.Children[0])
        || node.Kind == NodeKind.Power && (IsPositive(node.Children[0]) || node.Exponent.IsEven && node.Children[0].Kind == NodeKind.Constant && !node.Children[0].Value.IsZero);
    internal static bool IsNonnegative(Node node) => IsPositive(node) || node.Kind is NodeKind.Sqrt or NodeKind.Abs
        || node.Kind == NodeKind.Power && node.Exponent.IsEven || node.Kind == NodeKind.Constant && node.Value.IsZero;
}
