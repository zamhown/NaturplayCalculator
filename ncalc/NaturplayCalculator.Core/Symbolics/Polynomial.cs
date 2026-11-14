using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Model;

namespace NaturplayCalculator.Symbolics
{
    internal sealed class Factor
    {
        internal ExpressionNode Node { get; }
        internal int Power { get; }
        internal Factor(ExpressionNode node, int power) { Node = node; Power = power; }
    }

    internal sealed class Monomial
    {
        internal SortedDictionary<string, Factor> Factors { get; }
        internal int Degree => Factors.Values.Sum(f => f.Power);
        internal string Key { get; }
        internal static Monomial One => new Monomial(Enumerable.Empty<Factor>());
        internal Monomial(IEnumerable<Factor> factors)
        {
            Factors = new SortedDictionary<string, Factor>(StringComparer.Ordinal);
            foreach (var factor in factors)
            {
                if (factor.Power == 0) continue;
                int power = factor.Power + (Factors.TryGetValue(factor.Node.Key, out var existing) ? existing.Power : 0);
                if (power < 0 || power > 4096) ExpressionLimits.Fail();
                Factors[factor.Node.Key] = new Factor(factor.Node, power);
            }
            Key = string.Join("|", Factors.Select(f => f.Key + "@" + f.Value.Power.ToString(CultureInfo.InvariantCulture)));
        }
        internal Monomial Multiply(Monomial other) => new Monomial(Factors.Values.Concat(other.Factors.Values));
        internal bool TryDivide(Monomial divisor, out Monomial result)
        {
            foreach (var factor in divisor.Factors)
                if (!Factors.TryGetValue(factor.Key, out var value) || value.Power < factor.Value.Power) { result = null; return false; }
            result = new Monomial(Factors.Select(f => new Factor(f.Value.Node, f.Value.Power - (divisor.Factors.TryGetValue(f.Key, out var d) ? d.Power : 0))));
            return true;
        }
        internal Monomial Common(Monomial other) => new Monomial(Factors.Where(f => other.Factors.ContainsKey(f.Key)).Select(f => new Factor(f.Value.Node, Math.Min(f.Value.Power, other.Factors[f.Key].Power))));
        internal ExpressionNode ToExpression(ExpressionNode prefix = null)
        {
            ExpressionNode result = prefix;
            foreach (var factor in Factors.Values.OrderBy(f => f.Node.Kind == ExpressionKind.Symbol ? 0 : 1).ThenBy(f => f.Node.Key, StringComparer.Ordinal))
            {
                var node = factor.Power == 1 ? factor.Node : ExpressionNode.Binary("^", factor.Node, ExpressionNode.Number(factor.Power.ToString(CultureInfo.InvariantCulture)));
                result = result == null ? node : ExpressionNode.Binary("*", result, node);
            }
            return result ?? ExpressionNode.Number("1");
        }
    }

    internal sealed class Polynomial
    {
        internal sealed class Term
        {
            internal Monomial Monomial; internal Rational Coefficient;
            internal Term(Monomial monomial, Rational coefficient) { Monomial = monomial; Coefficient = coefficient; }
        }
        internal const int MaxTerms = 256;
        private readonly Dictionary<string, Term> terms = new Dictionary<string, Term>(StringComparer.Ordinal);
        internal IEnumerable<Term> Terms => terms.Values;
        internal bool IsZero => terms.Count == 0;
        internal int Count => terms.Count;
        internal Term Leading => terms.Values.OrderBy(t => t, TermComparer.Instance).First();
        internal static Polynomial Constant(Rational value)
        {
            var result = new Polynomial(); result.AddTerm(Monomial.One, value); return result;
        }
        internal static Polynomial Atom(ExpressionNode node)
        {
            var result = new Polynomial(); result.AddTerm(new Monomial(new[] { new Factor(node, 1) }), Rational.One); return result;
        }
        private void AddTerm(Monomial monomial, Rational coefficient)
        {
            CalculationContext.Check();
            if (terms.TryGetValue(monomial.Key, out var existing)) coefficient += existing.Coefficient;
            if (coefficient.IsZero) terms.Remove(monomial.Key);
            else terms[monomial.Key] = new Term(monomial, coefficient);
            if (terms.Count > MaxTerms) ExpressionLimits.Fail();
        }
        internal static Polynomial From(ExpressionNode node)
        {
            CalculationContext.Check();
            if (Rational.TryRead(node, out var rational)) return Constant(rational);
            if (node.Kind == ExpressionKind.Unary) return From(node.Children[0]).Scale(new Rational(-1));
            if (node.Kind == ExpressionKind.Binary)
            {
                if (node.Text == "+") return From(node.Children[0]).Add(From(node.Children[1]));
                if (node.Text == "-") return From(node.Children[0]).Add(From(node.Children[1]).Scale(new Rational(-1)));
                if (node.Text == "*") return From(node.Children[0]).Multiply(From(node.Children[1]));
                if (node.Text == "/" && Rational.TryRead(node.Children[1], out var denominator)) return From(node.Children[0]).Scale(Rational.One / denominator);
                if (node.Text == "^" && Rational.TryRead(node.Children[1], out var exponent) && exponent.IsInteger && exponent.Sign >= 0 && exponent.Numerator <= 4096)
                    return From(node.Children[0]).Power((int)exponent.Numerator);
            }
            return Atom(node);
        }
        internal Polynomial Add(Polynomial other)
        {
            var result = new Polynomial();
            foreach (var term in Terms.Concat(other.Terms)) result.AddTerm(term.Monomial, term.Coefficient);
            return result;
        }
        internal Polynomial Scale(Rational value)
        {
            var result = new Polynomial();
            foreach (var term in Terms) result.AddTerm(term.Monomial, term.Coefficient * value);
            return result;
        }
        internal Polynomial Multiply(Polynomial other)
        {
            if ((long)Count * other.Count > MaxTerms * MaxTerms) ExpressionLimits.Fail();
            var result = new Polynomial();
            foreach (var a in Terms) foreach (var b in other.Terms)
            {
                CalculationContext.Check();
                var merged = Normalize(a.Monomial.Multiply(b.Monomial), a.Coefficient * b.Coefficient);
                foreach (var term in merged.Terms) result.AddTerm(term.Monomial, term.Coefficient);
            }
            return result;
        }
        private static Polynomial Normalize(Monomial monomial, Rational coefficient)
        {
            var factors = new List<Factor>();
            var expanded = Constant(Rational.One);
            BigInteger radical = BigInteger.One;
            foreach (var factor in monomial.Factors.Values)
            {
                var node = factor.Node; int power = factor.Power;
                if (node.Kind == ExpressionKind.Symbol && node.Text == "i")
                {
                    if (power % 4 >= 2) coefficient = -coefficient;
                    if ((power & 1) != 0) factors.Add(new Factor(node, 1));
                }
                else if (node.Kind == ExpressionKind.Function && node.Text == "sqrt")
                {
                    if (power >= 2) expanded = expanded.Multiply(From(node.Children[0]).Power(power / 2));
                    if ((power & 1) != 0)
                    {
                        if (Rational.TryRead(node.Children[0], out var value) && value.IsInteger && value.Sign > 0) { radical *= value.Numerator; Rational.CheckSize(radical); }
                        else factors.Add(new Factor(node, 1));
                    }
                }
                else factors.Add(factor);
            }
            if (!radical.IsOne)
            {
                IntegerRoots.SquareFree(radical, out var outside, out var inside);
                coefficient *= new Rational(outside);
                if (!inside.IsOne) factors.Add(new Factor(ExpressionNode.Function("sqrt", ExpressionNode.Number(inside.ToString(CultureInfo.InvariantCulture))), 1));
            }
            var basis = new Monomial(factors);
            var result = new Polynomial();
            // Expanded radicands can introduce factors already in the term.
            if (expanded.IsConstant(out var scalar)) result.AddTerm(basis, coefficient * scalar);
            else
            {
                var basePolynomial = new Polynomial(); basePolynomial.AddTerm(basis, coefficient);
                return basePolynomial.Multiply(expanded);
            }
            return result;
        }
        internal Polynomial Power(int exponent)
        {
            if (exponent < 0 || exponent > 4096) ExpressionLimits.Fail();
            var result = Constant(Rational.One); var factor = this;
            while (exponent > 0)
            {
                CalculationContext.Check();
                if ((exponent & 1) != 0) result = result.Multiply(factor);
                exponent >>= 1;
                if (exponent > 0) factor = factor.Multiply(factor);
            }
            return result;
        }
        internal bool IsConstant(out Rational value)
        {
            if (IsZero) { value = Rational.Zero; return true; }
            if (Count == 1 && Leading.Monomial.Degree == 0) { value = Leading.Coefficient; return true; }
            value = Rational.Zero; return false;
        }
        internal Monomial CommonMonomial()
        {
            Monomial result = null;
            foreach (var term in Terms) result = result == null ? term.Monomial : result.Common(term.Monomial);
            return result ?? Monomial.One;
        }
        internal Polynomial DivideMonomial(Monomial divisor)
        {
            var result = new Polynomial();
            foreach (var term in Terms)
            {
                if (!term.Monomial.TryDivide(divisor, out var remainder)) throw new InvalidOperationException();
                result.AddTerm(remainder, term.Coefficient);
            }
            return result;
        }
        internal Rational Content()
        {
            BigInteger numerator = 0, denominator = 1;
            foreach (var term in Terms)
            {
                numerator = BigInteger.GreatestCommonDivisor(numerator, BigInteger.Abs(term.Coefficient.Numerator));
                denominator = Lcm(denominator, term.Coefficient.Denominator);
            }
            return new Rational(numerator, denominator);
        }
        internal bool Divide(Polynomial divisor, out Polynomial quotient, out Polynomial remainder)
        {
            if (divisor.IsZero) throw new DivideByZeroException();
            quotient = Constant(Rational.Zero); remainder = this;
            int rounds = 0;
            while (!remainder.IsZero)
            {
                CalculationContext.Check();
                if (++rounds > MaxTerms) break;
                var leading = remainder.Leading; var divisorLeading = divisor.Leading;
                if (!leading.Monomial.TryDivide(divisorLeading.Monomial, out var monomial)) break;
                var part = new Polynomial(); part.AddTerm(monomial, leading.Coefficient / divisorLeading.Coefficient);
                quotient = quotient.Add(part);
                remainder = remainder.Add(divisor.Multiply(part).Scale(new Rational(-1)));
            }
            return remainder.IsZero;
        }
        internal static Polynomial UnivariateGcd(Polynomial a, Polynomial b)
        {
            var atoms = a.Terms.Concat(b.Terms).SelectMany(t => t.Monomial.Factors.Keys).Distinct(StringComparer.Ordinal).ToArray();
            if (atoms.Length != 1 || a.Leading.Monomial.Degree > 32 || b.Leading.Monomial.Degree > 32) return Constant(Rational.One);
            for (int i = 0; !b.IsZero && i < 64; i++)
            {
                a.Divide(b, out _, out var remainder);
                a = b; b = remainder;
            }
            return a.Scale(Rational.One / a.Leading.Coefficient);
        }
        internal ExpressionNode ToExpression()
        {
            if (IsZero) return ExpressionNode.Number("0");
            BigInteger denominator = 1;
            foreach (var term in Terms) denominator = Lcm(denominator, term.Coefficient.Denominator);
            ExpressionNode result = null;
            foreach (var term in Terms.OrderBy(t => t, TermComparer.Instance))
            {
                BigInteger coefficient = term.Coefficient.Numerator * (denominator / term.Coefficient.Denominator);
                Rational.CheckSize(coefficient);
                var absolute = BigInteger.Abs(coefficient);
                ExpressionNode node = term.Monomial.Degree == 0 ? ExpressionNode.Number(absolute.ToString(CultureInfo.InvariantCulture))
                    : term.Monomial.ToExpression(absolute.IsOne ? null : ExpressionNode.Number(absolute.ToString(CultureInfo.InvariantCulture)));
                result = result == null ? coefficient.Sign < 0 ? ExpressionNode.Unary("-", node) : node : ExpressionNode.Binary(coefficient.Sign < 0 ? "-" : "+", result, node);
            }
            return denominator.IsOne ? result : ExpressionNode.Binary("/", result, ExpressionNode.Number(denominator.ToString(CultureInfo.InvariantCulture)));
        }
        private static BigInteger Lcm(BigInteger a, BigInteger b)
        {
            var result = a / BigInteger.GreatestCommonDivisor(a, b) * b; Rational.CheckSize(result); return result;
        }
        private sealed class TermComparer : IComparer<Term>
        {
            internal static readonly TermComparer Instance = new TermComparer();
            public int Compare(Term a, Term b)
            {
                int degree = b.Monomial.Degree.CompareTo(a.Monomial.Degree);
                if (degree != 0) return degree;
                foreach (var key in a.Monomial.Factors.Keys.Concat(b.Monomial.Factors.Keys).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
                {
                    int x = a.Monomial.Factors.TryGetValue(key, out var ax) ? ax.Power : 0;
                    int y = b.Monomial.Factors.TryGetValue(key, out var by) ? by.Power : 0;
                    int comparison = y.CompareTo(x); if (comparison != 0) return comparison;
                }
                return 0;
            }
        }
    }
}
