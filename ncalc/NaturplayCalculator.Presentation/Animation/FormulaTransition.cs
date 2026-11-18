using System;
using System.Collections.Generic;
using System.Linq;
using Naturplay.Tex;

namespace NaturplayCalculator.Presentation.Animation;

/// <summary>A drawing-only snapshot. Editing and hit testing always use the final TeX layout.</summary>
public sealed record FormulaDrawing(
    TexDrawKind Kind, string Text, TexFont Font, double FontSize,
    TexRect Bounds, double OriginX, double Baseline, double Thickness,
    IReadOnlyList<TexPoint> Points, bool IsSlot = false, double Opacity = 1, TexSpan Span = default)
{
    internal bool Matches(FormulaDrawing other) => IsSlot == other.IsSlot && Kind == other.Kind
        && Text == other.Text && Font == other.Font && Points.Count == other.Points.Count;

    internal FormulaDrawing OffsetY(double offset) => this with
    {
        Bounds = new TexRect(Bounds.X, Bounds.Y + offset, Bounds.Width, Bounds.Height),
        Baseline = Baseline + offset,
        Points = Points.Select(p => new TexPoint(p.X, p.Y + offset)).ToArray()
    };

    internal static FormulaDrawing Between(FormulaDrawing from, FormulaDrawing to, double t) => to with
    {
        FontSize = Lerp(from.FontSize, to.FontSize, t),
        Bounds = new TexRect(Lerp(from.Bounds.X, to.Bounds.X, t), Lerp(from.Bounds.Y, to.Bounds.Y, t),
            Lerp(from.Bounds.Width, to.Bounds.Width, t), Lerp(from.Bounds.Height, to.Bounds.Height, t)),
        OriginX = Lerp(from.OriginX, to.OriginX, t),
        Baseline = Lerp(from.Baseline, to.Baseline, t),
        Thickness = Lerp(from.Thickness, to.Thickness, t),
        Opacity = Lerp(from.Opacity, to.Opacity, t),
        Points = from.Points.Select((p, i) => new TexPoint(Lerp(p.X, to.Points[i].X, t), Lerp(p.Y, to.Points[i].Y, t))).ToArray()
    };
    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}

public sealed record FormulaFrame(double Width, double Height, IReadOnlyList<FormulaDrawing> Drawing)
{
    public static FormulaFrame FromLayout(TexRenderResult layout)
    {
        var slots = layout.Regions.Where(r => r.IsEmptySlot).Select(r => new FormulaDrawing(
            TexDrawKind.Rule, "", TexFont.Roman, 0, r.Bounds, 0, 0, 0, Array.Empty<TexPoint>(), true, Span: r.Span));
        var drawing = layout.Commands.Select(c => new FormulaDrawing(c.Kind, c.Text, c.Font, c.FontSize,
            c.Bounds, c.OriginX, c.Baseline, c.Thickness, c.Points, Span: c.Span));
        return new FormulaFrame(layout.Width, layout.Height, slots.Concat(drawing).ToArray());
    }
}

/// <summary>Order-preserving glyph correspondence and a short ease-out tween, independent of the UI clock.</summary>
public sealed class FormulaTransition
{
    public const double DurationMilliseconds = 150;
    private const int MaximumDrawingCount = 512;
    private readonly FormulaFrame target;
    private readonly (FormulaDrawing From, FormulaDrawing To)[] pairs;
    public bool CanAnimate { get; }

    public FormulaTransition(FormulaFrame source, FormulaFrame target, bool preserveVerticalCenter = false)
    {
        this.target = target;
        // Pasting a very large document must remain immediate and must not allocate
        // a quadratic matching table. Only interactive-sized formulas animate.
        CanAnimate = source.Drawing.Count + target.Drawing.Count <= MaximumDrawingCount;
        if (!CanAnimate) { pairs = Array.Empty<(FormulaDrawing, FormulaDrawing)>(); return; }
        double verticalOffset = preserveVerticalCenter ? (target.Height - source.Height) / 2 : 0;
        var from = source.Drawing.Where(c => c.Opacity > .001).Select(c => c.OffsetY(verticalOffset)).ToArray();
        var to = target.Drawing;
        var lengths = new ushort[from.Length + 1, to.Count + 1];
        for (int i = from.Length - 1; i >= 0; i--)
            for (int j = to.Count - 1; j >= 0; j--)
                lengths[i, j] = from[i].Matches(to[j]) ? (ushort)(1 + lengths[i + 1, j + 1])
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
        var matches = new List<(FormulaDrawing, FormulaDrawing)>();
        int a = 0, b = 0;
        while (a < from.Length || b < to.Count)
        {
            if (a < from.Length && b < to.Count && from[a].Matches(to[b]))
                matches.Add((from[a++], to[b++]));
            else if (a < from.Length && (b == to.Count || lengths[a + 1, b] >= lengths[a, b + 1]))
            {
                var removed = from[a++];
                matches.Add((removed, removed with { Opacity = 0 }));
            }
            else
            {
                var inserted = to[b++];
                matches.Add((inserted with { Opacity = 0 }, inserted));
            }
        }
        pairs = matches.ToArray();
    }

    public FormulaFrame At(double progress)
    {
        if (!CanAnimate || progress >= 1) return target;
        double p = Math.Clamp(progress, 0, 1);
        double eased = 1 - Math.Pow(1 - p, 3);
        return new FormulaFrame(target.Width, target.Height,
            pairs.Select(pair => FormulaDrawing.Between(pair.From, pair.To, eased)).ToArray());
    }
}
