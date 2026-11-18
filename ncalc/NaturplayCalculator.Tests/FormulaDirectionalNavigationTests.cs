using System;
using System.Linq;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Input;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class FormulaDirectionalNavigationTests
    {
        private static FormulaCaretMap Map(string source)
        {
            var formula = ExpressionPreview.Create(source, true, true).Formula;
            return new FormulaCaretMap(source, formula, new TexLayoutEngine().Render(formula.Latex));
        }

        private static string Buffer(string source, FormulaCaretSlot slot) =>
            new FormulaEditSession(source, slot.Region, slot.TextIndex).Buffer;

        private static FormulaCaretSlot InBuffer(FormulaCaretMap map, string source, string buffer, bool end = false) =>
            (end ? map.Slots.Reverse() : map.Slots).First(s => s.Region != null && Buffer(source, s) == buffer);

        [Fact]
        public void FractionMovesToNearestPositionInTheOtherOperand()
        {
            const string source = "123/(45+6)";
            var map = Map(source);
            var numerator = map.Slots.First(s => s.Region != null && s.TextIndex == 2 && Buffer(source, s) == "123");
            var denominator = map.MoveVertical(numerator, 1);
            Assert.Equal("45+6", Buffer(source, denominator));
            var expected = map.Slots.Where(s => s.Region == denominator.Region)
                .OrderBy(s => Math.Abs(s.Geometry.X - numerator.Geometry.X)).First();
            Assert.Same(expected, denominator);
            Assert.Equal("123", Buffer(source, map.MoveVertical(denominator, -1)));
        }

        [Theory]
        [InlineData("7+1/2", -1)]
        [InlineData("7+1/2", 1)]
        [InlineData("7+2^3", -1)]
        [InlineData("7+2^3", 1)]
        public void OrdinaryTermDoesNotJumpIntoUnrelatedTemplates(string source, int direction)
        {
            var map = Map(source);
            var beforeFirstTerm = map.Slots.First(s => s.IsRootStart);
            Assert.Same(beforeFirstTerm, map.MoveVertical(beforeFirstTerm, direction));
        }

        [Theory]
        [InlineData("1/2", -1)]
        [InlineData("1/2", 1)]
        [InlineData("2^3", -1)]
        public void OutsideOfCompletedTemplateStaysOutside(string source, int direction)
        {
            var map = Map(source);
            var outside = map.Slots.Last(s => s.IsRootEnd);
            Assert.Same(outside, map.MoveVertical(outside, direction));
        }

        [Fact]
        public void NestedFractionVerticalMoveTargetsSiblingOuterRow()
        {
            const string source = "12/(3/4+5)";
            var map = Map(source);
            var top = InBuffer(map, source, "12", true);
            var bottom = map.MoveVertical(top, 1);
            Assert.Equal("3/4+5", Buffer(source, bottom));
            Assert.NotEqual("3", Buffer(source, bottom));
            Assert.NotEqual("4", Buffer(source, bottom));
        }

        [Fact]
        public void NestedFractionUsesItsOwnOperandsBeforeTheEnclosingFraction()
        {
            const string source = "12/(3/4+5)";
            var map = Map(source);
            var top = InBuffer(map, source, "3");
            Assert.Equal("4", Buffer(source, map.MoveVertical(top, 1)));
            Assert.Equal("3", Buffer(source, map.MoveVertical(InBuffer(map, source, "4"), -1)));
        }

        [Fact]
        public void PowerMovesBetweenItsOwnBaseAndExponent()
        {
            const string source = "2^34+5^6";
            var map = Map(source);
            var exponent = InBuffer(map, source, "34");
            var basis = map.MoveVertical(exponent, 1);
            Assert.InRange(basis.TextIndex, 0, 1);
            Assert.Equal("34", Buffer(source, map.MoveVertical(basis, -1)));
            Assert.Same(exponent, map.MoveVertical(exponent, -1));
        }

        [Fact]
        public void IndexedRootMovesBetweenDegreeAndRadicand()
        {
            const string source = "8yroot3+7";
            var map = Map(source);
            var degree = InBuffer(map, source, "3");
            var radicand = map.MoveVertical(degree, 1);
            Assert.Equal("8", Buffer(source, radicand));
            Assert.Equal("3", Buffer(source, map.MoveVertical(radicand, -1)));
        }

        [Fact]
        public void EmptyFractionStillMovesBetweenItsSlots()
        {
            const string source = "()/()";
            var map = Map(source);
            var empty = map.Slots.Where(s => s.Region != null && Buffer(source, s) == "").ToArray();
            Assert.NotEmpty(empty);
            var numerator = empty.OrderBy(s => s.Geometry.Top).First();
            var denominator = map.MoveVertical(numerator, 1);
            Assert.True(denominator.Geometry.Top > numerator.Geometry.Top);
            Assert.Equal("", Buffer(source, denominator));
            Assert.Equal(numerator.Geometry.Row, map.MoveVertical(denominator, -1).Geometry.Row);
        }

        [Theory]
        [InlineData("1", "333")]
        [InlineData("2", "4")]
        public void MatrixVerticalMoveKeepsTheColumn(string upper, string lower)
        {
            const string source = "(1,2,,333,4)";
            var map = Map(source);
            var top = InBuffer(map, source, upper, true);
            var bottom = map.MoveVertical(top, 1);
            Assert.Equal(lower, Buffer(source, bottom));
            Assert.Equal(upper, Buffer(source, map.MoveVertical(bottom, -1)));
            Assert.Same(top, map.MoveVertical(top, -1));
            Assert.Same(bottom, map.MoveVertical(bottom, 1));
        }

        [Theory]
        [InlineData("1/2")]
        [InlineData("sqrt(12)")]
        [InlineData("2^(3+4)")]
        [InlineData("(1,2,,3,4)")]
        public void HorizontalKeysEnterAndLeaveTemplatesReversibly(string source)
        {
            var map = Map(source);
            var current = map.Slots.First(s => s.IsRootStart);
            for (int i = 1; i < map.Slots.Count; i++)
            {
                var previous = current;
                current = map.Move(current, 1);
                Assert.Same(previous, map.Move(current, -1));
            }
            Assert.True(current.IsRootEnd);
            Assert.Same(current, map.Move(current, 1));
        }
    }
}
