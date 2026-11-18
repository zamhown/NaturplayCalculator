using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Naturplay.Tex;
using NaturplayCalculator.Desktop.Controls;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.History;
using NaturplayCalculator.Input;
using NaturplayCalculator.Model;
using NaturplayCalculator.Presentation;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace NaturplayCalculator.Desktop;

public sealed partial class MainView : UserControl
{
    private readonly Window window;
    private readonly FileSessionStore store;
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly DispatcherTimer appearanceTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private CalculatorWorkspace workspace = new();
    private Grid root = null!;
    private Grid body = null!;
    private Grid keyboard = null!;
    private StackPanel formulas = null!;
    private TextBox input = null!;
    private TextBox formulaInput = null!;
    private TextBlock status = null!;
    private Button numeric = null!, symbolic = null!, angle = null!;
    private FormulaView? inputFormula;
    private ScrollViewer? inputFormulaScroll, resultFormulaScroll;
    private Button? locateErrorButton;
    private bool inputFormulaWasEditing;
    private FormulaCaretMap? caretMap;
    private FormulaCaretSlot? formulaCaret;
    private FormulaEditSession? formulaEdit;
    private bool updating, loaded, dark, shifted, showSteps, alwaysOnTop, acrylic = true;
    private ElementTheme theme = ElementTheme.Default;
    private string? notification;
    private string previousText = "";
    private readonly string dataDirectory;

    public MainView(Window window)
    {
        this.window = window;
        dataDirectory = Environment.GetEnvironmentVariable("NATURPLAY_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Naturplay Calculator");
        store = new FileSessionStore(dataDirectory);
        ReadPreferences();
        dark = theme == ElementTheme.Dark || theme == ElementTheme.Default && MacWindowIntegration.IsSystemDark;
        BuildSurface();
        Loaded += async (_, _) =>
        {
            if (loaded) return;
            IsEnabled = false;
            workspace.Dispose();
            workspace = new CalculatorWorkspace(await store.LoadAsync());
            loaded = true;
            UpdateAdaptiveLayout();
            IsEnabled = true;
            if (store.HadReadError) notification = store.RecoveredFromBackup ? "已从备份恢复会话" : "上次会话无法读取，原文件已保留";
            SyncInput(); Refresh();
            nativeEditMenuInstalled = MacEditMenu.Install(UndoEdit, RedoEdit);
            UpdateEditorTools();
            if (alwaysOnTop && !MacWindowIntegration.SetAlwaysOnTop(true)) alwaysOnTop = false;
            UpdateModeButtons();
            input.Focus(FocusState.Programmatic);
        };
        SizeChanged += (_, _) => UpdateAdaptiveLayout();
        saveTimer.Tick += async (_, _) => { saveTimer.Stop(); await SaveAsync(); };
        PreviewKeyDown += OnKeyDown;
        appearanceTimer.Tick += (_, _) =>
        {
            WriteWindowDiagnostics();
            if (dialogOpen || appearanceFingerprint == MacWindowIntegration.AppearanceFingerprint(theme)) return;
            dark = theme == ElementTheme.Dark || theme == ElementTheme.Default && MacWindowIntegration.IsSystemDark;
            RebuildSurfacePreservingInput();
        };
        appearanceTimer.Start();
    }

    private TextBlock Text(string text, double size = 13, bool secondary = false)
    {
        // Symbols stand alone; punctuation stays on the same baseline as digits.
        TextBlock label = text is "=" or "≡" or "+" or "−" or "×" or "÷" or "±" or "√" or "(" or ")" or "‹" or "›"
            ? new CenteredSymbol() : new TextBlock();
        label.Text = text; label.FontSize = size;
        // Helvetica Neue falls back to Apple Symbols for the menu glyph. Make
        // that choice explicit so ink measurement uses the font that draws it.
        label.FontFamily = new FontFamily(text == "≡" ? "Apple Symbols" : "Helvetica Neue");
        label.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
        label.Foreground = SystemBrush(secondary ? "secondaryLabelColor" : "labelColor");
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }
    private static StackPanel Row(double spacing = 8) => new() { Orientation = Orientation.Horizontal, Spacing = spacing, VerticalAlignment = VerticalAlignment.Center };
    private Button Button(string label, Action action, string? accessible = null, bool quiet = true)
    {
        var button = new Button
        {
            Content = Text(label), Padding = new Thickness(12, 7, 12, 7), CornerRadius = new CornerRadius(5),
            Style = (Style)Application.Current.Resources["InstrumentButtonStyle"],
            Foreground = SystemBrush("labelColor"),
            Background = quiet ? TransparentBrush() : SystemBrush("secondarySystemFillColor"),
            BorderBrush = TransparentBrush(), BorderThickness = new Thickness(0), MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(button, accessible ?? label);
        button.Click += (_, _) => action();
        return button;
    }
    private Button Icon(string glyph, string name, Action action)
    {
        var b = Button("", action, name);
        b.Content = new FontIcon { Glyph = glyph, FontSize = 13, Foreground = SystemBrush("secondaryLabelColor") };
        b.Width = 34; b.Height = 34; b.Padding = new Thickness(8);
        return b;
    }

    private void Key(string label)
    {
        if (label == "2nd") { shifted = !shifted; BuildKeyboard(); return; }
        // SHIFT arms exactly one following action, including a navigation key.
        if (label is not "矩阵" and not "ƒ") ConsumeShift();
        switch (label)
        {
            case "=": _ = CalculateAsync(); return;
            case "AC": Clear(); return;
            case "矩阵": case "ƒ": return;
            case "⌫": DeleteScientific(true); return;
            default:
                if (!ApplyOperator(label)) Insert(label);
                return;
        }
    }

    private void ConsumeShift() { if (shifted) { shifted = false; BuildKeyboard(); } }
    private void DeleteScientific(bool backward)
    {
        var parent = formulaEdit == null ? null : caretMap?.ParentScope(formulaEdit, formulaCaret, !backward);
        var edit = formulaEdit?.Delete(CaretPosition, formulaInput.SelectionLength, backward)
            ?? ScientificDeletion.Delete(input.Text, input.SelectionStart, input.SelectionLength, backward);
        ApplyEdit(edit, parentScope: edit.Text == input.Text ? parent : null);
    }


    private int CaretPosition => formulaEdit == null ? input.SelectionStart : formulaEdit.ContentStart + formulaInput.SelectionStart;
    private void Insert(string value)
    {
        if (formulaEdit != null)
        {
            var parent = value == ")" ? caretMap?.ParentScope(formulaEdit, formulaCaret) : null;
            ApplyEdit(formulaEdit.InsertKey(CaretPosition, formulaInput.SelectionLength, value), parentScope: parent);
            return;
        }
        bool newCalculation = workspace.Session.DraftIsSolved && input.SelectionLength == 0;
        var edit = newCalculation ? InputEditor.InsertKey("", 0, 0, value)
            : InputEditor.InsertKey(input.Text, input.SelectionStart, input.SelectionLength, value);
        if (newCalculation)
        {
            ApplyEdit(edit, context: WorkspaceEditContext.NewCalculation);
            return;
        }
        ApplyEdit(edit);
    }

    private void ApplyEdit(InputEdit edit, bool focus = true, WorkspaceEditContext? context = null, FormulaCaretSlot? parentScope = null)
    {
        workspace.Edit(edit.Text, context ?? (edit.StartsNewCalculation ? WorkspaceEditContext.NewCalculation : WorkspaceEditContext.Preserve));
        updating = true;
        input.Text = previousText = edit.Text;
        input.Select(Math.Min(edit.Caret, edit.Text.Length), 0);
        if (formulaEdit != null && !edit.ExitsFormulaScope)
        {
            formulaInput.Text = formulaEdit.Buffer;
            formulaInput.Select(Math.Clamp(edit.Caret - formulaEdit.ContentStart, 0, formulaInput.Text.Length), 0);
            if (focus) formulaInput.Focus(FocusState.Programmatic);
        }
        else { formulaEdit = null; if (focus) input.Focus(FocusState.Programmatic); }
        updating = false;
        RenderFormulas(); UpdateStatus(); QueueSave();
        if (edit.ExitsFormulaScope && parentScope != null && caretMap != null)
            SelectFormulaCaret(caretMap.FromGeometry(parentScope.Geometry));
        RememberEditorPosition();
    }

    private void OnInputChanged(object sender, TextChangedEventArgs args)
    {
        // Uno may deliver TextChanged after the property assignment has completed.
        // Rebuilding the theme or restoring a session is not an edit.
        if (updating || !loaded || input.Text == workspace.Session.Draft) return;
        formulaEdit = null;
        var edit = InputEditor.ApplyTypedText(previousText, input.Text, input.SelectionStart, workspace.Session.DraftIsSolved);
        workspace.Edit(edit.Text, edit.ContinuesFromAnswer ? WorkspaceEditContext.DisplayedResult
            : edit.StartsNewCalculation ? WorkspaceEditContext.NewCalculation : WorkspaceEditContext.Preserve);
        updating = true;
        if (input.Text != edit.Text) { input.Text = edit.Text; input.Select(edit.Caret, 0); }
        previousText = edit.Text;
        updating = false;
        RenderFormulas(); UpdateStatus(); QueueSave();
        RememberEditorPosition();
        ConsumeShift();
    }

    private void OnFormulaInputChanged(object sender, TextChangedEventArgs args)
    {
        if (updating || formulaEdit == null || formulaInput.Text == formulaEdit.Buffer) return;
        int caret = formulaInput.SelectionStart;
        var parent = formulaInput.Text.Length == formulaEdit.Buffer.Length + 1 && caret > 0
            && formulaInput.Text[caret - 1] == ')' ? caretMap?.ParentScope(formulaEdit, formulaCaret) : null;
        ApplyEdit(formulaEdit.ReplaceBuffer(formulaInput.Text, caret), parentScope: parent);
        ConsumeShift();
    }

    private void SyncInput()
    {
        updating = true;
        formulaEdit = null;
        input.Text = previousText = workspace.Session.Draft;
        input.Select(input.Text.Length, 0);
        updating = false;
    }

    private void Clear() { workspace.Edit("", WorkspaceEditContext.NewCalculation); SyncInput(); Refresh(); input.Focus(FocusState.Programmatic); QueueSave(); }
    private void ChangeMode(CalculationMode mode)
    {
        if (workspace.Session.Mode == mode) return;
        workspace.SetMode(mode); Refresh(); QueueSave();
    }
    private void Refresh() { UpdateModeButtons(); RenderFormulas(); UpdateStatus(); RememberEditorPosition(); }
    private void UpdateModeButtons()
    {
        bool isNumeric = workspace.Session.Mode == CalculationMode.Numeric;
        numericUnderline.Opacity = isNumeric ? 1 : 0;
        symbolicUnderline.Opacity = isNumeric ? 0 : 1;
        ((TextBlock)((Grid)numeric.Content).Children[1]).Foreground = SystemBrush(isNumeric ? "labelColor" : "secondaryLabelColor");
        ((TextBlock)((Grid)symbolic.Content).Children[1]).Foreground = SystemBrush(isNumeric ? "secondaryLabelColor" : "labelColor");
        AutomationProperties.SetHelpText(numeric, isNumeric ? "已选择" : "切换为数值计算");
        AutomationProperties.SetHelpText(symbolic, isNumeric ? "切换为符号计算" : "已选择");
        angle.Content = Caption(workspace.Session.DraftUseRadians ? "RAD" : "DEG");
        steps.Background = showSteps ? SystemBrush("secondarySystemFillColor") : TransparentBrush();
        AutomationProperties.SetHelpText(steps, showSteps ? "计算过程已显示" : "计算过程已隐藏");
        pin.Background = alwaysOnTop ? SystemBrush("secondarySystemFillColor") : TransparentBrush();
        ToolTipService.SetToolTip(pin, alwaysOnTop ? "取消置顶并恢复 Dock 入口" : "置顶到所有桌面与全屏空间（暂时隐藏 Dock 入口）");
    }

    private async Task CalculateAsync()
    {
        ConsumeShift();
        formulaEdit = null;
        var task = workspace.EvaluateAsync();
        UpdateStatus();
        await task;
        Refresh(); QueueSave(); input.Focus(FocusState.Programmatic);
    }

    private void RenderFormulas()
    {
        UpdateFormulaInputVisibility();
        var previousFormula = inputFormula;
        double previousOffset = inputFormulaScroll?.HorizontalOffset ?? 0;
        bool preserveVerticalCenter = inputFormulaWasEditing && workspace.Result == null;
        formulas.Children.Clear(); inputFormula = null; caretMap = null;
        inputFormulaScroll = resultFormulaScroll = null;
        inputFormulaWasEditing = false;
        string source = workspace.Session.Draft;
        if (source.Length == 0)
        {
            var welcome = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 18, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 20) };
            welcome.Children.Add(new BrandMark { Width = 94, Height = 94, Foreground = SystemBrush("quaternaryLabelColor") });
            var name = Text("Naturplay\nCalculator", 26); name.FontWeight = Microsoft.UI.Text.FontWeights.Light; name.Foreground = SystemBrush("quaternaryLabelColor");
            welcome.Children.Add(name); formulas.Children.Add(welcome);
            return;
        }
        if (!ExpressionPreview.IsMathCandidate(source))
        {
            formulas.Children.Add(Text(workspace.Content?.Kind == "xml" ? "XML 文档" : "结构化文档", 22));
            if (workspace.Content != null) RenderDocument(workspace.Content);
            else formulas.Children.Add(Text("按回车展开 JSON / XML", 13, true));
            return;
        }
        try
        {
            var preview = ExpressionPreview.Create(source, workspace.Session.Mode == CalculationMode.Symbolic, true);
            inputFormula = NewFormula(preview.Formula.Latex, workspace.Result == null ? 32 : 26, workspace.Result != null,
                previousFormula, animate: true, preserveVerticalCenter: preserveVerticalCenter);
            inputFormulaWasEditing = workspace.Result == null;
            caretMap = new FormulaCaretMap(source, preview.Formula, inputFormula.Layout!);
            // A transparent XAML hit surface also covers the gaps inside a fraction
            // or matrix; the GPU canvas itself has no background for hit testing.
            var editable = new Border { Child = inputFormula, Background = TransparentBrush(), HorizontalAlignment = HorizontalAlignment.Left };
            editable.ContextFlyout = FormulaNavigationMenu();
            editable.PointerPressed += (_, e) =>
            {
                var p = e.GetCurrentPoint(inputFormula).Position;
                SelectFormulaCaret(caretMap!.FromGeometry(inputFormula.HitTest(p.X, p.Y)));
                e.Handled = true;
            };
            inputFormulaScroll = FormulaLine(editable);
            var inputScroll = inputFormulaScroll;
            inputScroll.Loaded += (_, _) => { inputScroll.ChangeView(previousOffset, null, null, true); EnsureFormulaCaretVisible(); };
            inputScroll.SizeChanged += (_, _) => EnsureFormulaCaretVisible();
            formulas.Children.Add(inputScroll);
            if (showSteps && workspace.Content?.Calculation is { } calculation)
            {
                var formatter = new ExpressionTexFormatter();
                string? finalLatex = workspace.Result is { } finalResult ? formatter.Format(finalResult.Expression, true, true).Latex : null;
                string previousLatex = preview.Formula.Latex;
                int displayed = 0;
                foreach (var step in calculation.Steps)
                {
                    string latex = formatter.Format(step, true, true).Latex;
                    // The shared calculation trace includes the final value.
                    // Show it once, in the larger final-result row below.
                    if (latex == finalLatex || latex == previousLatex) continue;
                    previousLatex = latex;
                    formulas.Children.Add(FormulaLine(NewFormula("= " + latex, 22, true)));
                    if (++displayed == 12) break;
                }
            }
            if (workspace.DisplayResult is { } result)
            {
                var answer = NewFormula("= " + new ExpressionTexFormatter().Format(result.Expression, true, true).Latex, 38);
                var resultRow = new Grid();
                resultRow.ColumnDefinitions.Add(new ColumnDefinition());
                resultRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                resultFormulaScroll = FormulaLine(answer); resultRow.Children.Add(resultFormulaScroll);
                if (workspace.CanToggleResultFormat)
                {
                    string help = workspace.IsDecimalResult ? "显示精确结果" : "显示小数结果";
                    var format = Button(workspace.IsDecimalResult ? "精确" : "小数", () => { workspace.ToggleResultFormat(); RenderFormulas(); }, help);
                    format.Content = Text(workspace.IsDecimalResult ? "精确" : "小数", 11, true);
                    format.Padding = new Thickness(10, 10, 0, 10);
                    ToolTipService.SetToolTip(format, help + " · 不重新计算");
                    Grid.SetColumn(format, 1); resultRow.Children.Add(format);
                }
                formulas.Children.Add(resultRow);
                if (result.Conditions.Count > 0) formulas.Children.Add(Text("条件 · " + string.Join("；", result.DisplayConditions), 12, true));
                if (result.UsesFixedRadians) formulas.Children.Add(Text("结果中的三角函数保留原角度含义", 12, true));
            }
            UpdateCaret();
        }
        catch (Exception ex) when (ex is TexParseException || ex is ArgumentException || ex is InvalidOperationException)
        {
            formulas.Children.Add(Text("继续输入以完成算式", 15, true));
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private FormulaView NewFormula(string latex, double size, bool secondary = false,
        FormulaView? transitionFrom = null, bool animate = false, bool preserveVerticalCenter = false)
    {
        var view = new FormulaView();
        view.SetFormula(latex, size, SystemColor(secondary ? "secondaryLabelColor" : "labelColor"),
            SystemColor("controlAccentColor"), SystemColor("tertiarySystemFillColor"), transitionFrom, animate, preserveVerticalCenter);
        return view;
    }

    private void SelectFormulaCaret(FormulaCaretSlot slot)
    {
        formulaCaret = slot;
        // Clicking a solved formula explicitly re-enters its existing expression.
        bool resumed = workspace.Session.DraftIsSolved;
        if (resumed) workspace.Edit(workspace.Session.Draft, WorkspaceEditContext.Preserve);
        formulaEdit = new FormulaEditSession(input.Text, slot.Region, slot.TextIndex, slot.FollowsFraction, slot.FollowsStructure);
        updating = true;
        formulaInput.Text = formulaEdit.Buffer;
        UpdateFormulaInputVisibility();
        formulaInput.Select(Math.Clamp(formulaEdit.Caret - formulaEdit.ContentStart, 0, formulaInput.Text.Length), 0);
        formulaInput.Focus(FocusState.Programmatic);
        input.Select(slot.TextIndex, 0);
        updating = false;
        if (resumed) { RenderFormulas(); UpdateStatus(); QueueSave(); }
        else UpdateCaret();
        DispatcherQueue.TryEnqueue(() => { if (formulaEdit != null) formulaInput.Focus(FocusState.Programmatic); });
        RememberEditorPosition();
    }

    private void UpdateFormulaInputVisibility()
    {
        if (formulaInput == null) return;
        input.Visibility = formulaEdit == null ? Visibility.Visible : Visibility.Collapsed;
        formulaInput.Visibility = formulaEdit == null ? Visibility.Collapsed : Visibility.Visible;
        bool local = formulaEdit?.IsScoped == true;
        formulaInput.PlaceholderText = local ? "编辑选中的公式 · ⌘L 返回全文" : "写下一个算式…";
        AutomationProperties.SetName(formulaInput, local ? "公式局部输入，Command L 返回完整算式" : "算式输入");
        if (scopeAction != null)
        {
            scopeAction.Visibility = formulaEdit == null || !local && input.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            scopeAction.Content = local ? parentScopeSymbol : clearScopeSymbol;
            string label = local ? "退出当前结构" : "清空算式";
            AutomationProperties.SetName(scopeAction, label);
            ToolTipService.SetToolTip(scopeAction, local ? "退出结构  ⌥→ · 光标移到结构之后" : label);
        }
        // Local editing is indicated by the selected region in the formula and
        // a fine underline, without a permanent glyph beside the source text.
        formulaInput.BorderThickness = new Thickness(0, 0, 0, local ? 1 : 0);
        formulaInput.BorderBrush = SystemBrush("keyboardFocusIndicatorColor");
        foreach (var key in new[] { "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused" })
            formulaInput.Resources[key] = SystemBrush("keyboardFocusIndicatorColor");
    }

    private void MoveToParentScope(bool after = true)
    {
        if (formulaEdit?.IsScoped != true || caretMap == null || formulaCaret == null) return;
        var parent = caretMap.ParentScope(formulaEdit, formulaCaret, after);
        if (parent != null) SelectFormulaCaret(parent);
    }

    private void UpdateCaret()
    {
        if (updating) return;
        if (caretMap == null || inputFormula == null) { RememberEditorPosition(); return; }
        bool rootScope = formulaEdit?.IsScoped == false;
        formulaCaret = formulaEdit == null ? caretMap.FromText(input.SelectionStart)
            : rootScope ? caretMap.FromText(formulaEdit.Caret, preferRootScope: true)
            : caretMap.AfterEdit(formulaEdit.Caret, formulaEdit.ContentStart, formulaEdit.ContentEnd);
        inputFormula.ShowCaret(workspace.Session.DraftIsSolved ? null : formulaCaret?.Geometry);
        var region = formulaEdit?.IsScoped == true ? formulaCaret?.Region : null;
        inputFormula.ShowScope(region == null ? null : new TexSpan(region.TexStart, region.TexLength), SystemColor("secondaryLabelColor"));
        EnsureFormulaCaretVisible();
        RememberEditorPosition();
    }

    private void EnsureFormulaCaretVisible()
    {
        if (workspace.Session.DraftIsSolved || formulaCaret == null || inputFormulaScroll is not { ViewportWidth: > 0 } scroll) return;
        double x = formulaCaret.Geometry.X + 3, offset = scroll.HorizontalOffset, margin = Math.Min(24, scroll.ViewportWidth / 4);
        if (x < offset + margin) offset = Math.Max(0, x - margin);
        else if (x > offset + scroll.ViewportWidth - margin) offset = x - scroll.ViewportWidth + margin;
        scroll.ChangeView(Math.Clamp(offset, 0, scroll.ScrollableWidth), null, null, true);
    }

    private void LocateError()
    {
        if (workspace.ErrorStart < 0) return;
        int start = Math.Clamp(workspace.ErrorStart, 0, input.Text.Length);
        int length = Math.Clamp(workspace.ErrorLength, 0, input.Text.Length - start);
        workspace.Edit(workspace.Session.Draft, WorkspaceEditContext.Preserve);
        formulaEdit = null; input.Select(start, length); Refresh();
        input.Focus(FocusState.Programmatic); input.Select(start, length); UpdateCaret();
    }

    private void RenderHistory(StackPanel target, Action? recalled = null)
    {
        var history = target;
        history.Children.Clear();
        if (workspace.Session.History.Count == 0)
        {
            var empty = new StackPanel { Spacing = 10, Margin = new Thickness(0, 40, 0, 0) };
            empty.Children.Add(Text("每一步，都有迹可循", 12, true));
            empty.Children.Add(Text("计算后，结果将留在这里。", 11, true));
            history.Children.Add(empty); return;
        }
        foreach (var record in workspace.Session.History.AsEnumerable().Reverse())
        {
            var content = new StackPanel { Spacing = 12 };
            var label = Text(record.Input, 11, true); label.TextTrimming = TextTrimming.CharacterEllipsis; label.MaxWidth = 290; label.HorizontalAlignment = HorizontalAlignment.Left;
            label.FontFamily = new FontFamily("Helvetica Neue");
            content.Children.Add(label);
            if (record.Answer != null)
            {
                try
                {
                    var formula = NewFormula(new ExpressionTexFormatter().Format(record.Answer.Expression, true, true).Latex, 22);
                    formula.IsHitTestVisible = false;
                    content.Children.Add(new ScrollViewer { Content = formula, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Disabled, MaxHeight = 100 });
                }
                catch (TexParseException) { content.Children.Add(Text(record.Answer.DisplayText, 17)); }
            }
            else content.Children.Add(Text(record.Kind.ToUpperInvariant(), 17));
            var button = Button("", () => { workspace.Recall(record); SyncInput(); Refresh(); QueueSave(); recalled?.Invoke(); }, "恢复 " + record.Input);
            ToolTipService.SetToolTip(button, (record.Mode == CalculationMode.Symbolic ? "符号计算" : "数值计算") + " · " + (record.UseRadians ? "RAD" : "DEG") + "\n" + record.Input);
            button.Content = content; button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.HorizontalAlignment = HorizontalAlignment.Stretch; button.Padding = new Thickness(0, 20, 0, 22);
            button.CornerRadius = new CornerRadius(0);
            button.BorderThickness = new Thickness(0, 0, 0, 1); button.BorderBrush = SystemBrush("separatorColor");
            history.Children.Add(button);
        }
    }

    private void UpdateAdaptiveLayout()
    {
        if (!loaded) { UpdateSurfaceSize(); return; }
        if (wideLayout != IsWide || slimLayout != IsSlim) RebuildSurfacePreservingInput();
        else UpdateSurfaceSize();
    }

    private void RebuildSurfacePreservingInput()
    {
        int caret = input.SelectionStart, length = input.SelectionLength;
        var localEdit = formulaEdit;
        int localCaret = formulaInput.SelectionStart, localLength = formulaInput.SelectionLength;
        BuildSurface(false);
        if (localEdit != null)
        {
            updating = true;
            formulaEdit = localEdit; formulaInput.Text = localEdit.Buffer;
            formulaInput.Select(Math.Min(localCaret, formulaInput.Text.Length), Math.Min(localLength, Math.Max(0, formulaInput.Text.Length - localCaret)));
            updating = false;
        }
        Refresh();
        input.Select(Math.Min(caret, input.Text.Length), Math.Min(length, Math.Max(0, input.Text.Length - caret)));
        DispatcherQueue.TryEnqueue(() => (localEdit != null ? formulaInput : input).Focus(FocusState.Programmatic));
    }

    private void UpdateStatus()
    {
        UpdateEditorTools();
        status.Text = workspace.IsBusy ? "正在计算…  esc  取消" : workspace.Error ?? notification ?? "";
        status.Foreground = workspace.Error == null ? SystemBrush("secondaryLabelColor") : SystemBrush("systemRedColor");
        status.Visibility = string.IsNullOrEmpty(status.Text) ? Visibility.Collapsed : Visibility.Visible;
        status.MaxWidth = Math.Max(200, surface.Width * (wideLayout ? .6 : 1) - 40);
        status.TextTrimming = TextTrimming.CharacterEllipsis;
        if (locateErrorButton != null) locateErrorButton.Visibility = workspace.Error != null && workspace.ErrorStart >= 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool Pressed(VirtualKey key) => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!loaded || dialogOpen) return;
        bool command = MacWindowIntegration.IsCommandDown || Pressed(VirtualKey.Control) || Pressed(VirtualKey.LeftWindows) || Pressed(VirtualKey.RightWindows);
        bool shift = MacWindowIntegration.IsShiftDown || Pressed(VirtualKey.Shift);
        bool option = MacWindowIntegration.IsOptionDown || Pressed(VirtualKey.Menu);
        if (!command && !shift && option && e.Key is VirtualKey.Left or VirtualKey.Right)
        { e.Handled = true; MoveToParentScope(e.Key == VirtualKey.Right); ConsumeShift(); return; }
        if (!command && !shift && !option && e.Key is VirtualKey.Back or VirtualKey.Delete)
        { e.Handled = true; DeleteScientific(e.Key == VirtualKey.Back); ConsumeShift(); return; }
        if (TryHandleScientificKey(e, command, shift, option)) return;
        if (!command && !shift && e.Key is VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down)
        {
            e.Handled = true; NavigateFormula(e.Key); return;
        }
        if (formulaEdit == null && workspace.Session.DraftIsSolved
            && (e.Key is VirtualKey.Back or VirtualKey.Delete or VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down or VirtualKey.Home or VirtualKey.End
                || input.SelectionLength > 0 && (!command || e.Key is VirtualKey.V or VirtualKey.X)
                || command && e.Key == VirtualKey.L))
        {
            // Replay, deletion and explicit source selections edit the original
            // formula, retaining its historical Ans value for both input paths.
            workspace.Edit(workspace.Session.Draft, WorkspaceEditContext.Preserve);
            RenderFormulas(); UpdateStatus(); QueueSave();
        }
        if (e.Key == VirtualKey.Enter) { e.Handled = true; _ = CalculateAsync(); }
        else if (e.Key == VirtualKey.Escape) { e.Handled = true; if (workspace.IsBusy) { workspace.Cancel(); UpdateStatus(); } else Clear(); }
        else if (command && e.Key == VirtualKey.C && shift) { e.Handled = true; CopyResult(); }
        else if (command && e.Key == VirtualKey.L) { e.Handled = true; formulaEdit = null; UpdateFormulaInputVisibility(); input.Focus(FocusState.Keyboard); input.SelectAll(); }
        else if (command && e.Key == VirtualKey.S) { e.Handled = true; _ = ExportAsync(false); }
        else if (command && e.Key == VirtualKey.Z)
        {
            e.Handled = true;
            if (!nativeEditMenuInstalled) { if (shift) RedoEdit(); else UndoEdit(); }
        }
        // Uno's TextBox owns the standard AppKit copy/cut/paste/select-all path.
        // Handling those again here would paste twice after the routed event bubbles.
        else if (!command && formulaEdit != null && caretMap != null && formulaCaret != null && !shift)
        {
            FormulaCaretSlot? next = e.Key switch
            {
                VirtualKey.Left => caretMap.Move(formulaCaret, -1), VirtualKey.Right => caretMap.Move(formulaCaret, 1),
                VirtualKey.Up => caretMap.MoveVertical(formulaCaret, -1), VirtualKey.Down => caretMap.MoveVertical(formulaCaret, 1),
                VirtualKey.Home => caretMap.RowBoundary(formulaCaret, false), VirtualKey.End => caretMap.RowBoundary(formulaCaret, true), _ => null
            };
            if (next != null) { e.Handled = true; SelectFormulaCaret(next); }
        }
    }

    private void CopyResult()
    {
        if (workspace.Result == null) return;
        if (SetClipboard(workspace.Result.Code)) notification = "已复制可继续计算的表达式";
        UpdateStatus();
    }
    private bool SetClipboard(string text)
    {
        try { var data = new DataPackage(); data.SetText(text); Clipboard.SetContent(data); return true; }
        catch (Exception ex) { notification = "复制失败：" + ex.Message; return false; }
    }
    public void PrepareToClose() => workspace.Cancel();
    private void QueueSave() { notification = null; saveTimer.Stop(); saveTimer.Start(); }
    public async Task<bool> SaveAsync()
    {
        if (!loaded) return true;
        saveTimer.Stop();
        try { await store.SaveAsync(workspace.Snapshot()); return true; }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is FormatException)
        { notification = "保存失败：" + ex.Message; UpdateStatus(); return false; }
    }
}
