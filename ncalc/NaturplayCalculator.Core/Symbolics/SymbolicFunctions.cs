using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Model;

namespace NaturplayCalculator.Symbolics
{
    internal sealed partial class SymbolicDomain
    {
        private ExpressionNode Trig(string name, ExpressionNode argument)
        {
            bool explicitRadians = name.EndsWith("rad", StringComparison.Ordinal);
            string function = explicitRadians ? name.Substring(0, name.Length - 3) : name;
            var angle = radians || explicitRadians ? argument : algebra.Multiply(argument, algebra.Divide(ExpressionNode.Symbol("π"), Number("180")));
            var multiple = algebra.Divide(angle, ExpressionNode.Symbol("π"));
            if (Rational.TryRead(multiple, out var value))
            {
                var twelfths = value * new Rational(12);
                if (twelfths.IsInteger)
                {
                    int index = (int)((twelfths.Numerator % 24 + 24) % 24);
                    if (function == "sin") return Sine(index);
                    if (function == "cos") return Sine((index + 6) % 24);
                    return algebra.Divide(Sine(index), Sine((index + 6) % 24));
                }
            }
            // Unresolved trig calls keep their unit in the syntax, including Ans/history.
            if (function == "tan") Nonzero(ExpressionNode.Function("cosrad", angle));
            return ExpressionNode.Function(function + "rad", angle);
        }
        private ExpressionNode Sine(int index)
        {
            bool negative = index >= 12;
            index %= 12; if (index > 6) index = 12 - index;
            ExpressionNode value;
            switch (index)
            {
                case 0: value = SymbolicAlgebra.Zero; break;
                case 1: value = algebra.Divide(algebra.Subtract(algebra.Sqrt(Number("6")), algebra.Sqrt(Number("2"))), Number("4")); break;
                case 2: value = new Rational(1, 2).ToExpression(); break;
                case 3: value = algebra.Divide(algebra.Sqrt(Number("2")), Number("2")); break;
                case 4: value = algebra.Divide(algebra.Sqrt(Number("3")), Number("2")); break;
                case 5: value = algebra.Divide(algebra.Add(algebra.Sqrt(Number("6")), algebra.Sqrt(Number("2"))), Number("4")); break;
                default: value = SymbolicAlgebra.One; break;
            }
            return negative ? algebra.Negate(value) : value;
        }
        private ExpressionNode InverseTrig(string name, ExpressionNode argument)
        {
            bool explicitRadians = name.EndsWith("rad", StringComparison.Ordinal);
            string function = explicitRadians ? name.Substring(0, name.Length - 3) : name;
            ExpressionNode result = null;
            if (function != "arctan" && SymbolicAlgebra.ContainsVariables(argument)) conditions.Add("-1 ≤ " + argument.ToCode() + " ≤ 1");
            if (Rational.TryRead(argument, out var value))
            {
                if (function != "arctan" && BigInteger.Abs(value.Numerator) > value.Denominator) throw new MathException(CalculationResources.GetString("non_finite"));
                Rational? multiple = null;
                if (value.IsZero) multiple = function == "arccos" ? new Rational(1, 2) : Rational.Zero;
                else if (BigInteger.Abs(value.Numerator) == value.Denominator)
                    multiple = function == "arctan" ? new Rational(value.Sign, 4) : function == "arcsin" ? new Rational(value.Sign, 2) : value.Sign > 0 ? Rational.Zero : Rational.One;
                else if (BigInteger.Abs(value.Numerator) * 2 == value.Denominator && function != "arctan")
                    multiple = function == "arcsin" ? new Rational(value.Sign, 6) : new Rational(value.Sign > 0 ? 1 : 2, 3);
                if (multiple.HasValue) result = algebra.Multiply(multiple.Value.ToExpression(), ExpressionNode.Symbol("π"));
            }
            if (result == null) result = ExpressionNode.Function(function + "rad", argument);
            return radians || explicitRadians ? result : algebra.Divide(algebra.Multiply(Number("180"), result), ExpressionNode.Symbol("π"));
        }
        private ExpressionNode Statistic(string name, ExpressionNode argument)
        {
            if (argument.Kind != ExpressionKind.Matrix)
            {
                if (name == "E") return argument;
                if (name == "D") return SymbolicAlgebra.Zero;
                throw new MathException(CalculationResources.GetString("error_22"));
            }
            if (name == "E") return Mean(argument.Children.ToArray());
            if (name == "D") return Variance(argument.Children.ToArray());
            if (argument.Rows != 2) throw new MathException(CalculationResources.GetString("error_22"));
            var x = argument.Children.Take(argument.Columns).ToArray();
            var y = argument.Children.Skip(argument.Columns).ToArray();
            var xMean = Mean(x); var yMean = Mean(y); var total = SymbolicAlgebra.Zero;
            for (int i = 0; i < x.Length; i++) total = algebra.Add(total, algebra.Multiply(algebra.Subtract(x[i], xMean), algebra.Subtract(y[i], yMean)));
            var covariance = algebra.Divide(total, Number(x.Length.ToString(CultureInfo.InvariantCulture)));
            return name == "Cov" ? covariance : Binary("/", covariance, algebra.Multiply(algebra.Sqrt(Variance(x)), algebra.Sqrt(Variance(y))));
        }
        private ExpressionNode Mean(ExpressionNode[] values)
        {
            var total = SymbolicAlgebra.Zero;
            foreach (var value in values) total = algebra.Add(total, value);
            return algebra.Divide(total, Number(values.Length.ToString(CultureInfo.InvariantCulture)));
        }
        private ExpressionNode Variance(ExpressionNode[] values)
        {
            var mean = Mean(values); var total = SymbolicAlgebra.Zero;
            foreach (var value in values) total = algebra.Add(total, algebra.Power(algebra.Subtract(value, mean), Number("2")));
            return algebra.Divide(total, Number(values.Length.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
