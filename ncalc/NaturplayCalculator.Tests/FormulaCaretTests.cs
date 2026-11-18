using System;
using System.Linq;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Input;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class FormulaCaretTests
    {
        [Theory]
        [InlineData(@"\phantom{0}")]
        [InlineData(@"\frac{1}{\phantom{0}}")]
        [InlineData(@"\sqrt{\phantom{0}}")]
        [InlineData(@"2^{\phantom{0}}")]
        [InlineData(@"\begin{pmatrix}1&2\\3&\phantom{0}\end{pmatrix}")]
        public void SlotAndCaretUseTheSameActualFontHeight(string latex)
        {
            var layout = new TexLayoutEngine(new TallFontMetrics()).Render(latex);
            var slot = Assert.Single(layout.Regions, r => r.IsEmptySlot);
            var caret = layout.CaretAt(slot.Span.Start);
            Assert.Equal(slot.Bounds.Y, caret.Top, 6);
            Assert.Equal(slot.Bounds.Height, caret.Height, 6);
            Assert.InRange(caret.Top + caret.Height, 0, layout.Height + .001);
        }
        private sealed class TallFontMetrics : ITexFontMetrics
        {
            public TexGlyphMetrics Measure(string text, double size, TexFont font) =>
                new TexGlyphMetrics(text.Length * size * .55, size * 1.15, size * .35);
        }
        [Theory]
        [InlineData("12+3", "2", "124+3", "1+3")]
        [InlineData("1/(2+3)", "3", "1/(2+34)", "1/(2+)")]
        [InlineData("2^3", "3", "2^34", "2^")]
        [InlineData("sqrt(16)", "6", "sqrt(164)", "sqrt(1)")]
        [InlineData("(1,2,,3,5)", "3", "(1,2,,34,5)", "(1,2,,,5)")]
        public void ClickingFormulaFeedsTheSameInsertionAndDeletionPathAsTheInputBox(string input, string token, string inserted, string deleted)
        {
            var message = ExpressionPreview.Create(input, true).Formula;
            var layout = new TexLayoutEngine().Render(message.Latex);
            var glyph = layout.Commands.Single(c => c.Text == token).Bounds;
            var hit = layout.HitTest(glyph.Right - .001, glyph.Y + glyph.Height / 2);
            int position = message.InputInsertionAt(hit.Offset);
            Assert.Equal(inserted, InputEditor.Insert(input, position, 0, "4").Text);
            Assert.Equal(deleted, InputEditor.Delete(input, position, backward: true).Text);
            Assert.False(ExpressionPreview.Create(deleted, true).IsLimited);
        }
        [Theory]
        [InlineData("12+34")][InlineData("a-b+c")][InlineData("1/2")][InlineData("2^34")]
        public void EveryLiteralBoundaryRoundTripsThroughTheVisibleCaret(string input)
        {
            var message = ExpressionPreview.Create(input, true).Formula;
            var layout = new TexLayoutEngine().Render(message.Latex);
            for (int index = 0; index <= input.Length; index++)
            {
                var caret = layout.CaretAt(message.CaretOffsetAtInput(index));
                Assert.Equal(index, message.InputInsertionAt(caret.Offset));
            }
        }
        [Fact]
        public void SlashMovesFromNumeratorEndToDenominatorStart()
        {
            var message = ExpressionPreview.Create("12/34").Formula;
            var layout = new TexLayoutEngine().Render(message.Latex);
            var before = layout.CaretAt(message.CaretOffsetAtInput(2));
            var after = layout.CaretAt(message.CaretOffsetAtInput(3));
            Assert.True(before.Top < after.Top);
            Assert.True(before.X > after.X);
            var end = layout.CaretAt(message.CaretOffsetAtInput(5));
            Assert.Equal(after.Top, end.Top); Assert.True(end.X > after.X);
        }
        [Theory]
        [InlineData("(2,2,,2")][InlineData("1/")][InlineData("sqrt(")][InlineData("2^")]
        public void AnUnfilledInputEndPlacesTheCaretAtTheSlot(string input)
        {
            var message = ExpressionPreview.Create(input, true).Formula;
            var layout = new TexLayoutEngine().Render(message.Latex);
            var hole = layout.Regions.First(r => r.IsEmptySlot && message.InputIndexAt(r.Span.Start) == input.Length);
            var caret = layout.CaretAt(message.CaretOffsetAtInput(input.Length));
            Assert.Equal(hole.Bounds.X, caret.X, 6);
            Assert.InRange(caret.Top + caret.Height / 2, hole.Bounds.Y, hole.Bounds.Bottom);
            Assert.Equal(input.Length, message.InputInsertionAt(caret.Offset));
        }
        [Theory]
        [InlineData("sqrt(12)")][InlineData("sin(12)")][InlineData("asin(12)")][InlineData("√(12)")]
        [InlineData("(1,2,,3,4)")]
        public void ARealClosingParenthesisCanMoveTheCaretOutsideTheStructure(string input)
        {
            var message = ExpressionPreview.Create(input, true).Formula;
            var layout = new TexLayoutEngine().Render(message.Latex);
            var end = layout.CaretAt(message.CaretOffsetAtInput(input.Length));
            Assert.Equal(layout.Document.Root.ContentSpan.End, end.Offset);
            Assert.Equal(input.Length, message.InputInsertionAt(end.Offset));
        }
        [Fact]
        public void PointerHitTestingChoosesBothSidesOfADigitInsteadOfHighlightingIt()
        {
            var message = ExpressionPreview.Create("123").Formula;
            var layout = new TexLayoutEngine().Render(message.Latex);
            var glyph = layout.Commands.Single(c => c.Text == "2").Bounds;
            var left = layout.HitTest(glyph.X + glyph.Width * .1, glyph.Y + glyph.Height / 2);
            var right = layout.HitTest(glyph.Right - glyph.Width * .1, glyph.Y + glyph.Height / 2);
            Assert.Equal(1, message.InputInsertionAt(left.Offset));
            Assert.Equal(2, message.InputInsertionAt(right.Offset));
        }
        [Theory]
        [InlineData("sqrt(sin((9a+6)/6))")][InlineData("(2,2,,2")][InlineData("sin()+sqrt(1/)")]
        [InlineData("12.345678e-12")][InlineData("[title: demo]a+b")][InlineData("")]
        public void AllInputPositionsProduceFiniteCaretGeometryWithoutChangingTheFormula(string input)
        {
            var message = ExpressionPreview.Create(input, true, true).Formula;
            var layout = new TexLayoutEngine().Render(message.Latex);
            for (int i = 0; i <= input.Length; i++)
            {
                var caret = layout.CaretAt(message.CaretOffsetAtInput(i));
                Assert.InRange(caret.X, 0, layout.Width);
                Assert.InRange(caret.Top, 0, layout.Height);
                Assert.True(caret.Height > 0);
                Assert.False(double.IsNaN(caret.X));
            }
            Assert.Equal(message.Latex, layout.Document.Source);
        }
    }
}
