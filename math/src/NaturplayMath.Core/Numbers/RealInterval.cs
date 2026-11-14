using System;
using System.Numerics;
using NaturplayMath.Numbers;
namespace NaturplayMath.Numbers;

/// <summary>A closed enclosure with exact endpoints, deliberately not convertible to Rational.</summary>
public readonly struct RealInterval : IEquatable<RealInterval>
{
    public Rational Lower { get; }
    public Rational Upper { get; }
    public Rational Width => Upper - Lower;
    public Rational Midpoint => (Lower + Upper) / new Rational(2);
    public Rational AbsoluteError => Width / new Rational(2);
    public bool IsExact => Lower == Upper;
    public bool ContainsZero => Lower.Sign <= 0 && Upper.Sign >= 0;
    public RealInterval(Rational lower, Rational upper)
    {
        if (lower > upper) throw new ArgumentException("The lower bound must not exceed the upper bound.");
        Lower = lower; Upper = upper;
    }
    public static RealInterval Exact(Rational value) => new(value, value);
    public bool Contains(Rational value) => Lower <= value && value <= Upper;
    public static RealInterval operator +(RealInterval a, RealInterval b) => new(a.Lower + b.Lower, a.Upper + b.Upper);
    public static RealInterval operator -(RealInterval a) => new(-a.Upper, -a.Lower);
    public static RealInterval operator -(RealInterval a, RealInterval b) => a + -b;
    public static RealInterval operator *(RealInterval a, RealInterval b)
    {
        var p = a.Lower * b.Lower; var q = a.Lower * b.Upper;
        var r = a.Upper * b.Lower; var s = a.Upper * b.Upper;
        return new(Min(Min(p, q), Min(r, s)), Max(Max(p, q), Max(r, s)));
    }
    public RealInterval Reciprocal()
    {
        if (ContainsZero) throw new DivideByZeroException("An interval containing zero cannot be inverted.");
        return new(Upper.Reciprocal(), Lower.Reciprocal());
    }
    public static RealInterval operator /(RealInterval a, RealInterval b) => a * b.Reciprocal();
    public RealInterval Abs() => Lower.Sign >= 0 ? this : Upper.Sign <= 0 ? -this : new(Rational.Zero, Max(-Lower, Upper));
    public RealInterval Pow(BigInteger exponent, ComputationContext? context = null) => Pow(exponent, (context ?? ComputationContext.Default).Start());
    internal RealInterval Pow(BigInteger exponent, ComputationSession session)
    {
        session.Check();
        if (exponent.Sign <= 0 && ContainsZero) throw new MathDomainException("A nonpositive power requires an interval excluding zero.");
        if (exponent.IsZero) return Exact(Rational.One);
        if (exponent.Sign < 0) return Reciprocal().Pow(-exponent, session);
        if (!exponent.IsEven) return new(Lower.Pow(exponent, session), Upper.Pow(exponent, session));
        var a = Lower.Pow(exponent, session); var b = Upper.Pow(exponent, session);
        return new(ContainsZero ? Rational.Zero : Min(a, b), Max(a, b));
    }
    // Equal means both are the same point. Overlapping uncertainty never proves equality.
    public IntervalOrder Compare(RealInterval other) => Upper < other.Lower ? IntervalOrder.Less
        : Lower > other.Upper ? IntervalOrder.Greater
        : IsExact && other.IsExact && Lower == other.Lower ? IntervalOrder.Equal : IntervalOrder.Undetermined;
    internal static Rational Min(Rational a, Rational b) => a < b ? a : b;
    internal static Rational Max(Rational a, Rational b) => a > b ? a : b;
    public bool Equals(RealInterval other) => Lower == other.Lower && Upper == other.Upper;
    public override bool Equals(object? obj) => obj is RealInterval other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Lower, Upper);
    public override string ToString() => IsExact ? Lower.ToString() : "[" + Lower + ", " + Upper + "]";
}
