using System;
using System.Collections.Generic;
using System.Linq;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Input;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class FormulaEditingSequenceTests
    {
        // Reproduce the host's full edit / render / map / native-buffer rebind cycle,
        // not just consecutive calls on one unchanged FormulaEditSession.
        private sealed class Editor
        {
            public string Source;
            public TexRenderResult Layout;
            public FormulaCaretMap Map;
            public FormulaCaretSlot Caret;
            public FormulaEditSession Session;
            public readonly List<string> Trace = new List<string>();
            public Editor(string source) { Source = source; Render(); }
            private void Render()
            {
                var formula = ExpressionPreview.Create(Source, true).Formula;
                Layout = new TexLayoutEngine().Render(formula.Latex);
                Map = new FormulaCaretMap(Source, formula, Layout);
            }
            public void Click(string glyph, int occurrence = 0)
            {
                var bounds = Layout.Commands.Where(c => c.Text == glyph).ElementAt(occurrence).Bounds;
                Select(Map.FromGeometry(Layout.HitTest(bounds.Right - .001, bounds.Y + bounds.Height / 2)));
            }
            public void Select(FormulaCaretSlot slot)
            {
                Caret = slot;
                Session = new FormulaEditSession(Source, slot.Region, slot.TextIndex, slot.FollowsFraction);
                Trace.Add(Source + " | " + Session.Buffer + " @ " + slot.TextIndex + " region " + slot.Region?.InputStart + ":" + slot.Region?.InputLength);
            }
            public void Type(string text, bool native)
            {
                var candidate = InputEditor.Insert(Session.Buffer, Caret.TextIndex - Session.ContentStart, 0, text);
                var edit = native ? Session.ReplaceBuffer(candidate.Text, candidate.Caret) : Session.Insert(Caret.TextIndex, 0, text);
                var exit = edit.ExitsFormulaScope ? Map.Move(Map.RowBoundary(Caret, true), 1).Geometry : null;
                Source = edit.Text;
                Render();
                Select(exit == null ? Map.AfterEdit(edit.Caret, Session.ContentStart, Session.ContentEnd) : Map.FromGeometry(exit));
            }
            public void Backspace()
            {
                var edit = Session.Delete(Caret.TextIndex, 0, true);
                Source = edit.Text; Render();
                Select(Map.AfterEdit(edit.Caret, Session.ContentStart, Session.ContentEnd));
            }
        }

        [Theory]
        [InlineData(false)][InlineData(true)]
        public void ClosingAPredictedExponentDoesNotAppendAnExtraParenthesis(bool native)
        {
            var editor = new Editor("sin(√((9g-6)/9π))^(");
            editor.Select(editor.Map.Slots.First(s => s.Region != null && s.TextIndex == editor.Source.Length));
            editor.Type("2", native);
            editor.Type(")", native);
            Assert.Equal("sin(√((9g-6)/9π))^(2)", editor.Source);
            Assert.True(editor.Caret.IsRootEnd);
            Assert.NotNull(new ExpressionParser().Parse(editor.Source));
        }

        [Theory]
        [InlineData(false)][InlineData(true)]
        public void OpeningAGroupInTheDenominatorCannotConsumeTheParentsClosingDelimiter(bool native)
        {
            var editor = new Editor("7/9+2"); editor.Click("9");
            foreach (char c in "+(3+4)") editor.Type(c.ToString(), native);
            Assert.True(editor.Source == "7/(9+(3+4))+2", string.Join(Environment.NewLine, editor.Trace));
            Assert.Equal("7/(9+(3+4))+2", new ExpressionParser().Parse(editor.Source).Root.ToCode());
            editor.Type("*", native); editor.Type("2", native);
            Assert.Equal("7/(9+(3+4)*2)+2", editor.Source);
        }

        [Theory]
        [InlineData(false)][InlineData(true)]
        public void RepeatedEditsAtDifferentPositionsDoNotAccumulateDelimitersOrChangeMeaning(bool native)
        {
            const string source = "sin(√((9g-6)/9π))^(2)";
            var expected = new ExpressionParser().Parse(source).Root.Key;
            var editor = new Editor(source);
            for (int repeat = 0; repeat < 3; repeat++)
            {
                foreach (string glyph in new[] { "g", "6", "π", "2" })
                {
                    editor.Click(glyph); editor.Type("1", native);
                    Assert.NotNull(new ExpressionParser().Parse(editor.Source));
                    editor.Backspace();
                    Assert.Equal(expected, new ExpressionParser().Parse(editor.Source).Root.Key);
                }
                editor.Click("9", 1); editor.Type("1", native); editor.Backspace();
                Assert.Equal(expected, new ExpressionParser().Parse(editor.Source).Root.Key);
            }
            Assert.Equal(source.Length + 2, editor.Source.Length); // One necessary denominator pair, never one per edit.
        }

        [Theory]
        [InlineData("2^3", "3", "2^3+1")]
        [InlineData("2^(3)", "3", "2^(3)+1")]
        [InlineData("sqrt(9)", "9", "sqrt(9)+1")]
        [InlineData("sin(9)", "9", "sin(9)+1")]
        public void ClosingAScopeMovesOutBeforeFurtherTyping(string source, string glyph, string expected)
        {
            var editor = new Editor(source); editor.Click(glyph);
            editor.Type(")", true); editor.Type("+", true); editor.Type("1", true);
            Assert.Equal(expected, editor.Source);
            Assert.NotNull(new ExpressionParser().Parse(editor.Source));
        }

        [Fact]
        public void ConsecutiveClosingsLeaveNestedScopesOneAtATime()
        {
            var editor = new Editor("sin(sqrt(9))"); editor.Click("9");
            editor.Type(")", true); Assert.False(editor.Caret.IsRootEnd);
            editor.Type(")", true); Assert.True(editor.Caret.IsRootEnd);
            Assert.Equal("sin(sqrt(9))", editor.Source);
            editor.Type("+1", true); Assert.Equal("sin(sqrt(9))+1", editor.Source);
        }

        [Theory]
        [InlineData("+(3+4)", "7/(9+(3+4))+2")]
        [InlineData("+sqrt(4)", "7/(9+sqrt(4))+2")]
        [InlineData("^(2)", "7/(9^(2))+2")]
        public void PastingBalancedContentPreservesTheEnclosingSuffix(string text, string expected)
        {
            var editor = new Editor("7/9+2"); editor.Click("9"); editor.Type(text, true);
            Assert.Equal(expected, editor.Source);
            Assert.NotNull(new ExpressionParser().Parse(editor.Source));
        }

        [Theory]
        [InlineData("sqrt(", "sqrt()")]
        [InlineData("2^(", "2^()")]
        public void ClosingAnEmptyPredictedScopeCompletesExactlyOneDelimiter(string source, string expected)
        {
            var editor = new Editor(source);
            editor.Select(editor.Map.Slots.First(s => s.Region != null && s.TextIndex == source.Length));
            editor.Type(")", true);
            Assert.Equal(expected, editor.Source);
            Assert.True(editor.Caret.IsRootEnd);
            Assert.Single(editor.Layout.Regions, r => r.IsEmptySlot);
        }
    }
}
