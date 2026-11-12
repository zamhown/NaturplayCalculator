using System;

namespace NaturplayCalculator.Model
{
    internal static class RealFunctions
    {
        internal static double Trig(double angle, UnitaryOperator function, bool radians)
        {
            double quadrant = radians ? angle / (Math.PI / 2) : angle / 90;
            if (quadrant == Math.Truncate(quadrant) && Math.Abs(quadrant) <= 4503599627370496)
            {
                int index = (int)((quadrant % 4 + 4) % 4);
                if (function == UnitaryOperator.sin) return new[] { 0.0, 1, 0, -1 }[index];
                if (function == UnitaryOperator.cos) return new[] { 1.0, 0, -1, 0 }[index];
                if ((index & 1) != 0) throw new MathException(CalculationResources.GetString("non_finite"));
                return 0;
            }
            double value = radians ? angle : (angle % 360) * (Math.PI / 180);
            return function == UnitaryOperator.sin ? Math.Sin(value) : function == UnitaryOperator.cos ? Math.Cos(value) : Math.Tan(value);
        }
        internal static double Asinh(double value)
        {
            double x = Math.Abs(value);
            if (x < 1e-8) return value;
            double result = x > 1e150 ? Math.Log(x) + Math.Log(2) : Math.Log(x + Math.Sqrt(x * x + 1));
            return value < 0 ? -result : result;
        }
        internal static double Acosh(double value)
        {
            if (value < 1) throw new MathException(CalculationResources.GetString("non_finite"));
            return value > 1e150 ? Math.Log(value) + Math.Log(2) : Math.Log(value + Math.Sqrt((value - 1) * (value + 1)));
        }
        internal static double Atanh(double value)
        {
            if (Math.Abs(value) >= 1) throw new MathException(CalculationResources.GetString("non_finite"));
            return Math.Abs(value) < 1e-8 ? value : 0.5 * (Math.Log(1 + value) - Math.Log(1 - value));
        }
    }
}
