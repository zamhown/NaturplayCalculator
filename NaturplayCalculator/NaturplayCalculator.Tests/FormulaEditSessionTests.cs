using System.Linq;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Input;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class FormulaEditSessionTests
    {
        private static FormulaEditSession Click(string source, string glyph, int occurrence = 0, bool after = true)
        {
            var formula = ExpressionPreview.Create(source, true).Formula;
            var layout = new TexLayoutEngine().Render(formula.Latex);
            var bounds = layout.Commands.Where(c => c.Text == glyph).ElementAt(occurrence).Bounds;
            var caret = layout.HitTest(after ? bounds.Right - .001 : bounds.X + .001, bounds.Y + bounds.Height / 2);
            return new FormulaEditSession(source, formula, caret.Offset);
        }
        [Theory]
        [InlineData("(9-6)/9", "9", 1, "(9-6)/(9+1)")]
        [InlineData("(9-6)/9", "6", 0, "(9-6+1)/9")]
        [InlineData("1/2+3", "2", 0, "1/(2+1)+3")]
        [InlineData("1/2+3", "1", 0, "(1+1)/2+3")]
        [InlineData("2^3", "3", 0, "2^(3+1)")]
        [InlineData("2^3", "2", 0, "(2+1)^3")]
        [InlineData("sqrt(9)", "9", 0, "sqrt(9+1)")]
        [InlineData("√9", "9", 0, "√(9+1)")]
        [InlineData("sin(9)", "9", 0, "sin(9+1)")]
        [InlineData("sin9", "9", 0, "sin(9+1)")]
        [InlineData("9!", "9", 0, "(9+1)!")]
        [InlineData("(1,2,,3,4)", "3", 0, "(1,2,,3+1,4)")]
        [InlineData("1/(2/3)", "3", 0, "1/(2/(3+1))")]
        [InlineData("sqrt(1/2)", "2", 0, "sqrt(1/(2+1))")]
        [InlineData("2+3", "3", 0, "2+3+1")]
        [InlineData("sqrt(9", "9", 0, "sqrt(9+1)")]
        public void TypingAnOperatorStaysInTheClickedContainer(string source, string glyph, int occurrence, string expected)
        {
            var session = Click(source, glyph, occurrence);
            Assert.Equal(source, session.Source); // Clicking alone never changes input.
            var plus = session.Insert(session.Caret, 0, "+");
            Assert.Equal(expected, session.Insert(plus.Caret, 0, "1").Text);
            Assert.False(ExpressionPreview.Create(session.Source, true).IsLimited);
        }
        [Fact]
        public void NativeCompositionReplacesOnlyItsBufferAndDoesNotReceiveGroupingCharacters()
        {
            var session = Click("(9-6)/9", "9", 1);
            Assert.Equal("9", session.Buffer);
            Assert.Equal("(9-6)/(9+a)", session.ReplaceBuffer("9+a", 3).Text);
            Assert.Equal("9+a", session.Buffer);
            Assert.Equal("(9-6)/(9+ab)", session.ReplaceBuffer("9+ab", 4).Text);
            Assert.Equal("(9-6)/(9+b)", session.ReplaceBuffer("9+b", 3).Text);
        }
        [Fact]
        public void DeletingTheLastDenominatorCharacterKeepsAnEditableHole()
        {
            var session = Click("7/9+2", "9");
            var deleted = session.Delete(session.Caret, 0, true);
            Assert.Equal("7/()+2", deleted.Text);
            Assert.Equal(deleted.Text, session.Delete(deleted.Caret, 0, true).Text);
            Assert.Equal(deleted.Text, session.Delete(deleted.Caret, 0, false).Text);
            Assert.Equal("7/(4)+2", session.Insert(deleted.Caret, 0, "4").Text);
        }
        [Fact]
        public void ReplacementCannotConsumeANeighbouringMatrixCell()
        {
            var session = Click("(1,2,,3,4)", "3");
            Assert.Equal("(1,2,,8+9,4)", session.Insert(session.ContentStart, 100, "8+9").Text);
        }
        [Fact]
        public void LinearInputKeepsOrdinaryOperatorPrecedence()
        {
            Assert.Equal("(9-6)/9+1", InputEditor.Insert("(9-6)/9", 7, 0, "+1").Text);
        }

        [Fact]
        public void NativeCompositionCanOpenAndCloseGroupsWithoutReceivingGeneratedDelimiters()
        {
            var session = Click("7/9+2", "9");
            foreach (string buffer in new[] { "9+(", "9+(a", "9+(ab", "9+(b", "9+(b)" })
            {
                session.ReplaceBuffer(buffer, buffer.Length);
                Assert.Equal(buffer, session.Buffer);
                Assert.EndsWith(")+2", session.Source);
                Assert.Equal(2, session.Source.Count(c => c == '('));
                Assert.Equal(2, session.Source.Count(c => c == ')'));
            }
            Assert.Equal("7/(9+(b))+2", session.Source);
        }

        [Theory]
        [InlineData("9+[note", "7/(9+[note])+2")]
        [InlineData("9+[note\\", "7/(9+[note\\\\])+2")]
        public void IncompleteLocalCommentsCannotSwallowTheEnclosingSuffix(string buffer, string expected)
        {
            var session = Click("7/9+2", "9");
            Assert.Equal(expected, session.ReplaceBuffer(buffer, buffer.Length).Text);
            Assert.Equal(buffer, session.Buffer);
        }
    }
}
