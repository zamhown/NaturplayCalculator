using System;
using System.Collections.Generic;
using System.Linq;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Model;

namespace NaturplayCalculator.Symbolics
{
    internal sealed partial class SymbolicDomain
    {
        private static ExpressionNode Map(ExpressionNode matrix, Func<ExpressionNode, ExpressionNode> map) => ExpressionNode.Matrix(matrix.Rows, matrix.Columns, matrix.Children.Select(map));
        private static ExpressionNode Cell(ExpressionNode matrix, int row, int column) => matrix.Children[row * matrix.Columns + column];
        private ExpressionNode Identity(int size, ExpressionNode diagonal = null) => ExpressionNode.Matrix(size, size,
            Enumerable.Range(0, size * size).Select(i => i / size == i % size ? diagonal ?? SymbolicAlgebra.One : SymbolicAlgebra.Zero));
        private static ExpressionNode Transpose(ExpressionNode matrix) => ExpressionNode.Matrix(matrix.Columns, matrix.Rows,
            Enumerable.Range(0, matrix.Rows * matrix.Columns).Select(i => Cell(matrix, i % matrix.Rows, i / matrix.Rows)));
        private ExpressionNode MatrixBinary(string operation, ExpressionNode a, ExpressionNode b)
        {
            bool am = a.Kind == ExpressionKind.Matrix, bm = b.Kind == ExpressionKind.Matrix;
            if (operation == "+" || operation == "-")
            {
                if (!am) { if (b.Rows != b.Columns) MatrixError(); a = Identity(b.Rows, a); }
                if (!bm) { if (a.Rows != a.Columns) MatrixError(); b = Identity(a.Rows, b); }
                if (a.Rows != b.Rows || a.Columns != b.Columns) throw new MathException(CalculationResources.GetString("error_31"));
                return ExpressionNode.Matrix(a.Rows, a.Columns, a.Children.Select((cell, i) => Binary(operation, cell, b.Children[i])));
            }
            if (operation == "*")
            {
                if (!am) return Map(b, cell => algebra.Multiply(a, cell));
                if (!bm) return Map(a, cell => algebra.Multiply(cell, b));
                if (a.Columns != b.Rows) throw new MathException(CalculationResources.GetString("error_32"));
                var cells = new List<ExpressionNode>();
                for (int row = 0; row < a.Rows; row++) for (int column = 0; column < b.Columns; column++)
                {
                    var sum = SymbolicAlgebra.Zero;
                    for (int k = 0; k < a.Columns; k++) sum = algebra.Add(sum, algebra.Multiply(Cell(a, row, k), Cell(b, k, column)));
                    cells.Add(sum);
                }
                return a.Rows == 1 && b.Columns == 1 ? cells[0] : ExpressionNode.Matrix(a.Rows, b.Columns, cells);
            }
            if (operation == "/" && am && !bm) { Nonzero(b); return Map(a, cell => algebra.Divide(cell, b)); }
            if (operation == "^" && am && !bm)
            {
                if (a.Rows != a.Columns) MatrixError();
                // Do not pass an unevaluated matrix power to scalar polynomial algebra:
                // it would lose its dimensions and incorrectly make matrix products commute.
                if (!Rational.TryRead(b, out var power)) throw new MathException(CalculationResources.GetString("integer_required"));
                if (!power.IsInteger || System.Numerics.BigInteger.Abs(power.Numerator) > 1000000) throw new MathException(CalculationResources.GetString("integer_required"));
                int count = Math.Abs((int)power.Numerator);
                var factor = power.Sign < 0 ? Inverse(a) : a;
                var result = Identity(a.Rows);
                while (count > 0)
                {
                    if ((count & 1) != 0) result = Binary("*", result, factor);
                    count >>= 1;
                    if (count > 0) factor = Binary("*", factor, factor);
                }
                return result;
            }
            throw new MathException(CalculationResources.GetString("error_16"));
        }
        private ExpressionNode Inverse(ExpressionNode matrix)
        {
            if (matrix.Rows != matrix.Columns) MatrixError();
            int n = matrix.Rows;
            var values = matrix.Children.ToArray(); var inverse = Identity(n).Children.ToArray();
            for (int k = 0; k < n; k++)
            {
                int pivot = FindPivot(values, n, n, k, k);
                if (pivot < 0) throw new MathException(CalculationResources.GetString("error_33"));
                Swap(values, n, k, pivot); Swap(inverse, n, k, pivot);
                var divisor = values[k * n + k]; Nonzero(divisor);
                for (int j = 0; j < n; j++) { values[k * n + j] = algebra.Divide(values[k * n + j], divisor); inverse[k * n + j] = algebra.Divide(inverse[k * n + j], divisor); }
                for (int row = 0; row < n; row++) if (row != k)
                {
                    var factor = values[row * n + k];
                    for (int column = 0; column < n; column++)
                    {
                        values[row * n + column] = algebra.Subtract(values[row * n + column], algebra.Multiply(factor, values[k * n + column]));
                        inverse[row * n + column] = algebra.Subtract(inverse[row * n + column], algebra.Multiply(factor, inverse[k * n + column]));
                    }
                }
            }
            return ExpressionNode.Matrix(n, n, inverse);
        }
        private ExpressionNode Determinant(ExpressionNode matrix)
        {
            if (matrix.Rows != matrix.Columns) MatrixError();
            int n = matrix.Rows;
            if (n == 1) return matrix.Children[0];
            if (n <= 4 || SymbolicAlgebra.ContainsVariables(matrix))
            {
                // Expansion has no artificial pivot assumptions for symbolic entries.
                var sum = SymbolicAlgebra.Zero;
                for (int column = 0; column < n; column++)
                {
                    CalculationContext.Check();
                    if (SymbolicAlgebra.IsZero(Cell(matrix, 0, column))) continue;
                    var cells = new List<ExpressionNode>();
                    for (int row = 1; row < n; row++) for (int j = 0; j < n; j++) if (j != column) cells.Add(Cell(matrix, row, j));
                    var term = algebra.Multiply(Cell(matrix, 0, column), Determinant(ExpressionNode.Matrix(n - 1, n - 1, cells)));
                    sum = column % 2 == 0 ? algebra.Add(sum, term) : algebra.Subtract(sum, term);
                }
                return sum;
            }
            var values = matrix.Children.ToArray(); var result = SymbolicAlgebra.One;
            for (int k = 0; k < n; k++)
            {
                int pivot = FindPivot(values, n, n, k, k); if (pivot < 0) return SymbolicAlgebra.Zero;
                if (pivot != k) { Swap(values, n, pivot, k); result = algebra.Negate(result); }
                var divisor = values[k * n + k]; result = algebra.Multiply(result, divisor);
                for (int row = k + 1; row < n; row++)
                {
                    var factor = algebra.Divide(values[row * n + k], divisor);
                    for (int column = k + 1; column < n; column++) values[row * n + column] = algebra.Subtract(values[row * n + column], algebra.Multiply(factor, values[k * n + column]));
                    values[row * n + k] = SymbolicAlgebra.Zero;
                }
            }
            return result;
        }
        private ExpressionNode Rank(ExpressionNode matrix)
        {
            if (SymbolicAlgebra.ContainsVariables(matrix)) return ExpressionNode.Function("rank", matrix);
            var values = matrix.Children.ToArray(); int row = 0;
            for (int column = 0; column < matrix.Columns && row < matrix.Rows; column++)
            {
                int pivot = FindPivot(values, matrix.Rows, matrix.Columns, row, column); if (pivot < 0) continue;
                Swap(values, matrix.Columns, pivot, row);
                for (int i = row + 1; i < matrix.Rows; i++)
                {
                    var factor = algebra.Divide(values[i * matrix.Columns + column], values[row * matrix.Columns + column]);
                    for (int j = column + 1; j < matrix.Columns; j++) values[i * matrix.Columns + j] = algebra.Subtract(values[i * matrix.Columns + j], algebra.Multiply(factor, values[row * matrix.Columns + j]));
                    values[i * matrix.Columns + column] = SymbolicAlgebra.Zero;
                }
                row++;
            }
            return Number(row.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        private static int FindPivot(ExpressionNode[] values, int rows, int columns, int start, int column)
        {
            int fallback = -1;
            for (int row = start; row < rows; row++)
            {
                var cell = values[row * columns + column];
                if (!SymbolicAlgebra.IsZero(cell))
                {
                    if (!SymbolicAlgebra.ContainsVariables(cell)) return row;
                    if (fallback < 0) fallback = row;
                }
            }
            return fallback;
        }
        private static void Swap(ExpressionNode[] values, int columns, int a, int b)
        {
            for (int j = 0; j < columns; j++) { var value = values[a * columns + j]; values[a * columns + j] = values[b * columns + j]; values[b * columns + j] = value; }
        }
        private static void MatrixError() => throw new MathException(CalculationResources.GetString("error_34"));
    }
}
