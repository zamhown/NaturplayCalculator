using System;
using System.Text;

namespace NaturplayCalculator.Model
{
    public class Matrix
    {
        public Element[][] matrix;
        public int n;
        public int m;
        public Matrix() : this(0, 0) { }
        public Matrix(int rows, int columns)
        {
            if (rows < 0 || columns < 0 || rows > ExpressionLimits.MaxMatrixDimension || columns > ExpressionLimits.MaxMatrixDimension) ExpressionLimits.Fail();
            n = rows; m = columns;
            matrix = new Element[n][];
            for (int i = 0; i < n; i++) matrix[i] = new Element[m];
        }
        public Matrix(int rows, int columns, Element[][] elements) : this(rows, columns)
        {
            if (elements == null || elements.Length != n) throw new ArgumentException(nameof(elements));
            for (int i = 0; i < n; i++)
            {
                if (elements[i] == null || elements[i].Length != m) throw new ArgumentException(nameof(elements));
                for (int j = 0; j < m; j++) matrix[i][j] = elements[i][j];
            }
        }
        public Matrix(Matrix value) : this(value.n, value.m)
        {
            for (int i = 0; i < n; i++)
                for (int j = 0; j < m; j++) matrix[i][j] = value[i][j]?.Clone();
        }
        public Element[] this[int index] { get { return matrix[index]; } set { matrix[index] = value; } }
        internal static MathValue Entry(Matrix a, int i, int j, bool notify)
        {
            CalculationContext.Check();
            if (a[i][j] == null) throw new MathException(CalculationResources.GetString("error_11"));
            var value = Element.Solve(a[i][j], notify);
            if (value.IsMatrix || value.IsNull) throw new MathException(CalculationResources.GetString("error_16"));
            return value;
        }
        public static Matrix plus(Matrix a, Matrix b, bool notify)
        {
            if (a.n != b.n || a.m != b.m) throw new MathException(CalculationResources.GetString("error_31"));
            var result = new Matrix(a.n, a.m);
            for (int i = 0; i < a.n; i++) for (int j = 0; j < a.m; j++) result[i][j] = new Element(Entry(a, i, j, notify) + Entry(b, i, j, notify));
            return result;
        }
        public static Matrix numMultiply(Matrix a, Complex scale, bool notify)
        {
            var result = new Matrix(a.n, a.m);
            var factor = new MathValue(scale);
            for (int i = 0; i < a.n; i++) for (int j = 0; j < a.m; j++) result[i][j] = new Element(Entry(a, i, j, notify) * factor);
            return result;
        }
        public static Matrix multiply(Matrix a, Matrix b, bool notify)
        {
            if (a.m != b.n) throw new MathException(CalculationResources.GetString("error_32"));
            var result = new Matrix(a.n, b.m);
            for (int i = 0; i < a.n; i++) for (int j = 0; j < b.m; j++)
            {
                var sum = new MathValue(0);
                for (int k = 0; k < a.m; k++) sum += Entry(a, i, k, notify) * Entry(b, k, j, notify);
                result[i][j] = new Element(sum);
            }
            return result;
        }
        public static Matrix qiuT(Matrix a, bool notify)
        {
            var result = new Matrix(a.m, a.n);
            for (int i = 0; i < a.n; i++) for (int j = 0; j < a.m; j++) result[j][i] = new Element(Entry(a, i, j, notify).Clone());
            return result;
        }
        public static Matrix qiuE(int dimension, MathValue diagonal = null)
        {
            var result = new Matrix(dimension, dimension);
            for (int i = 0; i < dimension; i++) for (int j = 0; j < dimension; j++) result[i][j] = new Element(i == j ? (diagonal ?? new MathValue(1)).Clone() : new MathValue(0));
            return result;
        }
        internal static Complex[][] Numbers(Matrix a, bool notify)
        {
            var values = new Complex[a.n][];
            for (int i = 0; i < a.n; i++)
            {
                values[i] = new Complex[a.m];
                for (int j = 0; j < a.m; j++) values[i][j] = new Complex(Entry(a, i, j, notify).Cmx);
            }
            return values;
        }
        internal static int Pivot(Complex[][] values, int start, int column)
        {
            int pivot = start;
            for (int i = start + 1; i < values.Length; i++) if (values[i][column].Mod > values[pivot][column].Mod) pivot = i;
            return pivot;
        }
        internal static void Swap(Complex[][] values, int a, int b)
        {
            var row = values[a]; values[a] = values[b]; values[b] = row;
        }
        internal static bool Zero(Complex value) => value.Re == 0 && value.Im == 0;
        public static Matrix qiuNi(Matrix a, bool notify)
        {
            if (a.n != a.m || a.n == 0) throw new MathException(CalculationResources.GetString("error_34"));
            var values = Numbers(a, notify);
            var inverse = Numbers(qiuE(a.n), false);
            for (int k = 0; k < a.n; k++)
            {
                CalculationContext.Check();
                int pivot = Pivot(values, k, k);
                if (Zero(values[pivot][k])) throw new MathException(CalculationResources.GetString("error_33"));
                Swap(values, k, pivot); Swap(inverse, k, pivot);
                var divisor = values[k][k];
                for (int j = 0; j < a.n; j++) { values[k][j] /= divisor; inverse[k][j] /= divisor; }
                for (int i = 0; i < a.n; i++) if (i != k)
                {
                    var factor = values[i][k];
                    for (int j = 0; j < a.n; j++)
                    {
                        CalculationContext.Check();
                        values[i][j] -= factor * values[k][j];
                        inverse[i][j] -= factor * inverse[k][j];
                    }
                    values[i][k] = new Complex(0);
                }
            }
            return FromNumbers(inverse, a.n);
        }
        internal static Matrix FromNumbers(Complex[][] values, int columns)
        {
            var result = new Matrix(values.Length, columns);
            for (int i = 0; i < result.n; i++) for (int j = 0; j < result.m; j++) result[i][j] = new Element(new MathValue(values[i][j]));
            return result;
        }
        public static Matrix getStairs(Matrix a)
        {
            var values = Numbers(a, false);
            int row = 0;
            for (int column = 0; column < a.m && row < a.n; column++)
            {
                int pivot = Pivot(values, row, column);
                if (Zero(values[pivot][column])) continue;
                Swap(values, row, pivot);
                for (int i = row + 1; i < a.n; i++)
                {
                    var factor = values[i][column] / values[row][column];
                    for (int j = column + 1; j < a.m; j++)
                    {
                        CalculationContext.Check();
                        var original = values[i][j];
                        var product = factor * values[row][j];
                        var difference = original - product;
                        // Relative cancellation tolerance belongs to rank elimination only.
                        values[i][j] = difference.Mod <= 1e-14 * (original.Mod + product.Mod) ? new Complex(0) : difference;
                    }
                    values[i][column] = new Complex(0);
                }
                row++;
            }
            return FromNumbers(values, a.m);
        }
        public static Matrix qiuM(Matrix a, int row, int column, bool notify)
        {
            if (a.n != a.m) throw new MathException(CalculationResources.GetString("error_34"));
            var result = new Matrix(a.n - 1, a.m - 1);
            for (int i = 0, x = 0; i < a.n; i++) if (i != row)
            {
                for (int j = 0, y = 0; j < a.m; j++) if (j != column) result[x][y++] = new Element(Entry(a, i, j, notify).Clone());
                x++;
            }
            return result;
        }
        public static MathValue qiuA(Matrix a, int row, int column, bool notify) => new MathValue((row + column) % 2 == 0 ? 1 : -1) * Determinant.solve(qiuM(a, row, column, notify), notify);
        public static Matrix qiuBan(Matrix a, bool notify)
        {
            if (a.n != a.m) throw new MathException(CalculationResources.GetString("error_34"));
            var result = new Matrix(a.n, a.m);
            for (int i = 0; i < a.n; i++) for (int j = 0; j < a.m; j++) result[j][i] = new Element(qiuA(a, i, j, notify));
            return result;
        }
        public override string ToString()
        {
            var text = new StringBuilder("(");
            for (int i = 0; i < n; i++)
            {
                if (i > 0) text.Append(",,");
                for (int j = 0; j < m; j++)
                {
                    if (j > 0) text.Append(',');
                    text.Append(matrix[i][j]?.ToCode() ?? "?");
                }
            }
            return text.Append(')').ToString();
        }
    }
}
