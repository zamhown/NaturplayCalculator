using System;
using System.Linq;
using Naturplay.Tex;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class TexEditorTests
    {
        [Fact]
        public void FractionInsertionTypingAndUndoRedoRetainValidFormulaAndCaret()
        {
            var editor = new TexEditor("x+1");
            editor.SetSelection(0, 3); editor.InsertFraction();
            Assert.Equal(@"\frac{x+1}{}", editor.Latex);
            Assert.Equal(11, editor.Focus);
            editor.InsertLatex("2");
            Assert.Equal(@"\frac{x+1}{2}", editor.Latex);
            Assert.True(editor.Undo()); Assert.Equal(@"\frac{x+1}{}", editor.Latex); Assert.Equal(11, editor.Focus);
            Assert.True(editor.Undo()); Assert.Equal("x+1", editor.Latex); Assert.Equal(new TexSpan(0, 3), editor.Selection);
            Assert.True(editor.Redo()); Assert.True(editor.Redo()); Assert.Equal(@"\frac{x+1}{2}", editor.Latex);
            Assert.False(editor.Redo());
        }

        [Theory]
        [InlineData(@"\frac{a}{b}", "a", "b")]
        [InlineData(@"\frac{\sqrt{x}}{1+y}", "x", "y")]
        [InlineData(@"\begin{pmatrix}a&b\\c&d\end{pmatrix}", "a", "c")]
        [InlineData(@"\begin{pmatrix}a&b\\c&d\end{pmatrix}", "b", "d")]
        public void VerticalMovementUsesFormulaStructure(string latex, string upper, string lower)
        {
            var editor = new TexEditor(latex);
            var from = editor.Layout.Document.Descendants().Single(n => n.Kind == TexKind.Glyph && n.Text == upper);
            var to = editor.Layout.Document.Descendants().Single(n => n.Kind == TexKind.Glyph && n.Text == lower);
            editor.SetSelection(from.Span.Start, from.Span.Start); editor.MoveVertical(1);
            // A wider denominator may put the nearest horizontal caret before its
            // first glyph; require membership in the target row, not a raw index.
            var targetRow = editor.Layout.Carets.First(c => c.Offset == to.Span.Start).Row;
            Assert.True(targetRow.Contains(editor.Focus));
            Assert.True(editor.Caret.Top > editor.Layout.Carets.First(c => c.Offset == from.Span.Start).Top);
        }

        [Fact]
        public void ScriptsAndRootIndicesSupportVerticalNavigation()
        {
            var editor = new TexEditor("x_i^2");
            editor.SetSelection(0, 0); editor.MoveVertical(-1); Assert.InRange(editor.Focus, 4, 5);
            editor.MoveVertical(1); Assert.InRange(editor.Focus, 0, 1);
            editor.MoveVertical(1); Assert.InRange(editor.Focus, 2, 3);
            var root = new TexEditor(@"\sqrt[3]{x}");
            root.SetSelection(9, 9); root.MoveVertical(-1); Assert.InRange(root.Focus, 6, 7);
        }

        [Fact]
        public void HorizontalMovementNeverEntersCommandNamesOrSplitsUnicode()
        {
            var editor = new TexEditor(@"\alpha+😀");
            editor.MoveRight(); Assert.Equal(6, editor.Focus);
            editor.MoveRight(); Assert.Equal(7, editor.Focus);
            editor.MoveRight(); Assert.Equal(9, editor.Focus);
            editor.MoveLeft(); Assert.Equal(7, editor.Focus);
            editor.DeleteForward(); Assert.Equal(@"\alpha+", editor.Latex);
        }

        [Fact]
        public void EditingAtControlWordBoundariesDoesNotChangeCommandNames()
        {
            var editor = new TexEditor(@"\pi"); editor.MoveEnd(); editor.InsertLatex("x");
            Assert.Equal(@"\pi x", editor.Latex);
            editor = new TexEditor("x"); editor.InsertLatex(@"\pi"); Assert.Equal(@"\pi x", editor.Latex);
        }

        [Fact]
        public void InvalidEditsAreAtomicAndDoNotConsumeUndoHistory()
        {
            var editor = new TexEditor("x"); editor.MoveEnd();
            Assert.Throws<TexParseException>(() => editor.InsertLatex(@"\frac{x}"));
            Assert.Equal("x", editor.Latex); Assert.Equal(1, editor.Focus); Assert.False(editor.CanUndo);
            editor.InsertLatex("+1"); editor.Undo(); editor.InsertLatex("+2"); Assert.False(editor.CanRedo);
        }

        [Fact]
        public void WholeAtomDeletionAndCrossBranchSelectionPreserveSyntax()
        {
            var editor = new TexEditor(@"x+\sqrt{2}"); editor.MoveEnd(); editor.DeleteBackward();
            Assert.Equal("x+", editor.Latex); editor.Undo(); Assert.Equal(@"x+\sqrt{2}", editor.Latex);
            editor = new TexEditor(@"x+\frac{a}{b}+y"); editor.SetSelection(8, 12); editor.DeleteForward();
            Assert.Equal("x++y", editor.Latex);
            new TexParser().Parse(editor.Latex);
        }

        [Fact]
        public void EmptyContainersRemainEditableAndCanBeRemoved()
        {
            var editor = new TexEditor(); editor.InsertRoot(); editor.InsertLatex("a");
            Assert.Equal(@"\sqrt{a}", editor.Latex); editor.DeleteBackward();
            Assert.Equal(@"\sqrt{}", editor.Latex);
            editor.MoveRight(); editor.DeleteBackward(); Assert.Equal("", editor.Latex);
        }

        [Fact]
        public void ScriptsWrapTheCompletePreviousAtom()
        {
            var editor = new TexEditor(@"\frac{a}{b}"); editor.MoveEnd(); editor.InsertSuperscript(); editor.InsertLatex("2");
            Assert.Equal(@"{\frac{a}{b}}^{2}", editor.Latex);
            Assert.Single(editor.Layout.Document.Descendants(), n => n.Kind == TexKind.Scripts);
        }

        [Fact]
        public void HitTestingSelectionAndRowHomeEndUseDisplayCoordinates()
        {
            var editor = new TexEditor(@"\frac{ab}{cd}");
            var glyph = editor.Layout.Commands.Single(c => c.Text == "c");
            editor.HitTest(glyph.Bounds.X, glyph.Bounds.Y + glyph.Bounds.Height / 2);
            Assert.Equal(10, editor.Focus);
            editor.MoveEnd(true); Assert.Equal(new TexSpan(10, 2), editor.Selection);
            Assert.Equal(2, editor.Layout.SelectionBounds(editor.Selection).Count);
            editor.MoveHome(); Assert.Equal(10, editor.Focus); Assert.Equal(editor.Anchor, editor.Focus);
        }

        [Fact]
        public void EditingUnbracedArgumentsCannotStealNeighboringAtoms()
        {
            var editor = new TexEditor(@"\frac ab+y");
            editor.SetSelection(6, 6); editor.InsertLatex("1");
            Assert.Equal(@"\frac {1a}b+y", editor.Latex);
            var fraction = editor.Layout.Document.Descendants().Single(n => n.Kind == TexKind.Fraction);
            Assert.Equal("b", fraction.Children[1].Text);
            editor = new TexEditor("x^2+y"); editor.SetSelection(2, 2); editor.InsertLatex("3");
            Assert.Equal("x^{32}+y", editor.Latex);
            editor = new TexEditor(@"\sqrt x+y"); editor.SetSelection(7, 7); editor.DeleteBackward();
            Assert.Equal(@"\sqrt {}+y", editor.Latex);
        }

        [Fact]
        public void CaretAffinityDistinguishesExponentEndFromOuterRow()
        {
            var editor = new TexEditor("x^2+y"); editor.SetSelection(3, 3);
            double upper = editor.Caret.Top, innerHeight = editor.Caret.Height;
            var innerRow = editor.Caret.Row; editor.MoveRight();
            Assert.Equal(3, editor.Focus); Assert.NotEqual(innerRow, editor.Caret.Row);
            Assert.True(editor.Caret.Height > innerHeight); Assert.True(editor.Caret.Top <= upper);
            editor.InsertLatex("z"); Assert.Equal("x^2z+y", editor.Latex);
            editor.Undo(); Assert.NotEqual(innerRow, editor.Caret.Row); Assert.True(editor.Caret.Height > innerHeight);
            editor.MoveLeft(); Assert.Equal(3, editor.Focus); Assert.Equal(upper, editor.Caret.Top);
            Assert.Equal(innerRow, editor.Caret.Row); Assert.Equal(innerHeight, editor.Caret.Height);
            editor.InsertLatex("3"); Assert.Equal("x^{23}+y", editor.Latex);
        }

        [Fact]
        public void WrappingACrossBranchSelectionUsesTheCompleteSelectedStructure()
        {
            var editor = new TexEditor(@"x+\frac{a}{b}+y"); editor.SetSelection(8, 12); editor.InsertRoot();
            Assert.Equal(@"x+\sqrt{\frac{a}{b}}+y", editor.Latex);
        }

        [Fact]
        public void TemplatesInsideUnbracedScriptsRetainTheirOuterScope()
        {
            var editor = new TexEditor("x^2+y"); editor.SetSelection(3, 3); editor.InsertSuperscript(); editor.InsertLatex("3");
            Assert.Equal("x^{{2}^{3}}+y", editor.Latex);
            editor = new TexEditor(@"\frac ab+y"); editor.SetSelection(6, 7); editor.InsertRoot();
            Assert.Equal(@"\frac {\sqrt{a}}b+y", editor.Latex);
        }
    }
}
