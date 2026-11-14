using System;
using Naturplay.Tex.WinUI;
using Windows.Foundation;
using Windows.UI;
using Colors = Microsoft.UI.Colors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace NaturplayCalculator.Controls
{
    /// <summary>Native text entry with a measured, scrollable presentation and an
    /// independent insertion caret. Navigation never rewrites the text runs.</summary>
    public sealed partial class ExpressionInputBox : UserControl
    {
        private readonly RichTextBlock display = new RichTextBlock {
            TextWrapping = TextWrapping.NoWrap, TextAlignment = TextAlignment.Right,
            IsTextSelectionEnabled = false, Margin = new Thickness(2, 5, 5, 2) };
        private readonly Run run = new Run();
        private readonly Grid textSurface = new Grid { Background = new SolidColorBrush(Colors.Transparent) };
        private readonly ScrollViewer scroller = new ScrollViewer {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Disabled };
        private readonly BlinkingCaret caret = new BlinkingCaret();
        private bool updating, scrollToCaret;
        private bool scopedInput;
        private int editorOffset;
        internal bool IsUpdating => updating;
        internal int SourceSelectionStart => editorOffset + Editor.SelectionStart;
        internal event Action<int> CaretRequested;
        public bool IsActiveInputSurface { get => caret.IsBlinking; set => caret.IsBlinking = value; }
        private bool isCaretVisible = true;
        public bool IsCaretVisible { get => isCaretVisible; set { if (isCaretVisible == value) return; isCaretVisible = value; UpdateCaret(true); } }
        public bool FocusInput(FocusState state = FocusState.Programmatic) => Editor.Focus(state);
        // Retains Windows keyboard, IME, clipboard and selection handling. The
        // view above owns visual geometry instead of the native TextBox caret.
        internal TextBox Editor { get; } = new TextBox {
            Width = 0, Height = 0, MinWidth = 0, MinHeight = 0, MaxWidth = 0, MaxHeight = 0,
            PreventKeyboardDisplayOnProgrammaticFocus = true, IsSpellCheckEnabled = false, IsTextPredictionEnabled = false };
        public ExpressionInputBox()
        {
            caret.HostWindow = App.MainWindow;
            IsTabStop = false;
            var paragraph = new Paragraph(); paragraph.Inlines.Add(run); display.Blocks.Add(paragraph);
            var overlay = new Canvas { IsHitTestVisible = false }; overlay.Children.Add(caret);
            textSurface.Children.Add(display); textSurface.Children.Add(overlay); scroller.Content = textSurface;
            var root = new Grid(); root.Children.Add(scroller); root.Children.Add(Editor); Content = root;
            RegisterPropertyChangedCallback(ForegroundProperty, (s, p) => display.Foreground = Foreground);
            RegisterPropertyChangedCallback(FontFamilyProperty, (s, p) => display.FontFamily = FontFamily);
            RegisterPropertyChangedCallback(FontSizeProperty, (s, p) => display.FontSize = FontSize);
            display.Foreground = Foreground; display.FontFamily = FontFamily; display.FontSize = FontSize;
            Editor.TextChanged += (s, e) => { if (!updating) { if (!scopedInput) run.Text = Editor.Text; scrollToCaret = true; } };
            Editor.SelectionChanged += (s, e) => { if (!updating) RefreshSelection(); };
            scroller.SizeChanged += (s, e) => { textSurface.MinWidth = Math.Max(0, scroller.ViewportWidth); scrollToCaret = true; };
            display.LayoutUpdated += (s, e) => UpdateCaret(false);
            textSurface.Tapped += OnTapped;
            Loaded += (s, e) => { textSurface.MinWidth = Math.Max(0, scroller.ViewportWidth); RefreshSelection(); };
        }
        public void SetText(string text)
        {
            text = text ?? "";
            updating = true;
            try { scopedInput = false; editorOffset = 0; if (run.Text != text) run.Text = text; if (Editor.Text != text) Editor.Text = text; }
            finally { updating = false; }
            RefreshSelection();
        }
        internal void SetInputContext(string source, string buffer, int offset, int sourceCaret, int selectionLength = 0)
        {
            updating = true;
            try
            {
                scopedInput = true; editorOffset = offset; run.Text = source;
                if (Editor.Text != buffer) Editor.Text = buffer;
                int local = Math.Max(0, Math.Min(buffer.Length, sourceCaret - offset));
                int length = Math.Max(0, Math.Min(buffer.Length - local, selectionLength));
                if (Editor.SelectionStart != local || Editor.SelectionLength != length) Editor.Select(local, length);
            }
            finally { updating = false; }
            RefreshSelection();
        }
        internal void PresentSource(string source, int offset)
        {
            scopedInput = true; editorOffset = offset;
            if (run.Text != source) run.Text = source;
            RefreshSelection();
        }
        public void SetCaret(int index, bool clearSelection = true)
        {
            index = Math.Max(0, Math.Min(Editor.Text.Length, index - editorOffset));
            // Do not reset a native IME composition by reselecting an unchanged caret.
            if (clearSelection)
            {
                if (Editor.SelectionStart != index || Editor.SelectionLength != 0) Editor.Select(index, 0);
            }
            else if (Editor.SelectionStart != index) Editor.SelectionStart = index;
            RefreshSelection();
        }
        public void RefreshSelection()
        {
            display.TextHighlighters.Clear();
            if (Editor.SelectionLength > 0)
            {
                var selection = (TextHighlighter)XamlReader.Load("<TextHighlighter xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Background=\"{ThemeResource TextControlSelectionHighlightColor}\"/>");
                selection.Ranges.Add(new TextRange { StartIndex = SourceSelectionStart, Length = Editor.SelectionLength });
                display.TextHighlighters.Add(selection);
            }
            scrollToCaret = true; UpdateCaret(true);
        }
        private void UpdateCaret(bool restart)
        {
            if (!IsCaretVisible || Editor.SelectionLength > 0) { caret.Hide(); return; }
            int index = Math.Max(0, Math.Min(run.Text.Length, SourceSelectionStart));
            Rect rect;
            if (run.Text.Length == 0) rect = new Rect(Math.Max(0, display.ActualWidth), 0, 0, FontSize * 1.2);
            else
            {
                var position = run.ContentStart.GetPositionAtOffset(index, LogicalDirection.Forward);
                if (position == null) { caret.Hide(); return; }
                rect = position.GetCharacterRect(LogicalDirection.Forward);
                if (rect.Height <= 0) { scrollToCaret = true; return; }
            }
            Point origin = display.TransformToVisual(textSurface).TransformPoint(new Point(rect.X, rect.Y));
            caret.ShowAt(new Rect(origin.X, origin.Y, 0, rect.Height), restart);
            if (!scrollToCaret || scroller.ViewportWidth <= 0) return;
            scrollToCaret = false;
            double offset = scroller.HorizontalOffset, width = Math.Max(2, caret.Width);
            if (origin.X < offset + 2) offset = Math.Max(0, origin.X - 2);
            else if (origin.X + width + 2 > offset + scroller.ViewportWidth) offset = origin.X + width + 2 - scroller.ViewportWidth;
            scroller.ChangeView(Math.Min(scroller.ScrollableWidth, offset), null, null, true);
        }
        private void OnTapped(object sender, TappedRoutedEventArgs e)
        {
            var position = display.GetPositionFromPoint(e.GetPosition(display));
            int index = position == null ? run.Text.Length : position.Offset - run.ContentStart.Offset;
            if (CaretRequested != null) CaretRequested.Invoke(index); else SetCaret(index);
            FocusInput(FocusState.Pointer); e.Handled = true;
        }
    }
}
