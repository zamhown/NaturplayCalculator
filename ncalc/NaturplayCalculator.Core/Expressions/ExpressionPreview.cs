using System;
using System.Collections.Generic;
using NaturplayCalculator.Model;

namespace NaturplayCalculator.Expressions
{
    /// <summary>A display-only recovery result. It never substitutes for the actual
    /// input passed to CalculationEngine, Ans or persistent history.</summary>
    public sealed class ExpressionPreview
    {
        public ExpressionNode Root { get; private set; }
        public TexExpression Formula { get; private set; }
        public IReadOnlyList<string> Comments { get; private set; }
        public bool IsLimited { get; private set; }

        public static bool IsMathCandidate(string input)
        {
            string text = (input ?? "").TrimStart();
            if (text.Length == 0) return true;
            if (text[0] == '{' || text[0] == '"' || text[0] == '\'') return false;
            if (text[0] == '<' && text.Length > 1 && (char.IsLetter(text[1]) || text[1] == '?' || text[1] == '!')) return false;
            if (text[0] == '[')
            {
                string content = text.Substring(1).TrimStart();
                if (content.Length > 0 && (char.IsDigit(content[0]) || "\"{}[]-".IndexOf(content[0]) >= 0
                    || content.StartsWith("true", StringComparison.Ordinal) || content.StartsWith("false", StringComparison.Ordinal) || content.StartsWith("null", StringComparison.Ordinal))) return false;
            }
            return true;
        }

        public static ExpressionPreview Create(string input, bool implicitProducts = false, bool foldNumbers = false)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            int length = Math.Min(input.Length, ExpressionLimits.MaxLength);
            while (true)
            {
                try
                {
                    var parsed = new ExpressionParser().ParsePreview(input.Substring(0, length));
                    bool limited = length < input.Length;
                    var root = limited ? ExpressionNode.DraftRow(new[] { parsed.Root, ExpressionNode.DraftLiteral("…", -1) }) : parsed.Root;
                    return new ExpressionPreview { Root = root, Formula = new ExpressionTexFormatter().Format(root, implicitProducts, foldNumbers, input.Substring(0, length)),
                        Comments = parsed.Comments, IsLimited = limited };
                }
                catch (MathException)
                {
                    // Resource limits stay bounded. Show a structured prefix and an
                    // explicit ellipsis/limit notice instead of raw-text fallback.
                    if (length == 0) throw;
                    length /= 2;
                }
            }
        }
    }
}
