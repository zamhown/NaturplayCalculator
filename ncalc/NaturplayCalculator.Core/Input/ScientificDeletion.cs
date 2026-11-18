using System;
using System.Collections.Generic;
using NaturplayCalculator.Expressions;

namespace NaturplayCalculator.Input
{
    /// <summary>Deletes calculator input units while retaining ordinary digit editing.</summary>
    public static class ScientificDeletion
    {
        public static InputEdit Delete(string source, int caret, int selectionLength = 0, bool backward = true)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            caret = Math.Max(0, Math.Min(source.Length, caret));
            int length = Math.Max(0, Math.Min(selectionLength, source.Length - caret));
            int target = length > 0 || !backward ? caret : caret - 1;
            if (!ExpressionPreview.IsMathCandidate(source)
                || IsCommentCharacter(source, Math.Min(source.Length - 1, target)))
                return InputEditor.Delete(source, caret, length, backward);
            if (length > 0)
            {
                int end = caret + length;
                foreach (var token in NamedTokens(source))
                    if (token.Start < end && token.End > caret)
                    { caret = Math.Min(caret, token.Start); end = Math.Max(end, token.End); }
                return Remove(source, caret, end - caret);
            }
            if (TryCollapseEmptyTemplate(source, caret, backward, out var collapsed)) return collapsed;
            foreach (var token in NamedTokens(source))
                if (target >= token.Start && target < token.End)
                {
                    int opening = token.End;
                    while (opening < source.Length && char.IsWhiteSpace(source[opening])) opening++;
                    if (token.Function && opening < source.Length && source[opening] == '('
                        && TryCollapseEmptyTemplate(source, opening + 1, true, out collapsed)) return collapsed;
                    return Remove(source, token.Start, token.End - token.Start);
                }
            return InputEditor.Delete(source, caret, 0, backward);
        }

        internal static bool TryCollapseEmptyTemplate(string source, int caret, bool backward, out InputEdit edit)
        {
            edit = null;
            if (!ExpressionPreview.IsMathCandidate(source)
                || IsCommentCharacter(source, Math.Min(source.Length - 1, backward ? caret - 1 : caret))) return false;
            int left = caret - 1, right = caret;
            while (left >= 0 && char.IsWhiteSpace(source[left])) left--;
            while (right < source.Length && char.IsWhiteSpace(source[right])) right++;
            // The complete empty template is also removable from its outside edge.
            if (backward && left >= 0 && source[left] == ')')
            {
                right = left; left--;
                while (left >= 0 && char.IsWhiteSpace(source[left])) left--;
            }
            if (!backward && right < source.Length && source[right] == '(')
            {
                left = right; right++;
                while (right < source.Length && char.IsWhiteSpace(source[right])) right++;
            }
            if (left < 0 || source[left] != '(' || right < source.Length && source[right] != ')') return false;
            int start = left, end = right < source.Length ? right + 1 : right;
            int preceding = left - 1;
            while (preceding >= 0 && char.IsWhiteSpace(source[preceding])) preceding--;
            foreach (var token in NamedTokens(source))
                if (token.End == preceding + 1 && token.Function)
                { start = token.Start; break; }
            if (start == left && preceding >= 0 && (source[preceding] == '√' || source[preceding] == '^' || source[preceding] == '/'))
            {
                start = preceding;
                // Both slots of a freshly inserted fraction are empty.
                if (source[preceding] == '/' && preceding >= 2 && source.Substring(preceding - 2, 2) == "()") start = preceding - 2;
            }
            else if (start == left && preceding >= 4 && source.Substring(preceding - 4, 5) == "yroot")
                start = preceding - 4;
            if (start == left)
            {
                int following = end;
                while (following < source.Length && char.IsWhiteSpace(source[following])) following++;
                // Deleting an empty numerator removes its fraction scaffold,
                // retaining a denominator that the user already entered.
                if (following < source.Length && source[following] == '/')
                {
                    end = following + 1;
                    if (end + 1 < source.Length && source[end] == '(' && source[end + 1] == ')') end += 2;
                }
            }
            edit = Remove(source, start, end - start);
            return true;
        }

        internal static int TemplateEntry(string source, int outerStart)
        {
            int start = Math.Max(0, Math.Min(source.Length, outerStart));
            int preceding = start - 1;
            while (preceding >= 0 && char.IsWhiteSpace(source[preceding])) preceding--;
            foreach (var token in NamedTokens(source))
                if (token.Function && token.End == preceding + 1) return token.Start;
            if (preceding >= 0 && (source[preceding] == '√' || source[preceding] == '^' || source[preceding] == '/')) return preceding;
            return start;
        }

        private static InputEdit Remove(string source, int start, int length) =>
            new InputEdit { Text = source.Remove(start, length), Caret = start };

        private static bool IsCommentCharacter(string source, int index)
        {
            if (index < 0 || index >= source.Length) return false;
            for (int i = 0; i <= index;)
            {
                if (source[i] != '[') { i++; continue; }
                int start = i++;
                while (i < source.Length)
                {
                    char c = source[i++];
                    if (c == '\\' && i < source.Length) i++;
                    else if (c == ']') break;
                }
                if (index >= start && index < i) return true;
            }
            return false;
        }

        private static IEnumerable<(int Start, int End, bool Function)> NamedTokens(string source)
        {
            for (int i = 0; i < source.Length;)
            {
                if (source[i] == '[')
                {
                    i++;
                    while (i < source.Length)
                    {
                        char c = source[i++];
                        if (c == '\\' && i < source.Length) i++;
                        else if (c == ']') break;
                    }
                    continue;
                }
                string match = null;
                foreach (string name in FunctionCatalog.Tokens)
                    if (Matches(source, i, name)) { match = name; break; }
                bool function = match != null;
                if (match == null)
                    foreach (string name in new[] { "yroot", "nCr", "nPr", "mod", "Ans", "pi" })
                        if (Matches(source, i, name)) { match = name; break; }
                if (match == null) { i++; continue; }
                yield return (i, i + match.Length, function);
                i += match.Length;
            }
        }

        private static bool Matches(string source, int at, string name) => at + name.Length <= source.Length
            && string.Compare(source, at, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) == 0;
    }
}
