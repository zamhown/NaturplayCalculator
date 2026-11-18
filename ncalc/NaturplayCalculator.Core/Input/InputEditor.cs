using System;
using System.Collections.Generic;
using NaturplayCalculator.Expressions;

namespace NaturplayCalculator.Input
{
    public sealed class InputEdit
    {
        public string Text { get; internal set; }
        public int Caret { get; internal set; }
        public bool StartsNewCalculation { get; internal set; }
        public bool ContinuesFromAnswer { get; internal set; }
        public bool ExitsFormulaScope { get; internal set; }
    }
    public static class InputEditor
    {
        public static InputEdit Insert(string text, int caret, int selectionLength, string inserted)
        {
            if (text == null || inserted == null) throw new ArgumentNullException();
            caret = Math.Max(0, Math.Min(text.Length, caret));
            int length = Math.Max(0, Math.Min(selectionLength, text.Length - caret));
            return new InputEdit { Text = text.Remove(caret, length).Insert(caret, inserted), Caret = caret + inserted.Length };
        }

        /// <summary>Inserts a calculator literal key. Constants are complete values,
        /// whereas adjacent digit keys continue the current number. Ordinary text
        /// insertion and pasted scientific notation retain their original semantics.</summary>
        public static InputEdit InsertKey(string text, int caret, int selectionLength, string key)
        {
            if (text == null || key == null) throw new ArgumentNullException();
            bool digit = key.Length == 1 && key[0] >= '0' && key[0] <= '9';
            bool point = key == ".";
            bool constant = key == "e" || key == "π" || key == "i" || key == "Ans";
            if (!digit && !point && !constant) return Insert(text, caret, selectionLength, key);
            caret = Math.Max(0, Math.Min(text.Length, caret));
            int length = Math.Max(0, Math.Min(text.Length - caret, selectionLength));
            string remaining = text.Remove(caret, length);
            var tokens = LiteralTokens(remaining, caret, out bool inComment);
            if (inComment) return Insert(text, caret, length, key);
            LiteralToken left = null, right = null;
            foreach (var token in tokens)
            {
                if (token.Start < caret) left = token;
                if (right == null && token.End > caret) right = token;
            }
            bool joinLeft = !constant && left?.Kind == LiteralKind.Number && caret <= left.End;
            bool joinRight = !constant && right?.Kind == LiteralKind.Number && caret >= right.Start;
            if (point)
            {
                var number = joinLeft ? left : joinRight ? right : null;
                if (number != null)
                {
                    string value = remaining.Substring(number.Start, number.End - number.Start);
                    int exponent = value.IndexOfAny(new[] { 'e', 'E' });
                    if (value.IndexOf('.') >= 0 || exponent >= 0 && caret > number.Start + exponent)
                        return new InputEdit { Text = text, Caret = caret };
                }
                if (!joinLeft) key = "0.";
            }
            bool before = left != null && (left.Kind == LiteralKind.Number || left.Kind == LiteralKind.Value
                || left.Kind == LiteralKind.Close || left.Kind == LiteralKind.Postfix) && !joinLeft;
            bool after = right != null && (right.Kind == LiteralKind.Number || right.Kind == LiteralKind.Value
                || right.Kind == LiteralKind.Open || right.Kind == LiteralKind.Prefix) && !joinRight;
            // A literal beside a value is juxtaposition, not an explicit ×.
            // The parser intentionally gives this stronger binding than ÷.
            // Whitespace keeps atomic constants distinct from number tokens:
            // keypad 2, e, 3 must never become scientific notation 2e3.
            string prefix = before && (caret == 0 || !char.IsWhiteSpace(remaining[caret - 1])) ? " " : "";
            string suffix = after && (caret == remaining.Length || !char.IsWhiteSpace(remaining[caret])) ? " " : "";
            string inserted = prefix + key + suffix;
            // Keep the caret after the newly entered value, before a separator
            // added for the following value, so another digit extends this one.
            return new InputEdit
            {
                Text = remaining.Insert(caret, inserted),
                Caret = caret + prefix.Length + key.Length
            };
        }

        private enum LiteralKind { Number, Value, Prefix, Open, Close, Postfix, Operator }
        private sealed class LiteralToken
        {
            public LiteralKind Kind;
            public int Start, End;
            public LiteralToken(LiteralKind kind, int start, int end) { Kind = kind; Start = start; End = end; }
        }

        // Only lexical boundaries are needed here. Do not parse/rewrite the
        // incomplete expression just to insert a digit or an atomic constant.
        private static List<LiteralToken> LiteralTokens(string text, int caret, out bool inComment)
        {
            var tokens = new List<LiteralToken>();
            inComment = false;
            for (int i = 0; i < text.Length;)
            {
                int start = i;
                char c = text[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (c == '[')
                {
                    bool closed = false;
                    i++;
                    while (i < text.Length)
                    {
                        char next = text[i++];
                        if (next == '\\' && i < text.Length) { i++; continue; }
                        if (next == ']') { closed = true; break; }
                    }
                    if (caret > start && (caret < i || !closed && caret == i)) inComment = true;
                    continue;
                }
                if (char.IsDigit(c) || c == '.')
                {
                    bool point = false;
                    while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.' && !point))
                    { if (text[i] == '.') point = true; i++; }
                    if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
                    {
                        int exponent = i + 1;
                        if (exponent < text.Length && (text[exponent] == '+' || text[exponent] == '-')) exponent++;
                        if (exponent < text.Length && char.IsDigit(text[exponent]))
                        { i = exponent + 1; while (i < text.Length && char.IsDigit(text[i])) i++; }
                    }
                    tokens.Add(new LiteralToken(LiteralKind.Number, start, i)); continue;
                }
                string matched = null;
                LiteralKind kind = LiteralKind.Value;
                foreach (string name in new[] { "nCr", "nPr", "yroot", "mod" })
                    if (StartsWith(text, i, name)) { matched = name; kind = LiteralKind.Operator; break; }
                if (matched == null)
                    foreach (string name in new[] { "Ans", "pi" })
                        if (StartsWith(text, i, name)) { matched = name; break; }
                if (matched == null)
                    foreach (string name in FunctionCatalog.Tokens)
                        if (StartsWith(text, i, name)) { matched = name; kind = LiteralKind.Prefix; break; }
                if (matched != null) { i += matched.Length; tokens.Add(new LiteralToken(kind, start, i)); continue; }
                kind = c == '(' ? LiteralKind.Open : c == ')' ? LiteralKind.Close : c == '!' ? LiteralKind.Postfix
                    : c == '√' || c == 'ρ' ? LiteralKind.Prefix : char.IsLetter(c) || c == 'π' || c == '☺' ? LiteralKind.Value
                    : LiteralKind.Operator;
                tokens.Add(new LiteralToken(kind, start, ++i));
            }
            return tokens;
        }

        private static bool StartsWith(string text, int at, string token) => at + token.Length <= text.Length
            && string.Compare(text, at, token, 0, token.Length, StringComparison.OrdinalIgnoreCase) == 0;

        public static InputEdit ApplyTypedText(string previous, string candidate, int caret, bool solved)
        {
            if (previous == null || candidate == null) throw new ArgumentNullException();
            if (solved && candidate != previous)
            {
                if (candidate.StartsWith(previous, StringComparison.Ordinal)) candidate = candidate.Substring(previous.Length);
                bool continues = candidate.Length > 0 && "+-*/^×÷−".IndexOf(candidate[0]) >= 0;
                if (continues) candidate = "Ans" + candidate;
                return new InputEdit { Text = candidate, Caret = candidate.Length, StartsNewCalculation = true, ContinuesFromAnswer = continues };
            }
            return new InputEdit { Text = candidate, Caret = Math.Max(0, Math.Min(candidate.Length, caret)) };
        }
        public static InputEdit Delete(string text, int caret, int selectionLength = 0, bool backward = false)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            caret = Math.Max(0, Math.Min(text.Length, caret));
            int length = Math.Max(0, Math.Min(selectionLength, text.Length - caret));
            if (length == 0)
            {
                if (backward && caret > 0)
                {
                    caret--; length = 1;
                    if (char.IsLowSurrogate(text[caret]) && caret > 0 && char.IsHighSurrogate(text[caret - 1])) { caret--; length++; }
                }
                else if (!backward && caret < text.Length)
                {
                    length = char.IsHighSurrogate(text[caret]) && caret + 1 < text.Length && char.IsLowSurrogate(text[caret + 1]) ? 2 : 1;
                }
            }
            return new InputEdit { Text = text.Remove(caret, length), Caret = caret };
        }
    }
}
