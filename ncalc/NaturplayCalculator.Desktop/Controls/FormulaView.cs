using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Naturplay.Tex;
using NaturplayCalculator.Presentation.Animation;
using SkiaSharp;
using System.Diagnostics;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace NaturplayCalculator.Desktop.Controls;

/// <summary>Draws the shared TeX layout directly into Uno's GPU canvas.</summary>
public sealed class FormulaView : SKCanvasElement
{
    private static readonly MathFontMetrics Metrics = new();
    private TexCaretStop? caret;
    private SKColor color, caretColor, slotColor, inactiveColor;
    private TexSpan? activeScope;
    private FormulaFrame? frame;
    private FormulaTransition? transition;
    private long transitionStarted;
    private double drawingLeft, drawingTop;
    private readonly DispatcherTimer animationTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    public TexRenderResult? Layout { get; private set; }
    public string Latex { get; private set; } = "";
    public event Action<TexCaretStop>? CaretTapped;

    public FormulaView()
    {
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
        animationTimer.Tick += (_, _) =>
        {
            if (Progress >= 1) { transition = null; animationTimer.Stop(); SetCanvasBounds(null); }
            Invalidate();
        };
        Loaded += (_, _) =>
        {
            if (transition == null) return;
            if (Progress < 1) animationTimer.Start();
            else { transition = null; SetCanvasBounds(null); Invalidate(); }
        };
        Unloaded += (_, _) => animationTimer.Stop();
        Tapped += (_, e) =>
        {
            if (Layout == null) return;
            var p = e.GetPosition(this);
            CaretTapped?.Invoke(HitTest(p.X, p.Y));
            e.Handled = true;
        };
    }

    public void SetFormula(string latex, double size, Windows.UI.Color foreground, Windows.UI.Color insertion, Windows.UI.Color emptySlot,
        FormulaView? transitionFrom = null, bool animate = false, bool preserveVerticalCenter = false)
    {
        // Snapshot an interrupted animation before replacing its layout. Fast
        // input therefore continues from the visible frame, not an old endpoint.
        var previous = transitionFrom?.CurrentFrame() ?? CurrentFrame();
        animationTimer.Stop(); transition = null;
        Latex = latex;
        Layout = new TexLayoutEngine(Metrics).Render(latex, size);
        frame = FormulaFrame.FromLayout(Layout);
        if (animate && !MacMotionPreferences.ReduceMotion)
        {
            previous ??= new FormulaFrame(frame.Width, frame.Height, Array.Empty<FormulaDrawing>());
            var next = new FormulaTransition(previous, frame, preserveVerticalCenter);
            if (next.CanAnimate) { transition = next; transitionStarted = Stopwatch.GetTimestamp(); }
        }
        color = NativeColor(foreground); caretColor = NativeColor(insertion); slotColor = NativeColor(emptySlot);
        SetCanvasBounds(transition?.At(0));
        AutomationProperties.SetName(this, latex);
        if (transition != null && IsLoaded) animationTimer.Start();
        Invalidate();
    }

    private double Progress => Stopwatch.GetElapsedTime(transitionStarted).TotalMilliseconds / FormulaTransition.DurationMilliseconds;
    private FormulaFrame? CurrentFrame() => transition?.At(Progress) ?? frame;

    public TexCaretStop HitTest(double x, double y) => Layout!.HitTest(x - 3 - drawingLeft, y - 3 - drawingTop);

    private void SetCanvasBounds(FormulaFrame? outgoing)
    {
        if (Layout == null) return;
        double left = 0, top = 0, right = Layout.Width, bottom = Layout.Height;
        if (outgoing != null)
            foreach (var drawing in outgoing.Drawing.Where(d => d.Opacity > .001))
            {
                if (drawing.Kind is TexDrawKind.Polyline or TexDrawKind.CubicCurve)
                {
                    foreach (var point in drawing.Points)
                    {
                        left = Math.Min(left, point.X - drawing.Thickness);
                        right = Math.Max(right, point.X + drawing.Thickness);
                        top = Math.Min(top, point.Y - drawing.Thickness);
                        bottom = Math.Max(bottom, point.Y + drawing.Thickness);
                    }
                }
                else
                {
                    left = Math.Min(left, drawing.Bounds.X); right = Math.Max(right, drawing.Bounds.Right);
                    top = Math.Min(top, drawing.Bounds.Y); bottom = Math.Max(bottom, drawing.Bounds.Bottom);
                }
            }
        drawingLeft = Math.Ceiling(-left); drawingTop = Math.Ceiling(-top);
        double extraRight = Math.Ceiling(right - Layout.Width), extraBottom = Math.Ceiling(bottom - Layout.Height);
        Width = Math.Ceiling(Layout.Width) + 8 + drawingLeft + extraRight;
        Height = Math.Ceiling(Layout.Height) + 8 + drawingTop + extraBottom;
        // Give the GPU surface room for outgoing strokes without changing its
        // final layout footprint. Remove this bleed when the tween finishes.
        Margin = new Thickness(-drawingLeft, -drawingTop, -extraRight, -extraBottom);
    }

    public void ShowCaret(TexCaretStop? value) { caret = value; Invalidate(); }
    public void ShowScope(TexSpan? scope, Windows.UI.Color inactive)
    { activeScope = scope; inactiveColor = NativeColor(inactive); Invalidate(); }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (Layout == null || CurrentFrame() is not { } visible) return;
        canvas.Save();
        canvas.Translate((float)(3 + drawingLeft), (float)(3 + drawingTop));
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        foreach (var command in visible.Drawing)
        {
            if (command.Opacity <= .001) continue;
            bool active = activeScope is not { } scope || command.Span.Start >= scope.Start && command.Span.End <= scope.End;
            var ink = command.IsSlot ? slotColor : active ? color : inactiveColor;
            paint.Color = ink.WithAlpha((byte)Math.Clamp(Math.Round(ink.Alpha * command.Opacity), 0, 255));
            paint.Style = SKPaintStyle.Fill;
            if (command.IsSlot) canvas.DrawRoundRect(Rect(command.Bounds), 2, 2, paint);
            else if (command.Kind == TexDrawKind.Glyph)
            {
                using var font = Metrics.CreateFont(command.FontSize, command.Font);
                canvas.DrawText(command.Text, (float)command.OriginX, (float)command.Baseline, SKTextAlign.Left, font, paint);
            }
            else if (command.Kind == TexDrawKind.Rule) canvas.DrawRect(Rect(command.Bounds), paint);
            else
            {
                if (command.Points.Count < 2) continue;
                using var path = new SKPathBuilder();
                path.MoveTo((float)command.Points[0].X, (float)command.Points[0].Y);
                if (command.Kind == TexDrawKind.CubicCurve)
                {
                    for (int i = 1; i + 2 < command.Points.Count; i += 3)
                        path.CubicTo(Point(command.Points[i]), Point(command.Points[i + 1]), Point(command.Points[i + 2]));
                }
                else foreach (var point in command.Points.Skip(1)) path.LineTo(Point(point));
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = (float)command.Thickness;
                paint.StrokeCap = SKStrokeCap.Square;
                using var built = path.Detach();
                canvas.DrawPath(built, paint);
            }
        }
        if (caret != null)
        {
            using var pen = new SKPaint { Color = caretColor, StrokeWidth = 1.6f, IsAntialias = true };
            canvas.DrawLine((float)caret.X, (float)caret.Top, (float)caret.X, (float)(caret.Top + caret.Height), pen);
        }
        canvas.Restore();
    }

    private static SKColor NativeColor(Windows.UI.Color value) => new(value.R, value.G, value.B, value.A);
    private static SKRect Rect(TexRect r) => new((float)r.X, (float)r.Y, (float)r.Right, (float)r.Bottom);
    private static SKPoint Point(TexPoint p) => new((float)p.X, (float)p.Y);

    private sealed class MathFontMetrics : ITexFontMetrics
    {
        private readonly SKTypeface regular = SKTypeface.FromFamilyName("STIX Two Math");
        private readonly SKTypeface italic = SKTypeface.FromFamilyName("STIX Two Text", SKFontStyle.Italic);
        private readonly SKTypeface bold = SKTypeface.FromFamilyName("STIX Two Text", SKFontStyle.Bold);
        public SKFont CreateFont(double size, TexFont font) => new(font == TexFont.MathItalic ? italic : font == TexFont.Bold ? bold : regular, (float)size)
        { Edging = SKFontEdging.SubpixelAntialias, Subpixel = true };

        public TexGlyphMetrics Measure(string text, double fontSize, TexFont font)
        {
            using var skFont = CreateFont(fontSize, font);
            float advance = skFont.MeasureText(text, out var ink);
            return new TexGlyphMetrics(Math.Max(0, advance), Math.Max(0, -ink.Top) + .5,
                Math.Max(0, ink.Bottom) + .5, ink.Left, ink.Right);
        }
    }
}
