using System.Threading.Tasks;
using NaturplayCalculator.History;
using NaturplayCalculator.Input;
using NaturplayCalculator.Model;
using NaturplayCalculator.Presentation;
using Xunit;

namespace NaturplayCalculator.Tests;

public sealed class KeypadLiteralTests
{
    private static InputEdit Keys(params string[] keys)
    {
        var edit = InputEditor.Insert("", 0, 0, "");
        foreach (string key in keys) edit = InputEditor.InsertKey(edit.Text, edit.Caret, 0, key);
        return edit;
    }

    [Fact]
    public void DigitKeysBuildOneNumberAndASecondDecimalPointDoesNothing()
    {
        var edit = Keys("1", "2", ".", "3", ".", "4");
        Assert.Equal("12.34", edit.Text);
        Assert.Equal(5, edit.Caret);
    }

    [Fact]
    public void ALeadingDecimalStartsWithZero()
    {
        var edit = Keys(".", "5", "+", ".", "2");
        Assert.Equal("0.5+0.2", edit.Text);
        Assert.Equal("0.7", new CalculationEngine().Evaluate(edit.Text).Value.Code);
    }

    [Fact]
    public void EIsAnAtomicConstantRatherThanAnExponentMarker()
    {
        var edit = Keys("2", "e", "3");
        Assert.Equal("2 e 3", edit.Text);
        Assert.Equal(5, edit.Caret);
        // Full text input deliberately continues to accept scientific notation.
        Assert.Equal("2e3", InputEditor.Insert("2e", 2, 0, "3").Text);
        Assert.Equal("2000", new CalculationEngine().Evaluate("2e3").Value.Code);
    }

    [Fact]
    public void ConstantsAndNumbersAlwaysKeepTheirOwnBoundaries()
    {
        Assert.Equal("2 π 3 Ans i", Keys("2", "π", "3", "Ans", "i").Text);
        Assert.Equal("e 0.5", Keys("e", ".", "5").Text);
        Assert.Equal("i i", Keys("i", "i").Text);
        Assert.Equal("-1", new CalculationEngine().Evaluate(Keys("i", "i").Text).Value.Code);
    }

    [Theory]
    [InlineData("(2+3)", "4", "(2+3) 4")]
    [InlineData("sin(3)", "2", "sin(3) 2")]
    [InlineData("sqrt(9)", "π", "sqrt(9) π")]
    [InlineData("3!", "2", "3! 2")]
    [InlineData("Ans", ".", "Ans 0.")]
    [InlineData("2 nCr ", "3", "2 nCr 3")]
    [InlineData("sin", "3", "sin3")]
    [InlineData("2*[note]", "π", "2*[note]π")]
    [InlineData("2[note]", "π", "2[note] π")]
    public void LiteralsRespectThePreviousToken(string source, string key, string expected)
    {
        Assert.Equal(expected, InputEditor.InsertKey(source, source.Length, 0, key).Text);
    }

    [Theory]
    [InlineData("1234", 2, 0, "5", "12534", 3)]
    [InlineData("1234", 2, 0, "e", "12 e 34", 4)]
    [InlineData("1234", 1, 2, "e", "1 e 4", 3)]
    [InlineData("1234", 1, 2, "5", "154", 2)]
    [InlineData("12+34", 3, 2, "π", "12+π", 4)]
    [InlineData("12.34", 2, 1, ".", "12.34", 3)]
    [InlineData("12.34", 1, 1, ".", "12.34", 1)]
    [InlineData("34", 0, 0, ".", "0.34", 2)]
    [InlineData("sin(3)", 0, 0, "2", "2 sin(3)", 1)]
    [InlineData("(3)", 0, 0, "π", "π (3)", 1)]
    [InlineData("sin()", 4, 0, "2", "sin(2)", 5)]
    [InlineData("2e3", 3, 0, "4", "2e34", 4)]
    [InlineData("2e3", 3, 0, ".", "2e3", 3)]
    public void CaretAndSelectionBoundBothSidesOfTheInsertion(string source, int caret, int length,
        string key, string expected, int expectedCaret)
    {
        var edit = InputEditor.InsertKey(source, caret, length, key);
        Assert.Equal(expected, edit.Text);
        Assert.Equal(expectedCaret, edit.Caret);
    }

    [Fact]
    public void ASeparatorAddedOnTheRightDoesNotStealTheTypingPosition()
    {
        var first = InputEditor.InsertKey("sin(3)", 0, 0, "2");
        var second = InputEditor.InsertKey(first.Text, first.Caret, 0, "4");
        Assert.Equal("24 sin(3)", second.Text);
        Assert.Equal(2, second.Caret);
    }

    [Fact]
    public void DelimitersAndCommentContentsRemainOrdinaryText()
    {
        Assert.Equal("(2)", InputEditor.InsertKey("(2", 2, 0, ")").Text);
        Assert.Equal("(1,2)", InputEditor.InsertKey("(12)", 2, 0, ",").Text);
        Assert.Equal("2[note3]", InputEditor.InsertKey("2[note]", 6, 0, "3").Text);
    }

    [Theory]
    [InlineData("+2", true)]
    [InlineData("-2", true)]
    [InlineData("*2", true)]
    [InlineData("/2", true)]
    [InlineData("÷2", true)]
    [InlineData("×2", true)]
    [InlineData("^2", true)]
    [InlineData("2", false)]
    [InlineData("sin(", false)]
    [InlineData("Ans", false)]
    public void TypedInputDistinguishesAnswerContinuationFromANewExpression(string added, bool continues)
    {
        const string source = "2+3";
        var edit = InputEditor.ApplyTypedText(source, source + added, source.Length + added.Length, true);
        Assert.True(edit.StartsNewCalculation);
        Assert.Equal(continues, edit.ContinuesFromAnswer);
        Assert.Equal(continues ? "Ans" + added : added, edit.Text);
        Assert.False(InputEditor.ApplyTypedText(source, source + added, source.Length + added.Length, false).ContinuesFromAnswer);
    }

    [Theory]
    [InlineData("+2", "12")]
    [InlineData("Ans+2", "101")]
    public async Task TypedAnswerContinuationUsesTheVisibleHistoricalResultOnlyWhenRequested(string added, string expected)
    {
        using var workspace = new CalculatorWorkspace(new CalculatorSession { Answer = new MathValue(99) });
        workspace.Recall(new HistoryRecord { Input = "Ans*2", PreviousAnswer = new MathValue(5), Answer = new MathValue(10) });
        string source = workspace.Session.Draft;
        var edit = InputEditor.ApplyTypedText(source, source + added, source.Length + added.Length, true);
        workspace.Edit(edit.Text, edit.ContinuesFromAnswer ? WorkspaceEditContext.DisplayedResult : WorkspaceEditContext.NewCalculation);
        Assert.True(await workspace.EvaluateAsync());
        Assert.Equal(expected, workspace.Result.Code);
    }
}
