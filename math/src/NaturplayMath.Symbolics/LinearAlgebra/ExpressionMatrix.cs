using System.Numerics;
using NaturplayMath.Numbers;
namespace NaturplayMath.Symbolics;

/// <summary>Immutable matrix of exact complex expressions. Determinants use division-free Berkowitz recurrence.</summary>
public sealed class ExpressionMatrix
{
    private readonly ComplexExpression[,] values;
    public int Rows => values.GetLength(0);
    public int Columns => values.GetLength(1);
    public ComplexExpression this[int row, int column] => values[row, column];
    public ExpressionMatrix(ComplexExpression[,] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        this.values = (ComplexExpression[,])values.Clone();
        foreach (var value in this.values) ArgumentNullException.ThrowIfNull(value);
    }
    public static ExpressionMatrix FromRationals(Rational[,] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = new ComplexExpression[values.GetLength(0), values.GetLength(1)];
        for (int i = 0; i < result.GetLength(0); i++) for (int j = 0; j < result.GetLength(1); j++) result[i, j] = ComplexExpression.Constant(values[i, j]);
        return new(result);
    }
    public static ExpressionMatrix Identity(int order)
    {
        if (order < 0) throw new ArgumentOutOfRangeException(nameof(order));
        var result = new ComplexExpression[order, order];
        for (int i = 0; i < order; i++) for (int j = 0; j < order; j++) result[i, j] = ComplexExpression.Integer(i == j ? 1 : 0);
        return new(result);
    }
    public ExpressionMatrix Transpose(bool conjugate = false)
    {
        var result = new ComplexExpression[Columns, Rows];
        for (int i = 0; i < Rows; i++) for (int j = 0; j < Columns; j++) result[j, i] = conjugate ? values[i, j].Conjugate() : values[i, j];
        return new(result);
    }
    public ExpressionMatrix Add(ExpressionMatrix other, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Rows != other.Rows || Columns != other.Columns) throw new ArgumentException("Matrix dimensions must agree.", nameof(other));
        var session = (context ?? ComputationContext.Default).Start();
        var result = new ComplexExpression[Rows, Columns];
        for (int i = 0; i < Rows; i++) for (int j = 0; j < Columns; j++) { session.Check(); result[i, j] = values[i, j] + other[i, j]; }
        return new(result);
    }
    public ExpressionMatrix Scale(ComplexExpression factor, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(factor);
        var session = (context ?? ComputationContext.Default).Start();
        var result = new ComplexExpression[Rows, Columns];
        for (int i = 0; i < Rows; i++) for (int j = 0; j < Columns; j++) { session.Check(); result[i, j] = values[i, j] * factor; }
        return new(result);
    }
    public ExpressionMatrix Multiply(ExpressionMatrix other, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Columns != other.Rows) throw new ArgumentException("Inner matrix dimensions must agree.", nameof(other));
        var session = (context ?? ComputationContext.Default).Start();
        var result = new ComplexExpression[Rows, other.Columns];
        for (int i = 0; i < Rows; i++) for (int j = 0; j < other.Columns; j++)
        {
            var sum = ComplexExpression.Zero;
            for (int k = 0; k < Columns; k++) { session.Check(); sum += values[i, k] * other[k, j]; }
            result[i, j] = sum;
            session.Report(ComputationStage.Simplifying, "matrix multiplication", work: new BigInteger(i) * other.Columns + j + 1, total: new BigInteger(Rows) * other.Columns);
        }
        return new(result);
    }
    public ComplexExpression Determinant(ComputationContext? context = null)
    {
        RequireSquare();
        var session = (context ?? ComputationContext.Default).Start();
        ComplexExpression[] Coefficients(int offset, int count)
        {
            session.Check();
            if (count == 0) return [ComplexExpression.One];
            var smaller = Coefficients(offset + 1, count - 1);
            var toeplitz = new ComplexExpression[count + 1];
            toeplitz[0] = ComplexExpression.One; toeplitz[1] = -values[offset, offset];
            var vector = new ComplexExpression[count - 1];
            for (int i = 0; i < vector.Length; i++) vector[i] = values[offset + 1 + i, offset];
            for (int k = 2; k <= count; k++)
            {
                var dot = ComplexExpression.Zero;
                for (int i = 0; i < vector.Length; i++) { session.Check(); dot += values[offset, offset + 1 + i] * vector[i]; }
                toeplitz[k] = -dot;
                if (k == count) break;
                var next = new ComplexExpression[vector.Length];
                for (int i = 0; i < vector.Length; i++)
                {
                    next[i] = ComplexExpression.Zero;
                    for (int j = 0; j < vector.Length; j++) { session.Check(); next[i] += values[offset + 1 + i, offset + 1 + j] * vector[j]; }
                }
                vector = next;
            }
            var result = new ComplexExpression[count + 1];
            for (int i = 0; i <= count; i++)
            {
                result[i] = ComplexExpression.Zero;
                for (int j = 0; j <= i && j < smaller.Length; j++) { session.Check(); result[i] += toeplitz[i - j] * smaller[j]; }
            }
            session.Report(ComputationStage.Simplifying, "division-free determinant", work: count, total: Rows);
            return result;
        }
        var coefficient = Coefficients(0, Rows)[Rows];
        return Rows % 2 == 0 ? coefficient : -coefficient;
    }
    public ExpressionMatrix Inverse(ComputationContext? context = null)
    {
        RequireSquare();
        context ??= ComputationContext.Default;
        var session = context.Start(); var scoped = context.Bind(session);
        var determinant = Determinant(scoped);
        // Inverting explicitly enforces det != 0, including symbolic determinants.
        var reciprocal = ComplexExpression.One / determinant;
        var result = new ComplexExpression[Rows, Columns];
        for (int i = 0; i < Rows; i++) for (int j = 0; j < Columns; j++)
        {
            session.Check();
            var minor = new ComplexExpression[Rows - 1, Columns - 1];
            for (int r = 0, mr = 0; r < Rows; r++)
            {
                if (r == i) continue;
                for (int c = 0, mc = 0; c < Columns; c++) { if (c != j) minor[mr, mc++] = values[r, c]; }
                mr++;
            }
            var cofactor = new ExpressionMatrix(minor).Determinant(scoped);
            result[j, i] = ((i + j) % 2 == 0 ? cofactor : -cofactor) * reciprocal;
        }
        return new(result);
    }
    public ExpressionMatrix Pow(BigInteger exponent, ComputationContext? context = null)
    {
        RequireSquare();
        context ??= ComputationContext.Default;
        var session = context.Start(); var scoped = context.Bind(session);
        var factor = exponent.Sign < 0 ? Inverse(scoped) : this;
        var result = Identity(Rows); exponent = BigInteger.Abs(exponent);
        while (exponent > 0)
        {
            session.Check();
            if (!exponent.IsEven) result = result.Multiply(factor, scoped);
            exponent >>= 1;
            if (exponent > 0) factor = factor.Multiply(factor, scoped);
        }
        return result;
    }
    /// <summary>Exact rank; refuses an unproved symbolic zero test. Supply a certified zero classifier for other domains.</summary>
    public int Rank(ComputationContext? context = null, Func<ComplexExpression, bool?>? isZero = null)
    {
        if (isZero is null && values.Cast<ComplexExpression>().Any(v => v.Real.Conditions.Count != 0 || v.Imaginary.Conditions.Count != 0))
            throw new UndeterminedRankException();
        isZero ??= value => value.IsZero ? true : value.Real.TryGetRational(out var r) && value.Imaginary.TryGetRational(out var i) ? r.IsZero && i.IsZero : null;
        var session = (context ?? ComputationContext.Default).Start();
        var work = (ComplexExpression[,])values.Clone();
        int rank = 0;
        for (int column = 0; column < Columns && rank < Rows; column++)
        {
            int pivot = -1; bool unknown = false;
            for (int row = rank; row < Rows; row++)
            {
                session.Check();
                var zero = isZero(work[row, column]);
                if (zero is false) { pivot = row; break; }
                unknown |= zero is null;
            }
            if (pivot < 0)
            {
                if (unknown) throw new UndeterminedRankException();
                continue;
            }
            for (int j = column; j < Columns; j++) (work[rank, j], work[pivot, j]) = (work[pivot, j], work[rank, j]);
            for (int row = rank + 1; row < Rows; row++)
            {
                var factor = work[row, column] / work[rank, column];
                for (int j = column + 1; j < Columns; j++) { session.Check(); work[row, j] -= factor * work[rank, j]; }
                work[row, column] = ComplexExpression.Zero;
            }
            rank++;
        }
        return rank;
    }
    private void RequireSquare() { if (Rows != Columns) throw new MathDomainException("This operation requires a square matrix."); }
}

public sealed class UndeterminedRankException : ArithmeticException
{
    public UndeterminedRankException() : base("Matrix rank needs a zero test that has not been proved. Substitute exact values or use a certified numerical classifier.") { }
}
