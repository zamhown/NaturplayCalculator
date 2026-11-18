using System;
using System.Collections.Generic;
using System.Linq;
using NaturplayCalculator.Expressions;

namespace NaturplayCalculator.Model
{
    public enum CalculationMode { Numeric, Symbolic }

    public sealed class CalculationValue
    {
        public CalculationMode Mode { get; }
        public MathValue Numeric { get; }
        public ExpressionNode Expression { get; }
        public IReadOnlyList<string> Conditions { get; }
        public string Code => Expression.ToCode();
        public string DisplayText => Expression.ToDisplayString();
        public bool UsesFixedRadians => ExpressionFormatter.ContainsFixedRadians(Expression)
            || Conditions.Any(ExpressionFormatter.ConditionContainsFixedRadians);
        public IReadOnlyList<string> DisplayConditions => Array.AsReadOnly(Conditions.Select(ExpressionFormatter.DisplayCondition).ToArray());
        public Complex Cmx => RequireNumeric().Cmx;
        public Matrix Mtr => RequireNumeric().Mtr;
        public bool IsMatrix => Expression.Kind == ExpressionKind.Matrix;

        private CalculationValue(MathValue numeric, ExpressionNode expression, IEnumerable<string> conditions)
        {
            Numeric = numeric; Expression = expression;
            Mode = numeric == null ? CalculationMode.Symbolic : CalculationMode.Numeric;
            Conditions = Array.AsReadOnly((conditions ?? Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray());
            if (Conditions.Count > 64 || Conditions.Sum(condition => condition.Length) > ExpressionLimits.MaxResultLength) ExpressionLimits.Fail();
        }
        public static CalculationValue FromNumeric(MathValue value, IEnumerable<string> conditions = null) => new CalculationValue(value.Clone(), NumericExpression(value), conditions);
        public static CalculationValue FromSymbolic(ExpressionNode expression, IEnumerable<string> conditions = null) => new CalculationValue(null, expression ?? throw new ArgumentNullException(nameof(expression)), conditions);
        public CalculationValue Clone() => Numeric == null ? new CalculationValue(null, Expression, Conditions) : FromNumeric(Numeric, Conditions);
        public MathValue RequireNumeric() => Numeric ?? throw new MathException(CalculationResources.GetString("symbolic_numeric_required"));
        public static implicit operator CalculationValue(MathValue value) => value == null ? null : FromNumeric(value);
        public override string ToString() => Numeric?.ToString() ?? DisplayText;

        internal static ExpressionNode NumericExpression(MathValue value)
        {
            if (value.IsMatrix)
            {
                var cells = new List<ExpressionNode>();
                for (int i = 0; i < value.Mtr.n; i++) for (int j = 0; j < value.Mtr.m; j++) cells.Add(NumericExpression(value.Mtr[i][j].Value));
                return ExpressionNode.Matrix(value.Mtr.n, value.Mtr.m, cells);
            }
            var number = value.Cmx;
            if (number.Im == 0) return ExpressionNode.Number(NumericText.Format(number.Re));
            var imaginary = Math.Abs(number.Im) == 1 ? ExpressionNode.Symbol("i") : ExpressionNode.Binary("*", ExpressionNode.Number(NumericText.Format(Math.Abs(number.Im))), ExpressionNode.Symbol("i"));
            if (number.Re == 0) return number.Im < 0 ? ExpressionNode.Unary("-", imaginary) : imaginary;
            return ExpressionNode.Binary(number.Im < 0 ? "-" : "+", ExpressionNode.Number(NumericText.Format(number.Re)), imaginary);
        }
    }
}
