using System;
using System.Linq;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Input;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class SemanticFormulaCaretTests
    {
        [Fact]
        public void NestedFunctionBeginsWithTheFunctionThenItsArgument()
        {
            const string source = "sin(√((9g-6)/9*π))^(2+1)";
            var formula = ExpressionPreview.Create(source, true).Formula;
            var layout = new TexLayoutEngine().Render(formula.Latex);
            var map = new FormulaCaretMap(source, formula, layout);
            Assert.Equal(new[] { 0, 4, 6, 7, 8, 9, 10, 11, 13, 14, 15, 16, 17, 20, 21, 22, 23, 24 }, map.Slots.Select(s => s.TextIndex));
            Assert.Equal(map.Slots.Last().Geometry.Top, map.Slots[0].Geometry.Top, 6);
            Assert.Equal(map.Slots.Last().Geometry.Height, map.Slots[0].Geometry.Height, 6);
            Assert.Same(map.Slots[1], map.Move(map.Slots[0], 1));
            var piEnd = map.Slots.Single(s => s.TextIndex == 16);
            Assert.Equal(17, map.Move(piEnd, 1).TextIndex);
            foreach (var slot in map.Slots.Skip(1)) Assert.Same(slot, map.Move(map.Move(slot, -1), 1));
            Assert.All(map.Slots, slot => Assert.Same(slot, map.FromGeometry(slot.Geometry)));
            Assert.Equal(4, map.FromText(3).TextIndex); // The hidden name/fence gap maps into the argument.
            var edit = new FormulaEditSession(source, piEnd.Region, piEnd.TextIndex);
            Assert.Equal("sin(√((9g-6)/9*π+1))^(2+1)", edit.Insert(edit.Caret, 0, "+1").Text);
            var outsideRoot = map.Move(piEnd, 1);
            edit = new FormulaEditSession(source, outsideRoot.Region, outsideRoot.TextIndex);
            Assert.Equal("sin(√((9g-6)/9*π)+1)^(2+1)", edit.Insert(edit.Caret, 0, "+1").Text);
        }

        [Theory]
        [InlineData("sin(a)", 4)][InlineData("cos(a)", 4)][InlineData("tan(a)", 4)]
        [InlineData("asin(a)", 5)][InlineData("arcsin(a)", 7)][InlineData("sinh(a)", 5)]
        [InlineData("ln(a)", 3)][InlineData("Abs(a)", 4)][InlineData("sqrt(a)", 5)]
        [InlineData("sin(a)^2", 4)][InlineData("sin(sqrt(a))", 4)]
        public void FunctionNamesAndFencesHaveNoIntermediateStops(string source, int argumentStart)
        {
            var formula = ExpressionPreview.Create(source, true).Formula;
            var layout = new TexLayoutEngine().Render(formula.Latex);
            var map = new FormulaCaretMap(source, formula, layout);
            Assert.True(map.Slots[0].IsRootStart);
            Assert.Equal(argumentStart, map.Slots[1].TextIndex);
            Assert.Equal(argumentStart, map.FromText(argumentStart - 1).TextIndex);
            var next = map.Move(map.Slots[0], 1);
            Assert.NotNull(next.Region);
            var clicked = map.FromGeometry(layout.HitTest(next.Geometry.X, next.Geometry.Top + next.Geometry.Height / 2));
            Assert.Equal(next.Geometry.Row, clicked.Geometry.Row);
            Assert.Equal(next.TextIndex, clicked.TextIndex);
        }

        [Theory]
        [InlineData(@"\pi", @"\pi ")]
        [InlineData(@"\alpha+\beta", @"\alpha + \beta ")]
        [InlineData(@"\alpha\beta", @"\alpha \, \beta ")]
        [InlineData(@"\sin\left(x\right)", @"\sin \, \left( x \right) ")]
        [InlineData(@"\operatorname{foo}\left(x\right)", @"\operatorname{foo} \; \left(x\right) ")]
        [InlineData(@"\frac{1}{\pi}", @"\frac { 1 } { \pi } ")]
        public void CommandTerminatorsAndMathSpacingDoNotAddCaretStops(string compact, string spaced)
        {
            var first = new TexLayoutEngine().Render(compact);
            var second = new TexLayoutEngine().Render(spaced);
            Assert.Equal(first.Carets.Count, second.Carets.Count);
            foreach (var row in second.Carets.GroupBy(c => c.Row))
                Assert.Equal(row.Count(), row.Select(c => new { c.X, c.Top, c.Height }).Distinct().Count());
        }

        [Theory]
        [InlineData(false)][InlineData(true)]
        public void TypingPiInsideAndOutsideAFractionPreservesTheSelectedScope(bool native)
        {
            const string source = "sqrt((9g-6)/9)";
            var formula = ExpressionPreview.Create(source, true).Formula;
            var map = new FormulaCaretMap(source, formula, new TexLayoutEngine().Render(formula.Latex));
            var outside = map.Slots.Single(s => s.FollowsFraction);
            var inside = map.Slots.Last(s => s.TextIndex == outside.TextIndex && !s.FollowsFraction);
            foreach (var slot in new[] { inside, outside })
            {
                var edit = new FormulaEditSession(source, slot.Region, slot.TextIndex, slot.FollowsFraction);
                var changed = native ? edit.ReplaceBuffer(edit.Buffer + "π", edit.Buffer.Length + 1) : edit.Insert(edit.Caret, 0, "π");
                Assert.Equal(slot == outside ? "sqrt((9g-6)/9*π)" : "sqrt((9g-6)/(9π))", changed.Text);
            }
        }
    }
}
