using System;
using System.Collections.Generic;
using System.Linq;
using NaturplayCalculator.Expressions;

namespace NaturplayCalculator.Model
{
    internal sealed class NumericDomain : ICalculationDomain<MathValue>
    {
        private readonly CalculationValue previous;
        private readonly bool radians;
        private readonly Dictionary<string, MathValue> variables;
        private bool resolvingAnswer;
        private bool usesPreviousAnswer;
        internal IEnumerable<string> Conditions => usesPreviousAnswer ? previous.Conditions : Array.Empty<string>();
        internal NumericDomain(CalculationOptions options)
        {
            previous = options.PreviousAnswer?.Clone() ?? new MathValue(0);
            radians = options.UseRadians;
            variables = options.Variables?.ToDictionary(p => p.Key, p => p.Value.Clone(), StringComparer.Ordinal);
        }
        public MathValue Number(string text) => new MathValue(NumericText.Parse(text));
        public MathValue Symbol(string name)
        {
            if (name == "π") return new MathValue(Math.PI);
            if (name == "e") return new MathValue(Math.E);
            if (name == "i") return new MathValue(new Complex(0, 1));
            if (name == "Ans")
            {
                usesPreviousAnswer = true;
                if (resolvingAnswer) throw new MathException(CalculationResources.GetString("invalid_expression"));
                if (previous.Numeric != null) return previous.Numeric.Clone();
                resolvingAnswer = true;
                try { return new ExpressionEvaluation<MathValue>(this, previous.Expression).Evaluate(); }
                finally { resolvingAnswer = false; }
            }
            if (variables != null && variables.TryGetValue(name, out var value)) return value.Clone();
            throw new MathException(string.Format(CalculationResources.GetString("unbound_variable"), name));
        }
        public MathValue Negate(MathValue value) => new MathValue(0) - value;
        public MathValue Binary(string operation, MathValue a, MathValue b)
        {
            CalculationContext.Check();
            switch (operation)
            {
                case "+": return a + b;
                case "-": return a - b;
                case "*": return a * b;
                case "/": return a / b;
                case "^": return MathValue.Pow(a, b);
                case "nCr": return new MathValue(Combinatorics.Permutations(Real(a), Real(b), true));
                case "nPr": return new MathValue(Combinatorics.Permutations(Real(a), Real(b), false));
                case "mod": return new MathValue(Real(a) % Real(b));
                case "yroot":
                    double x = Real(a), degree = Real(b);
                    if (degree == 0) throw new MathException(CalculationResources.GetString("division_by_zero"));
                    return x < 0 && degree == Math.Truncate(degree) && Math.Abs(degree % 2) == 1
                        ? new MathValue(-Math.Pow(-x, 1 / degree)) : MathValue.Pow(a, new MathValue(1 / degree));
                default: throw new InvalidOperationException(operation);
            }
        }
        public MathValue Function(string name, MathValue value)
        {
            CalculationContext.Check();
            switch (name)
            {
                case "transpose": return value.IsMatrix ? new MathValue(Model.Matrix.qiuT(value.Mtr, false)) : value.Clone();
                case "E": return MathValue.Ex(value, false);
                case "D": return MathValue.Dx(value, false);
                case "Cov": return MathValue.Cov(value, false);
                case "rho": return MathValue.Rho(value, false);
                case "Abs": return value.IsMatrix ? Determinant.solve(value.Mtr, false) : new MathValue(value.Cmx.Mod);
                case "rank":
                    if (!value.IsMatrix) return new MathValue(value.IsZero ? 0 : 1);
                    var stairs = Model.Matrix.getStairs(value.Mtr);
                    return new MathValue(stairs.matrix.Count(row => row.Any(cell => !cell.Value.IsZero)));
                case "sqrt":
                    if (value.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                    return new MathValue(Complex.From(System.Numerics.Complex.Sqrt(value.Cmx.Number)));
                case "fact": return new MathValue(Combinatorics.Factorial(Real(value)));
                case "dfact": return new MathValue(Combinatorics.DoubleFactorial(Real(value)));
                case "sin": return new MathValue(RealFunctions.Trig(Real(value), UnitaryOperator.sin, radians));
                case "cos": return new MathValue(RealFunctions.Trig(Real(value), UnitaryOperator.cos, radians));
                case "tan": return new MathValue(RealFunctions.Trig(Real(value), UnitaryOperator.tan, radians));
                case "sinrad": return new MathValue(RealFunctions.Trig(Real(value), UnitaryOperator.sin, true));
                case "cosrad": return new MathValue(RealFunctions.Trig(Real(value), UnitaryOperator.cos, true));
                case "tanrad": return new MathValue(RealFunctions.Trig(Real(value), UnitaryOperator.tan, true));
                case "arcsinrad": return new MathValue(Math.Asin(Real(value)));
                case "arccosrad": return new MathValue(Math.Acos(Real(value)));
                case "arctanrad": return new MathValue(Math.Atan(Real(value)));
                case "arcsin": return Angle(Math.Asin(Real(value)));
                case "arccos": return Angle(Math.Acos(Real(value)));
                case "arctan": return Angle(Math.Atan(Real(value)));
                case "ln": return new MathValue(Math.Log(Real(value)));
                case "lg": return new MathValue(Math.Log10(Real(value)));
                case "sinh": return new MathValue(Math.Sinh(Real(value)));
                case "cosh": return new MathValue(Math.Cosh(Real(value)));
                case "tanh": return new MathValue(Math.Tanh(Real(value)));
                case "arsh": return new MathValue(RealFunctions.Asinh(Real(value)));
                case "arch": return new MathValue(RealFunctions.Acosh(Real(value)));
                case "arth": return new MathValue(RealFunctions.Atanh(Real(value)));
                default: throw new InvalidOperationException(name);
            }
        }
        private MathValue Angle(double value) => new MathValue(radians ? value : value / Math.PI * 180);
        private static double Real(MathValue value)
        {
            if (value.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
            if (value.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_17"));
            return value.Cmx.Re;
        }
        public MathValue Matrix(int rows, int columns, IReadOnlyList<MathValue> cells)
        {
            var result = new Model.Matrix(rows, columns);
            for (int i = 0; i < rows; i++) for (int j = 0; j < columns; j++)
            {
                var cell = cells[i * columns + j];
                if (cell.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                result[i][j] = new Element(cell.Clone());
            }
            return new MathValue(result);
        }
        public ExpressionNode ToExpression(MathValue value) => CalculationValue.NumericExpression(value);
    }
}
