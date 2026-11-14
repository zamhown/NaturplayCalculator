using System;

namespace NaturplayCalculator.Model
{
    internal static class Statistics
    {
        internal static MathValue Mean(MathValue value, bool notify)
        {
            if (!value.IsMatrix) return value.Clone();
            var numbers = Read(value.Mtr, notify);
            return new MathValue(Mean(numbers, Scale(numbers)));
        }

        internal static MathValue Variance(MathValue value, bool notify)
        {
            if (!value.IsMatrix) return new MathValue(0);
            var numbers = Read(value.Mtr, notify);
            double scale = Scale(numbers);
            var centered = Center(numbers, scale);
            return new MathValue(ProductMean(centered, centered) * scale * scale);
        }

        internal static MathValue Covariance(MathValue value, bool notify, bool correlation)
        {
            if (!value.IsMatrix || value.Mtr.n != 2 || value.Mtr.m == 0)
                throw new MathException(CalculationResources.GetString("error_22"));
            var x = ReadRow(value.Mtr, 0, notify);
            var y = ReadRow(value.Mtr, 1, notify);
            double xScale = Scale(x), yScale = Scale(y);
            x = Center(x, xScale); y = Center(y, yScale);
            var covariance = ProductMean(x, y);
            if (!correlation)
                return new MathValue(covariance * Math.Min(xScale, yScale) * Math.Max(xScale, yScale));

            // The scales cancel in correlation. Computing unscaled variances first
            // would overflow for large samples and underflow for very small ones.
            var xDeviation = Complex.From(System.Numerics.Complex.Sqrt(ProductMean(x, x).Number));
            var yDeviation = Complex.From(System.Numerics.Complex.Sqrt(ProductMean(y, y).Number));
            return new MathValue(covariance / (xDeviation * yDeviation));
        }

        private static Complex[] Read(Matrix matrix, bool notify)
        {
            if (matrix.n == 0 || matrix.m == 0) throw new MathException(CalculationResources.GetString("error_22"));
            var result = new Complex[matrix.n * matrix.m];
            for (int i = 0; i < matrix.n; i++)
                for (int j = 0; j < matrix.m; j++) result[i * matrix.m + j] = Matrix.Entry(matrix, i, j, notify).Cmx;
            return result;
        }

        private static Complex[] ReadRow(Matrix matrix, int row, bool notify)
        {
            var result = new Complex[matrix.m];
            for (int j = 0; j < matrix.m; j++) result[j] = Matrix.Entry(matrix, row, j, notify).Cmx;
            return result;
        }

        private static double Scale(Complex[] values)
        {
            double scale = 0;
            foreach (var value in values) scale = Math.Max(scale, Math.Max(Math.Abs(value.Re), Math.Abs(value.Im)));
            return scale;
        }

        private static Complex Mean(Complex[] values, double scale)
        {
            if (scale == 0) return new Complex(0);
            var real = new CompensatedSum(); var imaginary = new CompensatedSum();
            foreach (var value in values)
            {
                CalculationContext.Check();
                real.Add(value.Re / scale); imaginary.Add(value.Im / scale);
            }
            return new Complex(real.Value / values.Length * scale, imaginary.Value / values.Length * scale);
        }

        private static Complex[] Center(Complex[] values, double scale)
        {
            var mean = Mean(values, scale);
            var result = new Complex[values.Length];
            for (int i = 0; i < values.Length; i++)
                result[i] = scale == 0 ? new Complex(0) : new Complex(
                    Difference(values[i].Re, mean.Re, scale), Difference(values[i].Im, mean.Im, scale));
            return result;
        }

        private static double Difference(double value, double mean, double scale)
        {
            double difference = value - mean;
            return double.IsInfinity(difference) ? value / scale - mean / scale : difference / scale;
        }

        private static Complex ProductMean(Complex[] x, Complex[] y)
        {
            var real = new CompensatedSum(); var imaginary = new CompensatedSum();
            for (int i = 0; i < x.Length; i++)
            {
                CalculationContext.Check();
                var product = x[i] * y[i];
                real.Add(product.Re); imaginary.Add(product.Im);
            }
            // Preserve the calculator's existing non-conjugate complex moments.
            return new Complex(real.Value / x.Length, imaginary.Value / x.Length);
        }

        private sealed class CompensatedSum
        {
            private double sum, correction;
            internal double Value => sum + correction;
            internal void Add(double value)
            {
                double next = sum + value;
                correction += Math.Abs(sum) >= Math.Abs(value) ? (sum - next) + value : (value - next) + sum;
                sum = next;
            }
        }
    }
}
