using Microsoft.UI.Xaml;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Input;
using NaturplayCalculator.Presentation;

namespace NaturplayCalculator.Desktop;

public sealed partial class MainView
{
    private bool ApplyOperator(string operation)
    {
        var field = formulaEdit == null ? input : formulaInput;
        string source = field.Text;
        int start = field.SelectionStart, length = field.SelectionLength;
        bool afterResult = formulaEdit == null && workspace.Session.DraftIsSolved && length == 0;
        bool continueFromResult = afterResult && OperatorEditor.ContinuesAnswer(operation);
        // Prefix functions begin a new expression. Infix/postfix operations
        // continue with the displayed result; an explicit selection edits it.
        if (afterResult) { source = continueFromResult ? "Ans" : ""; start = source.Length; }
        bool followsFraction = formulaEdit?.FollowsFractionAt(CaretPosition) == true;
        int? completedStart = formulaEdit?.CompletedOperandStartAt(CaretPosition);
        if (!OperatorEditor.TryApply(source, start, length, operation, out var operationEdit, followsFraction, completedStart)) return false;
        if (!afterResult && operationEdit.Text == source && operationEdit.SelectionStart == start
            && operationEdit.SelectionLength == length) return true;

        int selectionStart, slotStart;
        if (formulaEdit != null)
        {
            var edit = formulaEdit.ReplaceBuffer(operationEdit.Text, operationEdit.SelectionStart);
            selectionStart = formulaEdit.ContentStart + operationEdit.SelectionStart;
            slotStart = formulaEdit.ContentStart + operationEdit.SlotStart;
            ApplyEdit(edit, false);
        }
        else
        {
            selectionStart = operationEdit.SelectionStart; slotStart = operationEdit.SlotStart;
            ApplyEdit(InputEditor.Insert(operationEdit.Text, selectionStart, 0, ""), false,
                continueFromResult ? WorkspaceEditContext.DisplayedResult
                : afterResult ? WorkspaceEditContext.NewCalculation : WorkspaceEditContext.Preserve);
        }

        // Only templates enter a new local buffer. Ordinary arithmetic retains
        // the insertion row, so a later + cannot silently become a denominator.
        if (operationEdit.FocusSlot && caretMap != null)
            SelectFormulaCaret(caretMap.AfterEdit(selectionStart, slotStart, slotStart + operationEdit.SlotLength));
        else if (operationEdit.CompletesOperand && operationEdit.SelectionLength == 0)
        {
            if (formulaEdit == null)
            {
                formulaEdit = new FormulaEditSession(input.Text, (TexEditRegion?)null, selectionStart);
                updating = true;
                formulaInput.Text = formulaEdit.Buffer;
                UpdateFormulaInputVisibility();
                updating = false;
            }
            formulaEdit.MarkCompletedOperand(slotStart, selectionStart);
        }

        field = formulaEdit == null ? input : formulaInput;
        int localStart = Math.Clamp(selectionStart - (formulaEdit?.ContentStart ?? 0), 0, field.Text.Length);
        field.Select(localStart, Math.Min(operationEdit.SelectionLength, field.Text.Length - localStart));
        field.Focus(FocusState.Programmatic);
        RememberEditorPosition();
        return true;
    }
}
