using System;
using System.Linq;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Model;

namespace NaturplayCalculator.Input
{
    /// <summary>Edits a visual math container through an ordinary native text buffer.
    /// Grouping belongs to serialization, so IME composition never receives inserted
    /// parentheses and a replacement cannot consume a neighbouring container.</summary>
    public sealed class FormulaEditSession
    {
        private string prefix, suffix, pendingClose = "";
        private bool protect;
        private bool scoped;
        private readonly bool closesParenthesis;
        private bool followsFraction, followsStructure;
        private int initialLocalCaret;
        private int? completedOperandStart;
        public string Source { get; private set; }
        public string Buffer { get; private set; }
        public bool IsScoped => scoped;
        public int ContentStart => prefix.Length;
        public int ContentEnd => ContentStart + Buffer.Length;
        public int OuterStart { get; private set; }
        public int OuterEnd => Source.Length - suffix.Length + (suffix.StartsWith(")", StringComparison.Ordinal) ? 1 : 0);
        public int Caret { get; private set; }
        public bool FollowsFractionAt(int sourceCaret) => followsFraction && sourceCaret == ContentStart + initialLocalCaret;
        public bool FollowsStructureAt(int sourceCaret) => followsStructure && sourceCaret == ContentStart + initialLocalCaret;
        public int? CompletedOperandStartAt(int sourceCaret) => FollowsStructureAt(sourceCaret) ? completedOperandStart : null;

        public void MoveCaret(int sourceCaret) => Caret = Math.Max(ContentStart, Math.Min(ContentEnd, sourceCaret));
        public FormulaEditSession Clone() => (FormulaEditSession)MemberwiseClone();
        public void MarkCompletedOperand(int sourceStart, int sourceEnd)
        {
            initialLocalCaret = sourceEnd - ContentStart;
            completedOperandStart = Math.Max(0, sourceStart - ContentStart);
            followsStructure = true;
            followsFraction = false;
        }

        public FormulaEditSession(string source, TexExpression formula, int texOffset)
            : this(source, formula?.EditRegionAt(texOffset), formula?.InputInsertionAt(texOffset) ?? 0) { }

        public FormulaEditSession(string source, TexEditRegion region, int inputPosition, bool followsFraction = false, bool followsStructure = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            int start = region?.InputStart ?? 0, end = start + (region?.InputLength ?? source.Length);
            start = Math.Max(0, Math.Min(source.Length, start)); end = Math.Max(start, Math.Min(source.Length, end));
            OuterStart = start;
            scoped = region != null;
            closesParenthesis = HasClosingParenthesis(source, region);
            protect = region?.RequiresGrouping ?? false;
            // Existing source grouping is retained outside the native input buffer.
            // An unfinished opening group is closed on the first actual edit.
            while (region != null && start < end && source[start] == '(')
            {
                int closing = ClosingAt(source, start, end);
                if (closing >= 0 && closing != end - 1) break;
                start++;
                if (closing < 0) pendingClose += ")"; else end--;
                protect = false;
            }
            prefix = source.Substring(0, start); suffix = source.Substring(end);
            Buffer = source.Substring(start, end - start); Source = source;
            Caret = Math.Max(start, Math.Min(end, inputPosition));
            this.followsFraction = followsFraction; this.followsStructure = followsFraction || followsStructure;
            initialLocalCaret = Caret - ContentStart;
        }

        public InputEdit ReplaceBuffer(string value, int localCaret)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            int added = value.Length - Buffer.Length, at = localCaret - added;
            if (followsStructure && added > 0 && at == initialLocalCaret && at >= 0 && at < value.Length
                && value.Remove(at, added) == Buffer && (char.IsLetterOrDigit(value[at]) || ".(√".IndexOf(value[at]) >= 0)
                && (at == 0 || "+-*/÷^,(".IndexOf(Buffer[at - 1]) < 0))
            {
                // A completed fraction or power is a value, not the final digit
                // of its denominator/exponent. New factors stay outside it.
                value = value.Insert(at, "*"); localCaret++;
            }
            // A scope's delimiters are outside the native buffer. A typed closing
            // delimiter advances over an existing delimiter or leaves that scope;
            // it must not become a second ')' inside the serialized expression.
            int inserted = localCaret - 1;
            if (scoped && inserted >= 0 && inserted < value.Length && value[inserted] == ')'
                && value.Length == Buffer.Length + 1 && value.Remove(inserted, 1) == Buffer)
            {
                if (inserted < Buffer.Length && Buffer[inserted] == ')')
                {
                    Caret = ContentStart + inserted + 1;
                    return new InputEdit { Text = Source, Caret = Caret };
                }
                if (inserted == Buffer.Length && MissingClosures(Buffer).Length == 0)
                {
                    // Fraction bars, radicals and superscripts have no closing
                    // parenthesis key. Their hidden serialization delimiter is
                    // left with a direction key, never by typing an extra ')'.
                    if (!closesParenthesis)
                        return new InputEdit { Text = Source, Caret = Caret = ContentEnd };
                    if (pendingClose.Length > 0)
                    { suffix = pendingClose + suffix; pendingClose = ""; Source = prefix + Buffer + suffix; }
                    Caret = OuterEnd;
                    return new InputEdit { Text = Source, Caret = Caret, ExitsFormulaScope = true };
                }
            }
            if (value != Buffer)
            {
                followsFraction = followsStructure = false; completedOperandStart = null;
                if (protect) { prefix += "("; suffix = ")" + suffix; protect = false; }
                if (pendingClose.Length > 0) { suffix = pendingClose + suffix; pendingClose = ""; }
                Buffer = value;
                // Complete newly opened local groups before the enclosing suffix,
                // so they cannot consume a parent's closing delimiter. These
                // generated characters never enter an in-progress native buffer.
                Source = prefix + Buffer + (scoped ? MissingClosures(Buffer) : "") + suffix;
            }
            Caret = ContentStart + Math.Max(0, Math.Min(Buffer.Length, localCaret));
            return new InputEdit { Text = Source, Caret = Caret };
        }
        public InputEdit Insert(int sourceCaret, int selectionLength, string inserted)
        {
            var edit = InputEditor.Insert(Buffer, sourceCaret - ContentStart, selectionLength, inserted);
            return ReplaceBuffer(edit.Text, edit.Caret);
        }
        public InputEdit InsertKey(int sourceCaret, int selectionLength, string key)
        {
            int local = sourceCaret - ContentStart;
            string text = Buffer;
            if (selectionLength == 0 && FollowsStructureAt(sourceCaret) && key.Length > 0
                && (char.IsLetterOrDigit(key[0]) || key == "." || key == "π"))
            {
                // Insert the separator before classifying the literal, so '.'
                // at an outside stop becomes '*0.' rather than an incomplete '*.'.
                text = text.Insert(local, "*"); local++;
            }
            var edit = InputEditor.InsertKey(text, local, selectionLength, key);
            return ReplaceBuffer(edit.Text, edit.Caret);
        }
        public InputEdit Delete(int sourceCaret, int selectionLength, bool backward)
        {
            int local = Math.Max(0, Math.Min(Buffer.Length, sourceCaret - ContentStart));
            if (scoped && selectionLength == 0)
            {
                if (Buffer.Length == 0 && ScientificDeletion.TryCollapseEmptyTemplate(Source, ContentStart, backward, out var collapsed))
                    return LeaveScope(collapsed);
                if (backward && local == 0)
                    return LeaveScope(new InputEdit { Text = Source, Caret = ScientificDeletion.TemplateEntry(Source, OuterStart) });
                if (!backward && local == Buffer.Length)
                    return LeaveScope(new InputEdit { Text = Source, Caret = OuterEnd });
            }
            var edit = ScientificDeletion.Delete(Buffer, local, selectionLength, backward);
            return ReplaceBuffer(edit.Text, edit.Caret);
        }

        private InputEdit LeaveScope(InputEdit edit)
        {
            prefix = suffix = pendingClose = "";
            protect = scoped = followsFraction = followsStructure = false;
            completedOperandStart = null;
            OuterStart = 0;
            Source = Buffer = edit.Text;
            Caret = edit.Caret;
            edit.ExitsFormulaScope = true;
            return edit;
        }
        public bool Contains(int position) => position >= ContentStart && position <= ContentEnd;

        private static bool HasClosingParenthesis(string source, TexEditRegion region)
        {
            if (region == null) return false;
            try
            {
                var owner = FindOwner(new ExpressionParser().ParsePreview(source).Root, region);
                if (owner != null)
                    return owner.Kind == ExpressionKind.Function && owner.Text != "sqrt" && owner.Text != "Abs"
                        && owner.Text != "fact" && owner.Text != "dfact" && owner.Text != "transpose";
            }
            catch (MathException) { }
            return region.InputStart < source.Length && source[region.InputStart] == '(';
        }

        private static ExpressionNode FindOwner(ExpressionNode node, TexEditRegion region)
        {
            bool structure = node.Kind == ExpressionKind.Function || node.Kind == ExpressionKind.Matrix
                || node.Kind == ExpressionKind.Binary && (node.Text == "/" || node.Text == "^" || node.Text == "yroot"
                    || node.Text == "nCr" || node.Text == "nPr");
            ExpressionNode result = structure && node.Children.Any(child => child.SourceStart == region.InputStart
                && child.SourceLength == region.InputLength) ? node : null;
            foreach (var child in node.Children)
            {
                var candidate = FindOwner(child, region);
                if (candidate != null && (result == null || candidate.SourceLength < result.SourceLength)) result = candidate;
            }
            return result;
        }

        public static void GetContentRange(string source, TexEditRegion region, out int start, out int end)
        {
            start = region?.InputStart ?? 0; end = start + (region?.InputLength ?? source.Length);
            while (region != null && start < end && source[start] == '(')
            {
                int closing = ClosingAt(source, start, end);
                if (closing >= 0 && closing != end - 1) break;
                start++; if (closing >= 0) end--;
            }
        }

        private static int ClosingAt(string text, int start, int end)
        {
            int depth = 0; bool comment = false, escaped = false;
            for (int i = start; i < end; i++)
            {
                char c = text[i];
                if (comment) { if (c == ']' && !escaped) comment = false; escaped = c == '\\' && !escaped; continue; }
                if (c == '[') { comment = true; continue; }
                if (c == '(') depth++;
                if (c == ')' && --depth == 0) return i;
            }
            return -1;
        }

        private static string MissingClosures(string text)
        {
            int depth = 0; bool comment = false, escaped = false;
            foreach (char c in text)
            {
                if (comment) { if (c == ']' && !escaped) comment = false; escaped = c == '\\' && !escaped; continue; }
                if (c == '[') { comment = true; escaped = false; continue; }
                if (c == '(') depth++;
                if (c == ')' && depth > 0) depth--;
            }
            return (comment ? (escaped ? "\\]" : "]") : "") + new string(')', depth);
        }
    }
}
