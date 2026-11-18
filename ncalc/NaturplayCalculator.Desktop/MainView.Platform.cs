using System.Xml;
using JsonParser;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.History;
using NaturplayCalculator.Model;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace NaturplayCalculator.Desktop;

public sealed partial class MainView
{
    private bool modalOpen, nativePickerOpen;
    private bool dialogOpen
    {
        get => modalOpen;
        set { modalOpen = value; UpdateEditorTools(); }
    }
    private async Task ToggleHistoryAsync()
    {
        if (dialogOpen) return;
        dialogOpen = true;
        try
        {
            var panel = new StackPanel { Spacing = 10, Width = 320 };
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot, Title = "历史记录", CloseButtonText = "关闭", RequestedTheme = RequestedTheme,
                Content = new ScrollViewer { Content = panel, MaxHeight = 400 }
            };
            RenderHistory(panel, dialog.Hide);
            await dialog.ShowAsync();
        }
        finally { dialogOpen = false; }
    }
    private MenuFlyout SettingsMenu()
    {
        var menu = new MenuFlyout();
        var appearance = new MenuFlyoutSubItem { Text = "外观" };
        foreach (var (name, value) in new[] { ("跟随系统", ElementTheme.Default), ("浅色", ElementTheme.Light), ("深色", ElementTheme.Dark) })
        {
            var item = new ToggleMenuFlyoutItem { Text = name, IsChecked = theme == value };
            item.Click += (_, _) => AfterMenuClosed(menu, () =>
            {
                theme = value;
                dark = value == ElementTheme.Dark || value == ElementTheme.Default && MacWindowIntegration.IsSystemDark;
                RebuildSurfacePreservingInput(); SavePreferences();
            });
            appearance.Items.Add(item);
        }
        menu.Items.Add(appearance);
        var material = new ToggleMenuFlyoutItem { Text = "系统毛玻璃", IsChecked = acrylic };
        material.Click += (_, _) => AfterMenuClosed(menu, () => { acrylic = material.IsChecked; RebuildSurfacePreservingInput(); SavePreferences(); });
        menu.Items.Add(material);
        var floating = new ToggleMenuFlyoutItem { Text = "置顶 · 所有桌面", IsChecked = alwaysOnTop };
        floating.Click += (_, _) => AfterMenuClosed(menu, ToggleAlwaysOnTop);
        menu.Opening += (_, _) => floating.IsChecked = alwaysOnTop;
        menu.Items.Add(floating);
        menu.Items.Add(new MenuFlyoutSeparator());
        MenuItem(menu, "导出公式为 SVG…", () => _ = ExportAsync(false));
        MenuItem(menu, "导出会话…", () => _ = ExportAsync(true));
        MenuItem(menu, "打开会话 / JSON / XML…", () => _ = ImportAsync());
        menu.Items.Add(new MenuFlyoutSeparator());
        MenuItem(menu, "清空历史记录…", () => _ = ClearHistoryAsync());
        MenuItem(menu, "使用说明", () => _ = ShowHelpAsync());
        return menu;
    }

    private void ToggleAlwaysOnTop()
    {
        bool next = !alwaysOnTop;
        if (!MacWindowIntegration.SetAlwaysOnTop(next))
        { notification = "暂时无法更改窗口置顶状态"; UpdateStatus(); return; }
        alwaysOnTop = next; UpdateModeButtons(); SavePreferences();
    }

    private static void MenuItem(MenuFlyout menu, string label, Action action)
    {
        var item = new MenuFlyoutItem { Text = label };
        // Close the XAML popup before AppKit opens a native modal file panel.
        item.Click += (_, _) => AfterMenuClosed(menu, action);
        menu.Items.Add(item);
    }

    private static void AfterMenuClosed(MenuFlyout menu, Action action)
    {
        EventHandler<object>? closed = null;
        closed = (_, _) => { menu.Closed -= closed; menu.DispatcherQueue.TryEnqueue(() => action()); };
        menu.Closed += closed;
    }

    private MenuFlyout FunctionMenu()
    {
        var menu = new MenuFlyout();
        foreach (var (name, value) in new[]
        {
            ("分数  a/b", "fraction"), ("2 × 2 矩阵", "matrix"), ("行列式  |A|", "Abs"), ("秩  rank", "rank"),
            ("转置", "^T"), ("逆矩阵", "1/x"), ("平均数  E", "Ex"), ("方差  D", "Dx"),
            ("协方差  Cov", "Cov"), ("相关系数  ρ", "rho"), ("排列  nPr", "nPr"), ("组合  nCr", "nCr")
        }) MenuItem(menu, name, () => Key(value));
        var extra = new MenuFlyoutSubItem { Text = "更多函数与常数" };
        foreach (var (name, value) in new[]
        {
            ("虚数单位 i", "i"), ("常用对数 log", "log"), ("绝对值 abs", "abs"),
            ("双曲正弦 sinh", "sinh"), ("双曲余弦 cosh", "cosh"), ("双曲正切 tanh", "tanh"),
            ("阶乘 n!", "n!"), ("双阶乘 n!!", "n!!"), ("百分数 %", "%"), ("平方 x²", "x²"), ("倒数 1/x", "1/x"),
            ("自然指数 eˣ", "eˣ"), ("十进制指数 10ˣ", "10ˣ"), ("输入负号 (-)", "(-)")
        })
        {
            var item = new MenuFlyoutItem { Text = name };
            item.Click += (_, _) => AfterMenuClosed(menu, () => Key(value)); extra.Items.Add(item);
        }
        menu.Items.Add(extra);
        return menu;
    }

    private async Task ClearHistoryAsync()
    {
        if (workspace.Session.History.Count == 0 || dialogOpen) return;
        dialogOpen = true;
        try
        {
            var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "清空历史记录？", Content = "当前算式与 Ans 会保留。", PrimaryButtonText = "清空", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close, RequestedTheme = RequestedTheme };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            { workspace.Session.History.Clear(); QueueSave(); }
        }
        finally { dialogOpen = false; }
    }

    private async Task ShowHelpAsync()
    {
        if (dialogOpen) return;
        dialogOpen = true;
        try
        {
            var stack = new StackPanel { Spacing = 16, MaxWidth = 430 };
            foreach (var line in new[]
            {
                "Naturplay Calculator", "按你思考的方式书写数学。",
                "数值模式用于近似计算；符号模式保留精确分数、根式和字母变量。精确结果旁的“小数／精确”只切换显示，不改变 Ans 或历史。",
                "按光标位置输入。√、sin 先打开参数位置；平方、阶乘作用于左侧操作数。÷ 保持行内除法，a/b 打开上下排列的分数模板。省略乘号先组成一个因子：6÷2(1+2)=1；明确输入 × 则 6÷2×(1+2)=9。",
                "← → 沿公式移动，↑ ↓ 在分子／分母、底数／指数或矩阵同列间移动。当前输入区域使用主文字色。输入框的折箭头或 ⌥→ 退出结构；⌥← 返回结构之前。2nd 后按方向键也可跳出结构。右括号只关闭括号或函数。",
                "结果后 ← 从原式末尾编辑，→ 从开头编辑；长结果优先左右滚动，点原式或 ⌘L 可编辑。↑ ↓ 查看历史。长按方向按钮连续移动，中央羽毛打开说明。",
                "2nd 只作用于下一次按键；(-) 输入负数前缀。DEL 删除字符、完整函数名或空模板。含 Ans 的算式连续按 =，会用最新结果继续计算。错误旁的“定位”可回到出错位置。⌘Z 撤销，⇧⌘Z 重做。",
                "1/3 + 1/6  →  1/2\nsqrt(8)  →  2√2\n(a+b)^2  →  a²+2ab+b²",
                "矩阵用逗号分列、双逗号分行，例如 (1,2,,3,4)。使用矩阵菜单进行行列式、转置、逆和统计运算。",
                "↵ 计算    esc 清空 / 取消计算\n⌘L 编辑算式    ⌘⇧C 复制结果    ⌘S 导出公式",
                "历史条目保留计算模式、角度单位与原始结果。点击条目恢复时不会重新计算，也不会修改 Ans。",
                "图钉将同一个窗口置顶到所有桌面和全屏空间。浮窗模式暂时隐藏 Dock 入口并禁用最小化；再次点击图钉即可恢复。"
            })
            {
                var text = Text(line, 14); text.TextWrapping = TextWrapping.Wrap; stack.Children.Add(text);
            }
            await new ContentDialog { XamlRoot = XamlRoot, Title = "自然书写，自然计算", Content = stack, CloseButtonText = "开始计算", RequestedTheme = RequestedTheme }.ShowAsync();
        }
        finally { dialogOpen = false; }
    }

    private async Task ExportAsync(bool session)
    {
        if (dialogOpen) return;
        dialogOpen = true;
        try
        {
            string text;
            if (session) text = workspace.Snapshot();
            else
            {
                var value = workspace.Result;
                if (value == null && string.IsNullOrWhiteSpace(workspace.Session.Draft)) { notification = "输入算式后即可导出"; UpdateStatus(); return; }
                string latex = value == null ? ExpressionPreview.Create(workspace.Session.Draft, true, true).Formula.Latex
                    : new ExpressionTexFormatter().Format(value.Expression, true, true).Latex;
                var formula = NewFormula(latex, 40);
                // Resolve the export ink in Aqua so an SVG remains readable on a light page.
                var ink = MacWindowIntegration.ResolveSystemColor("textColor", ElementTheme.Light);
                text = TexSvgWriter.Write(formula.Layout!, $"#{ink.R:X2}{ink.G:X2}{ink.B:X2}", "STIX Two Math, STIX Two Text, serif");
            }
            var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary, SuggestedFileName = session ? "Naturplay 会话" : "Naturplay 公式" };
            picker.FileTypeChoices.Add(session ? "Naturplay 会话" : "SVG 矢量公式", new[] { session ? ".json" : ".svg" });
            var file = await picker.PickSaveFileAsync();
            if (file == null) return;
            await FileIO.WriteTextAsync(file, text);
            notification = "已保存 " + file.Name; UpdateStatus();
        }
        catch (Exception ex) { notification = "导出失败：" + ex.Message; UpdateStatus(); }
        finally { dialogOpen = false; MacWindowIntegration.Activate(); input.Focus(FocusState.Programmatic); }
    }

    private async Task ImportAsync()
    {
        if (dialogOpen || nativePickerOpen) return;
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeFilter.Add(".json"); picker.FileTypeFilter.Add(".xml"); picker.FileTypeFilter.Add(".txt");
            nativePickerOpen = true; UpdateEditorTools();
            Windows.Storage.StorageFile? file;
            try { file = await picker.PickSingleFileAsync(); }
            finally { nativePickerOpen = false; UpdateEditorTools(); }
            if (file == null) return;
            if ((await file.GetBasicPropertiesAsync()).Size > SessionCodec.MaxFileLength * 4L) throw new IOException("文件超过读取限制。");
            string text = await FileIO.ReadTextAsync(file);
            CalculatorSession? imported = null;
            try { imported = await Task.Run(() => SessionCodec.Read(text)); }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException || ex is InvalidCastException || ex is Newtonsoft.Json.JsonException || ex is MathException) { }
            if (imported != null)
            {
                // Persist the current session before a deliberate replacement.
                if (!await SaveAsync()) return;
                if (dialogOpen) return;
                dialogOpen = true;
                try
                {
                    var result = await new ContentDialog { XamlRoot = XamlRoot, Title = "打开此会话？", Content = "将替换当前工作区。当前会话会保留为本地备份。", PrimaryButtonText = "打开", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close }.ShowAsync();
                    if (result != ContentDialogResult.Primary) return;
                    // Keep an independent recovery copy; rotating autosaves must not replace it.
                    await File.WriteAllTextAsync(Path.Combine(dataDirectory, "before-import-" + DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json"), workspace.Snapshot());
                }
                finally { dialogOpen = false; }
                workspace.Dispose(); workspace = new Presentation.CalculatorWorkspace(imported);
                SyncInput(); Refresh(); QueueSave();
            }
            else
            {
                if (text.Length > JSON.MaxInputLength) throw new IOException("文档超过 256 KiB 限制。");
                workspace.Edit(text, Presentation.WorkspaceEditContext.NewCalculation); SyncInput(); await CalculateAsync();
            }
        }
        catch (Exception ex) { notification = "打开失败：" + ex.Message; UpdateStatus(); }
        finally { MacWindowIntegration.Activate(); input.Focus(FocusState.Programmatic); }
    }

    private void RenderDocument(ContentResult content)
    {
        var tree = new TreeView { MaxHeight = 280 };
        if (content.Json != null) tree.RootNodes.Add(JsonNode("根", content.Json));
        if (content.Xml?.DocumentElement != null) tree.RootNodes.Add(XmlNode(content.Xml.DocumentElement));
        foreach (var node in tree.RootNodes) node.IsExpanded = true;
        formulas.Children.Add(tree);
    }
    private static TreeViewNode JsonNode(string name, JsonElement element)
    {
        var node = new TreeViewNode { Content = name + (element is JsonValue ? " : " + element : element is JsonArray ? " [ ]" : " { }") };
        if (element is JsonObject obj) foreach (var item in obj.KeyValue) node.Children.Add(JsonNode(item.Key, item.Value));
        if (element is JsonArray arr) for (int i = 0; i < arr.Elements.Count; i++) node.Children.Add(JsonNode(i.ToString(), arr.Elements[i]));
        return node;
    }
    private static TreeViewNode XmlNode(XmlNode value)
    {
        var node = new TreeViewNode { Content = value.NodeType == XmlNodeType.Text ? value.Value : value.Name };
        if (value.Attributes != null) foreach (XmlAttribute attribute in value.Attributes) node.Children.Add(new TreeViewNode { Content = "@" + attribute.Name + " = " + attribute.Value });
        foreach (XmlNode child in value.ChildNodes) node.Children.Add(XmlNode(child));
        return node;
    }

    private string? previousWindowDiagnostics;
    private void WriteWindowDiagnostics()
    {
        if (!loaded) return;
        string value = MacWindowIntegration.DescribeWindowBehavior();
        if (value == previousWindowDiagnostics) return;
        try
        {
            string logs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Logs", "Naturplay Calculator");
            Directory.CreateDirectory(logs);
            File.WriteAllText(Path.Combine(logs, "window-mode.log"), value);
            previousWindowDiagnostics = value;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
    }

    private void ReadPreferences()
    {
        try
        {
            string path = Path.Combine(dataDirectory, "appearance.txt");
            if (!File.Exists(path)) return;
            var lines = File.ReadAllLines(path);
            if (lines.Length >= 2)
            {
                if (Enum.TryParse<ElementTheme>(lines[0], out var savedTheme) && Enum.IsDefined(savedTheme)) theme = savedTheme;
                acrylic = lines[1] != "opaque";
                alwaysOnTop = lines.Length > 3 && lines[3] == "floating";
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
    }
    private void SavePreferences()
    {
        try
        {
            Directory.CreateDirectory(dataDirectory);
            string path = Path.Combine(dataDirectory, "appearance.txt");
            File.WriteAllLines(path + ".tmp", new[] { theme.ToString(), acrylic ? "glass" : "opaque", "hidden", alwaysOnTop ? "floating" : "normal" });
            File.Move(path + ".tmp", path, true);
            WriteWindowDiagnostics();
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        { notification = "外观设置未保存：" + ex.Message; UpdateStatus(); }
    }
}
