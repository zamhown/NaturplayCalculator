using System;
using System.Collections.Generic;
using System.Linq;

namespace Naturplay.Tex
{
    public struct TexPoint
    {
        public double X { get; }
        public double Y { get; }
        public TexPoint(double x, double y) { X = x; Y = y; }
    }
    public struct TexRect
    {
        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public double Right => X + Width;
        public double Bottom => Y + Height;
        public TexRect(double x, double y, double width, double height) { X = x; Y = y; Width = width; Height = height; }
        public bool Contains(double x, double y) => x >= X && x <= Right && y >= Y && y <= Bottom;
    }
    public struct TexGlyphMetrics
    {
        public double Width { get; }
        public double Ascent { get; }
        public double Descent { get; }
        public double InkLeft { get; }
        public double InkRight { get; }
        public TexGlyphMetrics(double width, double ascent, double descent, double? inkLeft = null, double? inkRight = null)
        {
            if (new[] { width, ascent, descent }.Any(n => double.IsNaN(n) || double.IsInfinity(n) || n < 0))
                throw new ArgumentOutOfRangeException(nameof(width));
            Width = width; Ascent = ascent; Descent = descent;
            InkLeft = inkLeft ?? 0; InkRight = inkRight ?? width;
            if (double.IsNaN(InkLeft) || double.IsNaN(InkRight) || double.IsInfinity(InkLeft) || double.IsInfinity(InkRight) || InkRight < InkLeft)
                throw new ArgumentOutOfRangeException(nameof(inkLeft));
        }
    }
    public interface ITexFontMetrics
    {
        TexGlyphMetrics Measure(string text, double fontSize, TexFont font);
    }
    /// <summary>Deterministic headless fallback. Native hosts should supply actual font measurements.</summary>
    public sealed class ApproximateTexFontMetrics : ITexFontMetrics
    {
        public TexGlyphMetrics Measure(string text, double fontSize, TexFont font) =>
            new TexGlyphMetrics(text.Sum(c => "il.,:;!′".Contains(c) ? 0.28 : "MW∑∏".Contains(c) ? 0.85 : 0.55) * fontSize, fontSize * 0.78, fontSize * 0.22);
    }
    public enum TexDrawKind { Glyph, Rule, Polyline, CubicCurve }
    public sealed class TexDrawCommand
    {
        public TexDrawKind Kind { get; internal set; }
        public TexRect Bounds { get; internal set; }
        public string Text { get; internal set; } = "";
        public double FontSize { get; internal set; }
        public double Baseline { get; internal set; }
        public double OriginX { get; internal set; }
        public TexFont Font { get; internal set; }
        public double Thickness { get; internal set; }
        public IReadOnlyList<TexPoint> Points { get; internal set; } = Array.Empty<TexPoint>();
    }
    public sealed class TexHitRegion
    {
        public TexSpan Span { get; internal set; }
        public TexRect Bounds { get; internal set; }
        public bool IsGlyph { get; internal set; }
        public bool IsEmptySlot { get; internal set; }
        public int Depth { get; internal set; }
    }
    public sealed class TexCaretStop
    {
        public int Offset { get; internal set; }
        public TexSpan Row { get; internal set; }
        public double X { get; internal set; }
        public double Top { get; internal set; }
        public double Height { get; internal set; }
    }
    public sealed class TexRenderResult
    {
        public TexDocument Document { get; internal set; }
        public double Width { get; internal set; }
        public double Height { get; internal set; }
        public double Baseline { get; internal set; }
        public IReadOnlyList<TexDrawCommand> Commands { get; internal set; }
        public IReadOnlyList<TexHitRegion> Regions { get; internal set; }
        public IReadOnlyList<TexCaretStop> Carets { get; internal set; }
        public TexCaretStop CaretAt(int offset) => Carets.OrderBy(c => Math.Abs(c.Offset - offset))
            .ThenBy(c => c.Row.Length).First();
        public TexCaretStop HitTest(double x, double y) => Carets.OrderBy(c =>
        {
            double dy = y < c.Top ? c.Top - y : y > c.Top + c.Height ? y - c.Top - c.Height : 0;
            return Math.Abs(c.X - x) + 3 * dy;
        }).ThenBy(c => c.Row.Length).First();
        public TexHitRegion HitTestAtom(double x, double y) => Regions.Where(r => r.Bounds.Contains(x, y))
            .OrderByDescending(r => r.Depth).ThenBy(r => r.Bounds.Width * r.Bounds.Height).FirstOrDefault();
        public IReadOnlyList<TexRect> SelectionBounds(TexSpan selection) => Regions.Where(r => r.IsGlyph && r.Span.Intersects(selection))
            .Select(r => r.Bounds).ToArray();
    }
}
