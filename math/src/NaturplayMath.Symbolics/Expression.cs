using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NaturplayMath.Computation;
using NaturplayMath.Numbers;
namespace NaturplayMath.Symbolics;

/// <summary>An immutable real expression with domain conditions. Equality is canonical structural equality.</summary>
public sealed class Expression : IEquatable<Expression>
{
    internal Node Root { get; }
    public IReadOnlyList<DomainCondition> Conditions { get; }
    private readonly string key;
    internal Expression(Node root, IEnumerable<DomainCondition> conditions)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        Conditions = Array.AsReadOnly(DomainNormalizer.Normalize(conditions));
        key = Root.Key + "|" + string.Join(";", Conditions.Select(c => c.Key));
    }
    public static Expression Constant(Rational value) => new(Node.Constant(value), Array.Empty<DomainCondition>());
    public static Expression Integer(BigInteger value) => Constant(new Rational(value));
    public static Expression Pi { get; } = new(Node.Make(NodeKind.Pi), Array.Empty<DomainCondition>());
    public static Expression Symbol(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !(char.IsLetter(name[0]) || name[0] == '_')
            || name.Any(c => !char.IsLetterOrDigit(c) && c != '_')) throw new ArgumentException("Use a nonempty identifier for a symbol.", nameof(name));
        return new(Node.Symbol(name), Array.Empty<DomainCondition>());
    }
    public bool TryGetRational(out Rational value)
    {
        bool exact = Root.Kind == NodeKind.Constant && Conditions.Count == 0;
        value = exact ? Root.Value : default;
        return exact;
    }
    private static ComputationSession Start(ComputationContext? context) => (context ?? ComputationContext.Default).Start();
    internal static Expression WithIntrinsicConditions(Node root)
    {
        var conditions = new List<DomainCondition>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Visit(Node node)
        {
            if (!seen.Add(node.Key)) return;
            foreach (var child in node.Children) Visit(child);
            if (node.Kind == NodeKind.Power && node.Exponent.Sign <= 0) conditions.Add(new DomainCondition(node.Children[0], DomainRequirement.NonZero));
            if (node.Kind == NodeKind.Sqrt) conditions.Add(new DomainCondition(node.Children[0], DomainRequirement.Nonnegative));
            if (node.Kind == NodeKind.Ln) conditions.Add(new DomainCondition(node.Children[0], DomainRequirement.Positive));
            if (node.Kind == NodeKind.Root && node.Exponent.IsEven) conditions.Add(new DomainCondition(node.Children[0], DomainRequirement.Nonnegative));
            if (node.Kind is NodeKind.GeneralPower or NodeKind.Factorial or NodeKind.DoubleFactorial or NodeKind.Choose or NodeKind.Permute or NodeKind.Atan2 or NodeKind.ComplexPowerReal or NodeKind.ComplexPowerImag)
                conditions.Add(new DomainCondition(node, DomainRequirement.Defined));
            if (node.Kind == NodeKind.Asin)
                conditions.Add(new DomainCondition((Integer(1) - new Expression(node.Children[0], Array.Empty<DomainCondition>()).Pow(2)).Root, DomainRequirement.Nonnegative));
        }
        Visit(root);
        return new(root, conditions);
    }
    private IEnumerable<DomainCondition> With(Expression other) => Conditions.Concat(other.Conditions);
    public Expression Add(Expression other, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new(ExpressionNormalizer.Add(new[] { Root, other.Root }, Start(context)), With(other));
    }
    public Expression Negate(ComputationContext? context = null) => new(ExpressionNormalizer.Multiply(new[] { Node.Constant(new Rational(-1)), Root }, Start(context)), Conditions);
    public Expression Subtract(Expression other, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(other);
        var session = Start(context);
        return new(ExpressionNormalizer.Add(new[] { Root, ExpressionNormalizer.Multiply(new[] { Node.Constant(new Rational(-1)), other.Root }, session) }, session), With(other));
    }
    public Expression Multiply(Expression other, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new(ExpressionNormalizer.Multiply(new[] { Root, other.Root }, Start(context)), With(other));
    }
    public Expression Divide(Expression other, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(other);
        var session = Start(context);
        var conditions = With(other).Append(new DomainCondition(other.Root, DomainRequirement.NonZero));
        return new(ExpressionNormalizer.Multiply(new[] { Root, ExpressionNormalizer.Pow(other.Root, -1, session) }, session), conditions);
    }
    public Expression Pow(BigInteger exponent, ComputationContext? context = null)
    {
        var conditions = exponent.Sign <= 0 ? Conditions.Append(new DomainCondition(Root, DomainRequirement.NonZero)) : Conditions;
        return new(ExpressionNormalizer.Pow(Root, exponent, Start(context)), conditions);
    }
    public Expression Sqrt(ComputationContext? context = null) => new(ExpressionNormalizer.Sqrt(Root, Start(context)),
        Conditions.Append(new DomainCondition(Root, DomainRequirement.Nonnegative)));
    public Expression NthRoot(BigInteger degree, ComputationContext? context = null)
    {
        if (degree.Sign <= 0) throw new ArgumentOutOfRangeException(nameof(degree));
        return new(ExpressionNormalizer.Root(Root, degree, Start(context)), degree.IsEven
            ? Conditions.Append(new DomainCondition(Root, DomainRequirement.Nonnegative)) : Conditions);
    }
    public Expression Pow(Rational exponent, ComputationContext? context = null)
        => exponent.IsInteger ? Pow(exponent.Numerator, context) : NthRoot(exponent.Denominator, context).Pow(exponent.Numerator, context);
    public Expression Pow(Expression exponent, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(exponent);
        if (Root.Kind == NodeKind.Constant && Root.Value.IsOne) return new(Root, With(exponent));
        if (exponent.Root.Kind == NodeKind.Constant)
        {
            var value = Pow(exponent.Root.Value, context);
            return new(value.Root, value.Conditions.Concat(exponent.Conditions));
        }
        var node = Node.Make(NodeKind.GeneralPower, Root, exponent.Root);
        return new(ExpressionNormalizer.GeneralPower(Root, exponent.Root, Start(context)), With(exponent).Append(new DomainCondition(node, DomainRequirement.Defined)));
    }
    public Expression Abs() => new(ExpressionNormalizer.Abs(Root), Conditions);
    public Expression Ln() => new(ExpressionNormalizer.Ln(Root), Conditions.Append(new DomainCondition(Root, DomainRequirement.Positive)));
    public Expression Exp() => new(ExpressionNormalizer.Exp(Root), Conditions);
    public Expression Sin() => new(ExpressionNormalizer.Trig(Root, false), Conditions);
    public Expression Cos() => new(ExpressionNormalizer.Trig(Root, true), Conditions);
    public Expression Tan() => Sin() / Cos();
    public Expression Atan() => new(ExpressionNormalizer.Inverse(Root, NodeKind.Atan), Conditions);
    /// <summary>Principal argument of x+i*this in (-pi,pi]; undefined at the origin.</summary>
    public Expression Atan2(Expression x)
    {
        ArgumentNullException.ThrowIfNull(x);
        var node = ExpressionNormalizer.ComplexPart(NodeKind.Atan2, Root, x.Root, Start(null));
        return new(node, With(x).Append(new DomainCondition(node, DomainRequirement.Defined)));
    }
    internal static Expression ComplexRootPart(Expression real, Expression imaginary, bool imaginaryPart)
        => new(ExpressionNormalizer.ComplexPart(imaginaryPart ? NodeKind.ComplexRootImag : NodeKind.ComplexRootReal, real.Root, imaginary.Root, Start(null)), real.With(imaginary));
    internal static Expression ComplexPowerPart(ComplexExpression basis, ComplexExpression exponent, bool imaginaryPart, ComputationContext? context = null)
    {
        var node = ExpressionNormalizer.ComplexPower(imaginaryPart ? NodeKind.ComplexPowerImag : NodeKind.ComplexPowerReal,
            new[] { basis.Real.Root, basis.Imaginary.Root, exponent.Real.Root, exponent.Imaginary.Root }, Start(context));
        return new(node, basis.Real.Conditions.Concat(basis.Imaginary.Conditions).Concat(exponent.Real.Conditions).Concat(exponent.Imaginary.Conditions)
            .Append(new DomainCondition(node, DomainRequirement.Defined)));
    }
    public Expression Asin()
    {
        var argument = Integer(1) - Pow(2);
        return new(ExpressionNormalizer.Inverse(Root, NodeKind.Asin),
            Conditions.Concat(argument.Conditions).Append(new DomainCondition(argument.Root, DomainRequirement.Nonnegative)));
    }
    public Expression Acos() => Pi / Integer(2) - Asin();
    public Expression Asinh() => new(ExpressionNormalizer.Inverse(Root, NodeKind.Asinh), Conditions);
    public Expression Sinh() => (Exp() - Negate().Exp()) / Integer(2);
    public Expression Cosh() => (Exp() + Negate().Exp()) / Integer(2);
    public Expression Tanh() => new(ExpressionNormalizer.Inverse(Root, NodeKind.Tanh), Conditions);
    public Expression Acosh() => (this + (this - Integer(1)).Sqrt() * (this + Integer(1)).Sqrt()).Ln();
    public Expression Atanh() => ((Integer(1) + this).Ln() - (Integer(1) - this).Ln()) / Integer(2);
    public Expression Log10() => Log(Integer(10));
    public Expression Log(Expression basis, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(basis);
        if (Root.Kind == NodeKind.Constant && basis.Root.Kind == NodeKind.Constant)
        {
            var value = Root.Value; var b = basis.Root.Value;
            if (value.Sign <= 0 || b.Sign <= 0 || b.IsOne) throw new MathDomainException("A logarithm requires a positive value and a positive base other than one.");
            if (value.IsOne) return new(Node.Constant(Rational.Zero), With(basis));
            int sign = 1;
            if (b < Rational.One) { b = b.Reciprocal(); sign = -sign; }
            if (value < Rational.One) { value = value.Reciprocal(); sign = -sign; }
            var powers = new List<(Rational Value, BigInteger Exponent)>();
            var power = b; BigInteger exponent = 1;
            var session = Start(context);
            long numeratorBits = value.Numerator.GetBitLength(), denominatorBits = value.Denominator.GetBitLength();
            while (power <= value && power.Numerator.GetBitLength() <= numeratorBits && power.Denominator.GetBitLength() <= denominatorBits)
            { session.Check(); powers.Add((power, exponent)); power *= power; exponent *= 2; }
            BigInteger result = 0;
            for (int i = powers.Count - 1; i >= 0; i--)
            {
                session.Check();
                if (powers[i].Value <= value) { value /= powers[i].Value; result += powers[i].Exponent; }
            }
            if (value.IsOne) return new(Node.Constant(new Rational(sign * result)), With(basis));
        }
        return Ln().Divide(basis.Ln(), context);
    }
    public Expression Floor() => new(ExpressionNormalizer.Discrete(NodeKind.Floor, new[] { Root }, Start(null)), Conditions);
    public Expression Ceiling() => new(ExpressionNormalizer.Discrete(NodeKind.Ceiling, new[] { Root }, Start(null)), Conditions);
    public Expression Truncate() => new(ExpressionNormalizer.Discrete(NodeKind.Truncate, new[] { Root }, Start(null)), Conditions);
    /// <summary>Remainder with a quotient truncated toward zero, matching the calculator's mod operator.</summary>
    public Expression Remainder(Expression divisor) => this - (this / divisor).Truncate() * divisor;
    public Expression Factorial(ComputationContext? context = null) => Discrete(NodeKind.Factorial, null, context);
    public Expression DoubleFactorial(ComputationContext? context = null) => Discrete(NodeKind.DoubleFactorial, null, context);
    public Expression Choose(Expression k, ComputationContext? context = null) => Discrete(NodeKind.Choose, k, context);
    public Expression Permute(Expression k, ComputationContext? context = null) => Discrete(NodeKind.Permute, k, context);
    private Expression Discrete(NodeKind kind, Expression? other, ComputationContext? context)
    {
        var node = ExpressionNormalizer.Discrete(kind, other is null ? new[] { Root } : new[] { Root, other.Root }, Start(context));
        return new(node, (other is null ? Conditions : With(other)).Append(new DomainCondition(node, DomainRequirement.Defined)));
    }

    public Expression Substitute(IReadOnlyDictionary<string, Expression> values, ComputationContext? context = null) => Substitute(values, Start(context));
    internal Expression Substitute(IReadOnlyDictionary<string, Expression> values, ComputationSession session)
    {
        ArgumentNullException.ThrowIfNull(values);
        var extra = new List<DomainCondition>();
        var cache = new Dictionary<string, Node>(StringComparer.Ordinal);
        Node Visit(Node node)
        {
            session.Check();
            if (cache.TryGetValue(node.Key, out var cached)) return cached;
            Node result;
            if (node.Kind == NodeKind.Symbol && values.TryGetValue(node.Name, out var value))
            {
                ArgumentNullException.ThrowIfNull(value);
                extra.AddRange(value.Conditions); result = value.Root;
            }
            else
            {
                var children = node.Children.Select(Visit).ToArray();
                result = node.Kind switch
                {
                    NodeKind.Sum => ExpressionNormalizer.Add(children, session),
                    NodeKind.Product => ExpressionNormalizer.Multiply(children, session),
                    NodeKind.Power => ExpressionNormalizer.Pow(children[0], node.Exponent, session),
                    NodeKind.Sqrt => ExpressionNormalizer.Sqrt(children[0], session),
                    NodeKind.Root => ExpressionNormalizer.Root(children[0], node.Exponent, session),
                    NodeKind.GeneralPower => SubstitutePower(children[0], children[1]),
                    NodeKind.Abs => ExpressionNormalizer.Abs(children[0]),
                    NodeKind.Ln => ExpressionNormalizer.Ln(children[0]),
                    NodeKind.Exp => ExpressionNormalizer.Exp(children[0]),
                    NodeKind.Sin => ExpressionNormalizer.Trig(children[0], false),
                    NodeKind.Cos => ExpressionNormalizer.Trig(children[0], true),
                    NodeKind.Atan or NodeKind.Asin or NodeKind.Asinh or NodeKind.Tanh => ExpressionNormalizer.Inverse(children[0], node.Kind),
                    NodeKind.Floor or NodeKind.Ceiling or NodeKind.Truncate or NodeKind.Factorial or NodeKind.DoubleFactorial or NodeKind.Choose or NodeKind.Permute
                        => ExpressionNormalizer.Discrete(node.Kind, children, session),
                    NodeKind.Atan2 or NodeKind.ComplexRootReal or NodeKind.ComplexRootImag => ExpressionNormalizer.ComplexPart(node.Kind, children[0], children[1], session),
                    NodeKind.ComplexPowerReal or NodeKind.ComplexPowerImag => ExpressionNormalizer.ComplexPower(node.Kind, children, session),
                    _ => node
                };
            }
            cache[node.Key] = result;
            return result;
        }
        Node SubstitutePower(Node basis, Node exponent)
        {
            var value = new Expression(basis, Array.Empty<DomainCondition>()).Pow(new Expression(exponent, Array.Empty<DomainCondition>()), session.BoundContext);
            extra.AddRange(value.Conditions);
            return value.Root;
        }
        var root = Visit(Root);
        var guards = Conditions.Select(c => new DomainCondition(Visit(c.ArgumentNode), c.Requirement)).ToArray();
        return new(root, guards.Concat(extra));
    }
    public static Expression operator +(Expression a, Expression b) => a.Add(b);
    public static Expression operator -(Expression a, Expression b) => a.Subtract(b);
    public static Expression operator -(Expression a) => a.Negate();
    public static Expression operator *(Expression a, Expression b) => a.Multiply(b);
    public static Expression operator /(Expression a, Expression b) => a.Divide(b);
    public bool Equals(Expression? other) => other is not null && key == other.key;
    public override bool Equals(object? obj) => obj is Expression other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(key);
    public static bool operator ==(Expression? a, Expression? b) => ReferenceEquals(a, b) || a is not null && a.Equals(b);
    public static bool operator !=(Expression? a, Expression? b) => !(a == b);
    /// <summary>A structural diagnostic string. Use ExpressionFormatter for presentation.</summary>
    public override string ToString() => key;
}
