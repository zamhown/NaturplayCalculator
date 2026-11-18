using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Input;
using NaturplayCalculator.Model;
using Xunit;

namespace NaturplayCalculator.Tests;

public sealed class StructureTemplateScopeTests
{
    [Fact]
    public void RootInsertedAfterFractionStaysOutsideItsDenominatorAndFocusesItsOwnArgument()
    {
        const string source = "1/2";
        var formula = ExpressionPreview.Create(source, true).Formula;
        var layout = new TexLayoutEngine().Render(formula.Latex);
        var outside = new FormulaCaretMap(source, formula, layout).FromText(source.Length);
        Assert.True(outside.IsRootEnd);
        Assert.True(outside.FollowsFraction);
        var session = new FormulaEditSession(source, outside.Region, outside.TextIndex, outside.FollowsFraction);
        int start = session.Caret - session.ContentStart;
        const int length = 0;
        var template = StructureTemplates.Insert(session.Buffer, start, length, StructureTemplate.Root);

        // Replacement sees the end of the insertion, while the template's
        // interior caret is applied separately after fraction-boundary handling.
        int insertedLength = template.Text.Length - session.Buffer.Length + length;
        var applied = session.ReplaceBuffer(template.Text, start + insertedLength);
        int insertedBoundaryLength = session.Buffer.Length - template.Text.Length;
        int selectionStart = session.ContentStart + template.SelectionStart + insertedBoundaryLength;
        int slotStart = session.ContentStart + template.SlotStart + insertedBoundaryLength;
        Assert.Equal("1/2*sqrt()", applied.Text);
        Assert.Equal(1, insertedBoundaryLength);
        Assert.Equal(applied.Text.IndexOf(')'), selectionStart);

        var insertedFormula = ExpressionPreview.Create(applied.Text, true).Formula;
        var insertedLayout = new TexLayoutEngine().Render(insertedFormula.Latex);
        var argument = new FormulaCaretMap(applied.Text, insertedFormula, insertedLayout)
            .AfterEdit(selectionStart, slotStart, slotStart + template.SlotLength);
        var argumentSession = new FormulaEditSession(applied.Text, argument.Region, argument.TextIndex, argument.FollowsFraction);
        Assert.Equal("", argumentSession.Buffer);
        var filled = argumentSession.Insert(argumentSession.Caret, template.SelectionLength, "9");
        Assert.Equal("1/2*sqrt(9)", filled.Text);
        Assert.Equal("1.5", new CalculationEngine().Evaluate(filled.Text).Value.Code);
    }
}
