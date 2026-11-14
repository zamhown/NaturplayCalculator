using System.Linq;
using Naturplay.Tex;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class InkLayoutTests
    {
        private sealed class InkMetrics : ITexFontMetrics
        {
            public TexGlyphMetrics Measure(string text, double size, TexFont font) =>
                new TexGlyphMetrics(text.Length * .55 * size, .65 * size, text.Contains("g") ? .22 * size : 0);
        }
        private sealed class ItalicOverhangMetrics : ITexFontMetrics
        {
            public TexGlyphMetrics Measure(string text, double size, TexFont font) => text == "g"
                ? new TexGlyphMetrics(10, 10, 4, -1, 13) : new TexGlyphMetrics(10, 10, 0, 1, 9);
        }
        [Theory]
        [InlineData("9g-2", 2)][InlineData("9g", 2)][InlineData("gg", 1)]
        public void CaretClearsItalicInkAndCoversTheRowsDescenders(string latex, int offset)
        {
            var layout = new TexLayoutEngine(new ItalicOverhangMetrics()).Render(latex, 24);
            var g = layout.Commands.First(c => c.Text == "g");
            var caret = layout.CaretAt(offset);
            Assert.True(caret.X - g.Bounds.Right >= 1);
            Assert.True(caret.Top <= g.Bounds.Y);
            Assert.True(caret.Top + caret.Height >= g.Bounds.Bottom);
            Assert.NotEqual(g.Bounds.X, g.OriginX);
            Assert.Contains("x=\"" + g.OriginX.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "\"", TexSvgWriter.Write(layout));
        }
        [Theory]
        [InlineData("3")][InlineData(@"\frac{1}{3}")][InlineData(@"\sqrt{\frac{3g-2}{3}}")]
        public void MathBoundsDoNotAddAParagraphDescenderReserveBelowTheInk(string latex)
        {
            var layout = new TexLayoutEngine(new InkMetrics()).Render(latex, 24);
            double bottom = layout.Commands.Max(c => c.Points.Count == 0 ? c.Bounds.Bottom : c.Points.Max(p => p.Y) + c.Thickness / 2);
            Assert.InRange(layout.Height - bottom, -.001, .001);
        }
        [Fact]
        public void ActualDescendersRemainInsideTheLayout()
        {
            var layout = new TexLayoutEngine(new InkMetrics()).Render("3g-2", 24);
            var digit = layout.Commands.First(c => c.Text == "3"); var g = layout.Commands.First(c => c.Text == "g");
            Assert.Equal(digit.Baseline, g.Baseline);
            Assert.True(g.Bounds.Bottom > digit.Bounds.Bottom);
            Assert.Equal(g.Bounds.Bottom, layout.Height, 6);
        }
        [Fact]
        public void FractionClearanceIsMeasuredFromTheActualDescender()
        {
            var layout = new TexLayoutEngine(new InkMetrics()).Render(@"\frac{3g-2}{3}", 24);
            var g = layout.Commands.Single(c => c.Text == "g");
            var rule = layout.Commands.Single(c => c.Kind == TexDrawKind.Rule);
            var denominator = layout.Commands.Last(c => c.Text == "3");
            Assert.Equal(24 * .2, rule.Bounds.Y - g.Bounds.Bottom, 6);
            Assert.Equal(24 * .2, denominator.Bounds.Y - rule.Bounds.Bottom, 6);
        }
    }
}
