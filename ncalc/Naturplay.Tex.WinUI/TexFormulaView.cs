using System;
using System.Collections.Generic;
using Windows.System;
using Windows.Foundation;
using Windows.UI;
using Colors = Microsoft.UI.Colors;
using Windows.UI.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Shapes;

namespace Naturplay.Tex.WinUI
{
    /// <summary>Standalone native TeX view. Its only formula input is a TeX string.
    /// Editing is deliberately disabled in the calculator integration.</summary>
    public sealed partial class TexFormulaView : UserControl
    {
        private readonly Canvas surface = new Canvas { Background = new SolidColorBrush(Colors.Transparent) };
        private readonly WinUiFontMetrics metrics = new WinUiFontMetrics();
        private readonly List<FrameworkElement> overlays = new List<FrameworkElement>();
        private Brush foreground = new SolidColorBrush(Colors.Black);
        private int? sourceCaret;
        private TexCaretStop positionedCaret;
        private TexSpan? sourceSelection;
        private readonly BlinkingCaret caretView = new BlinkingCaret();
        private TexEditor editor;
        private char? pendingSurrogate;
        public TexRenderResult FormulaLayout { get; private set; }
        public string Latex { get; private set; } = "";
        public double FormulaFontSize { get; set; } = 24;
        public bool IsEditingEnabled { get; private set; }
        public Window HostWindow { get => caretView.HostWindow; set => caretView.HostWindow = value; }
        public bool IsCaretActive
        {
            get => caretView.IsBlinking;
            set => caretView.IsBlinking = value;
        }
        private bool showEmptySlots;
        public bool ShowEmptySlots
        {
            get => showEmptySlots;
            set { showEmptySlots = value; Paint(); }
        }
        public event Action<int> SourceTapped;
        public event Action<TexCaretStop> CaretTapped;
        public event Action<string> LatexEdited;
        public event Action<TexParseException> EditRejected;
        public new Brush Foreground
        {
            get => foreground;
            set { foreground = value; Paint(); }
        }
        public TexFormulaView()
        {
            Content = surface; IsTabStop = false;
            Tapped += OnTapped; KeyDown += OnEditorKeyDown; CharacterReceived += OnEditorCharacter;
        }
        public void SetFormula(string latex)
        {
            var rendered = new TexLayoutEngine(metrics).Render(latex, FormulaFontSize);
            Latex = latex; FormulaLayout = rendered; editor = null; IsEditingEnabled = false; IsTabStop = false; sourceCaret = null; positionedCaret = null;
            AutomationProperties.SetName(this, latex);
            Paint();
        }
        // The host supplies a TeX insertion boundary, including the end of input.
        // This shows a caret without enabling formula editing.
        public void ShowCaret(int? offset) { sourceCaret = offset; positionedCaret = null; PaintOverlays(); }
        public void ShowPositionedCaret(TexCaretStop caret) { sourceCaret = null; positionedCaret = caret; PaintOverlays(); }
        public void ShowSourceSelection(TexSpan? selection) { sourceSelection = selection; PaintOverlays(); }
        // Available to a future host editor; the calculator never calls this method.
        public void EnableEditing()
        {
            editor = new TexEditor(Latex, metrics, FormulaFontSize); IsEditingEnabled = true; IsTabStop = true; PaintOverlays();
        }
        public void DisableEditing() { editor = null; IsEditingEnabled = false; IsTabStop = false; pendingSurrogate = null; PaintOverlays(); }
        public void Edit(Action<TexEditor> operation)
        {
            if (!IsEditingEnabled || editor == null) throw new InvalidOperationException("Formula editing is disabled.");
            string previous = Latex;
            operation(editor); FormulaLayout = editor.Layout; Latex = editor.Latex;
            AutomationProperties.SetName(this, Latex);
            if (Latex != previous) { Paint(); LatexEdited?.Invoke(Latex); }
            else PaintOverlays();
        }
        private void OnTapped(object sender, TappedRoutedEventArgs e)
        {
            if (FormulaLayout == null) return;
            Point point = e.GetPosition(this);
            if (IsEditingEnabled)
            {
                Focus(FocusState.Pointer); editor.HitTest(point.X, point.Y, KeyPressed(VirtualKey.Shift)); PaintOverlays(); e.Handled = true;
            }
            else
            {
                if (CaretTapped != null)
                {
                    CaretTapped.Invoke(FormulaLayout.HitTest(point.X, point.Y)); e.Handled = true;
                }
                else if (SourceTapped != null)
                {
                    SourceTapped.Invoke(FormulaLayout.HitTest(point.X, point.Y).Offset);
                    // The host owns focus; do not let a row container steal it.
                    e.Handled = true;
                }
            }
        }
        private static bool KeyPressed(VirtualKey key) => (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;
        private void OnEditorKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!IsEditingEnabled) return;
            bool extend = KeyPressed(VirtualKey.Shift), control = KeyPressed(VirtualKey.Control);
            Action<TexEditor> command = null;
            switch (e.Key)
            {
                case VirtualKey.Left: command = m => m.MoveLeft(extend); break;
                case VirtualKey.Right: command = m => m.MoveRight(extend); break;
                case VirtualKey.Up: command = m => m.MoveVertical(-1, extend); break;
                case VirtualKey.Down: command = m => m.MoveVertical(1, extend); break;
                case VirtualKey.Home: command = m => m.MoveHome(extend); break;
                case VirtualKey.End: command = m => m.MoveEnd(extend); break;
                case VirtualKey.Back: command = m => m.DeleteBackward(); break;
                case VirtualKey.Delete: command = m => m.DeleteForward(); break;
                case VirtualKey.A: if (control) command = m => m.SetSelection(0, m.Latex.Length); break;
                case VirtualKey.Z: if (control) command = m => { if (extend) m.Redo(); else m.Undo(); }; break;
                case VirtualKey.Y: if (control) command = m => m.Redo(); break;
            }
            if (command != null) { TryEdit(command); e.Handled = true; }
        }
        private void OnEditorCharacter(UIElement sender, CharacterReceivedRoutedEventArgs e)
        {
            if (!IsEditingEnabled || char.IsControl(e.Character) || KeyPressed(VirtualKey.Control)) return;
            e.Handled = true;
            char character = e.Character;
            if (char.IsHighSurrogate(character)) { pendingSurrogate = character; return; }
            string value = pendingSurrogate.HasValue && char.IsLowSurrogate(character) ? new string(new[] { pendingSurrogate.Value, character }) : character.ToString();
            pendingSurrogate = null;
            if (character == '^') TryEdit(m => m.InsertSuperscript());
            else if (character == '_') TryEdit(m => m.InsertSubscript());
            else if (character == '\\') TryEdit(m => m.InsertLatex(@"\backslash "));
            else TryEdit(m => m.InsertLatex("{}%#&$".IndexOf(character) >= 0 ? "\\" + value : value));
        }
        private void TryEdit(Action<TexEditor> action)
        {
            try { Edit(action); }
            catch (TexParseException error) { EditRejected?.Invoke(error); }
        }
        private void Paint()
        {
            surface.Children.Clear(); overlays.Clear();
            if (FormulaLayout == null) return;
            Width = Math.Max(1, FormulaLayout.Width) + 3; Height = Math.Max(1, FormulaLayout.Height);
            if (ShowEmptySlots)
                foreach (var region in FormulaLayout.Regions)
                    if (region.IsEmptySlot)
                    {
                        // A live ThemeResource uses exactly the platform text
                        // selection brush, including theme and high-contrast updates.
                        var slot = SelectionRectangle();
                        slot.Width = region.Bounds.Width; slot.Height = region.Bounds.Height;
                        Canvas.SetLeft(slot, region.Bounds.X); Canvas.SetTop(slot, region.Bounds.Y);
                        surface.Children.Add(slot);
                    }
            foreach (var d in FormulaLayout.Commands)
            {
                FrameworkElement item;
                if (d.Kind == TexDrawKind.Glyph)
                {
                    var block = metrics.CreateText(d.Text, d.FontSize, d.Font); block.Foreground = foreground;
                    item = block; metrics.PositionText(block, d.OriginX, d.Baseline);
                }
                else if (d.Kind == TexDrawKind.Rule)
                {
                    item = new Rectangle { Width = d.Bounds.Width, Height = d.Bounds.Height, Fill = foreground, IsHitTestVisible = false };
                    Canvas.SetLeft(item, d.Bounds.X); Canvas.SetTop(item, d.Bounds.Y);
                }
                else
                {
                    var geometry = new PathGeometry();
                    var figure = new PathFigure { StartPoint = new Point(d.Points[0].X, d.Points[0].Y), IsClosed = false, IsFilled = false };
                    if (d.Kind == TexDrawKind.CubicCurve)
                    {
                        for (int i = 1; i + 2 < d.Points.Count; i += 3)
                            figure.Segments.Add(new BezierSegment
                            {
                                Point1 = new Point(d.Points[i].X, d.Points[i].Y), Point2 = new Point(d.Points[i + 1].X, d.Points[i + 1].Y),
                                Point3 = new Point(d.Points[i + 2].X, d.Points[i + 2].Y)
                            });
                    }
                    else for (int i = 1; i < d.Points.Count; i++) figure.Segments.Add(new LineSegment { Point = new Point(d.Points[i].X, d.Points[i].Y) });
                    geometry.Figures.Add(figure);
                    item = new Path { Data = geometry, Stroke = foreground, StrokeThickness = d.Thickness, IsHitTestVisible = false };
                }
                surface.Children.Add(item);
            }
            surface.Children.Add(caretView);
            PaintOverlays();
        }
        private static Rectangle SelectionRectangle() => (Rectangle)XamlReader.Load(
            "<Rectangle xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" IsHitTestVisible=\"False\" Fill=\"{ThemeResource TextControlSelectionHighlightColor}\"/>");
        private void PaintOverlays()
        {
            foreach (var previous in overlays) surface.Children.Remove(previous);
            overlays.Clear();
            if (FormulaLayout == null) { caretView.Hide(); return; }
            var selectionSpan = IsEditingEnabled ? editor.Selection : sourceSelection ?? new TexSpan(0, 0);
            if (selectionSpan.Length > 0)
            {
                foreach (var rect in FormulaLayout.SelectionBounds(selectionSpan))
                {
                    var selection = SelectionRectangle(); selection.Opacity = 0.4;
                    selection.Width = rect.Width; selection.Height = rect.Height;
                    Canvas.SetLeft(selection, rect.X); Canvas.SetTop(selection, rect.Y);
                    surface.Children.Add(selection); overlays.Add(selection);
                }
                if (overlays.Count > 0) { caretView.Hide(); return; }
            }
            var caret = IsEditingEnabled ? editor.Caret : positionedCaret ?? (sourceCaret.HasValue ? FormulaLayout.CaretAt(sourceCaret.Value) : null);
            if (caret == null) caretView.Hide();
            else caretView.ShowAt(new Rect(caret.X, caret.Top, 0, caret.Height), centered: true);
        }
    }
}
