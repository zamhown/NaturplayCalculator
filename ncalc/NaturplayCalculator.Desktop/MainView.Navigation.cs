using Microsoft.UI.Xaml;
using NaturplayCalculator.Presentation;
using Windows.System;

namespace NaturplayCalculator.Desktop;

public sealed partial class MainView
{
    private readonly CalculationReplay replay = new();

    private void NavigateFormula(VirtualKey direction)
    {
        if (!loaded || dialogOpen || nativePickerOpen || workspace.IsBusy) return;
        bool vertical = direction is VirtualKey.Up or VirtualKey.Down;
        if (shifted)
        {
            ConsumeShift();
            if (!vertical) { MoveToParentScope(direction == VirtualKey.Right); return; }
        }
        if (!vertical && workspace.Session.DraftIsSolved && resultFormulaScroll is { ScrollableWidth: > 1 } resultScroll)
        {
            resultScroll.ChangeView(Math.Clamp(resultScroll.HorizontalOffset + (direction == VirtualKey.Left ? -1 : 1)
                * Math.Max(40, resultScroll.ViewportWidth * .7), 0, resultScroll.ScrollableWidth), null, null, true);
            return;
        }
        if (vertical && (workspace.Session.DraftIsSolved || string.IsNullOrWhiteSpace(input.Text)))
        {
            RememberEditorPosition();
            if (replay.Move(workspace, direction == VirtualKey.Up ? -1 : 1))
            {
                var savedEditor = workspace.EditorState;
                SyncInput(); Refresh(); QueueSave();
                if (!RestoreEditorPosition(savedEditor)) input.Focus(FocusState.Programmatic);
            }
            return;
        }
        if (caretMap == null)
        {
            if (!vertical)
            {
                input.Select(Math.Clamp(input.SelectionStart + (direction == VirtualKey.Left ? -1 : 1), 0, input.Text.Length), 0);
                input.Focus(FocusState.Programmatic);
            }
            return;
        }
        if (workspace.Session.DraftIsSolved)
        {
            // Replay enters at the end with Left, at the start with Right.
            // This first press only opens editing; it must not also move a slot.
            SelectFormulaCaret(caretMap.FromText(direction == VirtualKey.Left ? input.Text.Length : 0, preferRootScope: true));
            return;
        }
        var current = formulaEdit == null ? caretMap.FromText(input.SelectionStart, preferRootScope: true)
            : formulaCaret ?? caretMap.FromText(CaretPosition);
        var next = vertical ? caretMap.MoveVertical(current, direction == VirtualKey.Up ? -1 : 1)
            : caretMap.Move(current, direction == VirtualKey.Left ? -1 : 1);
        // A vertical edge is a no-op, including its scope affinity.
        if (!vertical || next != current) SelectFormulaCaret(next);
    }
}
