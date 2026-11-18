using System;
using System.Collections.Generic;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Model;

namespace NaturplayCalculator.Input
{
    /// <summary>A keypad edit in the current native buffer. Offsets never refer
    /// to the outer formula; the host owns scope serialization and navigation.</summary>
    public sealed class OperatorEdit
    {
        public string Text { get; }
        public int SelectionStart { get; }
        public int SelectionLength { get; }
        public int SlotStart { get; }
        public int SlotLength { get; }
        public bool FocusSlot { get; }
        public bool CompletesOperand { get; }

        internal OperatorEdit(string text, int selectionStart, int selectionLength, int slotStart, int slotLength, bool focusSlot, bool completesOperand = false)
        {
            Text = text; SelectionStart = selectionStart; SelectionLength = selectionLength;
            SlotStart = slotStart; SlotLength = slotLength; FocusSlot = focusSlot;
            CompletesOperand = completesOperand;
        }
    }

    /// <summary>Scientific-calculator input: infix keys insert at the cursor,
    /// prefix keys open a new argument, and postfix keys bind the adjacent value.
    /// A scope limits editing; it is never an implicit operand selection.</summary>
    public static class OperatorEditor
    {
        private static readonly HashSet<string> Functions = new HashSet<string>(FunctionCatalog.Tokens, StringComparer.OrdinalIgnoreCase);

        /// <summary>Only keys requiring a left operand continue a displayed answer.
        /// Functions and the sign key begin a fresh expression after a result.</summary>
        public static bool ContinuesAnswer(string operation)
        {
            string op = Normalize(operation);
            return Binary(op) != null || Postfix(op) != null || op == "power" || op == "yroot" || op == "fraction";
        }

        public static bool TryApply(string source, int selectionStart, int selectionLength, string operation,
            out OperatorEdit edit, bool followsFraction = false, int? completedOperandStart = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            int start = Math.Max(0, Math.Min(source.Length, selectionStart));
            int length = Math.Max(0, Math.Min(source.Length - start, selectionLength));
            string op = Normalize(operation), binary = Binary(op), postfix = Postfix(op);
            edit = null;

            if (binary != null)
            {
                if (length > 0)
                {
                    string operand = CompleteOperand(source.Substring(start, length));
                    string body = LeftOperand(operand, binary + "1") + binary;
                    bool nested = start > 0 || start + length < source.Length;
                    string replacement = nested ? "(" + body + ")" : body;
                    int slot = start + (nested ? 1 : 0);
                    edit = new OperatorEdit(Replace(source, start, length, replacement), slot + body.Length, 0,
                        slot, body.Length, nested);
                    return true;
                }
                int pending = PendingOperatorStart(source, start);
                if (pending >= 0)
                {
                    // Arithmetic keys correct an unfilled binary operator.
                    // A negative right operand uses the separate (-) key.
                    if (binary != "+" && binary != "-" && !TryLeftOperand(source, pending, false, false, out _, out _))
                        return NoChange(source, start, out edit);
                    return Insert(source, pending, start - pending, binary, out edit);
                }
                if (!TryLeftOperand(source, start, followsFraction, false, out _, out _)
                    && binary != "-" && binary != "+") return NoChange(source, start, out edit);
                return Insert(source, start, 0, binary, out edit);
            }

            if (op == "matrix")
            {
                string operand = length > 0 ? CompleteOperand(source.Substring(start, length)) : "";
                string prefix = length == 0 ? ImplicitProduct(source, start, followsFraction) : "";
                string body = operand.Length == 0 ? "(0,0,,0,0)" : "(" + operand + ",0,,0,0)";
                int slot = start + prefix.Length + (operand.Length == 0 ? 1 : operand.Length + 2);
                string tail = NeedsRightProduct(source, start + length) ? " " : "";
                edit = new OperatorEdit(Replace(source, start, length, prefix + body + tail), slot, 1, slot, 1, true);
                return true;
            }

            string function = Prefix(op);
            if (function != null)
            {
                if (length > 0)
                {
                    string operand = CompleteOperand(source.Substring(start, length));
                    string replacement = function + operand + ")";
                    // Reuse a written argument boundary when it is exactly the
                    // selected group, but do not format or rewrite its contents.
                    if (op != "(" && IsWholeGroup(operand))
                    {
                        string compact = function.Substring(0, function.Length - 1) + operand;
                        if (SameExpression(compact, replacement)) replacement = compact;
                    }
                    return Finish(source, start, length, replacement, true, out edit);
                }
                string product = ImplicitProduct(source, start, followsFraction);
                string tail = NeedsRightProduct(source, start) ? " " : "";
                int slot = start + product.Length + function.Length;
                edit = new OperatorEdit(Replace(source, start, 0, product + function + ")" + tail), slot, 0, slot, 0, true);
                return true;
            }

            if (op == "power" || op == "yroot" || op == "fraction")
            {
                bool selected = length > 0;
                int end = start + length;
                if (!selected && !TryLeftOperand(source, start, followsFraction, false, out start, out end, completedOperandStart))
                {
                    start = selectionStart < 0 ? 0 : Math.Min(source.Length, selectionStart);
                    if (op != "fraction") return NoChange(source, start, out edit);
                    string product = EndsWithValue(source, start) ? "*" : "";
                    int first = start + product.Length + 1;
                    edit = new OperatorEdit(Replace(source, start, 0, product + "()/()"), first, 0, first, 0, true);
                    return true;
                }
                length = end - start;
                string operand = CompleteOperand(source.Substring(start, length));
                string token = op == "power" ? "^" : op == "yroot" ? " yroot " : "/";
                string body = LeftOperand(operand, token + "1") + token + "()";
                int slotOffset = body.Length - 1;
                if (selected && (start > 0 || end < source.Length)) { body = "(" + body + ")"; slotOffset++; }
                else if (NeedsProtection(source, start, length, body.Replace("()", "(1)"))) { body = "(" + body + ")"; slotOffset++; }
                string tail = end < source.Length && NeedsRightProduct(source, end) ? "*" : "";
                int slot = start + slotOffset;
                edit = new OperatorEdit(Replace(source, start, length, body + tail), slot, 0, slot, 0, true);
                return true;
            }

            if (op == "negative")
            {
                if (length > 0)
                {
                    string operand = CompleteOperand(source.Substring(start, length));
                    string replacement = SameExpression("-" + operand, "-(" + operand + ")")
                        ? "-" + operand : "-(" + operand + ")";
                    return Finish(source, start, length, replacement, true, out edit);
                }
                if (EndsWithValue(source, start))
                {
                    // After a value this is a new negative factor, not the
                    // binary subtraction key and not a sign flip of the lhs.
                    string product = ImplicitProduct(source, start, followsFraction);
                    string tail = NeedsRightProduct(source, start) ? " " : "";
                    int slot = start + product.Length + 2;
                    edit = new OperatorEdit(Replace(source, start, 0, product + "(-)" + tail), slot, 0, slot, 0, true);
                    return true;
                }
                return Insert(source, start, 0, "-", out edit);
            }

            if (postfix != null)
            {
                bool selected = length > 0;
                int end = start + length;
                if (!selected && !TryLeftOperand(source, start, followsFraction, false, out start, out end, completedOperandStart))
                {
                    start = Math.Max(0, Math.Min(source.Length, selectionStart));
                    return NoChange(source, start, out edit);
                }
                length = end - start;
                string operand = CompleteOperand(source.Substring(start, length));
                string replacement = LeftOperand(operand, postfix) + postfix;
                return Finish(source, start, length, replacement, selected, out edit);
            }
            return false;
        }

        private static string Normalize(string operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            string op = operation.Trim();
            switch (op)
            {
                case "−": return "-";
                case "×": return "*";
                case "/": case "÷": return "÷";
                case "(-)": case "±": return "negative";
                case "xʸ": case "^": return "power";
                case "x²": case "^2": return "square";
                case "1/x": case "^(-1)": return "reciprocal";
                case "n!": case "!": return "factorial";
                case "n!!": case "!!": return "double-factorial";
                case "%": case "/100": return "percent";
                case "^T": case "transpose": return "transpose";
                case "eˣ": case "e^(": return "exp";
                case "10ˣ": case "10^(": return "exp10";
                case "√": return "sqrt";
                case "2x2": case "矩阵": return "matrix";
                case "a/b": return "fraction";
            }
            if (op.Length > 1 && op.EndsWith("(", StringComparison.Ordinal)) op = op.Substring(0, op.Length - 1);
            if (op == "log") return "lg";
            if (op == "E") return "Ex";
            if (op == "D") return "Dx";
            return op;
        }

        private static string Binary(string op)
        {
            switch (op)
            {
                case "+": case "-": case "*": case "÷": return op;
                case "nCr": case "nPr": case "mod": return " " + op + " ";
                default: return null;
            }
        }
        private static string Postfix(string op)
        {
            switch (op)
            {
                case "square": return "^2";
                case "reciprocal": return "^(-1)";
                case "factorial": return "!";
                case "double-factorial": return "!!";
                case "percent": return "/100";
                case "transpose": return "^T";
                default: return null;
            }
        }
        private static string Prefix(string op)
        {
            if (op == "(") return "(";
            if (op == "exp") return "e^(";
            if (op == "exp10") return "10^(";
            if (Functions.Contains(op)) return FunctionCatalog.Canonical(op) + "(";
            return null;
        }

        private static bool Insert(string source, int start, int length, string value, out OperatorEdit edit)
        {
            edit = new OperatorEdit(Replace(source, start, length, value), start + value.Length, 0, start, value.Length, false);
            return true;
        }
        private static bool NoChange(string source, int caret, out OperatorEdit edit)
        {
            edit = new OperatorEdit(source, caret, 0, caret, 0, false); return true;
        }
        private static bool Finish(string source, int start, int length, string replacement, bool selected, out OperatorEdit edit)
        {
            if (NeedsProtection(source, start, length, replacement)) replacement = "(" + replacement + ")";
            string tail = !selected && NeedsRightProduct(source, start + length) ? "*" : "";
            edit = new OperatorEdit(Replace(source, start, length, replacement + tail),
                selected ? start : start + replacement.Length, selected ? replacement.Length : 0, start, replacement.Length, false, true);
            return true;
        }
        private static string Replace(string source, int start, int length, string inserted) => source.Remove(start, length).Insert(start, inserted);

        private static string ImplicitProduct(string source, int caret, bool followsFraction)
        {
            if (!EndsWithValue(source, caret)) return "";
            // Exiting a visual fraction establishes a completed operand. A
            // factor outside it must not re-enter its linear denominator.
            return followsFraction ? "*" : " ";
        }

        private static bool NeedsProtection(string source, int start, int length, string replacement)
        {
            if (start == 0 && length == source.Length) return false;
            string plain = Replace(source, start, length, replacement);
            string grouped = Replace(source, start, length, "(" + replacement + ")");
            return !SameExpression(CompleteOperand(plain), CompleteOperand(grouped));
        }
        private static string LeftOperand(string operand, string following)
        {
            string grouped = "(" + operand + ")";
            return SameExpression(operand + following, grouped + following) ? operand : grouped;
        }
        private static bool SameExpression(string left, string right)
        {
            try { return SameTree(new ExpressionParser().Parse(left).Root, new ExpressionParser().Parse(right).Root); }
            catch (MathException) { return false; }
        }
        private static bool SameTree(ExpressionNode left, ExpressionNode right)
        {
            if (left.Kind != right.Kind || left.Text != right.Text || left.Rows != right.Rows
                || left.Columns != right.Columns || left.Children.Count != right.Children.Count) return false;
            for (int i = 0; i < left.Children.Count; i++) if (!SameTree(left.Children[i], right.Children[i])) return false;
            return true;
        }

        private static bool TryLeftOperand(string source, int caret, bool followsFraction, bool includeSign, out int start, out int end,
            int? completedOperandStart = null)
        {
            start = end = caret;
            // The host knows whether the cursor just completed a template or
            // postfix value. Retain that exact value across chained postfix
            // keys instead of inferring it from the linear spelling again.
            if (completedOperandStart.HasValue && completedOperandStart.Value >= 0 && completedOperandStart.Value < caret)
            { start = completedOperandStart.Value; return true; }
            int rowStart = CurrentRowStart(source, caret);
            string row = source.Substring(rowStart, caret - rowStart).TrimEnd();
            if (row.Length == 0) return false;
            try
            {
                var node = new ExpressionParser().ParsePreview(row).Root;
                while (true)
                {
                    if (node.Kind == ExpressionKind.Placeholder || node.Kind == ExpressionKind.DraftLiteral || node.Kind == ExpressionKind.DraftRow) return false;
                    string written = row.Substring(node.SourceStart, node.SourceLength);
                    if (IsWholeGroup(written)) break;
                    if (node.Kind == ExpressionKind.Unary && !includeSign) { node = node.Children[0]; continue; }
                    // A visual fraction can be the rhs of an ordinary ÷. Only
                    // its written '/' is a completed fraction boundary; the
                    // outer ÷ must still descend to the adjacent right value.
                    bool completedFraction = followsFraction && node.Text == "/"
                        && node.SourceIndex >= 0 && node.SourceIndex < row.Length && row[node.SourceIndex] == '/';
                    if (node.Kind == ExpressionKind.Binary && node.Text != "^" && node.Text != "yroot" && !completedFraction)
                    { node = node.Children[1]; continue; }
                    break;
                }
                if (ContainsMissing(node)) return false;
                start = rowStart + node.SourceStart;
                end = rowStart + node.SourceStart + node.SourceLength;
                return end == rowStart + row.Length && end > start;
            }
            catch (MathException) { return false; }
        }
        private static bool ContainsMissing(ExpressionNode node)
        {
            if (node.Kind == ExpressionKind.Placeholder || node.Kind == ExpressionKind.DraftLiteral) return true;
            foreach (var child in node.Children) if (ContainsMissing(child)) return true;
            return false;
        }

        // Ignore operators and brackets inside the calculator's written comments.
        private static string MaskComments(string text)
        {
            char[] chars = text.ToCharArray(); bool comment = false, escaped = false;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!comment && c == '[') { comment = true; escaped = false; chars[i] = ' '; continue; }
                if (!comment) continue;
                chars[i] = ' ';
                if (c == ']' && !escaped) comment = false;
                escaped = c == '\\' && !escaped;
            }
            return new string(chars);
        }
        private static int CurrentRowStart(string source, int caret)
        {
            string clean = MaskComments(source.Substring(0, caret));
            var levels = new Stack<int>(); levels.Push(0);
            for (int i = 0; i < clean.Length; i++)
            {
                if (clean[i] == '(') levels.Push(i + 1);
                else if (clean[i] == ')' && levels.Count > 1) levels.Pop();
                else if (clean[i] == ',') { levels.Pop(); levels.Push(i + 1); }
            }
            return levels.Peek();
        }
        private static bool EndsWithValue(string source, int caret) => TryLeftOperand(source, caret, false, false, out _, out _);
        private static bool NeedsRightProduct(string source, int at)
        {
            string rest = MaskComments(source.Substring(at)).TrimStart();
            return rest.Length > 0 && (char.IsLetterOrDigit(rest[0]) || rest[0] == '.' || rest[0] == '(' || rest[0] == 'π' || rest[0] == '√');
        }
        private static int PendingOperatorStart(string source, int caret)
        {
            int row = CurrentRowStart(source, caret);
            string left = MaskComments(source.Substring(row, caret - row)).TrimEnd();
            int signs = left.Length;
            while (signs > 0 && (left[signs - 1] == '-' || left[signs - 1] == '+')) signs--;
            string before = left.Substring(0, signs).TrimEnd();
            foreach (string token in new[] { "nCr", "nPr", "mod", "*", "/", "×", "÷" })
                if (before.EndsWith(token, StringComparison.Ordinal)) return row + before.Length - token.Length;
            return signs < left.Length ? row + signs : -1;
        }
        private static bool IsWholeGroup(string value)
        {
            if (value.Length < 2 || value[0] != '(' || value[value.Length - 1] != ')') return false;
            string clean = MaskComments(value); int depth = 0;
            for (int i = 0; i < clean.Length; i++)
            {
                if (clean[i] == '(') depth++;
                if (clean[i] == ')' && --depth == 0) return i == clean.Length - 1;
            }
            return false;
        }
        private static string CompleteOperand(string text)
        {
            int depth = 0; bool comment = false, escaped = false;
            foreach (char c in text)
            {
                if (comment) { if (c == ']' && !escaped) comment = false; escaped = c == '\\' && !escaped; continue; }
                if (c == '[') { comment = true; escaped = false; continue; }
                if (c == '(') depth++;
                if (c == ')' && depth > 0) depth--;
            }
            return text + (comment ? (escaped ? "\\]" : "]") : "") + new string(')', depth);
        }
    }
}
