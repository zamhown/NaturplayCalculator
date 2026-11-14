using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NaturplayCalculator.Expressions
{
    public static class ExpressionFormatter
    {
        private static readonly Regex fixedRadiansInCondition = new Regex(@"(?<![\p{L}\p{N}_])(arcsin|arccos|arctan|sin|cos|tan)rad(?=\()", RegexOptions.CultureInvariant);

        // Angle binding is part of the stored expression, not part of a function's
        // mathematical name. The UI reports that unit separately from the formula.
        public static string DisplayFunctionName(string name)
        {
            switch (name)
            {
                case "sinrad": case "cosrad": case "tanrad":
                case "arcsinrad": case "arccosrad": case "arctanrad":
                    return name.Substring(0, name.Length - 3);
                default: return name;
            }
        }

        public static bool ContainsFixedRadians(ExpressionNode node) =>
            node.Kind == ExpressionKind.Function && DisplayFunctionName(node.Text) != node.Text
            || node.Children.Any(ContainsFixedRadians);
        public static bool ConditionContainsFixedRadians(string condition) => fixedRadiansInCondition.IsMatch(condition);
        public static string DisplayCondition(string condition) => fixedRadiansInCondition.Replace(condition, "$1");

        public static int Precedence(ExpressionNode node)
        {
            if (node.Kind == ExpressionKind.DraftRow) return node.Text == "sum" ? 10 : node.Text == "product" ? 20 : 100;
            if (node.Kind == ExpressionKind.Function && node.Text == "transpose") return 30;
            if (node.Kind == ExpressionKind.Unary || node.Kind == ExpressionKind.Number && node.Text.StartsWith("-", StringComparison.Ordinal)) return 25;
            if (node.Kind != ExpressionKind.Binary) return 100;
            if (node.Text == "+" || node.Text == "-") return 10;
            return node.Text == "^" || node.Text == "yroot" ? 30 : 20;
        }

        public static string Format(ExpressionNode node, bool display)
        {
            if (node.Kind == ExpressionKind.Placeholder || node.Kind == ExpressionKind.DraftLiteral) return node.Text;
            if (node.Kind == ExpressionKind.DraftGroup) return "(" + Format(node.Children[0], display) + ")";
            if (node.Kind == ExpressionKind.DraftRow) return string.Concat(node.Children.Select(n => Format(n, display)));
            if (node.Kind == ExpressionKind.Number || node.Kind == ExpressionKind.Symbol) return node.Text;
            if (node.Kind == ExpressionKind.Unary)
                return node.Text + Wrap(node.Children[0], 25, display);
            if (node.Kind == ExpressionKind.Function)
            {
                var argument = node.Children[0];
                if (node.Text == "fact" || node.Text == "dfact") return Wrap(argument, 40, display) + (node.Text == "fact" ? "!" : "!!");
                if (node.Text == "transpose") return Wrap(argument, 31, display) + "^T";
                if (display && node.Text == "sqrt")
                    return "√" + (argument.Kind == ExpressionKind.Number && !argument.Text.StartsWith("-", StringComparison.Ordinal) || argument.Kind == ExpressionKind.Symbol ? Format(argument, true) : "(" + Format(argument, true) + ")");
                string name = display ? DisplayFunctionName(node.Text) : node.Text;
                return name + (argument.Kind == ExpressionKind.Matrix ? Format(argument, display) : "(" + Format(argument, display) + ")");
            }
            if (node.Kind == ExpressionKind.Matrix)
            {
                var text = new StringBuilder("(");
                for (int row = 0; row < node.Rows; row++)
                {
                    if (row > 0) text.Append(",,");
                    for (int column = 0; column < node.Columns; column++)
                    {
                        if (column > 0) text.Append(',');
                        text.Append(Format(node.Children[row * node.Columns + column], display));
                    }
                }
                if (node.Rows == 1 && node.Columns == 1) text.Append(',');
                return text.Append(')').ToString();
            }
            bool implicitProduct = display && node.Text == "*" && CanOmitMultiply(node.Children[0], node.Children[1]);
            int precedence = implicitProduct ? 25 : Precedence(node);
            bool wrapLeftPower = node.Text == "^" || precedence == 30 && (node.Children[0].Kind == ExpressionKind.Binary && node.Children[0].Text == "^"
                || node.Children[0].Kind == ExpressionKind.Function && node.Children[0].Text == "transpose");
            string left = Wrap(node.Children[0], precedence + (wrapLeftPower ? 1 : 0), display);
            string right = Wrap(node.Children[1], precedence + (node.Text == "^" ? 0 : 1), display);
            string op = node.Text;
            if (implicitProduct) op = "";
            if (op == "mod" || op == "nCr" || op == "nPr" || op == "yroot") op = " " + op + " ";
            return left + op + right;
        }
        private static string Wrap(ExpressionNode node, int minimum, bool display)
        {
            var value = Format(node, display);
            int precedence = display && node.Kind == ExpressionKind.Binary && node.Text == "*"
                && CanOmitMultiply(node.Children[0], node.Children[1]) ? 25 : Precedence(node);
            return precedence < minimum ? "(" + value + ")" : value;
        }
        public static bool CanOmitMultiply(ExpressionNode left, ExpressionNode right)
        {
            if (right.Kind == ExpressionKind.Number || right.Kind == ExpressionKind.Unary) return false;
            // Adjacent variable/function names can become a different function: a*sin(x)
            // must not be displayed as asin(x). Numeric coefficients remain conventional.
            if (right.Kind == ExpressionKind.Function && right.Text != "sqrt" && right.Text != "Abs"
                && right.Text != "fact" && right.Text != "dfact" && right.Text != "transpose"
                && left.Kind != ExpressionKind.Number) return false;
            if (left.Kind == ExpressionKind.Symbol && right.Kind == ExpressionKind.Symbol)
                return left.Text.Length == 1 && right.Text.Length == 1 && left.Text != "p";
            return right.Kind == ExpressionKind.Symbol || right.Kind == ExpressionKind.Function
                || right.Kind == ExpressionKind.Binary && (right.Text == "^" || Precedence(right) < 20);
        }
    }
}
