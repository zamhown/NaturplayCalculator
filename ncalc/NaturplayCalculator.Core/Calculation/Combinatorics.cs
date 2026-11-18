using System;

namespace NaturplayCalculator.Model
{
    internal static class Combinatorics
    {
        internal static int Integer(double value, int min = 0, int max = int.MaxValue)
        {
            if (double.IsNaN(value) || value < min || value > max || value != Math.Truncate(value))
                throw new MathException(CalculationResources.GetString("integer_required"));
            return (int)value;
        }
        internal static double Factorial(double value)
        {
            int n = Integer(value, 0, 170);
            double result = 1;
            for (int i = 2; i <= n; i++) { CalculationContext.Check(); result *= i; }
            return result;
        }
        internal static double DoubleFactorial(double value)
        {
            int n = Integer(value, -299, 300);
            if (n < 0 && n % 2 == 0) throw new MathException(CalculationResources.GetString("error_14"));
            double result = 1;
            if (n >= 0)
                for (int i = n; i >= 2; i -= 2) { CalculationContext.Check(); result *= i; }
            else
                for (int i = -1; i > n; i -= 2) { CalculationContext.Check(); result /= i; }
            NumericText.RequireFinite(result);
            return result;
        }
        internal static double Permutations(double nValue, double rValue, bool combinations)
        {
            int n = Integer(nValue), r = Integer(rValue);
            if (r > n) throw new MathException(CalculationResources.GetString("error_15"));
            if (combinations) r = Math.Min(r, n - r);
            if (r > 100000) ExpressionLimits.Fail();
            double result = 1;
            for (int i = 0; i < r; i++)
            {
                CalculationContext.Check();
                result *= combinations ? (double)(n - r + i + 1) / (i + 1) : n - i;
                NumericText.RequireFinite(result);
            }
            return result;
        }
    }
}
