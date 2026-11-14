using System;
using System.Collections.Generic;
using System.Linq;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Model;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class ExpressionPreviewTests
    {
        private static IEnumerable<ExpressionNode> Nodes(ExpressionNode node)
        {
            yield return node;
            foreach (var child in node.Children) foreach (var item in Nodes(child)) yield return item;
        }
        [Theory]
        [InlineData("(2,2,", "(2,2,?)", 1, 3, 1)]
        [InlineData("(2,2,,2", "(2,2,,2,?)", 2, 2, 1)]
        [InlineData("(2,2,,", "(2,2,,?,?)", 2, 2, 2)]
        [InlineData("(2,,2,2", "(2,?,,2,2)", 2, 2, 1)]
        [InlineData("(,2,,3,)", "(?,2,,3,?)", 2, 2, 2)]
        [InlineData("(2,2,,,3", "(2,2,,?,3)", 2, 2, 1)]
        [InlineData("(,,,)", "(?,?,,?,?)", 2, 2, 4)]
        [InlineData("(2,)", "(2,)", 1, 1, 0)]
        public void RaggedMatricesAreCompletedAtEveryMissingCell(string input, string code, int rows, int columns, int holes)
        {
            var preview = ExpressionPreview.Create(input);
            Assert.False(preview.IsLimited);
            Assert.Equal(ExpressionKind.Matrix, preview.Root.Kind);
            Assert.Equal(rows, preview.Root.Rows); Assert.Equal(columns, preview.Root.Columns);
            Assert.Equal(code, preview.Root.ToCode());
            var layout = new TexLayoutEngine().Render(preview.Formula.Latex);
            Assert.Equal(holes, layout.Regions.Count(r => r.IsEmptySlot));
            Assert.DoesNotContain(layout.Commands, c => c.Text == "□" || c.Text == "?");
        }

        [Theory]
        [InlineData("(", 1)][InlineData("()", 1)][InlineData("sin", 1)][InlineData("sin(", 1)]
        [InlineData("sqrt()", 1)][InlineData("sqrt(1/)", 1)][InlineData("1+*2", 1)]
        [InlineData("/", 2)][InlineData("1/", 1)][InlineData("/2", 1)]
        [InlineData("1+)", 1)][InlineData("1^", 1)][InlineData("^", 2)]
        [InlineData("nCr", 2)][InlineData("yroot", 2)][InlineData("!", 1)]
        [InlineData("(1+,/2,,sin(),^)", 5)][InlineData("1.2e-", 1)]
        public void MissingOperandsStayInsideTheirPredictedContainers(string input, int holes)
        {
            var preview = ExpressionPreview.Create(input, true);
            Assert.False(preview.IsLimited);
            Assert.Equal(holes, new TexLayoutEngine().Render(preview.Formula.Latex).Regions.Count(r => r.IsEmptySlot));
        }

        [Fact]
        public void FractionAndFunctionHolesAreNotMovedToTheEndOfTheFormula()
        {
            var preview = ExpressionPreview.Create("sqrt(1/)+sin()+2/", true);
            Assert.Equal(@"\sqrt{\frac{1}{\phantom{0}}}+\sin \left(\phantom{0}\right)+\frac{2}{\phantom{0}}", preview.Formula.Latex);
        }

        [Theory]
        [InlineData("(2,2,,2")][InlineData("(,2,,3,)")][InlineData("1+*2")]
        [InlineData("sin(")][InlineData("1/")][InlineData("1.2e-")]
        public void PreviewRecoveryNeverMakesIncompleteCalculationInputValid(string input)
        {
            var parser = new ExpressionParser();
            var draft = parser.ParsePreview(input);
            Assert.NotNull(draft.Root);
            Assert.Throws<MathException>(() => parser.Parse(input));
            Assert.Throws<MathException>(() => new CalculationEngine().Evaluate(input));
            Assert.NotNull(parser.ParsePreview(input).Root);
        }

        [Theory]
        [InlineData("sqrt(8)+sqrt(2)")][InlineData("(a+b)c+bc")]
        [InlineData("(a+b)/(c+d)")][InlineData("(a^b)^c")][InlineData("a^(b^c)")]
        [InlineData("(1,2,,3,4)^T")][InlineData("1.25e-3+2e4")]
        [InlineData("sin(cos(a))+Abs(a/b)")][InlineData("Cov(1,2,,3,4)")]
        [InlineData("[title: sample]sqrt(sin((9a+6)/6))")]
        public void CompleteInputRetainsTheExistingMathematicalPresentation(string input)
        {
            var strict = new ExpressionParser().Parse(input);
            foreach (bool implicitProducts in new[] { false, true })
            {
                var preview = ExpressionPreview.Create(input, implicitProducts);
                Assert.Equal(new ExpressionTexFormatter().Format(strict.Root, implicitProducts).Latex, preview.Formula.Latex);
                Assert.Equal(strict.Comments, preview.Comments);
            }
        }

        [Fact]
        public void PredictedCellsDoNotShiftTheOriginalInputPositions()
        {
            const string input = "(2,,3,4";
            var preview = ExpressionPreview.Create(input);
            var layout = new TexLayoutEngine().Render(preview.Formula.Latex);
            foreach (var glyph in layout.Document.Descendants().Where(n => n.Kind == TexKind.Glyph && n.Text != "0"))
            {
                int source = preview.Formula.InputIndexAt(glyph.Span.Start);
                Assert.Equal(glyph.Text, input[source].ToString());
            }
            var hole = Assert.Single(layout.Regions, r => r.IsEmptySlot);
            Assert.Equal(2, preview.Formula.InputIndexAt(hole.Span.Start));
            Assert.Equal(0, preview.Formula.SourceMap.Single(m => m.TexStart == hole.Span.Start).InputLength);
            preview = ExpressionPreview.Create("(2,2,,2");
            hole = Assert.Single(new TexLayoutEngine().Render(preview.Formula.Latex).Regions, r => r.IsEmptySlot);
            Assert.Equal(7, preview.Formula.InputIndexAt(hole.Span.Start));
            Assert.Equal(hole.Span.Start, preview.Formula.RangeAtInput(7).TexStart);
        }

        [Theory]
        [InlineData("(2,2,,2,2)")][InlineData("(1+2,sqrt(8),,sin(1/2),3^2)")]
        [InlineData("sqrt(sin((9a+6)/6))")][InlineData("(a+b)c+bc")]
        [InlineData("(1,,2,3,,4,5,6)")][InlineData("1.25e-10+1/2")]
        [InlineData("((1,2,,3,4)^T)^2")][InlineData("Abs((a,b,,c,d))")]
        [InlineData("1+*2^/3")][InlineData("sin(,,sqrt(/),,2+)")]
        public void EveryTypingAndBackspacePrefixHasAFormulaPreview(string complete)
        {
            for (int length = 1; length <= complete.Length; length++)
            {
                string input = complete.Substring(0, length);
                var preview = ExpressionPreview.Create(input, true);
                Assert.False(preview.IsLimited, input);
                var layout = new TexLayoutEngine().Render(preview.Formula.Latex);
                Assert.True(layout.Commands.Count > 0 || layout.Regions.Any(r => r.IsEmptySlot), input);
                Assert.All(preview.Formula.SourceMap, m => Assert.InRange(m.InputStart, 0, input.Length));
            }
        }

        [Fact]
        public void EditingTheMiddleOfAnExpressionAlwaysKeepsAFormula()
        {
            const string formula = "sqrt((1+2)/(3-4))+(1,2,,3,4)";
            for (int index = 0; index < formula.Length; index++)
            {
                foreach (string input in new[] { formula.Remove(index, 1), formula.Insert(index, ","), formula.Insert(index, "/") })
                {
                    var preview = ExpressionPreview.Create(input, true);
                    Assert.False(preview.IsLimited, input);
                    Assert.NotNull(new TexLayoutEngine().Render(preview.Formula.Latex));
                }
            }
        }

        [Fact]
        public void LongInputsDoNotHitTheOld512CharacterFallbackOrADepthLimit()
        {
            string input = string.Join("+", Enumerable.Repeat("1", 900)) + "+";
            var preview = ExpressionPreview.Create(input);
            Assert.False(preview.IsLimited);
            var layout = new TexLayoutEngine().Render(preview.Formula.Latex);
            Assert.Equal(900, layout.Commands.Count(c => c.Text == "1"));
            Assert.Single(layout.Regions, r => r.IsEmptySlot);
            input = new string('a', 1500) + "/";
            preview = ExpressionPreview.Create(input, true);
            Assert.False(preview.IsLimited);
            layout = new TexLayoutEngine().Render(preview.Formula.Latex);
            Assert.Equal(1500, layout.Commands.Count(c => c.Text == "a"));
            Assert.Single(layout.Regions, r => r.IsEmptySlot);
        }

        [Fact]
        public void NestedInputWithinTheCalculationBudgetStillRenders()
        {
            string input = string.Concat(Enumerable.Repeat("sqrt(", ExpressionLimits.MaxDepth)) + "1/";
            var preview = ExpressionPreview.Create(input);
            Assert.False(preview.IsLimited);
            Assert.Single(new TexLayoutEngine().Render(preview.Formula.Latex).Regions, r => r.IsEmptySlot);
        }

        [Theory]
        [InlineData("{\"a\":1}", false)][InlineData("[1,2]", false)][InlineData("<root>", false)]
        [InlineData("\"hello\"", false)][InlineData("[title: sample]1/2", true)]
        [InlineData("1+*2", true)][InlineData("(2,2,,2", true)][InlineData("a=b", true)]
        [InlineData("<2", true)][InlineData("[comment", true)]
        public void OnlyRecognizableDocumentPrefixesUseDocumentPresentation(string input, bool math)
        { Assert.Equal(math, ExpressionPreview.IsMathCandidate(input)); }

        [Fact]
        public void ResourceOverflowStillReturnsAStructuredPrefixAndAnExplicitLimit()
        {
            var preview = ExpressionPreview.Create(new string('(', ExpressionLimits.MaxDepth + 20) + "1+");
            Assert.True(preview.IsLimited); Assert.EndsWith(@"\ldots ", preview.Formula.Latex);
            Assert.NotNull(new TexLayoutEngine().Render(preview.Formula.Latex));
        }

        [Fact]
        public void PhantomSlotsHaveGeometryButNeverRenderABoxGlyphOrTheHiddenDigit()
        {
            var layout = new TexLayoutEngine().Render(@"\frac{\phantom{0}}{x^{\phantom{0}}}+\square");
            var slots = layout.Regions.Where(r => r.IsEmptySlot).ToArray();
            Assert.Equal(2, slots.Length);
            Assert.True(slots[0].Bounds.Width > slots[1].Bounds.Width);
            Assert.All(slots, s => { Assert.True(s.Bounds.Width > 0); Assert.True(s.Bounds.Height > 0); });
            Assert.DoesNotContain(layout.Commands, c => c.Text == "0");
            Assert.Single(layout.Commands, c => c.Text == "□"); // Explicit TeX squares retain their meaning.
            Assert.Contains(slots, s => ReferenceEquals(s, layout.HitTestAtom(s.Bounds.X + s.Bounds.Width / 2, s.Bounds.Y + s.Bounds.Height / 2)));
            Assert.DoesNotContain(">0</text>", TexSvgWriter.Write(layout));
        }

        [Fact]
        public void RelationTokensRemainVisibleWithoutInventedMultiplication()
        {
            Assert.Equal("a=b", ExpressionPreview.Create("a=b", true).Formula.Latex);
            Assert.Equal(@"\begin{pmatrix}a=b&2\\3&\phantom{0}\end{pmatrix}", ExpressionPreview.Create("(a=b,2,,3", true).Formula.Latex);
        }

        [Theory]
        [InlineData("sin(√((9g-6)/9π))^(2)")]
        [InlineData("sqrt(1/2)")][InlineData("1+")]
        public void UnmatchedClosingDelimitersDoNotInventAGlobalOpeningGroup(string prefix)
        {
            var expected = ExpressionPreview.Create(prefix, true).Formula.Latex;
            foreach (string extra in new[] { ")", "))" })
            {
                var preview = ExpressionPreview.Create(prefix + extra, true);
                Assert.Equal(expected + extra, preview.Formula.Latex);
                Assert.False(preview.IsLimited);
                var layout = new TexLayoutEngine().Render(preview.Formula.Latex);
                Assert.Equal(extra.Length, layout.Commands.Count(c => c.Text == ")"));
                Assert.Throws<MathException>(() => new ExpressionParser().Parse(prefix + extra));
            }
        }

        [Fact]
        public void MixedIncompleteTokensAlwaysRenderAndKeepEveryEnteredNumber()
        {
            var random = new Random(14731);
            string[] tokens = { "1", "2", "a", "sin", "sqrt", "(", ")", ",", "+", "-", "*", "/", "^", "!", "?", "=" };
            for (int sample = 0; sample < 600; sample++)
            {
                string input = string.Concat(Enumerable.Range(0, random.Next(1, 19)).Select(_ => tokens[random.Next(tokens.Length)]));
                var preview = ExpressionPreview.Create(input, true);
                Assert.False(preview.IsLimited, input);
                var layout = new TexLayoutEngine().Render(preview.Formula.Latex);
                Assert.Equal(input.Count(c => c == '1'), layout.Commands.Count(c => c.Text == "1"));
                Assert.Equal(input.Count(c => c == '2'), layout.Commands.Count(c => c.Text == "2"));
            }
        }
    }
}
