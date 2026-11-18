using System.Linq;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Input;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class FormulaCaretMapTests
    {
        private static FormulaCaretMap Map(string source)
        {
            var formula = ExpressionPreview.Create(source, true).Formula;
            return new FormulaCaretMap(source, formula, new TexLayoutEngine().Render(formula.Latex));
        }
        [Fact]
        public void FractionHasEightStructuralSlotsAndANonBijectiveTextMapping()
        {
            var map = Map("(9-6)/9");
            Assert.Equal(8, map.Slots.Count);
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 6, 7, 7 }, map.Slots.Select(s => s.TextIndex));
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 4, 5, 7 }, Enumerable.Range(0, 8).Select(i => map.Slots.ToList().IndexOf(map.FromText(i))));
            Assert.NotEqual(map.Slots[6].Geometry.Row, map.Slots[7].Geometry.Row);
            Assert.NotNull(map.Slots[6].Region); Assert.Null(map.Slots[7].Region);
        }
        [Fact]
        public void FormulaNavigationVisitsEverySlotInBothDirectionsWithoutARoundTripThroughText()
        {
            var map = Map("(9-6)/9"); var caret = map.Slots[0];
            for (int i = 0; i < map.Slots.Count; i++) { Assert.Same(map.Slots[i], caret); caret = map.Move(caret, 1); }
            for (int i = map.Slots.Count - 1; i >= 0; i--) { Assert.Same(map.Slots[i], caret); caret = map.Move(caret, -1); }
        }
        [Fact]
        public void SameTextIndexCanInsertInsideOrOutsideTheFraction()
        {
            const string input = "(9-6)/9"; var map = Map(input);
            var inside = new FormulaEditSession(input, map.Slots[6].Region, map.Slots[6].TextIndex);
            var outside = new FormulaEditSession(input, map.Slots[7].Region, map.Slots[7].TextIndex);
            Assert.Equal("(9-6)/(9+1)", inside.Insert(inside.Caret, 0, "+1").Text);
            Assert.Equal("(9-6)/9+1", outside.Insert(outside.Caret, 0, "+1").Text);
        }
        [Fact]
        public void EditingAndRerenderingPreserveTheDenominatorAffinity()
        {
            var map = Map("(9-6)/9");
            var context = new FormulaEditSession("(9-6)/9", map.Slots[6].Region, 7);
            var edit = context.Insert(context.Caret, 0, "+1");
            var next = Map(edit.Text);
            var caret = next.AfterEdit(edit.Caret, context.ContentStart, context.ContentEnd);
            Assert.NotNull(caret.Region);
            Assert.Equal(edit.Caret, caret.TextIndex);
            Assert.False(caret.IsRootEnd);
        }
        [Theory]
        [InlineData("1/2")][InlineData("sqrt(12)")][InlineData("2^3")]
        [InlineData("(1,2,,3,4)")][InlineData("sin(a)")][InlineData("1/(2/3)")]
        public void StructuralAndTextSlotsNeedNotHaveTheSameCount(string input)
        {
            var map = Map(input);
            Assert.NotEqual(input.Length + 1, map.Slots.Count);
            Assert.All(map.Slots, s => Assert.InRange(s.TextIndex, 0, input.Length));
            for (int i = 0; i <= input.Length; i++) Assert.Contains(map.FromText(i), map.Slots);
            foreach (var slot in map.Slots) Assert.Same(slot, map.FromGeometry(slot.Geometry));
        }
        [Fact]
        public void HomeEndAndVerticalMovesStayInTheFormulaCoordinateSpace()
        {
            var map = Map("(9-6)/9");
            Assert.Same(map.Slots[1], map.RowBoundary(map.Slots[3], false));
            Assert.Same(map.Slots[4], map.RowBoundary(map.Slots[3], true));
            Assert.Equal(map.Slots[5].Geometry.Row, map.MoveVertical(map.Slots[3], 1).Geometry.Row);
        }
    }
}
