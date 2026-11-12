using System;
using System.Collections.Generic;
using System.Threading;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Symbolics;

namespace NaturplayCalculator.Model
{
    public sealed class CalculationResult
    {
        public ExpressionNode Expression { get; internal set; }
        public ExpressionNode EvaluatedExpression => Value.Expression;
        public CalculationValue Value { get; internal set; }
        public MathValue Answer => Value.RequireNumeric();
        public IReadOnlyList<ExpressionNode> Steps { get; internal set; }
        public IReadOnlyList<string> Comments { get; internal set; }
    }

    public sealed class CalculationEngine
    {
        public CalculationResult Evaluate(string input, CalculationOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            options = options ?? new CalculationOptions();
            if (options.MaxOperations < 1 || options.MaxSteps < 0 || options.MaxSteps > 128 || options.Timeout <= TimeSpan.Zero || !Enum.IsDefined(typeof(CalculationMode), options.Mode))
                throw new ArgumentOutOfRangeException(nameof(options));
            using (var context = new CalculationContext(options, cancellationToken))
            {
                CalculationContext.Check();
                ExpressionLimits.Validate(input);
                var parsed = new ExpressionParser().Parse(input);
                int maxSteps = options.CaptureSteps ? options.MaxSteps : 0;
                if (options.Mode == CalculationMode.Symbolic)
                {
                    var domain = new SymbolicDomain(options);
                    var evaluation = new ExpressionEvaluation<ExpressionNode>(domain, parsed.Root, maxSteps);
                    var answer = evaluation.Evaluate();
                    return new CalculationResult { Expression = parsed.Root, Value = CalculationValue.FromSymbolic(answer, domain.Conditions), Steps = evaluation.Steps, Comments = parsed.Comments };
                }
                else
                {
                    var domain = new NumericDomain(options);
                    var evaluation = new ExpressionEvaluation<MathValue>(domain, parsed.Root, maxSteps);
                    var answer = evaluation.Evaluate();
                    RequireNumericAnswer(answer);
                    return new CalculationResult { Expression = parsed.Root, Value = CalculationValue.FromNumeric(answer, domain.Conditions), Steps = evaluation.Steps, Comments = parsed.Comments };
                }
            }
        }

        private static void RequireNumericAnswer(MathValue value)
        {
            if (value.IsNull) throw new MathException(CalculationResources.GetString("error_22"));
            if (!value.IsMatrix)
            {
                NumericText.RequireFinite(value.Cmx.Re);
                NumericText.RequireFinite(value.Cmx.Im);
                return;
            }
            if (value.Mtr.n == 0 || value.Mtr.m == 0) throw new MathException(CalculationResources.GetString("error_22"));
            for (int row = 0; row < value.Mtr.n; row++)
                for (int column = 0; column < value.Mtr.m; column++)
                {
                    CalculationContext.Check();
                    var entry = value.Mtr[row][column].Value;
                    if (entry.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                    RequireNumericAnswer(entry);
                }
        }
    }

    public static class ExpressionLimits
    {
        public const int MaxLength = 4096;
        public const int MaxResultLength = 16384;
        public const int MaxDepth = 48;
        public const int MaxMatrixDimension = 32;

        public static void Validate(string input, int maxLength = MaxLength, int maxDepth = MaxDepth)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Length > maxLength) Fail();
            int depth = 0;
            bool comment = false, escaped = false;
            foreach (char c in input)
            {
                if (comment)
                {
                    if (c == ']' && !escaped) comment = false;
                    escaped = c == '\\' && !escaped;
                    continue;
                }
                if (c == '[') { comment = true; continue; }
                if (c == '(' && ++depth > maxDepth) Fail();
                if (c == ')') depth--;
            }
        }

        internal static void Fail() { throw new MathException(CalculationResources.GetString("calculation_limit")); }
    }
}
