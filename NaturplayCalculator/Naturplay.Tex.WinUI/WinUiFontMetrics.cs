using System;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Naturplay.Tex.WinUI
{
    public sealed class WinUiFontMetrics : ITexFontMetrics
    {
        private readonly Dictionary<string, TexGlyphMetrics> cache = new Dictionary<string, TexGlyphMetrics>();
        public FontFamily FontFamily { get; } = new FontFamily("Cambria Math");
        public TexGlyphMetrics Measure(string text, double fontSize, TexFont font)
        {
            string key = text + "|" + fontSize.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + font;
            if (cache.TryGetValue(key, out var metrics)) return metrics;
            using (var format = new CanvasTextFormat {
                FontFamily = FontFamily.Source, FontSize = (float)fontSize,
                FontStyle = font == TexFont.MathItalic ? FontStyle.Italic : FontStyle.Normal,
                FontWeight = font == TexFont.Bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
                // A known measurement baseline avoids CanvasLineMetrics[] (its bool
                // field required non-blittable array marshalling in the UWP backend).
                // Ink extents relative to this baseline are independent of line spacing.
                LineSpacingMode = CanvasLineSpacingMode.Uniform,
                LineSpacing = (float)fontSize * 2,
                LineSpacingBaseline = (float)fontSize,
                WordWrapping = CanvasWordWrapping.NoWrap })
            using (var layout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), text, format, 0, 0))
            {
                // Math boxes use ink extents, not the font's paragraph line box.
                // In particular, a digit has no artificial descender reserve; g/j
                // keep their measured descenders. Half a DIP protects edge AA.
                var ink = layout.DrawBounds;
                double baseline = format.LineSpacingBaseline;
                metrics = new TexGlyphMetrics(layout.LayoutBounds.Width,
                    Math.Max(0, baseline - ink.Y) + .5,
                    Math.Max(0, ink.Bottom - baseline) + .5, ink.X, ink.Right);
            }
            if (cache.Count >= 4096) cache.Clear();
            cache.Add(key, metrics);
            return metrics;
        }
        internal TextBlock CreateText(string text, double size, TexFont font) => new TextBlock
        {
            Text = text, FontFamily = FontFamily, FontSize = size,
            FontStyle = font == TexFont.MathItalic ? FontStyle.Italic : FontStyle.Normal,
            FontWeight = font == TexFont.Bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
            IsHitTestVisible = false, TextWrapping = TextWrapping.NoWrap, IsTextScaleFactorEnabled = false
        };
        internal void PositionText(TextBlock block, double x, double baseline)
        {
            // The TextBlock still draws the full, unclipped glyph. Only its origin
            // moves so that its real baseline matches the math layout baseline.
            block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(block, x); Canvas.SetTop(block, baseline - block.BaselineOffset);
        }
    }
}
