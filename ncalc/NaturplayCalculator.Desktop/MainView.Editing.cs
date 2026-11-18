using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using NaturplayCalculator.Desktop.Controls;
using NaturplayCalculator.Input;
using Windows.System;

namespace NaturplayCalculator.Desktop;

public sealed partial class MainView
{
    private Button? undoButton, redoButton;
    private EditorSymbol? menuSymbol;
    private Grid? editingTools;
    private RowDefinition? editingToolsRow;
    private BrandMark? aboutMark;
    private Grid? replayPad;
    private readonly List<EditorSymbol> directionSymbols = new();
    private readonly List<RepeatButton> directionButtons = new();
    private readonly List<EditorSymbol> editingSymbols = new();
    private bool nativeEditMenuInstalled;
    private sealed record EditorSnapshot(string Source, int Caret, int SelectionLength, FormulaEditSession? Formula);

    private void RememberEditorPosition()
    {
        if (!loaded || updating || input == null || input.Text != workspace.Session.Draft
            || formulaEdit != null && formulaEdit.Source != input.Text) return;
        var field = formulaEdit == null ? input : formulaInput;
        workspace.EditorState = new EditorSnapshot(input.Text, CaretPosition, field.SelectionLength, formulaEdit?.Clone());
    }

    private bool RestoreEditorPosition(object? state)
    {
        if (state is not EditorSnapshot saved || saved.Source != workspace.Session.Draft) return false;
        updating = true;
        formulaEdit = saved.Formula?.Clone();
        input.Select(saved.Caret, formulaEdit == null ? saved.SelectionLength : 0);
        if (formulaEdit != null)
        {
            formulaInput.Text = formulaEdit.Buffer;
            formulaInput.Select(Math.Clamp(saved.Caret - formulaEdit.ContentStart, 0, formulaInput.Text.Length), saved.SelectionLength);
        }
        UpdateFormulaInputVisibility();
        updating = false;
        UpdateCaret();
        (formulaEdit == null ? input : formulaInput).Focus(FocusState.Programmatic);
        RememberEditorPosition();
        return true;
    }

    private Grid Navigation()
    {
        editingSymbols.Clear();
        directionSymbols.Clear(); directionButtons.Clear();
        var row = editingTools = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        undoButton = EditingButton(EditorSymbolKind.Undo, "撤销  ⌘Z", UndoEdit);
        row.Children.Add(undoButton);
        var pad = replayPad = new Grid { Background = SystemBrush("tertiarySystemFillColor"), CornerRadius = new CornerRadius(34) };
        foreach (double width in new[] { 1.0, 1.2, 1 }) pad.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width, GridUnitType.Star) });
        foreach (double height in new[] { 1.0, .8, 1 }) pad.RowDefinitions.Add(new RowDefinition { Height = new GridLength(height, GridUnitType.Star) });
        var left = DirectionButton(EditorSymbolKind.Previous, VirtualKey.Left, "向左移动；结果后编辑原式末尾");
        Grid.SetRowSpan(left, 3); pad.Children.Add(left);
        var right = DirectionButton(EditorSymbolKind.Next, VirtualKey.Right, "向右移动；结果后编辑原式开头");
        Grid.SetColumn(right, 2); Grid.SetRowSpan(right, 3); pad.Children.Add(right);
        var up = DirectionButton(EditorSymbolKind.ChevronUp, VirtualKey.Up, "向上移动；结果后查看更早记录");
        Grid.SetColumn(up, 1); pad.Children.Add(up);
        var down = DirectionButton(EditorSymbolKind.ChevronDown, VirtualKey.Down, "向下移动；结果后查看较新记录");
        Grid.SetColumn(down, 1); Grid.SetRow(down, 2); pad.Children.Add(down);
        var about = Button("", () => _ = ShowHelpAsync(), "Naturplay 使用说明与快捷键");
        about.Padding = new Thickness(0);
        about.HorizontalAlignment = HorizontalAlignment.Stretch; about.VerticalAlignment = VerticalAlignment.Stretch;
        about.Content = aboutMark = new BrandMark { Foreground = SystemBrush("secondaryLabelColor") };
        ToolTipService.SetToolTip(about, "Naturplay · 使用说明与快捷键");
        Grid.SetColumn(about, 1); Grid.SetRow(about, 1); pad.Children.Add(about);
        Grid.SetColumn(pad, 1); row.Children.Add(pad);
        redoButton = EditingButton(EditorSymbolKind.Redo, "重做  ⇧⌘Z", RedoEdit);
        Grid.SetColumn(redoButton, 2); row.Children.Add(redoButton);
        UpdateEditorTools(); UpdateEditorToolSizes();
        return row;
    }

    private RepeatButton DirectionButton(EditorSymbolKind kind, VirtualKey direction, string help)
    {
        var button = new RepeatButton
        {
            Style = (Style)Application.Current.Resources["InstrumentDirectionStyle"], Delay = 400, Interval = 75,
            Background = TransparentBrush(), BorderBrush = TransparentBrush(), BorderThickness = new Thickness(0),
            Padding = new Thickness(0), MinWidth = 0, MinHeight = 0, CornerRadius = new CornerRadius(16),
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch
        };
        var symbol = new EditorSymbol(kind, SystemBrush("labelColor"));
        directionSymbols.Add(symbol); directionButtons.Add(button); button.Content = symbol;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, help);
        ToolTipService.SetToolTip(button, help);
        button.Click += (_, _) => NavigateFormula(direction);
        return button;
    }

    private Button EditingButton(EditorSymbolKind kind, string help, Action action)
    {
        var button = Button("", action, help);
        var glyph = new EditorSymbol(kind, SystemBrush("labelColor"));
        editingSymbols.Add(glyph); button.Content = glyph;
        button.Padding = new Thickness(0); button.Margin = new Thickness(2, 0, 2, 0);
        button.HorizontalAlignment = HorizontalAlignment.Stretch; button.VerticalAlignment = VerticalAlignment.Stretch;
        ToolTipService.SetToolTip(button, help);
        return button;
    }

    private void UpdateEditorToolSizes()
    {
        double scale = SurfaceScale;
        UpdateMenuSymbolSize();
        if (entry != null) entry.ColumnDefinitions[0].Width = new GridLength(40 / scale);
        if (scopeAction != null) scopeAction.Width = 36 / scale;
        if (parentScopeSymbol != null) parentScopeSymbol.Width = parentScopeSymbol.Height = 20 / scale;
        if (clearScopeSymbol != null) clearScopeSymbol.Width = clearScopeSymbol.Height = 20 / scale;
        if (formulaInput != null) formulaInput.Margin = new Thickness(0, 0, 36 / scale, 0);
        if (editingTools != null) { editingTools.MaxWidth = 204 / scale; editingTools.Height = 68 / scale; }
        if (editingToolsRow != null) editingToolsRow.Height = new GridLength(72 / scale);
        if (replayPad != null) { replayPad.Width = 104 / scale; replayPad.Height = 64 / scale; }
        if (aboutMark != null) aboutMark.Width = aboutMark.Height = 20 / scale;
        for (int i = 0; i < editingSymbols.Count; i++)
            editingSymbols[i].Width = editingSymbols[i].Height = 20 / scale;
        foreach (var symbol in directionSymbols) symbol.Width = symbol.Height = 20 / scale;
    }

    private void UpdateMenuSymbolSize()
    {
        if (menuSymbol == null || equalsKey == null || KeyLabel(equalsKey) is not CenteredSymbol { InkWidth: > 0 } label) return;
        var box = (Viewbox)equalsKey.Content;
        double shrink = label.ActualWidth > 0 && label.ActualHeight > 0
            ? Math.Min(1, Math.Min(box.ActualWidth / label.ActualWidth, box.ActualHeight / label.ActualHeight)) : 1;
        if (shrink <= 0) return;
        // The vector's round caps occupy 19.6 of its 20 design units.
        menuSymbol.Width = label.InkWidth * shrink * 20 / 19.6;
        menuSymbol.Height = menuSymbol.Width * .8;
    }

    private void UpdateEditorTools()
    {
        bool available = loaded && !dialogOpen && !nativePickerOpen;
        if (undoButton != null) undoButton.IsEnabled = available && workspace.CanUndo;
        if (redoButton != null) redoButton.IsEnabled = available && workspace.CanRedo;
        foreach (var direction in directionButtons) direction.IsEnabled = available;
        if (nativeEditMenuInstalled) MacEditMenu.Update(available && workspace.CanUndo, available && workspace.CanRedo);
    }

    private void UndoEdit() => RestoreEdit(false);
    private void RedoEdit() => RestoreEdit(true);
    private void RestoreEdit(bool redo)
    {
        if (!loaded || dialogOpen || nativePickerOpen) return;
        RememberEditorPosition();
        string before = workspace.Session.Draft;
        bool local = formulaEdit != null;
        int priorCaret = CaretPosition, priorLength = (local ? formulaInput : input).SelectionLength;
        int? scopeStart = formulaEdit?.ContentStart, scopeEnd = formulaEdit?.ContentEnd;
        bool rootScope = formulaEdit?.IsScoped == false;
        if (!(redo ? workspace.Redo() : workspace.Undo())) return;
        var savedEditor = workspace.EditorState;
        string after = workspace.Session.Draft;
        int suffix = 0;
        while (suffix < before.Length && suffix < after.Length && before[^(suffix + 1)] == after[^(suffix + 1)]) suffix++;
        int caret = before == after ? priorCaret : Math.Clamp(after.Length - suffix, 0, after.Length);
        int length = before == after ? priorLength : 0;
        SyncInput(); input.Select(caret, length); Refresh(); QueueSave();
        if (RestoreEditorPosition(savedEditor)) return;
        if (local && !workspace.Session.DraftIsSolved && caretMap != null)
        {
            // First retain the same visual container. At its final source index,
            // a shorter nested exponent can otherwise win AfterEdit's tie-break.
            int restoredEnd = scopeEnd!.Value + after.Length - before.Length;
            var restoredScope = caretMap.Slots.Where(slot =>
            {
                if (slot.Region == null) return false;
                FormulaEditSession.GetContentRange(after, slot.Region, out int start, out int end);
                return start == scopeStart && end == restoredEnd;
            }).OrderBy(slot => Math.Abs(slot.TextIndex - caret)).FirstOrDefault();
            // A fraction denominator and the parent row can share a source
            // index, including in the middle of the expression. Retain the
            // root buffer after undoing an edit made just after leaving it.
            SelectFormulaCaret(rootScope && priorCaret == before.Length
                ? caretMap.FromText(after.Length)
                : rootScope && priorCaret == 0 ? caretMap.FromText(0)
                : rootScope ? caretMap.FromText(caret, preferRootScope: true)
                : restoredScope ?? caretMap.AfterEdit(caret, scopeStart, scopeEnd));
            if (formulaEdit != null && length > 0)
            {
                int start = Math.Clamp(caret - formulaEdit.ContentStart, 0, formulaInput.Text.Length);
                formulaInput.Select(start, Math.Min(length, formulaInput.Text.Length - start));
            }
        }
        else input.Focus(FocusState.Programmatic);
    }

    private MenuFlyout FormulaNavigationMenu()
    {
        var menu = new MenuFlyout();
        MenuItem(menu, "上一个公式位置  ←", () => MoveInputCaret(-1));
        MenuItem(menu, "下一个公式位置  →", () => MoveInputCaret(1));
        MenuItem(menu, "编辑完整算式  ⌘L", () =>
        {
            formulaEdit = null; UpdateFormulaInputVisibility();
            input.Focus(FocusState.Programmatic); input.SelectAll();
        });
        return menu;
    }
}
