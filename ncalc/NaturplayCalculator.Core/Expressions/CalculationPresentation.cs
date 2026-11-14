using System.Collections.Generic;

namespace NaturplayCalculator.Expressions
{
    public static class CalculationPresentation
    {
        /// <summary>Deduplicate consecutive visible steps, retaining the final
        /// value's complete semantics for copying and subsequent calculations.</summary>
        public static IReadOnlyList<ExpressionNode> ResultSequence(ExpressionNode input, IEnumerable<ExpressionNode> steps,
            ExpressionNode result, int intermediateCharacterBudget = 4096)
        {
            var visible = new List<ExpressionNode>();
            string previous = input.ToDisplayString();
            foreach (var step in steps)
            {
                string display = step.ToDisplayString();
                if (display == previous) continue;
                if ((intermediateCharacterBudget -= step.ToCode().Length) < 0) break;
                visible.Add(step); previous = display;
            }
            if (visible.Count > 0 && previous == result.ToDisplayString()) visible[visible.Count - 1] = result;
            else visible.Add(result);
            return visible.AsReadOnly();
        }
    }
}
