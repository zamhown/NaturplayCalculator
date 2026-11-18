using System;

namespace NaturplayCalculator.Input
{
    public enum StructureTemplate
    {
        Fraction,
        Root,
        Power,
        Matrix,
        Parentheses
    }

    /// <summary>A source edit and the editable slot to focus after inserting it.
    /// All positions refer to Text, so the same API also works on a local formula buffer.</summary>
    public sealed class StructureTemplateEdit
    {
        public string Text { get; }
        public int SelectionStart { get; }
        public int SelectionLength { get; }
        public int SlotStart { get; }
        public int SlotLength { get; }

        internal StructureTemplateEdit(string text, int slotStart, int slotLength)
        {
            Text = text;
            SelectionStart = SlotStart = slotStart;
            SelectionLength = SlotLength = slotLength;
        }
    }

    public static class StructureTemplates
    {
        public static StructureTemplateEdit Insert(string source, int selectionStart, int selectionLength, StructureTemplate template)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            int start = Math.Max(0, Math.Min(source.Length, selectionStart));
            int length = Math.Max(0, Math.Min(source.Length - start, selectionLength));
            string selected = source.Substring(start, length);
            bool hasSelection = length > 0;
            string inserted;
            int slotStart, slotLength = 0;

            switch (template)
            {
                case StructureTemplate.Fraction:
                    inserted = "(" + selected + ")/()";
                    slotStart = hasSelection ? length + 4 : 1;
                    break;
                case StructureTemplate.Root:
                    inserted = "sqrt(" + selected + ")";
                    slotStart = 5; slotLength = length;
                    break;
                case StructureTemplate.Power:
                    inserted = "(" + selected + ")^()";
                    slotStart = hasSelection ? length + 4 : 1;
                    break;
                case StructureTemplate.Matrix:
                    // Explicit zero cells have distinct source ranges and remain
                    // replaceable selections. Adjacent empty comma cells can share
                    // one source caret position in the draft parser.
                    inserted = hasSelection ? "((" + selected + "),0,,0,0)" : "(0,0,,0,0)";
                    slotStart = hasSelection ? length + 4 : 1;
                    slotLength = 1;
                    break;
                case StructureTemplate.Parentheses:
                    inserted = "(" + selected + ")";
                    slotStart = 1; slotLength = length;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(template));
            }

            // A fraction or power is one inserted operand. Group it when it sits
            // inside existing source, so 2^9 -> 2^((9)/()) rather than (2^9)/().
            if ((template == StructureTemplate.Fraction || template == StructureTemplate.Power)
                && (HasContent(source, 0, start) || HasContent(source, start + length, source.Length)))
            {
                inserted = "(" + inserted + ")";
                slotStart++;
            }

            return new StructureTemplateEdit(source.Remove(start, length).Insert(start, inserted), start + slotStart, slotLength);
        }

        private static bool HasContent(string text, int start, int end)
        {
            for (int i = start; i < end; i++)
                if (!char.IsWhiteSpace(text[i])) return true;
            return false;
        }
    }
}
