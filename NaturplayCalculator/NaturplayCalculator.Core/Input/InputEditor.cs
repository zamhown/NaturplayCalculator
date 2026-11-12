using System;

namespace NaturplayCalculator.Input
{
    public sealed class InputEdit
    {
        public string Text { get; internal set; }
        public int Caret { get; internal set; }
        public bool StartsNewCalculation { get; internal set; }
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

        public static InputEdit ApplyTypedText(string previous, string candidate, int caret, bool solved)
        {
            if (previous == null || candidate == null) throw new ArgumentNullException();
            if (solved && candidate != previous)
            {
                if (candidate.StartsWith(previous, StringComparison.Ordinal)) candidate = candidate.Substring(previous.Length);
                if (candidate.Length > 0 && "+-*/^".IndexOf(candidate[0]) >= 0) candidate = "Ans" + candidate;
                return new InputEdit { Text = candidate, Caret = candidate.Length, StartsNewCalculation = true };
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
