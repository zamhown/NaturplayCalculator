using System;
using System.Linq;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Input;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class StructuralCaretHeightTests
    {
        private sealed class InkMetrics : ITexFontMetrics
        {
            public TexGlyphMetrics Measure(string text, double size, TexFont font) =>
                new TexGlyphMetrics(text.Length * .55 * size, .65 * size, text.Contains("g") ? .22 * size : 0);
        }

        private static TexRenderResult Render(string latex, double size = 24) =>
            new TexLayoutEngine(new InkMetrics()).Render(latex, size);
        private static TexCaretStop Caret(TexRenderResult layout, TexSpan row, int offset) =>
            layout.Carets.First(c => c.Row.Equals(row) && c.Offset == offset);
        private static TexRect Bounds(TexRenderResult layout, TexNode node) =>
            layout.Regions.First(r => r.Span.Equals(node.Span)).Bounds;
        private static void SameHeight(TexCaretStop caret, TexRect bounds)
        {
            Assert.Equal(bounds.Y, caret.Top, 6);
            Assert.Equal(bounds.Bottom, caret.Top + caret.Height, 6);
        }

        [Theory]
        [InlineData(12)][InlineData(24)][InlineData(48)]
        public void FractionSideWithinARootCoversTheWholeFractionAndKeepsItsScope(double size)
        {
            const string source = "sqrt((9g-6)/9)";
            var formula = ExpressionPreview.Create(source, true).Formula;
            var layout = Render(formula.Latex, size);
            var map = new FormulaCaretMap(source, formula, layout);
            var root = layout.Document.Descendants().Single(n => n.Kind == TexKind.Radical);
            var fraction = layout.Document.Descendants().Single(n => n.Kind == TexKind.Fraction);
            var after = Caret(layout, root.Children[0].ContentSpan, fraction.Span.End);
            var before = Caret(layout, root.Children[0].ContentSpan, fraction.Span.Start);
            SameHeight(after, Bounds(layout, fraction));
            SameHeight(before, Bounds(layout, fraction));
            Assert.True(after.Top > layout.Commands.Single(c => c.Kind == TexDrawKind.Polyline).Points.Min(p => p.Y));

            var slot = map.FromGeometry(layout.HitTest(after.X, after.Top + after.Height / 2));
            Assert.Equal(after.Row, slot.Geometry.Row);
            Assert.Equal(after.Offset, slot.Geometry.Offset);
            Assert.NotNull(slot.Region);
            var edit = new FormulaEditSession(source, slot.Region, slot.TextIndex);
            Assert.Equal("sqrt((9g-6)/9+1)", edit.Insert(edit.Caret, 0, "+1").Text);

            foreach (var branch in fraction.Children)
            {
                var inner = Caret(layout, branch.ContentSpan, branch.ContentSpan.End);
                SameHeight(inner, Bounds(layout, branch));
                Assert.True(inner.Height < after.Height);
                Assert.Equal(inner.Row, layout.HitTest(inner.X, inner.Top + inner.Height / 2).Row);
            }
        }

        [Theory]
        [InlineData(@"\frac{9g-6}{9}")]
        [InlineData(@"\frac{1}{\frac{2}{3}}")]
        [InlineData(@"\sqrt{\frac{1}{2}}")]
        [InlineData(@"\sqrt[3]{x}")]
        [InlineData(@"x^{2}_{g}")]
        [InlineData(@"\left(\frac{1}{2}\right)")]
        [InlineData(@"\begin{pmatrix}a&b\\c&d\end{pmatrix}")]
        [InlineData(@"\begin{bmatrix}1&2\\3&4\end{bmatrix}")]
        [InlineData(@"\binom{a}{b}")]
        public void OuterCaretsCoverTheCompoundIncludingItsStrokes(string latex)
        {
            var layout = Render(latex);
            double top = layout.Commands.Min(c => c.Points.Count == 0 ? c.Bounds.Y : c.Points.Min(p => p.Y) - c.Thickness / 2);
            double bottom = layout.Commands.Max(c => c.Points.Count == 0 ? c.Bounds.Bottom : c.Points.Max(p => p.Y) + c.Thickness / 2);
            foreach (int offset in new[] { 0, latex.Length })
            {
                var caret = Caret(layout, layout.Document.Root.ContentSpan, offset);
                Assert.InRange(top - caret.Top, -0.000001, 1.1);
                Assert.Equal(bottom, caret.Top + caret.Height, 6);
                Assert.InRange(caret.Top, 0, layout.Height);
                Assert.InRange(caret.Top + caret.Height, 0, layout.Height + .000001);
            }
        }

        [Theory]
        [InlineData(@"\frac{a}{b}", "b")]
        [InlineData(@"\sqrt{x}", "x")]
        [InlineData(@"x^{2}_{g}", "2")]
        [InlineData(@"x^{2}_{g}", "g")]
        [InlineData(@"\begin{pmatrix}a&b\\c&d\end{pmatrix}", "d")]
        public void InnerSlotsRemainLocalAndClickable(string latex, string text)
        {
            var layout = Render(latex);
            var node = layout.Document.Descendants().Single(n => n.Kind == TexKind.Glyph && n.Text == text);
            var glyph = layout.Commands.Single(c => c.Text == text);
            var inner = layout.CaretAt(node.Span.End);
            var outer = Caret(layout, layout.Document.Root.ContentSpan, latex.Length);
            SameHeight(inner, glyph.Bounds);
            Assert.True(inner.Height < outer.Height);
            var hit = layout.HitTest(glyph.Bounds.Right - .1, glyph.Bounds.Y + glyph.Bounds.Height / 2);
            Assert.Equal(inner.Offset, hit.Offset);
            Assert.Equal(inner.Row, hit.Row);
        }

        [Fact]
        public void OnlySlotsAdjacentToTheTallAtomGrow()
        {
            var layout = Render(@"a+b+\frac{1}{2}+c+d");
            var row = layout.Document.Root.ContentSpan;
            var fraction = layout.Document.Descendants().Single(n => n.Kind == TexKind.Fraction);
            SameHeight(Caret(layout, row, fraction.Span.Start), Bounds(layout, fraction));
            SameHeight(Caret(layout, row, fraction.Span.End), Bounds(layout, fraction));
            var a = layout.Commands.Single(c => c.Text == "a");
            foreach (int offset in new[] { 0, 1, 2, 3, row.End - 3, row.End - 2, row.End - 1, row.End })
                SameHeight(Caret(layout, row, offset), a.Bounds);
        }

        [Fact]
        public void ASharedBoundaryCoversBothAdjacentCompounds()
        {
            var layout = Render(@"x^{\frac{1}{2}}\frac{3}{4}");
            var compounds = layout.Document.Root.Children;
            var first = Bounds(layout, compounds[0]); var second = Bounds(layout, compounds[1]);
            var between = Caret(layout, layout.Document.Root.ContentSpan, compounds[0].Span.End);
            Assert.Equal(Math.Min(first.Y, second.Y), between.Top, 6);
            Assert.Equal(Math.Max(first.Bottom, second.Bottom), between.Top + between.Height, 6);
        }
    }
}
