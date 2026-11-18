using System;
using System.Globalization;
using System.Numerics;
using NaturplayMath.Computation;
namespace NaturplayMath.Numbers;

/// <summary>An immutable, normalized arbitrary-precision fraction. Default is zero.</summary>
public readonly struct Rational : IEquatable<Rational>, IComparable<Rational>
{
    private readonly BigInteger denominator;
    public BigInteger Numerator { get; }
    public BigInteger Denominator => denominator.IsZero ? BigInteger.One : denominator;
    public static Rational Zero => default;
    public static Rational One => new(BigInteger.One);
    public bool IsZero => Numerator.IsZero;
    public bool IsOne => Numerator.IsOne && Denominator.IsOne;
    public bool IsInteger => Denominator.IsOne;
    public int Sign => Numerator.Sign;
    public Rational(BigInteger numerator) : this(numerator, BigInteger.One) { }
    public Rational(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero) throw new DivideByZeroException();
        if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
        var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
        Numerator = numerator / gcd;
        this.denominator = denominator / gcd;
    }
    public Rational Abs() => Sign < 0 ? -this : this;
    public BigInteger Truncate() => Numerator / Denominator;
    public BigInteger Floor() => BigInteger.DivRem(Numerator, Denominator, out var r) - (r.Sign < 0 ? 1 : 0);
    public BigInteger Ceiling() => BigInteger.DivRem(Numerator, Denominator, out var r) + (r.Sign > 0 ? 1 : 0);
    public Rational Reciprocal() => new(Denominator, Numerator);
    public Rational Pow(BigInteger exponent, ComputationContext? context = null) => Pow(exponent, (context ?? ComputationContext.Default).Start());
    internal Rational Pow(BigInteger exponent, ComputationSession session)
    {
        session.Check();
        if (IsZero && exponent.IsZero) throw new MathDomainException("Zero to the zeroth power is undefined.");
        var factor = exponent.Sign < 0 ? Reciprocal() : this;
        exponent = BigInteger.Abs(exponent);
        var result = One;
        while (!exponent.IsZero)
        {
            session.Check();
            if (!exponent.IsEven) result *= factor;
            exponent >>= 1;
            if (!exponent.IsZero) factor *= factor;
        }
        return result;
    }
    public static Rational operator +(Rational a, Rational b)
    {
        var gcd = BigInteger.GreatestCommonDivisor(a.Denominator, b.Denominator);
        var left = a.Denominator / gcd;
        var right = b.Denominator / gcd;
        return new(a.Numerator * right + b.Numerator * left, left * b.Denominator);
    }
    public static Rational operator -(Rational a, Rational b) => a + -b;
    public static Rational operator -(Rational a) => new(-a.Numerator, a.Denominator);
    public static Rational operator *(Rational a, Rational b)
    {
        var x = BigInteger.GreatestCommonDivisor(a.Numerator, b.Denominator);
        var y = BigInteger.GreatestCommonDivisor(b.Numerator, a.Denominator);
        return new((a.Numerator / x) * (b.Numerator / y), (a.Denominator / y) * (b.Denominator / x));
    }
    public static Rational operator /(Rational a, Rational b) => a * b.Reciprocal();
    public static Rational operator %(Rational a, Rational b) => a - new Rational((a / b).Truncate()) * b;
    public int CompareTo(Rational other) => (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);
    public bool Equals(Rational other) => Numerator == other.Numerator && Denominator == other.Denominator;
    public override bool Equals(object? obj) => obj is Rational other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);
    public static bool operator ==(Rational a, Rational b) => a.Equals(b);
    public static bool operator !=(Rational a, Rational b) => !a.Equals(b);
    public static bool operator <(Rational a, Rational b) => a.CompareTo(b) < 0;
    public static bool operator >(Rational a, Rational b) => a.CompareTo(b) > 0;
    public static bool operator <=(Rational a, Rational b) => a.CompareTo(b) <= 0;
    public static bool operator >=(Rational a, Rational b) => a.CompareTo(b) >= 0;
    public static implicit operator Rational(BigInteger value) => new(value);
    public override string ToString() => Numerator.ToString(CultureInfo.InvariantCulture)
        + (Denominator.IsOne ? "" : "/" + Denominator.ToString(CultureInfo.InvariantCulture));
}
