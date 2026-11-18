using System;
using System.Linq;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Input;
using NaturplayCalculator.Model;
using Xunit;

namespace NaturplayCalculator.Tests;

public sealed class StructureTemplatesTests
{
    [Theory]
    [InlineData(StructureTemplate.Fraction, "()/()", 1, 2)]
    [InlineData(StructureTemplate.Root, "sqrt()", 5, 1)]
    [InlineData(StructureTemplate.Power, "()^()", 1, 2)]
    [InlineData(StructureTemplate.Parentheses, "()", 1, 1)]
    public void EmptyTemplatesExposeRealEditableHoles(StructureTemplate kind, string expected, int caret, int holes)
    {
        var edit = StructureTemplates.Insert("", 0, 0, kind);
        Assert.Equal(expected, edit.Text);
        Assert.Equal(caret, edit.SelectionStart);
        Assert.Equal(0, edit.SelectionLength);
        Assert.Equal(edit.SelectionStart, edit.SlotStart);
        Assert.Equal(edit.SelectionLength, edit.SlotLength);
        var preview = ExpressionPreview.Create(edit.Text, true);
        Assert.False(preview.IsLimited);
        var layout = new TexLayoutEngine().Render(preview.Formula.Latex);
        Assert.Equal(holes, layout.Regions.Count(region => region.IsEmptySlot));
        Assert.DoesNotContain('□', edit.Text);
        Assert.DoesNotContain('?', edit.Text);
    }

    [Theory]
    [InlineData(StructureTemplate.Fraction, "(a+b)/()", 7, 0)]
    [InlineData(StructureTemplate.Power, "(a+b)^()", 7, 0)]
    [InlineData(StructureTemplate.Root, "sqrt(a+b)", 5, 3)]
    [InlineData(StructureTemplate.Parentheses, "(a+b)", 1, 3)]
    public void SelectionBecomesTheTemplateOperand(StructureTemplate kind, string expected, int selectionStart, int selectionLength)
    {
        var edit = StructureTemplates.Insert("a+b", 0, 3, kind);
        Assert.Equal(expected, edit.Text);
        Assert.Equal(selectionStart, edit.SelectionStart);
        Assert.Equal(selectionLength, edit.SelectionLength);
        Assert.False(ExpressionPreview.Create(edit.Text, true).IsLimited);
    }

    [Fact]
    public void FractionTargetsTheEmptyDenominatorAndEvaluatesAfterFillingIt()
    {
        var edit = StructureTemplates.Insert("2+4", 0, 3, StructureTemplate.Fraction);
        var filled = InputEditor.Insert(edit.Text, edit.SelectionStart, edit.SelectionLength, "3");
        Assert.Equal("(2+4)/(3)", filled.Text);
        Assert.Equal("2", new CalculationEngine().Evaluate(filled.Text).Value.Code);
    }

    [Fact]
    public void EmbeddedFractionPreservesTheSurroundingPower()
    {
        var edit = StructureTemplates.Insert("2^8+1", 2, 1, StructureTemplate.Fraction);
        var filled = InputEditor.Insert(edit.Text, edit.SelectionStart, edit.SelectionLength, "2");
        Assert.Equal("2^((8)/(2))+1", filled.Text);
        Assert.Equal("17", new CalculationEngine().Evaluate(filled.Text).Value.Code);
    }

    [Fact]
    public void EmbeddedPowerPreservesTheSurroundingFactorial()
    {
        var edit = StructureTemplates.Insert("2!", 0, 1, StructureTemplate.Power);
        var filled = InputEditor.Insert(edit.Text, edit.SelectionStart, edit.SelectionLength, "3");
        Assert.Equal("((2)^(3))!", filled.Text);
        Assert.Equal("40320", new CalculationEngine().Evaluate(filled.Text).Value.Code);
    }

    [Fact]
    public void MatrixStartsWithFourIndividuallyEditableCells()
    {
        var edit = StructureTemplates.Insert("", 0, 0, StructureTemplate.Matrix);
        Assert.Equal("(0,0,,0,0)", edit.Text);
        Assert.Equal(1, edit.SelectionStart);
        Assert.Equal(1, edit.SelectionLength);
        var preview = ExpressionPreview.Create(edit.Text);
        Assert.Equal(2, preview.Root.Rows);
        Assert.Equal(2, preview.Root.Columns);
        var filled = InputEditor.Insert(edit.Text, edit.SelectionStart, edit.SelectionLength, "7");
        Assert.Equal("(7,0,,0,0)", filled.Text);
        Assert.Equal("1", new CalculationEngine().Evaluate("rank(" + filled.Text + ")").Value.Code);
    }

    [Fact]
    public void MatrixKeepsSelectedExpressionInFirstCellAndSelectsSecondCell()
    {
        var edit = StructureTemplates.Insert("2+3", 0, 3, StructureTemplate.Matrix);
        Assert.Equal("((2+3),0,,0,0)", edit.Text);
        Assert.Equal("0", edit.Text.Substring(edit.SelectionStart, edit.SelectionLength));
        var filled = InputEditor.Insert(edit.Text, edit.SelectionStart, edit.SelectionLength, "4");
        Assert.Equal("((2+3),4,,0,0)", filled.Text);
        var matrix = new ExpressionParser().Parse(filled.Text).Root;
        Assert.Equal(2, matrix.Rows);
        Assert.Equal(2, matrix.Columns);
    }

    [Fact]
    public void LocalFormulaBufferInsertionDoesNotConsumeTheNeighbouringOperand()
    {
        const string source = "7/9+2";
        var formula = ExpressionPreview.Create(source, true).Formula;
        var region = formula.EditRegions.First(r => r.InputStart == 2 && r.InputLength == 1);
        var local = new FormulaEditSession(source, region, 2);
        var template = StructureTemplates.Insert(local.Buffer, 0, 1, StructureTemplate.Root);
        var applied = local.ReplaceBuffer(template.Text, template.SelectionStart);
        Assert.Equal("7/(sqrt(9))+2", applied.Text);
        Assert.Equal("sqrt(9)", local.Buffer);
        Assert.Equal(template.SelectionStart, applied.Caret - local.ContentStart);
    }

    [Fact]
    public void SelectionBoundsAreClampedLikeOrdinaryInputEdits()
    {
        Assert.Equal("sqrt(12)", StructureTemplates.Insert("12", -5, int.MaxValue, StructureTemplate.Root).Text);
        Assert.Equal("12sqrt()", StructureTemplates.Insert("12", int.MaxValue, -5, StructureTemplate.Root).Text);
        Assert.Throws<ArgumentNullException>(() => StructureTemplates.Insert(null!, 0, 0, StructureTemplate.Root));
        Assert.Throws<ArgumentOutOfRangeException>(() => StructureTemplates.Insert("", 0, 0, (StructureTemplate)99));
    }
}
