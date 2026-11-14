using System;
using System.Globalization;
using System.Numerics;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Model;

namespace NaturplayCalculator.Symbolics
{
    internal readonly struct Rational : IEquatable<Rational>
    {
        internal const int MaxBytes = 513;
        internal BigInteger Numerator { get; }
        internal BigInteger Denominator { get; }
        internal static Rational Zero => new Rational(0);
        internal static Rational One => new Rational(1);
        internal bool IsZero => Numerator.IsZero;
        internal bool IsOne => Numerator.IsOne && Denominator.IsOne;
        internal bool IsInteger => Denominator.IsOne;
        internal int Sign => Numerator.Sign;
        internal Rational(BigInteger numerator) : this(numerator, BigInteger.One) { }
        internal Rational(BigInteger numerator, BigInteger denominator)
        {
            CalculationContext.Check();
            if (denominator.IsZero) throw new MathException(CalculationResources.GetString("division_by_zero"));
            if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
            var divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            numerator /= divisor; denominator /= divisor;
            CheckSize(numerator); CheckSize(denominator);
            Numerator = numerator; Denominator = denominator;
        }
        internal static void CheckSize(BigInteger number)
        {
            if (number.ToByteArray().Length > MaxBytes) ExpressionLimits.Fail();
        }
        internal static Rational Parse(string text)
        {
            text = text.ToUpperInvariant();
            int exponent = 0, marker = text.IndexOf('E');
            if (marker >= 0)
            {
                if (!int.TryParse(text.Substring(marker + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent) || Math.Abs((long)exponent) > 4096) ExpressionLimits.Fail();
                text = text.Substring(0, marker);
            }
            int point = text.IndexOf('.');
            if (point >= 0) { exponent -= text.Length - point - 1; text = text.Remove(point, 1); }
            if (text.Length > 1300 || Math.Abs((long)exponent) > 4096) ExpressionLimits.Fail();
            var number = BigInteger.Parse(text, CultureInfo.InvariantCulture);
            if (number.IsZero) return Zero;
            return exponent >= 0 ? new Rational(number * BigInteger.Pow(10, exponent)) : new Rational(number, BigInteger.Pow(10, -exponent));
        }
        internal static bool TryRead(ExpressionNode node, out Rational value)
        {
            if (node.Kind == ExpressionKind.Number) { value = Parse(node.Text); return true; }
            if (node.Kind == ExpressionKind.Unary && node.Text == "-" && TryRead(node.Children[0], out value)) { value = -value; return true; }
            if (node.Kind == ExpressionKind.Binary && node.Text == "/" && TryRead(node.Children[0], out var a) && TryRead(node.Children[1], out var b)) { value = a / b; return true; }
            value = Zero; return false;
        }
        internal ExpressionNode ToExpression() => Denominator.IsOne ? ExpressionNode.Number(Numerator.ToString(CultureInfo.InvariantCulture))
            : ExpressionNode.Binary("/", ExpressionNode.Number(Numerator.ToString(CultureInfo.InvariantCulture)), ExpressionNode.Number(Denominator.ToString(CultureInfo.InvariantCulture)));
        public static Rational operator +(Rational a, Rational b) => new Rational(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);
        public static Rational operator -(Rational a, Rational b) => a + -b;
        public static Rational operator -(Rational a) => new Rational(-a.Numerator, a.Denominator);
        public static Rational operator *(Rational a, Rational b) => new Rational(a.Numerator * b.Numerator, a.Denominator * b.Denominator);
        public static Rational operator /(Rational a, Rational b) => new Rational(a.Numerator * b.Denominator, a.Denominator * b.Numerator);
        public bool Equals(Rational other) => Numerator == other.Numerator && Denominator == other.Denominator;
        public override bool Equals(object other) => other is Rational value && Equals(value);
        public override int GetHashCode() => Numerator.GetHashCode() ^ Denominator.GetHashCode();
    }

    internal static class IntegerRoots
    {
        internal static BigInteger Sqrt(BigInteger value)
        {
            if (value.Sign < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (value <= 1) return value;
            var x = BigInteger.One << ((value.ToByteArray().Length * 8 + 1) / 2);
            while (true)
            {
                CalculationContext.Check();
                var next = (x + value / x) / 2;
                if (next >= x) return x;
                x = next;
            }
        }
        internal static void SquareFree(BigInteger value, out BigInteger outside, out BigInteger inside)
        {
            outside = BigInteger.One; inside = BigInteger.One;
            if (value.IsZero) { outside = 0; return; }
            var root = Sqrt(value);
            if (root * root == value) { outside = root; return; }
            for (BigInteger factor = 2; factor * factor <= value; factor = factor == 2 ? 3 : factor + 2)
            {
                CalculationContext.Check();
                int count = 0;
                while (value % factor == 0) { CalculationContext.Check(); value /= factor; count++; }
                if (count > 0)
                {
                    outside *= BigInteger.Pow(factor, count / 2);
                    if ((count & 1) != 0) inside *= factor;
                    root = Sqrt(value);
                    if (root * root == value) { outside *= root; value = 1; break; }
                }
            }
            inside *= value;
        }
    }
}
