namespace NaturplayCalculator.Model
{
    public static class Determinant
    {
        // Partial-pivot elimination replaces factorial-time cofactor expansion.
        public static MathValue solve(Matrix matrix, bool notify)
        {
            if (matrix.n != matrix.m) throw new MathException(CalculationResources.GetString("error_20"));
            var values = Matrix.Numbers(matrix, notify);
            var result = new Complex(1);
            for (int k = 0; k < matrix.n; k++)
            {
                CalculationContext.Check();
                int pivot = Matrix.Pivot(values, k, k);
                if (Matrix.Zero(values[pivot][k])) return new MathValue(0);
                if (pivot != k) { Matrix.Swap(values, k, pivot); result *= -1; }
                var divisor = values[k][k];
                result *= divisor;
                for (int i = k + 1; i < matrix.n; i++)
                {
                    var factor = values[i][k] / divisor;
                    for (int j = k + 1; j < matrix.m; j++) { CalculationContext.Check(); values[i][j] -= factor * values[k][j]; }
                    values[i][k] = new Complex(0);
                }
            }
            return new MathValue(result);
        }
    }
}
