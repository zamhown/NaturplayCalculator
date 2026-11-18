using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Naturplay.Tex;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class TexRendererTests
    {
        public static readonly string[] Examples =
        {
            @"\sqrt{8}+\sqrt{2}=3\sqrt{2}",
            @"\sqrt{\sin\left(\frac{3a+2}{2}\right)}",
            @"\frac{1}{1+\frac{1}{1+\frac{1}{x}}}",
            @"\left(\frac{a+b}{c+d}\right)^{2}",
            @"x_i^2+x^{a_b}_{c^d}", @"\sqrt[3]{\frac{x^2+1}{y}}",
            @"\begin{pmatrix}1&\frac{1}{2}\\\sqrt{2}&x^2\end{pmatrix}",
            @"\begin{cases}x^2&x\ge 0\\-x&x<0\end{cases}",
            @"\begin{aligned}a&=b+c\\x+y&=z\end{aligned}",
            @"\begin{array}{|r|lc||}a&b&c\\12&345&6\end{array}",
            @"\sum_{i=1}^{n}i=\frac{n(n+1)}{2}",
            @"\int_0^\infty e^{-x}\,\mathrm{d}x",
            @"\lim_{x\to 0}\frac{\sin x}{x}=1",
            @"\left\langle\alpha,\beta\right\rangle\ne\infty",
            @"\hat{x}+\vec{v}+\overline{AB}+\underline{a}",
            @"\mathbf{A}+\mathrm{B}+\mathit{xyz}+\text{mass flow}",
            @"\binom{n}{k}=\frac{n!}{k!(n-k)!}",
            @"\left.\frac{\mathrm{d}f}{\mathrm{d}x}\right|_{x=0}",
            @"a\!b+\qquad c", @"\!x\!", @"\frac{}{}", @"x^{}_{}",
            @"{\scriptstyle x^2}+y", @"\left(\displaystyle\frac{a}{b}\right)",
            @"\begin{matrix}\displaystyle\frac{a}{b}&c\\d&e\\\end{matrix}",
            @"\sum\nolimits_{i=0}^n i", @"\int\limits_0^1 x", @"\sqrt[\alpha+1]{x}",
            @"x^{\begin{bmatrix}a&b\\c&d\end{bmatrix}}", @"\text{\{a\}\&\%}",
            "😀+α", "a% comment\n+b", ""
        };
        public static System.Collections.Generic.IEnumerable<object[]> FormulaCases => Examples.Select(s => new object[] { s });

        [Theory, MemberData(nameof(FormulaCases))]
        public void FormulasHaveBoundedGeometryAndAccurateSourceSpans(string latex)
        {
            var layout = new TexLayoutEngine().Render(latex);
            Assert.InRange(layout.Width, 0, 100000);
            Assert.InRange(layout.Height, 0.01, 100000);
            Assert.InRange(layout.Baseline, 0, layout.Height);
            foreach (var node in layout.Document.Descendants())
            {
                Assert.InRange(node.Span.Start, 0, latex.Length);
                Assert.InRange(node.Span.End, node.Span.Start, latex.Length);
                foreach (var child in node.Children)
                {
                    Assert.True(node.Span.Contains(child.Span.Start));
                    Assert.True(node.Span.Contains(child.Span.End));
                }
            }
            Assert.NotEmpty(layout.Carets);
            foreach (var caret in layout.Carets)
            {
                Assert.InRange(caret.Offset, 0, latex.Length);
                Assert.InRange(caret.X, -0.001, layout.Width + 0.001);
                Assert.InRange(caret.Top, -0.001, layout.Height + 0.001);
                Assert.InRange(caret.Top + caret.Height, 0, layout.Height + 0.001);
                Assert.True(caret.Row.Contains(caret.Offset));
            }
            foreach (var command in layout.Commands)
            {
                if (command.Points.Count == 0)
                {
                    Assert.InRange(command.Bounds.X, -0.001, layout.Width + 0.001);
                    Assert.InRange(command.Bounds.Right, 0, layout.Width + 0.001);
                    Assert.InRange(command.Bounds.Y, -0.001, layout.Height + 0.001);
                    Assert.InRange(command.Bounds.Bottom, 0, layout.Height + 0.001);
                }
                else foreach (var point in command.Points)
                {
                    Assert.InRange(point.X - command.Thickness / 2, -0.001, layout.Width + 0.001);
                    Assert.InRange(point.X + command.Thickness / 2, 0, layout.Width + 0.001);
                    Assert.InRange(point.Y - command.Thickness / 2, -0.001, layout.Height + 0.001);
                    Assert.InRange(point.Y + command.Thickness / 2, 0, layout.Height + 0.001);
                }
            }
            Assert.Equal("svg", XDocument.Parse(TexSvgWriter.Write(layout)).Root.Name.LocalName);
        }

        [Theory]
        [InlineData(@"\frac{1}")][InlineData(@"\sqrt[")][InlineData(@"x^^2")]
        [InlineData(@"x_1_2")][InlineData(@"\left(x")][InlineData(@"\right)")]
        [InlineData(@"\left\foo x\right)")][InlineData(@"{x")][InlineData(@"x}")]
        [InlineData(@"\begin{pmatrix}a&b\\c\end{pmatrix}")]
        [InlineData(@"\begin{matrix}a\end{bmatrix}")]
        [InlineData(@"\begin{array}{c}a&b\end{array}")]
        [InlineData(@"\begin{array}{p{2cm}}a\end{array}")]
        [InlineData(@"\begin{cases}x\end{cases}")]
        [InlineData(@"\input{file}")][InlineData(@"\def\a{b}")][InlineData(@"\href{url}{text}")]
        [InlineData(@"\unknown{x}")][InlineData(@"\text{broken")][InlineData(@"x&y")]
        [InlineData(@"\")][InlineData(@"\limits x")]
        public void InvalidOrUnsupportedInputFailsExplicitlyWithoutPartialLayout(string latex)
        {
            var ex = Assert.Throws<TexParseException>(() => new TexLayoutEngine().Render(latex));
            Assert.InRange(ex.Position, 0, latex.Length);
        }

        [Fact]
        public void ResourceLimitsFailWithoutUnboundedRecursionOrAllocation()
        {
            var parser = new TexParser();
            Assert.Throws<TexParseException>(() => parser.Parse(new string('a', TexLimits.MaxSourceLength + 1)));
            Assert.Throws<TexParseException>(() => parser.Parse(new string('a', TexLimits.MaxNodes + 1)));
            Assert.Throws<TexParseException>(() => parser.Parse(new string('{', TexLimits.MaxDepth + 1) + "x" + new string('}', TexLimits.MaxDepth + 1)));
            Assert.Throws<TexParseException>(() => parser.Parse(string.Concat(Enumerable.Repeat(@"\sqrt", TexLimits.MaxDepth + 1)) + "x"));
            Assert.Throws<TexParseException>(() => parser.Parse(@"\begin{matrix}" + string.Join("&", Enumerable.Repeat("x", 33)) + @"\end{matrix}"));
            Assert.Equal("x", Assert.Single(parser.Parse("x").Root.Children).Text);
        }

        [Fact]
        public void GroupBracesAreInvisibleAndDoNotAddParentheses()
        {
            var engine = new TexLayoutEngine();
            Assert.Equal(engine.Render("a+b").Width, engine.Render("{a+b}").Width, 8);
            Assert.DoesNotContain(engine.Render(@"\sqrt{2}").Commands, c => c.Text.Contains("("));
            Assert.Single(engine.Render(@"\sqrt{2}").Commands, c => c.Kind == TexDrawKind.Polyline);
            Assert.Equal(2, engine.Render(@"\left(\sqrt{2}\right)").Commands.Count(c => c.Kind == TexDrawKind.CubicCurve));
        }

        [Fact]
        public void FractionsScriptsAndLimitsSeparateTheirChildren()
        {
            var fraction = new TexLayoutEngine().Render(@"\frac{a}{b}");
            var a = Assert.Single(fraction.Commands, c => c.Text == "a").Bounds;
            var b = Assert.Single(fraction.Commands, c => c.Text == "b").Bounds;
            var bar = Assert.Single(fraction.Commands, c => c.Kind == TexDrawKind.Rule).Bounds;
            Assert.True(a.Bottom < bar.Y && bar.Bottom < b.Y);
            var scripts = new TexLayoutEngine().Render("x_i^2");
            var x = Assert.Single(scripts.Commands, c => c.Text == "x");
            var sub = Assert.Single(scripts.Commands, c => c.Text == "i");
            var sup = Assert.Single(scripts.Commands, c => c.Text == "2");
            Assert.True(sup.Bounds.Bottom < sub.Bounds.Y);
            Assert.True(sup.FontSize < x.FontSize && sub.FontSize < x.FontSize);
            var limits = new TexLayoutEngine().Render(@"\sum_{a}^{b}");
            var sum = Assert.Single(limits.Commands, c => c.Text == "∑").Bounds;
            Assert.True(Assert.Single(limits.Commands, c => c.Text == "b").Bounds.Bottom < sum.Y);
            Assert.True(Assert.Single(limits.Commands, c => c.Text == "a").Bounds.Y > sum.Bottom);
        }

        [Fact]
        public void ArrayVerticalRulesAndAlignmentAreRendered()
        {
            var layout = new TexLayoutEngine().Render(@"\begin{array}{|r|l||}a&b\\aa&bb\end{array}");
            Assert.Equal(4, layout.Commands.Count(c => c.Kind == TexDrawKind.Rule));
            var glyphs = layout.Commands.Where(c => c.Kind == TexDrawKind.Glyph).ToArray();
            Assert.Equal(glyphs[0].Bounds.Right, glyphs[3].Bounds.Right, 8);
            Assert.Equal(glyphs[1].Bounds.X, glyphs[4].Bounds.X, 8);
        }

        [Fact]
        public void NativeMetricsDriveGlyphGeometryAndUseTheDrawnMinus()
        {
            var layout = new TexLayoutEngine(new TestMetrics()).Render("a-2");
            Assert.All(layout.Commands, c => Assert.Equal(11, c.Bounds.Width));
            Assert.Equal("−", layout.Commands[1].Text);
            Assert.All(layout.Commands, c => Assert.Equal(13, c.Bounds.Height));
        }
        private sealed class TestMetrics : ITexFontMetrics
        {
            public TexGlyphMetrics Measure(string text, double size, TexFont font)
            { Assert.NotEqual("-", text); return new TexGlyphMetrics(11, 9, 4); }
        }

        [Fact]
        public void SvgEscapesTextAndIsIndependentOfCurrentCulture()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                string svg = TexSvgWriter.Write(new TexLayoutEngine().Render(@"\text{<script>\&}", 25.5));
                var xml = XDocument.Parse(svg);
                Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "script");
                Assert.Contains("&lt;script&gt;&amp;", svg);
                Assert.DoesNotContain("25,5", svg);
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }
    }
}
