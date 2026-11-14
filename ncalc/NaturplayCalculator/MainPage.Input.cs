using System;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Input;
using Windows.System;
using Windows.UI.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace NaturplayCalculator
{
    public sealed partial class MainPage
    {
        private enum InputSurface { Text, Formula }
        private InputSurface activeInputSurface = InputSurface.Text;
        private FormulaEditSession formulaEdit;
        private FormulaCaretMap formulaCaretMap;
        private FormulaCaretSlot formulaCaret;
        private FormulaCaretSlot formulaSelectionAnchor;
        private string formulaCaretSource;
        private bool changingCaret, composingFormula, caretsSuppressed;

        private TexCaretStop FormulaScopeExit(InputEdit edit) => edit.ExitsFormulaScope && formulaCaret != null
            ? formulaCaretMap.Move(formulaCaretMap.RowBoundary(formulaCaret, true), 1).Geometry : null;

        private void FinishInputCaret(InputEdit edit, TexCaretStop exit)
        {
            if (exit == null) setThePtr(edit.Caret);
            else SelectFormulaCaret(formulaCaretMap.FromGeometry(exit), false);
        }

        private void ResumeEditingCarets()
        { caretsSuppressed = false; inputBox.IsCaretVisible = true; }
        private void HideEditingCarets()
        { caretsSuppressed = true; formulaSelectionAnchor = null; inputBox.IsCaretVisible = false; UpdateFormulaCaret(textPtr); }
        private void ActivateTextInput(int index)
        {
            ResumeEditingCarets(); activeInputSurface = InputSurface.Text;
            formulaEdit = null; formulaSelectionAnchor = null; inputBox.SetText(lastInput);
            inputBox.IsActiveInputSurface = true; setThePtr(index);
        }
        private void BindFormulaCarets(TexExpression formula, TexRenderResult layout)
        {
            var old = formulaCaret;
            var map = new FormulaCaretMap(lastInput, formula, layout);
            formulaSelectionAnchor = formulaSelectionAnchor == null || formulaCaretSource != lastInput ? null
                : map.FromGeometry(formulaSelectionAnchor.Geometry);
            formulaCaret = activeInputSurface == InputSurface.Text || old == null ? map.FromText(textPtr)
                : formulaCaretSource == lastInput ? map.FromGeometry(old.Geometry)
                : map.AfterEdit(textPtr, formulaEdit?.ContentStart, formulaEdit?.ContentEnd);
            formulaCaretMap = map; formulaCaretSource = lastInput;
        }
        private void SelectFormulaCaret(FormulaCaretSlot slot, bool focus = true, bool extend = false)
        {
            if (slot == null) return;
            changingCaret = true;
            try
            {
                ResumeEditingCarets(); solved = false; activeInputSurface = InputSurface.Formula;
                formulaSelectionAnchor = extend ? formulaSelectionAnchor ?? formulaCaret : null;
                formulaCaret = slot;
                int start = formulaSelectionAnchor == null ? slot.TextIndex : Math.Min(formulaSelectionAnchor.TextIndex, slot.TextIndex);
                int length = formulaSelectionAnchor == null ? 0 : Math.Abs(formulaSelectionAnchor.TextIndex - slot.TextIndex);
                var region = length == 0 || formulaSelectionAnchor.Region == slot.Region ? slot.Region : null;
                formulaEdit = new FormulaEditSession(lastInput, region, start, length == 0 && slot.FollowsFraction);
                textPtr = start;
                inputBox.SetInputContext(lastInput, formulaEdit.Buffer, formulaEdit.ContentStart, textPtr, length);
                UpdateFormulaCaret(textPtr);
            }
            finally { changingCaret = false; }
            if (focus) inputBox.FocusInput(FocusState.Pointer);
        }
        private void RebindFormulaBuffer()
        {
            if (activeInputSurface != InputSurface.Formula || formulaCaret == null || composingFormula || caretsSuppressed || _text.SelectionLength > 0) return;
            var context = new FormulaEditSession(lastInput, formulaCaret.Region, formulaCaret.TextIndex, formulaCaret.FollowsFraction);
            changingCaret = true;
            try
            {
                formulaEdit = context;
                // Keep an unchanged native buffer intact, including IME/undo state.
                if (_text.Text != context.Buffer || inputBox.SourceSelectionStart != formulaCaret.TextIndex)
                    inputBox.SetInputContext(lastInput, context.Buffer, context.ContentStart, formulaCaret.TextIndex);
                else inputBox.PresentSource(lastInput, context.ContentStart);
                textPtr = formulaCaret.TextIndex;
            }
            finally { changingCaret = false; }
        }
        private bool MoveFormulaCaret(int direction, bool vertical = false, bool boundary = false, bool extend = false, bool global = false)
        {
            if (activeInputSurface != InputSurface.Formula || composingFormula) return false;
            if (inputC == null) TextChanged();
            if (formulaCaretMap == null || formulaCaret == null) return false;
            var next = global ? formulaCaretMap.Slots[direction < 0 ? 0 : formulaCaretMap.Slots.Count - 1]
                : boundary ? formulaCaretMap.RowBoundary(formulaCaret, direction > 0)
                : vertical ? formulaCaretMap.MoveVertical(formulaCaret, direction)
                : formulaCaretMap.Move(formulaCaret, direction);
            SelectFormulaCaret(next, false, extend); return true;
        }
        private void FormulaPreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (activeInputSurface != InputSurface.Formula || composingFormula) return;
            bool extend = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down) != 0;
            bool control = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0;
            switch (e.Key)
            {
                case VirtualKey.Left: e.Handled = MoveFormulaCaret(-1, boundary: control, extend: extend); break;
                case VirtualKey.Right: e.Handled = MoveFormulaCaret(1, boundary: control, extend: extend); break;
                case VirtualKey.Up: e.Handled = MoveFormulaCaret(-1, vertical: true, extend: extend); break;
                case VirtualKey.Down: e.Handled = MoveFormulaCaret(1, vertical: true, extend: extend); break;
                case VirtualKey.Home: e.Handled = MoveFormulaCaret(-1, boundary: true, extend: extend, global: control); break;
                case VirtualKey.End: e.Handled = MoveFormulaCaret(1, boundary: true, extend: extend, global: control); break;
                case VirtualKey.A:
                    if (control && formulaCaretMap != null)
                    {
                        SelectFormulaCaret(formulaCaretMap.Slots[0], false);
                        MoveFormulaCaret(1, extend: true, global: true); e.Handled = true;
                    }
                    break;
            }
        }
        private void RefreshInputPresentation()
        {
            if (formulaEdit != null && activeInputSurface == InputSurface.Formula && formulaEdit.Source == lastInput)
            {
                if (_text.Text != formulaEdit.Buffer)
                    inputBox.SetInputContext(lastInput, formulaEdit.Buffer, formulaEdit.ContentStart, formulaEdit.Caret);
                else inputBox.PresentSource(lastInput, formulaEdit.ContentStart);
            }
            else { formulaEdit = null; inputBox.SetText(lastInput); }
        }
        private void displayGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (TreeViewState || !ExpressionPreview.IsMathCandidate(lastInput)) return;
            if (inputC == null) AddInputPreview();
            SelectFormulaCaret(formulaCaret ?? formulaCaretMap?.FromText(textPtr));
            e.Handled = true;
        }
    }
}
