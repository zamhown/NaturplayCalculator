using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Model;

namespace NaturplayCalculator.Symbolics
{
    internal sealed class SymbolicAlgebra
    {
        internal static ExpressionNode Zero => ExpressionNode.Number("0");
        internal static ExpressionNode One => ExpressionNode.Number("1");
        internal static bool IsZero(ExpressionNode node) => Rational.TryRead(node, out var value) && value.IsZero;
        internal static bool IsOne(ExpressionNode node) => Rational.TryRead(node, out var value) && value.IsOne;
        internal ExpressionNode Negate(ExpressionNode value) => value.Kind == ExpressionKind.Binary && value.Text == "/"
            ? Divide(Polynomial.From(value.Children[0]).Scale(new Rational(-1)).ToExpression(), value.Children[1])
            : Polynomial.From(value).Scale(new Rational(-1)).ToExpression();

        internal ExpressionNode Add(ExpressionNode a, ExpressionNode b)
        {
            SplitFraction(a, out var an, out var ad); SplitFraction(b, out var bn, out var bd);
            if (ad.Key == bd.Key) return Divide(Polynomial.From(an).Add(Polynomial.From(bn)).ToExpression(), ad);
            return Divide(Polynomial.From(an).Multiply(Polynomial.From(bd)).Add(Polynomial.From(bn).Multiply(Polynomial.From(ad))).ToExpression(), Multiply(ad, bd));
        }
        internal ExpressionNode Subtract(ExpressionNode a, ExpressionNode b) => Add(a, Negate(b));
        internal ExpressionNode Multiply(ExpressionNode a, ExpressionNode b)
        {
            SplitFraction(a, out var an, out var ad); SplitFraction(b, out var bn, out var bd);
            var numerator = Polynomial.From(an).Multiply(Polynomial.From(bn)).ToExpression();
            var denominator = Polynomial.From(ad).Multiply(Polynomial.From(bd)).ToExpression();
            return Divide(numerator, denominator);
        }
        internal ExpressionNode Divide(ExpressionNode numerator, ExpressionNode denominator)
        {
            CalculationContext.Check();
            SplitFraction(numerator, out var an, out var ad); SplitFraction(denominator, out var bn, out var bd);
            var a = Polynomial.From(an).Multiply(Polynomial.From(bd));
            var b = Polynomial.From(bn).Multiply(Polynomial.From(ad));
            if (b.IsZero) throw new MathException(CalculationResources.GetString("division_by_zero"));
            if (a.IsZero) return Zero;
            if (b.IsConstant(out var scalar)) return a.Scale(Rational.One / scalar).ToExpression();
            var common = a.CommonMonomial().Common(b.CommonMonomial());
            if (common.Degree != 0) { a = a.DivideMonomial(common); b = b.DivideMonomial(common); }
            if (b.IsConstant(out scalar)) return a.Scale(Rational.One / scalar).ToExpression();
            if (a.Divide(b, out var exact, out _)) return exact.ToExpression();

            var gcd = Polynomial.UnivariateGcd(a, b);
            if (!gcd.IsConstant(out _))
            {
                a.Divide(gcd, out var reducedA, out _); b.Divide(gcd, out var reducedB, out _);
                a = reducedA; b = reducedB;
                if (b.IsConstant(out scalar)) return a.Scale(Rational.One / scalar).ToExpression();
            }
            if (TryRationalize(a, b, out var rationalized)) return rationalized;
            var content = b.Content();
            if (b.Leading.Coefficient.Sign < 0) content = -content;
            a = a.Scale(Rational.One / content); b = b.Scale(Rational.One / content);
            var top = a.ToExpression();
            SplitFraction(top, out var integerTop, out var integerScale);
            var bottom = b.Multiply(Polynomial.From(integerScale)).ToExpression();
            return IsOne(bottom) ? integerTop : ExpressionNode.Binary("/", integerTop, bottom);
        }

        private bool TryRationalize(Polynomial a, Polynomial b, out ExpressionNode result)
        {
            result = null;
            if (b.Count == 1)
            {
                var term = b.Leading;
                if (term.Monomial.Factors.Values.All(f => f.Power == 1 && (f.Node.Kind == ExpressionKind.Function && f.Node.Text == "sqrt" || f.Node.Kind == ExpressionKind.Symbol && f.Node.Text == "i")))
                {
                    var radical = Polynomial.From(term.Monomial.ToExpression());
                    var bottom = b.Multiply(radical);
                    if (bottom.IsConstant(out var constant)) { result = a.Multiply(radical).Scale(Rational.One / constant).ToExpression(); return true; }
                }
            }
            if (b.Count != 2) return false;
            var terms = b.Terms.ToArray();
            var first = Polynomial.From(terms[0].Monomial.ToExpression()).Scale(terms[0].Coefficient);
            var second = Polynomial.From(terms[1].Monomial.ToExpression()).Scale(terms[1].Coefficient);
            var squareDifference = first.Multiply(first).Add(second.Multiply(second).Scale(new Rational(-1)));
            if (squareDifference.IsConstant(out var divisor) && !divisor.IsZero)
            {
                result = a.Multiply(first.Add(second.Scale(new Rational(-1)))).Scale(Rational.One / divisor).ToExpression();
                return true;
            }
            return false;
        }

        internal ExpressionNode Power(ExpressionNode value, ExpressionNode exponent)
        {
            if (Rational.TryRead(exponent, out var power))
            {
                if (power.IsZero) return One;
                if (power.IsOne) return value;
                if (power.IsInteger)
                {
                    if (BigInteger.Abs(power.Numerator) > 4096) ExpressionLimits.Fail();
                    int integer = (int)power.Numerator;
                    SplitFraction(value, out var numerator, out var denominator);
                    var a = Polynomial.From(numerator).Power(Math.Abs(integer)).ToExpression();
                    var b = Polynomial.From(denominator).Power(Math.Abs(integer)).ToExpression();
                    return integer < 0 ? Divide(b, a) : Divide(a, b);
                }
                if (power.Denominator == 2) return Power(Sqrt(value), new Rational(power.Numerator).ToExpression());
                if (Rational.TryRead(value, out var number) && number.Sign >= 0 && power.Denominator <= 64 && BigInteger.Abs(power.Numerator) <= 4096)
                {
                    if (TryExactRoot(number.Numerator, (int)power.Denominator, out var n) && TryExactRoot(number.Denominator, (int)power.Denominator, out var d))
                        return Power(new Rational(n, d).ToExpression(), new Rational(power.Numerator).ToExpression());
                }
            }
            if (IsOne(value)) return One;
            return ExpressionNode.Binary("^", value, exponent);
        }
        internal ExpressionNode Sqrt(ExpressionNode value)
        {
            if (Rational.TryRead(value, out var rational))
            {
                if (rational.IsZero) return Zero;
                IntegerRoots.SquareFree(BigInteger.Abs(rational.Numerator) * rational.Denominator, out var outside, out var inside);
                ExpressionNode result = new Rational(outside, rational.Denominator).ToExpression();
                if (!inside.IsOne) result = Multiply(result, ExpressionNode.Function("sqrt", ExpressionNode.Number(inside.ToString(CultureInfo.InvariantCulture))));
                if (rational.Sign < 0) result = Multiply(ExpressionNode.Symbol("i"), result);
                return result;
            }
            var polynomial = Polynomial.From(value);
            if (polynomial.Count == 1)
            {
                var term = polynomial.Leading;
                var outside = One; var remainder = new Rational(term.Coefficient.Numerator, term.Coefficient.Denominator).ToExpression();
                bool changed = false;
                foreach (var factor in term.Monomial.Factors.Values)
                {
                    int pairs = KnownReal(factor.Node) ? factor.Power / 2 : 0;
                    if (pairs > 0)
                    {
                        changed = true; outside = Multiply(outside, Abs(Power(factor.Node, ExpressionNode.Number(pairs.ToString(CultureInfo.InvariantCulture)))));
                    }
                    int remaining = factor.Power - pairs * 2;
                    if (remaining > 0) remainder = Multiply(remainder, Power(factor.Node, ExpressionNode.Number(remaining.ToString(CultureInfo.InvariantCulture))));
                }
                if (changed) return Multiply(outside, Sqrt(remainder));
            }
            return ExpressionNode.Function("sqrt", value);
        }
        internal ExpressionNode Abs(ExpressionNode value)
        {
            if (Rational.TryRead(value, out var rational)) return new Rational(BigInteger.Abs(rational.Numerator), rational.Denominator).ToExpression();
            if (value.Kind == ExpressionKind.Function && value.Text == "Abs") return value;
            if (value.Kind == ExpressionKind.Symbol && (value.Text == "e" || value.Text == "π")) return value;
            if (value.Kind == ExpressionKind.Unary) return Abs(value.Children[0]);
            if (value.Kind == ExpressionKind.Binary && value.Text == "^" && KnownReal(value.Children[0]) && Rational.TryRead(value.Children[1], out var power) && power.IsInteger && power.Numerator.IsEven) return value;
            if (value.Kind == ExpressionKind.Symbol && value.Text == "i") return One;
            return ExpressionNode.Function("Abs", value);
        }
        internal static bool KnownReal(ExpressionNode node)
        {
            if (node.Kind == ExpressionKind.Number) return true;
            if (node.Kind == ExpressionKind.Symbol) return node.Text != "i";
            if (node.Kind == ExpressionKind.Unary) return KnownReal(node.Children[0]);
            if (node.Kind == ExpressionKind.Binary)
                return node.Text == "^" ? KnownReal(node.Children[0]) && Rational.TryRead(node.Children[1], out var p) && p.IsInteger : node.Children.All(KnownReal);
            return node.Kind == ExpressionKind.Function && node.Text == "Abs";
        }
        internal static bool ContainsVariables(ExpressionNode node)
        {
            if (node.Kind == ExpressionKind.Symbol) return node.Text != "e" && node.Text != "π" && node.Text != "i";
            return node.Children.Any(ContainsVariables);
        }
        private static void SplitFraction(ExpressionNode value, out ExpressionNode numerator, out ExpressionNode denominator)
        {
            if (value.Kind == ExpressionKind.Binary && value.Text == "/") { numerator = value.Children[0]; denominator = value.Children[1]; }
            else { numerator = value; denominator = One; }
        }
        private static bool TryExactRoot(BigInteger value, int degree, out BigInteger result)
        {
            if (degree == 2) { result = IntegerRoots.Sqrt(value); return result * result == value; }
            BigInteger low = 0, high = BigInteger.One << (value.ToByteArray().Length * 8 / degree + 1);
            while (low <= high)
            {
                CalculationContext.Check();
                var middle = (low + high) / 2; var power = BigInteger.Pow(middle, degree);
                if (power == value) { result = middle; return true; }
                if (power < value) low = middle + 1; else high = middle - 1;
            }
            result = 0; return false;
        }
    }
}
