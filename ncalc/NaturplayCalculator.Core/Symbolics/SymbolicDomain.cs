using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Model;

namespace NaturplayCalculator.Symbolics
{
    internal sealed partial class SymbolicDomain : ICalculationDomain<ExpressionNode>
    {
        private readonly SymbolicAlgebra algebra = new SymbolicAlgebra();
        private readonly CalculationValue previous;
        private readonly bool radians;
        private bool resolvingAnswer;
        private readonly HashSet<string> conditions = new HashSet<string>(StringComparer.Ordinal);
        internal IEnumerable<string> Conditions => conditions.OrderBy(c => c, StringComparer.Ordinal);
        internal SymbolicDomain(CalculationOptions options)
        { previous = options.PreviousAnswer?.Clone() ?? new MathValue(0); radians = options.UseRadians; }
        public ExpressionNode Number(string text) => Rational.Parse(text).ToExpression();
        public ExpressionNode Symbol(string name)
        {
            if (name == "?") throw new MathException(CalculationResources.GetString("invalid_expression"));
            if (name != "Ans") return ExpressionNode.Symbol(name);
            if (resolvingAnswer) throw new MathException(CalculationResources.GetString("invalid_expression"));
            foreach (string condition in previous.Conditions) conditions.Add(condition);
            if (previous.Mode == CalculationMode.Symbolic) return previous.Expression;
            resolvingAnswer = true;
            try { return new ExpressionEvaluation<ExpressionNode>(this, previous.Expression).Evaluate(); }
            finally { resolvingAnswer = false; }
        }
        public ExpressionNode Negate(ExpressionNode value) => value.Kind == ExpressionKind.Matrix ? Map(value, algebra.Negate) : algebra.Negate(value);
        public ExpressionNode Binary(string operation, ExpressionNode a, ExpressionNode b)
        {
            CalculationContext.Check();
            if (a.Kind == ExpressionKind.Matrix || b.Kind == ExpressionKind.Matrix) return MatrixBinary(operation, a, b);
            switch (operation)
            {
                case "+": return algebra.Add(a, b);
                case "-": return algebra.Subtract(a, b);
                case "*": return algebra.Multiply(a, b);
                case "/": Nonzero(b); return algebra.Divide(a, b);
                case "^":
                    if (Rational.TryRead(b, out var power) && power.Sign < 0) Nonzero(a);
                    return algebra.Power(a, b);
                case "yroot":
                    Nonzero(b);
                    if (Rational.TryRead(a, out var radicand) && radicand.Sign < 0 && Rational.TryRead(b, out var degree) && degree.IsInteger && !degree.Numerator.IsEven)
                        return algebra.Negate(algebra.Power((-radicand).ToExpression(), (Rational.One / degree).ToExpression()));
                    return algebra.Power(a, algebra.Divide(SymbolicAlgebra.One, b));
                case "nCr": case "nPr": return Combinations(a, b, operation == "nCr");
                case "mod":
                    Nonzero(b);
                    if (Rational.TryRead(a, out var dividend) && Rational.TryRead(b, out var divisor))
                    {
                        var quotient = dividend / divisor;
                        return (dividend - new Rational(quotient.Numerator / quotient.Denominator) * divisor).ToExpression();
                    }
                    return ExpressionNode.Binary(operation, a, b);
                default: throw new InvalidOperationException(operation);
            }
        }
        public ExpressionNode Function(string name, ExpressionNode argument)
        {
            CalculationContext.Check();
            if (name == "transpose") return argument.Kind == ExpressionKind.Matrix ? Transpose(argument) : argument;
            if (name == "E" || name == "D" || name == "Cov" || name == "rho") return Statistic(name, argument);
            if (argument.Kind == ExpressionKind.Matrix)
            {
                if (name == "Abs") return Determinant(argument);
                if (name == "rank") return Rank(argument);
                throw new MathException(CalculationResources.GetString("error_16"));
            }
            if (name == "sqrt") return algebra.Sqrt(argument);
            if (name == "Abs") return algebra.Abs(argument);
            if (name == "fact" || name == "dfact") return Factorial(argument, name == "dfact");
            if (name == "sin" || name == "cos" || name == "tan" || name == "sinrad" || name == "cosrad" || name == "tanrad")
                return Trig(name, argument);
            if (name == "arcsin" || name == "arccos" || name == "arctan" || name == "arcsinrad" || name == "arccosrad" || name == "arctanrad")
                return InverseTrig(name, argument);
            if (name == "ln" && argument.Kind == ExpressionKind.Symbol && argument.Text == "e") return SymbolicAlgebra.One;
            if (SymbolicAlgebra.ContainsVariables(argument))
            {
                string code = argument.ToCode();
                if (name == "ln" || name == "lg") conditions.Add(code + " > 0");
                if (name == "arch") conditions.Add(code + " ≥ 1");
                if (name == "arth") conditions.Add("-1 < " + code + " < 1");
            }
            if (Rational.TryRead(argument, out var value))
            {
                if (name == "rank") return Number(value.IsZero ? "0" : "1");
                if (name == "ln" || name == "lg")
                {
                    if (value.Sign <= 0) throw new MathException(CalculationResources.GetString("non_finite"));
                    if (value.IsOne) return SymbolicAlgebra.Zero;
                    if (name == "lg")
                    {
                        if (PowerOfTen(value.Numerator, out var n) && PowerOfTen(value.Denominator, out var d)) return Number((n - d).ToString(CultureInfo.InvariantCulture));
                    }
                }
                if (value.IsZero)
                {
                    if (name == "sinh" || name == "tanh" || name == "arsh" || name == "arth") return SymbolicAlgebra.Zero;
                    if (name == "cosh") return SymbolicAlgebra.One;
                }
                if (name == "arch" && value.IsOne) return SymbolicAlgebra.Zero;
                if (name == "arch" && value.Numerator < value.Denominator || name == "arth" && BigInteger.Abs(value.Numerator) >= value.Denominator)
                    throw new MathException(CalculationResources.GetString("non_finite"));
            }
            return ExpressionNode.Function(name, argument);
        }
        public ExpressionNode Matrix(int rows, int columns, IReadOnlyList<ExpressionNode> cells)
        {
            if (cells.Any(cell => cell.Kind == ExpressionKind.Matrix)) throw new MathException(CalculationResources.GetString("error_16"));
            return ExpressionNode.Matrix(rows, columns, cells);
        }
        public ExpressionNode ToExpression(ExpressionNode value) => value;
        private void Nonzero(ExpressionNode value)
        {
            if (SymbolicAlgebra.IsZero(value)) throw new MathException(CalculationResources.GetString("division_by_zero"));
            if (SymbolicAlgebra.ContainsVariables(value)) conditions.Add(value.ToCode() + " ≠ 0");
        }
        private ExpressionNode Factorial(ExpressionNode argument, bool twice)
        {
            if (!Rational.TryRead(argument, out var value))
            {
                if (SymbolicAlgebra.ContainsVariables(argument)) conditions.Add(argument.ToCode() + (twice ? " ∈ {−1, −3, …} ∪ ℤ≥0" : " ∈ ℤ≥0"));
                return ExpressionNode.Function(twice ? "dfact" : "fact", argument);
            }
            if (!value.IsInteger || value.Numerator > 1000 || value.Numerator < (twice ? -999 : 0) || twice && value.Sign < 0 && value.Numerator.IsEven)
                throw new MathException(CalculationResources.GetString("integer_required"));
            int n = (int)value.Numerator;
            var result = Rational.One;
            if (n >= 0)
                for (int i = n; i >= 2; i -= twice ? 2 : 1) result *= new Rational(i);
            else
                for (int i = -1; i > n; i -= 2) result /= new Rational(i);
            return result.ToExpression();
        }
        private ExpressionNode Combinations(ExpressionNode a, ExpressionNode b, bool combinations)
        {
            if (!Rational.TryRead(a, out var nValue) || !Rational.TryRead(b, out var rValue))
            {
                if (SymbolicAlgebra.ContainsVariables(a) || SymbolicAlgebra.ContainsVariables(b))
                {
                    conditions.Add(a.ToCode() + ", " + b.ToCode() + " ∈ ℤ");
                    conditions.Add("0 ≤ " + b.ToCode() + " ≤ " + a.ToCode());
                }
                return ExpressionNode.Binary(combinations ? "nCr" : "nPr", a, b);
            }
            if (!nValue.IsInteger || !rValue.IsInteger || nValue.Sign < 0 || rValue.Sign < 0 || rValue.Numerator > nValue.Numerator || nValue.Numerator > int.MaxValue)
                throw new MathException(CalculationResources.GetString("integer_required"));
            int n = (int)nValue.Numerator, r = (int)rValue.Numerator;
            if (combinations) r = Math.Min(r, n - r);
            if (r > 100000) ExpressionLimits.Fail();
            BigInteger result = 1;
            for (int i = 1; i <= r; i++)
            {
                CalculationContext.Check(); result *= n - r + i;
                if (combinations) result /= i;
                Rational.CheckSize(result);
            }
            return new Rational(result).ToExpression();
        }
        private static bool PowerOfTen(BigInteger value, out int power)
        {
            power = 0;
            while (value > 1 && value % 10 == 0) { CalculationContext.Check(); value /= 10; power++; }
            return value.IsOne;
        }
    }
}
