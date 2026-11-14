using System;
using System.Globalization;
using System.Text;
using NumericComplex = System.Numerics.Complex;

namespace NaturplayCalculator.Model
{
    public sealed class Complex : IEquatable<Complex>
    {
        public double Re { get; set; }
        public double Im { get; set; }
        public Complex() { }
        public Complex(double real) : this(real, 0) { }
        public Complex(double real, double imaginary) { Re = real; Im = imaginary; }
        public Complex(Complex value) : this(value.Re, value.Im) { }
        internal NumericComplex Number => new NumericComplex(Re, Im);
        internal static Complex From(NumericComplex value) => new Complex(value.Real, value.Imaginary);
        public double Mod => NumericComplex.Abs(Number);
        public double AngleInRad => Math.Atan2(Im, Re);
        public double AngleInDeg => AngleInRad * 180 / Math.PI;
        public static Complex operator +(Complex a, Complex b) => From(a.Number + b.Number);
        public static Complex operator -(Complex a, Complex b) => From(a.Number - b.Number);
        public static Complex operator *(Complex a, Complex b) => From(a.Number * b.Number);
        public static Complex operator *(Complex a, double b) => From(a.Number * b);
        public static Complex operator *(double a, Complex b) => b * a;
        public static Complex operator /(Complex a, Complex b)
        {
            if (b.Re == 0 && b.Im == 0) throw new MathException(CalculationResources.GetString("division_by_zero"));
            return From(a.Number / b.Number);
        }
        public static Complex operator /(Complex a, double b) => a / new Complex(b);
        public static Complex powcc(Complex value, double exponent)
        {
            if (exponent == Math.Truncate(exponent) && Math.Abs(exponent) <= int.MaxValue)
            {
                long count = (long)Math.Abs(exponent);
                var factor = exponent < 0 ? new Complex(1) / value : new Complex(value);
                var result = new Complex(1);
                while (count > 0)
                {
                    CalculationContext.Check();
                    if ((count & 1) != 0) result *= factor;
                    count >>= 1;
                    if (count > 0) factor *= factor;
                }
                return result;
            }
            if (value.Re == 0 && value.Im == 0 && exponent < 0) throw new MathException(CalculationResources.GetString("division_by_zero"));
            return From(NumericComplex.Pow(value.Number, exponent));
        }
        internal static Complex Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new FormatException();
            if (!text.EndsWith("i", StringComparison.Ordinal)) return new Complex(NumericText.Parse(text));
            string body = text.Substring(0, text.Length - 1);
            int split = -1, depth = 0;
            for (int i = 0; i < body.Length; i++)
            {
                if (body[i] == '(') depth++;
                else if (body[i] == ')') depth--;
                else if (i > 0 && depth == 0 && (body[i] == '+' || body[i] == '-') && body[i - 1] != 'E' && body[i - 1] != 'e') split = i;
            }
            string imaginary = split < 0 ? body : body.Substring(split);
            double sign = 1;
            if (imaginary.StartsWith("-", StringComparison.Ordinal)) { sign = -1; imaginary = imaginary.Substring(1); }
            else if (imaginary.StartsWith("+", StringComparison.Ordinal)) imaginary = imaginary.Substring(1);
            if (imaginary.StartsWith("(", StringComparison.Ordinal) && imaginary.EndsWith(")", StringComparison.Ordinal)) imaginary = imaginary.Substring(1, imaginary.Length - 2);
            return new Complex(split < 0 ? 0 : NumericText.Parse(body.Substring(0, split)), sign * (imaginary.Length == 0 ? 1 : NumericText.Parse(imaginary)));
        }
        public static bool operator >(Complex a, Complex b) => a.Re > b.Re;
        public static bool operator <(Complex a, Complex b) => a.Re < b.Re;
        public static bool operator ==(Complex a, Complex b) => ReferenceEquals(a, b) || (!ReferenceEquals(a, null) && a.Equals(b));
        public static bool operator !=(Complex a, Complex b) => !(a == b);
        public bool Equals(Complex other) => !ReferenceEquals(other, null) && Re == other.Re && Im == other.Im;
        public override bool Equals(object value) => Equals(value as Complex);
        public override int GetHashCode() => Re.GetHashCode() ^ Im.GetHashCode();
        public override string ToString()
        {
            var text = new StringBuilder();
            if (Re != 0 || Im == 0) text.Append(NumericText.Format(Re));
            if (Im != 0)
            {
                if (Im < 0) text.Append('-'); else if (Re != 0) text.Append('+');
                if (Math.Abs(Im) != 1) text.Append(NumericText.Format(Math.Abs(Im)));
                text.Append('i');
            }
            return text.ToString();
        }
    }
    internal static class NumericText
    {
        internal static double Parse(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        internal static string Format(double value) => value == 0 ? "0" : value.ToString("R", CultureInfo.InvariantCulture);
        internal static void RequireFinite(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new MathException(CalculationResources.GetString("non_finite"));
        }
    }
}
