using System.Numerics;
namespace NaturplayMath.Numerics;

internal sealed partial class BinaryKernel
{
    private enum SeriesKind { Sin, Cos, Exp }
    private BinaryInterval Series(BigInteger numerator, int denominatorBits, SeriesKind kind)
    {
        if (numerator.IsZero) return kind == SeriesKind.Sin ? Zero : One;
        bool negative = numerator.Sign < 0; numerator = BigInteger.Abs(numerator);
        int zeros = (int)((numerator & -numerator).GetBitLength() - 1);
        zeros = Math.Min(zeros, denominatorBits);
        numerator >>= zeros; denominatorBits -= zeros;
        var denominator = BigInteger.One << denominatorBits;
        if (numerator > denominator) throw new InvalidOperationException("Series argument was not reduced.");
        long decay = Math.Max(0, (long)denominatorBits - numerator.GetBitLength());
        long boundedBits = 0; int terms = 0;
        // Bound the first omitted term using factorial factors and |x| <= 2^-decay.
        while (boundedBits < (long)Precision + 16)
        {
            session.Check(); terms++;
            BigInteger k = terms;
            boundedBits += kind == SeriesKind.Exp ? decay + k.GetBitLength() - 1
                : 2 * decay + (2 * k - 1).GetBitLength() - 1 + (2 * k).GetBitLength() - 1;
        }
        terms = Math.Max(terms, 2);
        var pStep = kind == SeriesKind.Exp ? numerator : -(numerator * numerator);
        var qStep = kind == SeriesKind.Exp ? denominator : denominator * denominator;
        int completed = 0;
        string detail = kind.ToString().ToLowerInvariant() + ": binary splitting";
        (BigInteger P, BigInteger Q, BigInteger T) Split(int a, int b)
        {
            session.Check();
            if (b - a == 1)
            {
                session.Report(ComputationStage.Evaluating, detail, Precision, ++completed, terms);
                if (a == 0) return (1, 1, 1);
                BigInteger k = a;
                var q = qStep * (kind == SeriesKind.Exp ? k : kind == SeriesKind.Sin ? (2 * k) * (2 * k + 1) : (2 * k - 1) * (2 * k));
                return (pStep, q, pStep);
            }
            int m = a + (b - a) / 2;
            var left = Split(a, m); var right = Split(m, b);
            return (left.P * right.P, left.Q * right.Q, left.T * right.Q + left.P * right.T);
        }
        var sum = Split(0, terms);
        if (kind == SeriesKind.Sin) { sum.T *= numerator; sum.Q *= denominator; }
        var result = new BinaryInterval(BinaryInterval.FloorDiv(sum.T << Precision, sum.Q) - 1,
            BinaryInterval.CeilingDiv(sum.T << Precision, sum.Q) + 1, Precision);
        return negative && kind == SeriesKind.Sin ? -result : result;
    }
    private (BinaryInterval Sin, BinaryInterval Cos) SmallSinCos(BigInteger numerator, int denominatorBits)
    {
        bool negative = numerator.Sign < 0;
        numerator = BigInteger.Abs(numerator);
        var sine = Zero; var cosine = One;
        BigInteger previous = 0;
        int previousBits = 0;
        for (int bits = Math.Min(16, denominatorBits); ; bits = (int)Math.Min((long)bits * 2, denominatorBits))
        {
            session.Check();
            var prefix = numerator >> (denominatorBits - bits);
            var chunk = prefix - (previous << (bits - previousBits));
            if (!chunk.IsZero)
            {
                var s = Series(chunk, bits, SeriesKind.Sin); var c = Series(chunk, bits, SeriesKind.Cos);
                (sine, cosine) = (sine * c + cosine * s, cosine * c - sine * s);
            }
            previous = prefix; previousBits = bits;
            if (bits == denominatorBits) break;
        }
        return (negative ? -sine : sine, cosine);
    }
    internal (BinaryInterval Sin, BinaryInterval Cos) SinCos(BinaryInterval value)
    {
        session.Check();
        var reduced = value;
        if (BigInteger.Max(BigInteger.Abs(value.Lower), BigInteger.Abs(value.Upper)) > Scale * 3)
        {
            var twoPi = Pi().Times(2);
            var middle = (value.Lower + value.Upper) / 2;
            var divisor = (twoPi.Lower + twoPi.Upper) / 2;
            var turns = BinaryInterval.FloorDiv(middle + divisor / 2, divisor);
            reduced -= twoPi.Times(turns);
        }
        if (reduced.Width > Scale || BigInteger.Max(BigInteger.Abs(reduced.Lower), BigInteger.Abs(reduced.Upper)) > Scale * 4)
        {
            long magnitudeBits = BigInteger.Max(BigInteger.Abs(value.Lower), BigInteger.Abs(value.Upper)).GetBitLength() - Precision;
            throw new RefinementNeededException(checked(Precision + (int)Math.Max(64, magnitudeBits)));
        }
        var midpoint = (reduced.Lower + reduced.Upper) / 2;
        var radius = BigInteger.Max(midpoint - reduced.Lower, reduced.Upper - midpoint);
        var pair = SmallSinCos(midpoint, checked(Precision + 3));
        for (int i = 0; i < 3; i++) pair = (pair.Sin * pair.Cos.Times(2), pair.Cos * pair.Cos - pair.Sin * pair.Sin);
        // Both sin and cos are globally 1-Lipschitz on the real line.
        return (pair.Sin.Widen(radius).Intersect(-Scale, Scale), pair.Cos.Widen(radius).Intersect(-Scale, Scale));
    }
    internal BinaryInterval Exp(BinaryInterval value)
    {
        var lower = ExpPoint(value.Lower);
        if (value.Lower == value.Upper) return lower;
        var upper = ExpPoint(value.Upper);
        return new(lower.Lower, upper.Upper, Precision);
    }
    private BinaryInterval ExpPoint(BigInteger units)
    {
        session.Check();
        if (units.IsZero) return One;
        if (units < -Scale * (Precision + new BigInteger(8))) return new(0, 1, Precision);
        if (units.Sign < 0) return ExpPoint(-units).Reciprocal();
        int halvings = (int)Math.Max(0, units.GetBitLength() - Precision);
        int denominatorBits = checked(Precision + halvings);
        var result = One;
        BigInteger previous = 0; int previousBits = 0;
        for (int bits = Math.Min(16, denominatorBits); ; bits = (int)Math.Min((long)bits * 2, denominatorBits))
        {
            session.Check();
            var prefix = units >> (denominatorBits - bits);
            var chunk = prefix - (previous << (bits - previousBits));
            if (!chunk.IsZero) result *= Series(chunk, bits, SeriesKind.Exp);
            previous = prefix; previousBits = bits;
            if (bits == denominatorBits) break;
        }
        for (int i = 0; i < halvings; i++) { session.Check(); result *= result; }
        return result;
    }
}
