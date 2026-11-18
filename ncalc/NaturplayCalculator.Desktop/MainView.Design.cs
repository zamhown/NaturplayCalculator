using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using NaturplayCalculator.Desktop.Controls;
using NaturplayCalculator.Model;
using NaturplayCalculator.Presentation;

namespace NaturplayCalculator.Desktop;

public sealed partial class MainView
{
    private Border numericUnderline = null!, symbolicUnderline = null!;
    private Button steps = null!, pin = null!;
    private Border functionHost = null!;
    private Grid entry = null!, navigationHost = null!;
    private Button? scopeAction;
    private EditorSymbol? parentScopeSymbol, clearScopeSymbol;
    private Grid surface = null!;
    private bool wideLayout, slimLayout;
    private Button? equalsKey;
    private readonly Dictionary<Button, double> keyTypeSizes = new();
    private TextBlock Caption(string label) => Text(label, 11, true);

    // Windows uses a 780-unit design height and a 4:3 orientation breakpoint.
    // Leave the Mac traffic lights in a native-size drag strip; scale the canvas
    // below it, with a width cap so very tall windows retain readable controls.
    private double SurfaceScale => Math.Max(.35, Math.Min(Math.Max(1, ActualHeight - 32) / 748, Math.Max(1, ActualWidth) / 320));
    private bool IsWide => ActualWidth >= ActualHeight * 4 / 3 && ActualWidth > 0;
    private bool IsSlim => ActualWidth > 0 && !IsWide && Math.Max(0, ActualHeight - 32) / 2 >= ActualWidth;

    private void BuildSurface(bool restoreFocus = true)
    {
        updating = true;
        keyTypeSizes.Clear();
        formulaEdit = null;
        wideLayout = IsWide; slimLayout = IsSlim;
        RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
        window.SystemBackdrop = acrylic ? new DesktopAcrylicBackdrop() : null;
        MacWindowIntegration.SetAppearance(theme);
        UpdateSystemPalette();
        root = new Grid { Background = acrylic ? TransparentBrush() : SystemBrush("windowBackgroundColor") };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
        root.RowDefinitions.Add(new RowDefinition());
        var titlebar = new Grid { Background = TransparentBrush() };
        root.Children.Add(titlebar);
        window.ExtendsContentIntoTitleBar = true; window.SetTitleBar(titlebar);
        surface = new Grid();
        editingToolsRow = null;
        var scaler = new Viewbox { Child = surface, Stretch = Stretch.Uniform };
        Grid.SetRow(scaler, 1); root.Children.Add(scaler);
        body = surface;
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(wideLayout ? 1 : 0, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(wideLayout ? 3 : 1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(wideLayout ? 1 : 0, GridUnitType.Star) });
        var middle = new Grid { Margin = new Thickness(wideLayout ? 10 : 0, 0, wideLayout ? 10 : 0, 0) };
        foreach (double height in wideLayout ? new[] { 1.0, 1, 13 } : new[] { 6.0, 1, 7 })
            middle.RowDefinitions.Add(new RowDefinition { Height = new GridLength(height, GridUnitType.Star) });
        if (wideLayout) editingToolsRow = middle.RowDefinitions[0];
        Grid.SetColumn(middle, 1); body.Children.Add(middle);
        var display = new Grid();
        display.RowDefinitions.Add(new RowDefinition());
        display.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        display.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
        formulas = new StackPanel { Spacing = 14, Margin = new Thickness(20, 18, 20, 14), VerticalAlignment = VerticalAlignment.Center };
        var formulaScroll = new ScrollViewer
        {
            Content = formulas, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center
        };
        formulas.SizeChanged += (_, _) => { if (workspace.Result != null) formulaScroll.ChangeView(null, formulaScroll.ScrollableHeight, null, true); };
        display.Children.Add(formulaScroll);
        // Errors and completion messages appear only when needed, inside the
        // calculation display. There is no permanent status bar.
        status = Text("", 11, true); status.TextWrapping = TextWrapping.Wrap;
        status.Margin = new Thickness(16, 0, 16, 6); status.Visibility = Visibility.Collapsed;
        var errorRow = new Grid();
        errorRow.ColumnDefinitions.Add(new ColumnDefinition());
        errorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        errorRow.Children.Add(status);
        locateErrorButton = Button("定位", LocateError, "定位算式中的错误");
        locateErrorButton.Visibility = Visibility.Collapsed; locateErrorButton.Margin = new Thickness(0, 0, 12, 0);
        Grid.SetColumn(locateErrorButton, 1); errorRow.Children.Add(locateErrorButton);
        Grid.SetRow(errorRow, 1); display.Children.Add(errorRow);
        var tools = new Grid { Margin = new Thickness(8, 0, 8, 0) };
        var modes = Row(2);
        numeric = ModeTab("数值", CalculationMode.Numeric, out numericUnderline);
        symbolic = ModeTab("符号", CalculationMode.Symbolic, out symbolicUnderline);
        modes.Children.Add(numeric); modes.Children.Add(symbolic); tools.Children.Add(modes);
        var context = Row(0); context.HorizontalAlignment = HorizontalAlignment.Right;
        angle = Button("RAD", () => { workspace.SetRadians(!workspace.Session.DraftUseRadians); Refresh(); QueueSave(); });
        angle.Padding = new Thickness(6, 6, 6, 6); context.Children.Add(angle);
        steps = Button("过程", () => { showSteps = !showSteps; UpdateModeButtons(); RenderFormulas(); }, "显示或隐藏计算过程");
        steps.Content = Caption("过程"); steps.Padding = new Thickness(7, 6, 7, 6); context.Children.Add(steps);
        context.Children.Add(Icon("\uE81C", "历史记录", () => _ = ToggleHistoryAsync()));
        context.Children.Add(Icon("\uE8C8", "复制结果  ⌘⇧C", CopyResult));
        pin = Icon("\uE718", "置顶到所有桌面与全屏空间", ToggleAlwaysOnTop);
        context.Children.Add(pin); tools.Children.Add(context);
        Grid.SetRow(tools, 2); display.Children.Add(tools);
        Grid.SetRow(display, wideLayout ? 2 : 0); middle.Children.Add(display);

        entry = new Grid { Background = SystemBrush("controlBackgroundColor") };
        entry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        entry.ColumnDefinitions.Add(new ColumnDefinition());
        entry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
        var settings = Button("", () => { }, "设置与导出");
        settings.Content = menuSymbol = new EditorSymbol(EditorSymbolKind.Menu, SystemBrush("labelColor"));
        settings.Background = TransparentBrush(); ToolTipService.SetToolTip(settings, "外观、文件与使用说明");
        settings.CornerRadius = new CornerRadius(0); settings.Padding = new Thickness(0);
        settings.HorizontalAlignment = HorizontalAlignment.Stretch; settings.VerticalAlignment = VerticalAlignment.Stretch;
        settings.Flyout = SettingsMenu(); entry.Children.Add(settings);
        input = SourceInput("写下一个算式…"); input.Text = workspace.Session.Draft;
        AutomationProperties.SetName(input, "算式输入"); previousText = input.Text;
        input.TextChanged += OnInputChanged; input.SelectionChanged += (_, _) => UpdateCaret();
        // TextBox handles pointer presses itself before placing the caret.
        // Observe handled presses as well so clicking a solved source always
        // resumes editing, including when the field already has focus.
        input.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, e) =>
        {
            if (!loaded || !workspace.Session.DraftIsSolved || !e.GetCurrentPoint(input).Properties.IsLeftButtonPressed) return;
            for (var child = e.OriginalSource as DependencyObject; child != null && child != input; child = VisualTreeHelper.GetParent(child))
                if (child is Button { Name: "DeleteButton" }) return;
            workspace.Edit(workspace.Session.Draft, WorkspaceEditContext.Preserve);
            RenderFormulas(); UpdateStatus(); QueueSave();
        }), handledEventsToo: true);
        input.GotFocus += (_, _) =>
        {
            // A queued focus event from a closing popup must not cancel the
            // formula scope that has just replaced this hidden source field.
            if (input.Visibility != Visibility.Visible) return;
            formulaEdit = null; UpdateFormulaInputVisibility(); UpdateCaret();
        };
        Grid.SetColumn(input, 1); entry.Children.Add(input);
        formulaInput = SourceInput("编辑选中的公式 · ⌘L 返回全文"); formulaInput.Visibility = Visibility.Collapsed;
        ((ExpressionTextBox)formulaInput).SuppressClearButton = true;
        AutomationProperties.SetName(formulaInput, "公式局部输入，Command L 返回完整算式");
        formulaInput.TextChanged += OnFormulaInputChanged;
        formulaInput.SelectionChanged += (_, _) =>
        {
            if (updating || formulaEdit == null) return;
            formulaEdit.MoveCaret(formulaEdit.ContentStart + formulaInput.SelectionStart);
            UpdateCaret();
        };
        Grid.SetColumn(formulaInput, 1); entry.Children.Add(formulaInput);
        scopeAction = Button("", () =>
        {
            if (formulaEdit?.IsScoped == true) MoveToParentScope();
            else Clear();
        }, "上一级作用域");
        parentScopeSymbol = new EditorSymbol(EditorSymbolKind.ParentScope, SystemBrush("secondaryLabelColor"));
        clearScopeSymbol = new EditorSymbol(EditorSymbolKind.Clear, SystemBrush("secondaryLabelColor"));
        scopeAction.Content = parentScopeSymbol;
        scopeAction.Padding = new Thickness(0); scopeAction.CornerRadius = new CornerRadius(0);
        scopeAction.HorizontalAlignment = HorizontalAlignment.Right; scopeAction.VerticalAlignment = VerticalAlignment.Stretch;
        scopeAction.Visibility = Visibility.Collapsed;
        Grid.SetColumn(scopeAction, 1); entry.Children.Add(scopeAction);
        var equal = equalsKey = KeyButton("=", 24);
        KeyLabel(equal).LayoutUpdated += (_, _) => UpdateMenuSymbolSize();
        equal.Background = SystemBrush("systemRedColor"); equal.Foreground = SystemBrush("whiteColor");
        KeyLabel(equal).Foreground = SystemBrush("whiteColor");
        Grid.SetColumn(equal, 2); entry.Children.Add(equal);
        Grid.SetRow(entry, 1); middle.Children.Add(entry);
        functionHost = new Border { Background = SystemBrush("underPageBackgroundColor") };
        keyboard = new Grid(); navigationHost = new Grid();
        if (wideLayout)
        {
            body.Children.Add(functionHost);
            Grid.SetColumn(keyboard, 2); body.Children.Add(keyboard);
            middle.Children.Add(navigationHost);
        }
        else { Grid.SetRow(keyboard, 2); middle.Children.Add(keyboard); }
        BuildKeyboard();
        UpdateSurfaceSize();
        Content = root; updating = false;
        if (loaded && restoreFocus) DispatcherQueue.TryEnqueue(() => input.Focus(FocusState.Programmatic));
    }

    private void UpdateSurfaceSize()
    {
        double height = ActualHeight > 32 ? ActualHeight - 32 : 748;
        double width = ActualWidth > 0 ? ActualWidth : 500;
        double scale = ActualHeight > 32 ? SurfaceScale : 1;
        surface.Width = width / scale; surface.Height = height / scale;
        UpdateKeyTypography();
        UpdateEditorToolSizes();
    }

    private Button ModeTab(string label, CalculationMode mode, out Border underline)
    {
        underline = new Border { CornerRadius = new CornerRadius(10), Background = SystemBrush("secondarySystemFillColor") };
        var content = new Grid { Height = 27, MinWidth = 39 };
        content.Children.Add(underline);
        var labelText = Text(label, 11); labelText.HorizontalAlignment = HorizontalAlignment.Center;
        content.Children.Add(labelText);
        var button = Button(label, () => ChangeMode(mode));
        button.Content = content; button.Padding = new Thickness(0); button.CornerRadius = new CornerRadius(10);
        return button;
    }

    private static ScrollViewer FormulaLine(UIElement content) => new()
    {
        Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        VerticalScrollMode = ScrollMode.Disabled
    };

    private TextBox SourceInput(string placeholder)
    {
        var field = new ExpressionTextBox
        {
            PlaceholderText = placeholder, FontFamily = new FontFamily("Helvetica Neue"), FontSize = 15,
            FontWeight = FontWeights.Normal, Padding = new Thickness(10, 6, 8, 6),
            CornerRadius = new CornerRadius(0), Background = TransparentBrush(),
            Foreground = SystemBrush("textColor"), BorderThickness = new Thickness(0),
            SelectionHighlightColor = SystemBrush("selectedTextBackgroundColor"), IsSpellCheckEnabled = false,
            MaxLength = 256 * 1024, VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, MinHeight = 36
        };
        foreach (var key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused",
            "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused" }) field.Resources[key] = TransparentBrush();
        field.Resources["TextControlForegroundFocused"] = SystemBrush("textColor");
        field.Resources["TextControlPlaceholderForeground"] = SystemBrush("placeholderTextColor");
        field.Resources["TextControlPlaceholderForegroundFocused"] = SystemBrush("placeholderTextColor");
        field.Resources["TextControlPlaceholderForegroundPointerOver"] = SystemBrush("placeholderTextColor");
        return field;
    }

    private void BuildKeyboard()
    {
        keyTypeSizes.Clear();
        if (equalsKey != null) keyTypeSizes[equalsKey] = 24;
        keyboard.Children.Clear(); keyboard.ColumnDefinitions.Clear(); keyboard.RowDefinitions.Clear(); navigationHost.Children.Clear();
        var clear = KeyGrid(new[,] { { "DEL", "AC" } }, 15); clear.Background = SystemBrush("systemOrangeColor");
        foreach (var key in clear.Children.OfType<Button>()) { key.Foreground = SystemBrush("whiteColor"); KeyLabel(key).Foreground = SystemBrush("whiteColor"); }
        var baseOps = KeyGrid(new[,] { { "(", ")", "(-)" } }, 19);
        var arithmetic = KeyGrid(new[,] { { "+", "−" }, { "×", "÷" } }, 22);
        var advanced = KeyGrid(new[,] { { "xʸ", "√" }, { "n!", "a/b" }, { "%", "Ans" } }, 19);
        var digits = KeyGrid(new[,] { { "7", "8", "9" }, { "4", "5", "6" }, { "1", "2", "3" }, { ",", "0", "." } }, 30, true);
        digits.Background = SystemBrush("controlBackgroundColor");
        if (slimLayout)
        {
            foreach (double h in new[] { 1.0, 2, 4, 1, 2 }) keyboard.RowDefinitions.Add(new RowDefinition { Height = new GridLength(h, GridUnitType.Star) });
            editingToolsRow = keyboard.RowDefinitions[0];
            keyboard.Children.Add(Navigation());
            var upper = KeyGrid(new[,] { { "DEL", "AC", "(", ")" }, { "+", "−", "×", "÷" } }, 21);
            upper.Background = SystemBrush("underPageBackgroundColor");
            foreach (var key in upper.Children.OfType<Button>().Where(k => KeyLabel(k).Text is "DEL" or "AC"))
            { keyTypeSizes[key] = 15; key.Background = SystemBrush("systemOrangeColor"); key.Foreground = SystemBrush("whiteColor"); KeyLabel(key).Foreground = SystemBrush("whiteColor"); }
            Grid.SetRow(upper, 1); keyboard.Children.Add(upper);
            Grid.SetRow(digits, 2); keyboard.Children.Add(digits);
            var lower = KeyGrid(new[,] { { "xʸ", "√", "a/b", "Ans", "(-)" } }, 19); Grid.SetRow(lower, 3); keyboard.Children.Add(lower);
            var compactFunctions = KeyGrid(shifted ? new[,] { { "asin", "acos", "atan", "2nd" }, { "nCr", "nPr", "Ex", "Dx" } }
                : new[,] { { "sin", "cos", "tan", "2nd" }, { "e", "π", "矩阵", "ln" } }, 16);
            compactFunctions.Background = SystemBrush("underPageBackgroundColor"); Grid.SetRow(compactFunctions, 4); keyboard.Children.Add(compactFunctions);
            UpdateKeyTypography();
            return;
        }
        var functions = new Grid();
        functions.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        functions.RowDefinitions.Add(new RowDefinition { Height = new GridLength(wideLayout ? 10 : 6, GridUnitType.Star) });
        var constants = KeyGrid(new[,] { { "e", "π", "i", wideLayout ? "ƒ" : "2nd" } }, 16);
        functions.Children.Add(constants);
        string[,] functionLabels = wideLayout
            ? new[,] { { "nCr", "nPr" }, { "sin", "asin" }, { "cos", "acos" }, { "tan", "atan" }, { "ln", "log" }, { "Ex", "Dx" }, { "sinh", "cosh" }, { "tanh", "1/x" }, { "eˣ", "10ˣ" }, { "矩阵", "x²" } }
            : shifted ? new[,] { { "sinh", "cosh" }, { "tanh", "1/x" }, { "eˣ", "10ˣ" }, { "n!!", "%" }, { "x²", "abs" }, { "矩阵", "rank" } }
            : new[,] { { "nCr", "nPr" }, { "sin", "asin" }, { "cos", "acos" }, { "tan", "atan" }, { "ln", "log" }, { "Ex", "Dx" } };
        var functionKeys = KeyGrid(functionLabels, 16); Grid.SetRow(functionKeys, 1); functions.Children.Add(functionKeys); functionHost.Child = functions;
        if (wideLayout)
        {
            navigationHost.Children.Add(Navigation());
            foreach (double h in new[] { 1.0, 1, 2, 5, 3 }) keyboard.RowDefinitions.Add(new RowDefinition { Height = new GridLength(h, GridUnitType.Star) });
            keyboard.Background = SystemBrush("underPageBackgroundColor");
            keyboard.Children.Add(clear); Grid.SetRow(baseOps, 1); keyboard.Children.Add(baseOps);
            Grid.SetRow(arithmetic, 2); keyboard.Children.Add(arithmetic);
            Grid.SetRow(digits, 3); keyboard.Children.Add(digits); Grid.SetRow(advanced, 4); keyboard.Children.Add(advanced);
        }
        else
        {
            foreach (double w in new[] { 3.0, 5, 3 }) keyboard.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });
            keyboard.Children.Add(functionHost);
            var center = new Grid { Background = SystemBrush("controlBackgroundColor") };
            center.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            center.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4, GridUnitType.Star) });
            editingToolsRow = center.RowDefinitions[0];
            center.Children.Add(Navigation()); Grid.SetRow(digits, 1); center.Children.Add(digits);
            Grid.SetColumn(center, 1); keyboard.Children.Add(center);
            var operators = new Grid { Background = SystemBrush("underPageBackgroundColor") };
            foreach (double h in new[] { 1.0, 1, 2, 3 }) operators.RowDefinitions.Add(new RowDefinition { Height = new GridLength(h, GridUnitType.Star) });
            operators.Children.Add(clear); Grid.SetRow(baseOps, 1); operators.Children.Add(baseOps);
            Grid.SetRow(arithmetic, 2); operators.Children.Add(arithmetic); Grid.SetRow(advanced, 3); operators.Children.Add(advanced);
            Grid.SetColumn(operators, 2); keyboard.Children.Add(operators);
        }
        UpdateKeyTypography();
        UpdateEditorToolSizes();
    }

    private Grid KeyGrid(string[,] labels, double size, bool digits = false)
    {
        var grid = new Grid();
        for (int i = 0; i < labels.GetLength(0); i++) grid.RowDefinitions.Add(new RowDefinition());
        for (int i = 0; i < labels.GetLength(1); i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (int r = 0; r < labels.GetLength(0); r++) for (int c = 0; c < labels.GetLength(1); c++)
        {
            var key = KeyButton(labels[r, c], size);
            if (digits) KeyLabel(key).FontWeight = FontWeights.Light;
            Grid.SetRow(key, r); Grid.SetColumn(key, c); grid.Children.Add(key);
        }
        return grid;
    }
    private static TextBlock KeyLabel(Button key) => (TextBlock)((Viewbox)key.Content).Child;
    private Button KeyButton(string label, double size)
    {
        var key = Button(label, () => Key(label == "DEL" ? "⌫" : label == "a/b" ? "fraction" : label),
            label == "DEL" ? "删除前一个字符或模板" : label == "2nd" ? "第二功能，仅作用于下一键" : label == "a/b" ? "分数模板" : label == "(-)" ? "输入负号" : label);
        key.HorizontalAlignment = HorizontalAlignment.Stretch; key.VerticalAlignment = VerticalAlignment.Stretch;
        key.Padding = new Thickness(0); key.CornerRadius = new CornerRadius(3); key.Margin = new Thickness(1);
        var labelBox = new Viewbox
        {
            Child = Text(label, size), Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Width = 40, Height = size * 1.35
        };
        key.SizeChanged += (_, _) =>
        {
            labelBox.Width = Math.Max(1, key.ActualWidth - (label == "2nd" ? 8 : 16));
            labelBox.Height = Math.Max(1, key.ActualHeight - 12);
        };
        key.Content = labelBox;
        KeyLabel(key).TextAlignment = TextAlignment.Center;
        keyTypeSizes[key] = size;
        if (label is "矩阵" or "ƒ") { key.Flyout = FunctionMenu(); AutomationProperties.SetName(key, "矩阵、统计与更多函数"); }
        if (label == "2nd" && shifted)
        { key.Background = SystemBrush("systemOrangeColor"); key.Foreground = SystemBrush("whiteColor"); KeyLabel(key).Foreground = SystemBrush("whiteColor"); }
        return key;
    }

    private void UpdateKeyTypography()
    {
        // Keep the whole key as the hit target. Inner labels only shrink to fit;
        // expanding the window adds space before it enlarges the type.
        double typeScale = Math.Min(1, 1.1 / SurfaceScale);
        foreach (var (key, size) in keyTypeSizes) KeyLabel(key).FontSize = size * typeScale;
    }

    private void MoveInputCaret(int direction)
    {
        NavigateFormula(direction < 0 ? Windows.System.VirtualKey.Left : Windows.System.VirtualKey.Right);
    }
}
