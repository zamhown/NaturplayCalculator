using System;
using NaturplayCalculator.Expressions;

namespace NaturplayCalculator.Input
{
    /// <summary>Edits a visual math container through an ordinary native text buffer.
    /// Grouping belongs to serialization, so IME composition never receives inserted
    /// parentheses and a replacement cannot consume a neighbouring container.</summary>
    public sealed class FormulaEditSession
    {
        private string prefix, suffix, pendingClose = "";
        private bool protect;
        private readonly bool scoped;
        private bool followsFraction;
        private readonly int initialLocalCaret;
        public string Source { get; private set; }
        public string Buffer { get; private set; }
        public int ContentStart => prefix.Length;
        public int ContentEnd => ContentStart + Buffer.Length;
        public int OuterStart { get; }
        public int OuterEnd => Source.Length - suffix.Length + (suffix.StartsWith(")", StringComparison.Ordinal) ? 1 : 0);
        public int Caret { get; private set; }

        public FormulaEditSession(string source, TexExpression formula, int texOffset)
            : this(source, formula?.EditRegionAt(texOffset), formula?.InputInsertionAt(texOffset) ?? 0) { }

        public FormulaEditSession(string source, TexEditRegion region, int inputPosition, bool followsFraction = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            int start = region?.InputStart ?? 0, end = start + (region?.InputLength ?? source.Length);
            start = Math.Max(0, Math.Min(source.Length, start)); end = Math.Max(start, Math.Min(source.Length, end));
            OuterStart = start;
            scoped = region != null;
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
            this.followsFraction = followsFraction; initialLocalCaret = Caret - ContentStart;
        }

        public InputEdit ReplaceBuffer(string value, int localCaret)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            int added = value.Length - Buffer.Length, at = localCaret - added;
            if (followsFraction && added > 0 && at == initialLocalCaret && at >= 0 && at < value.Length
                && value.Remove(at, added) == Buffer && (char.IsLetterOrDigit(value[at]) || ".(√".IndexOf(value[at]) >= 0)
                && (at == 0 || "+-*/^,(".IndexOf(Buffer[at - 1]) < 0))
            {
                // At the fraction's outer stop a new factor must remain outside
                // its denominator, whose juxtaposed factors now bind together.
                value = value.Insert(at, "*"); localCaret++; followsFraction = false;
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
                    if (pendingClose.Length > 0)
                    { suffix = pendingClose + suffix; pendingClose = ""; Source = prefix + Buffer + suffix; }
                    Caret = OuterEnd;
                    return new InputEdit { Text = Source, Caret = Caret, ExitsFormulaScope = true };
                }
            }
            if (value != Buffer)
            {
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
        public InputEdit Delete(int sourceCaret, int selectionLength, bool backward)
        {
            var edit = InputEditor.Delete(Buffer, sourceCaret - ContentStart, selectionLength, backward);
            return ReplaceBuffer(edit.Text, edit.Caret);
        }
        public bool Contains(int position) => position >= ContentStart && position <= ContentEnd;

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
