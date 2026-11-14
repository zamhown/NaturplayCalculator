using System.Numerics;
using NaturplayMath.Numbers;
namespace NaturplayMath.Symbolics;

/// <summary>Immutable Cartesian complex expression. Transcendental functions use their principal branches.</summary>
public sealed record ComplexExpression
{
    public Expression Real { get; }
    public Expression Imaginary { get; }
    public ComplexExpression(Expression real, Expression? imaginary = null)
    {
        ArgumentNullException.ThrowIfNull(real);
        imaginary ??= Expression.Integer(0);
        var conditions = real.Conditions.Concat(imaginary.Conditions).ToArray();
        Real = new(real.Root, conditions); Imaginary = new(imaginary.Root, conditions);
    }
    public static ComplexExpression Zero { get; } = Integer(0);
    public static ComplexExpression One { get; } = Integer(1);
    public static ComplexExpression I { get; } = new(Expression.Integer(0), Expression.Integer(1));
    public static ComplexExpression Integer(BigInteger value) => new(Expression.Integer(value));
    public static ComplexExpression Constant(Rational real, Rational imaginary = default) => new(Expression.Constant(real), Expression.Constant(imaginary));
    public bool IsReal => Imaginary.TryGetRational(out var value) && value.IsZero;
    public bool IsZero => IsReal && Real.TryGetRational(out var value) && value.IsZero;
    public ComplexExpression Conjugate() => new(Real, -Imaginary);
    public Expression Abs() => (Real.Pow(2) + Imaginary.Pow(2)).Sqrt();
    public Expression Argument() => Imaginary.Atan2(Real);
    public ComplexExpression Sqrt() => new(Expression.ComplexRootPart(Real, Imaginary, false), Expression.ComplexRootPart(Real, Imaginary, true));
    public ComplexExpression Ln() => new(Abs().Ln(), Argument());
    public ComplexExpression Exp() => new(Real.Exp() * Imaginary.Cos(), Real.Exp() * Imaginary.Sin());
    public ComplexExpression Sin() => new(Real.Sin() * Imaginary.Cosh(), Real.Cos() * Imaginary.Sinh());
    public ComplexExpression Cos() => new(Real.Cos() * Imaginary.Cosh(), -(Real.Sin() * Imaginary.Sinh()));
    public ComplexExpression Tan() => Sin() / Cos();
    public ComplexExpression Sinh() => new(Real.Sinh() * Imaginary.Cos(), Real.Cosh() * Imaginary.Sin());
    public ComplexExpression Cosh() => new(Real.Cosh() * Imaginary.Cos(), Real.Sinh() * Imaginary.Sin());
    public ComplexExpression Tanh() => IsReal ? new(Real.Tanh()) : Sinh() / Cosh();
    public ComplexExpression Asin() => -I * (I * this + (One - this * this).Sqrt()).Ln();
    public ComplexExpression Acos() => new ComplexExpression(Expression.Pi / Expression.Integer(2)) - Asin();
    public ComplexExpression Atan() => I * ((One - I * this).Ln() - (One + I * this).Ln()) / Integer(2);
    public ComplexExpression Asinh() => IsReal ? new(Real.Asinh()) : (this + (this * this + One).Sqrt()).Ln();
    public ComplexExpression Acosh() => (this + (this + One).Sqrt() * (this - One).Sqrt()).Ln();
    public ComplexExpression Atanh() => ((One + this).Ln() - (One - this).Ln()) / Integer(2);
    public ComplexExpression Pow(BigInteger exponent, ComputationContext? context = null)
    {
        var session = (context ?? ComputationContext.Default).Start();
        if (exponent.IsZero && IsZero) throw new MathDomainException("Zero to the zeroth power is undefined.");
        if (exponent.IsZero)
        {
            // Keep the nonzero requirement even when the base is still symbolic.
            return this / this;
        }
        var factor = exponent.Sign < 0 ? One / this : this;
        var result = One; exponent = BigInteger.Abs(exponent);
        while (exponent > 0)
        {
            session.Check();
            if (!exponent.IsEven) result *= factor;
            exponent >>= 1;
            if (exponent > 0) factor *= factor;
        }
        return result;
    }
    public ComplexExpression Pow(ComplexExpression exponent, ComputationContext? context = null)
    {
        if (IsZero && exponent.IsReal) return new(Real.Pow(exponent.Real, context));
        if (exponent.IsReal && exponent.Real.TryGetRational(out var rational))
        {
            if (rational.IsInteger) return Pow(rational.Numerator, context);
            if (IsZero && rational.Sign > 0) return Zero;
            if (rational == new Rational(1, 2)) return Sqrt();
        }
        return new(Expression.ComplexPowerPart(this, exponent, false, context), Expression.ComplexPowerPart(this, exponent, true, context));
    }
    public ComplexExpression Substitute(IReadOnlyDictionary<string, Expression> values, ComputationContext? context = null)
        => new(Real.Substitute(values, context), Imaginary.Substitute(values, context));
    public static ComplexExpression operator +(ComplexExpression a, ComplexExpression b) => new(a.Real + b.Real, a.Imaginary + b.Imaginary);
    public static ComplexExpression operator -(ComplexExpression a) => new(-a.Real, -a.Imaginary);
    public static ComplexExpression operator -(ComplexExpression a, ComplexExpression b) => a + -b;
    public static ComplexExpression operator *(ComplexExpression a, ComplexExpression b)
        => new(a.Real * b.Real - a.Imaginary * b.Imaginary, a.Real * b.Imaginary + a.Imaginary * b.Real);
    public static ComplexExpression operator /(ComplexExpression a, ComplexExpression b)
    {
        var norm = b.Real.Pow(2) + b.Imaginary.Pow(2);
        return new((a.Real * b.Real + a.Imaginary * b.Imaginary) / norm, (a.Imaginary * b.Real - a.Real * b.Imaginary) / norm);
    }
}
