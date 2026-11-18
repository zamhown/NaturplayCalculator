using System.Linq;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Input;
using Xunit;

namespace NaturplayCalculator.Tests;

public sealed class ScientificDeletionTests
{
    private static FormulaEditSession Scope(string source, string buffer)
    {
        var region = ExpressionPreview.Create(source, true).Formula.EditRegions.First(r =>
        {
            FormulaEditSession.GetContentRange(source, r, out int start, out int end);
            return source.Substring(start, end - start) == buffer;
        });
        return new FormulaEditSession(source, region, region.InputStart + region.InputLength);
    }

    [Theory]
    [InlineData("sin(3)", 3, "(3)", 0)]
    [InlineData("arcsin(3)", 6, "(3)", 0)]
    [InlineData("2+ln(3)", 4, "2+(3)", 2)]
    [InlineData("12.34", 5, "12.3", 4)]
    [InlineData("Ans+2", 3, "+2", 0)]
    [InlineData("12[note sin]", 10, "12[note sn]", 9)]
    public void BackspaceDeletesOneCalculatorUnit(string source, int caret, string expected, int expectedCaret)
    {
        var edit = ScientificDeletion.Delete(source, caret);
        Assert.Equal(expected, edit.Text);
        Assert.Equal(expectedCaret, edit.Caret);
    }

    [Theory]
    [InlineData("sqrt()", 5, "", 0)]
    [InlineData("sqrt(", 5, "", 0)]
    [InlineData("sin()", 4, "", 0)]
    [InlineData("2^()", 3, "2", 1)]
    [InlineData("8/()", 3, "8", 1)]
    [InlineData("()/()", 1, "", 0)]
    [InlineData("()/()", 4, "", 0)]
    [InlineData("()/9", 1, "9", 0)]
    [InlineData("3+sin(sqrt())+4", 11, "3+sin()+4", 6)]
    [InlineData("sqrt(2^())+4", 8, "sqrt(2)+4", 6)]
    [InlineData("7/(8/())+2", 6, "7/(8)+2", 4)]
    public void EmptyTemplatesCollapseWithoutDeletingTheirParentOrLeftOperand(string source, int caret, string expected, int expectedCaret)
    {
        foreach (bool backward in new[] { true, false })
        {
            var edit = ScientificDeletion.Delete(source, caret, 0, backward);
            Assert.Equal(expected, edit.Text);
            Assert.Equal(expectedCaret, edit.Caret);
        }
    }

    [Fact]
    public void ForwardDeleteAfterAnEmptyTemplateDoesNotRemoveTheTemplateBehindTheCursor()
    {
        Assert.Equal("sqrt()2", ScientificDeletion.Delete("sqrt()+2", 6, 0, false).Text);
        Assert.Equal("+2", ScientificDeletion.Delete("sqrt()+2", 0, 0, false).Text);
    }

    [Fact]
    public void SelectionOfPartOfAFunctionNeverLeavesAnInvalidFunctionFragment()
    {
        var edit = ScientificDeletion.Delete("2+sin(3)", 3, 1);
        Assert.Equal("2+(3)", edit.Text);
        Assert.Equal(2, edit.Caret);
    }

    [Fact]
    public void DeletionAtTheStartOfANonemptyArgumentLeavesTheArgumentIntact()
    {
        const string source = "2+sin(34)+5";
        var session = Scope(source, "34");
        var edit = session.Delete(session.ContentStart, 0, true);
        Assert.Equal(source, edit.Text);
        Assert.Equal(2, edit.Caret);
        Assert.True(edit.ExitsFormulaScope);
    }

    [Fact]
    public void SequentialDeleteRemovesDigitsThenOnlyTheInnermostTemplate()
    {
        const string source = "sin(sqrt(12))+4";
        var session = Scope(source, "12");
        var edit = session.Delete(session.Caret, 0, true);
        Assert.Equal("sin(sqrt(1))+4", edit.Text);
        edit = session.Delete(edit.Caret, 0, true);
        Assert.Equal("sin(sqrt())+4", edit.Text);
        edit = session.Delete(edit.Caret, 0, true);
        Assert.Equal("sin()+4", edit.Text);
        Assert.Equal(4, edit.Caret);
        Assert.True(edit.ExitsFormulaScope);
    }

    [Theory]
    [InlineData("2[note ()]", "2[note )]", "2[note (]")]
    [InlineData("[sqrt()]", "[sqrt)]", "[sqrt(]")]
    [InlineData("2[note \\] sqrt()]", "2[note \\] sqrt)]", "2[note \\] sqrt(]")]
    [InlineData("2[note sqrt(", "2[note sqrt", "2[note sqrt(")]
    public void ParenthesesInsideCommentsAreOrdinaryCharacters(string source, string backwardExpected, string forwardExpected)
    {
        int caret = source.IndexOf('(') + 1;
        Assert.Equal(backwardExpected, ScientificDeletion.Delete(source, caret, backward: true).Text);
        Assert.Equal(forwardExpected, ScientificDeletion.Delete(source, caret, backward: false).Text);
    }

    [Theory]
    [InlineData("{\"sin\":\"sqrt()\"}")]
    [InlineData("[\"sin\",\"sqrt()\"]")]
    [InlineData("<sin>sqrt()</sin>")]
    public void DocumentsUsePlainTextDeletionForNamesTemplatesAndSelections(string source)
    {
        int name = source.IndexOf("sin", System.StringComparison.Ordinal);
        var backwards = ScientificDeletion.Delete(source, name + 3);
        Assert.Equal(source.Remove(name + 2, 1), backwards.Text);
        var selection = ScientificDeletion.Delete(source, name + 1, 1);
        Assert.Equal(source.Remove(name + 1, 1), selection.Text);
        int hole = source.IndexOf('(') + 1;
        Assert.Equal(source.Remove(hole - 1, 1), ScientificDeletion.Delete(source, hole).Text);
        Assert.Equal(source.Remove(hole, 1), ScientificDeletion.Delete(source, hole, backward: false).Text);
    }
}
