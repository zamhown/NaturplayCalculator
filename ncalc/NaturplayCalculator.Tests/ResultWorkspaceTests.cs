using System.Linq;
using System.Threading.Tasks;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.History;
using NaturplayCalculator.Model;
using NaturplayCalculator.Presentation;
using Xunit;

namespace NaturplayCalculator.Tests;

public sealed class ResultWorkspaceTests
{
    [Theory]
    [InlineData(CalculationMode.Numeric)]
    [InlineData(CalculationMode.Symbolic)]
    public async Task RepeatedEqualsUsesTheLatestAnswerAndCapturesEachIteration(CalculationMode mode)
    {
        using var workspace = new CalculatorWorkspace();
        workspace.SetMode(mode);
        workspace.Edit("2");
        Assert.True(await workspace.EvaluateAsync());
        workspace.Edit("Ans+1");
        for (int expected = 3; expected <= 5; expected++)
        {
            Assert.True(await workspace.EvaluateAsync(), workspace.Error);
            Assert.Equal(expected.ToString(), workspace.Result.Code);
            Assert.Equal((expected - 1).ToString(), workspace.Session.DraftPreviousAnswer.Code);
            Assert.Equal(expected.ToString(), workspace.Session.Answer.Code);
        }
        Assert.Equal(new[] { "2", "3", "4", "5" }, workspace.Session.History.Select(record => record.Answer.Code));
        Assert.Equal(new[] { "0", "2", "3", "4" }, workspace.Session.History.Select(record => record.PreviousAnswer.Code));
    }

    [Fact]
    public async Task EditingAndReplayingAnIterationKeepItsOriginalAnswerUntilItHasBeenEvaluated()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.Edit("2");
        await workspace.EvaluateAsync();
        workspace.Edit("Ans+1");
        await workspace.EvaluateAsync();
        var firstIteration = workspace.Session.History.Last();
        await workspace.EvaluateAsync();
        await workspace.EvaluateAsync();
        Assert.Equal("5", workspace.Result.Code);

        workspace.Recall(firstIteration);
        Assert.Equal("5", workspace.Session.Answer.Code);
        Assert.True(await workspace.EvaluateAsync());
        Assert.Equal("3", workspace.Result.Code);
        Assert.True(await workspace.EvaluateAsync());
        Assert.Equal("4", workspace.Result.Code);

        workspace.Edit("Ans+2", WorkspaceEditContext.Preserve);
        Assert.Equal("3", workspace.Session.DraftPreviousAnswer.Code);
        Assert.True(await workspace.EvaluateAsync());
        Assert.Equal("5", workspace.Result.Code);
    }

    [Fact]
    public async Task UndoAndRedoRestoreRepeatBehaviorWithoutRewindingGlobalAnswer()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.Edit("2");
        await workspace.EvaluateAsync();
        var older = workspace.Session.History.Last();
        workspace.Edit("Ans+1");
        await workspace.EvaluateAsync();
        workspace.Recall(older);
        Assert.True(workspace.Undo());
        Assert.Equal("3", workspace.Result.Code);
        Assert.True(await workspace.EvaluateAsync());
        Assert.Equal("4", workspace.Result.Code);
        Assert.True(workspace.Redo());
        Assert.Equal("2", workspace.Result.Code);
        Assert.Equal("4", workspace.Session.Answer.Code);
        Assert.True(await workspace.EvaluateAsync());
        Assert.Equal("2", workspace.Result.Code);
    }

    [Fact]
    public async Task SavedLiveIterationContinuesAfterRestoringTheSession()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.Edit("2");
        await workspace.EvaluateAsync();
        workspace.Edit("Ans+1");
        await workspace.EvaluateAsync();
        using var restored = new CalculatorWorkspace(SessionCodec.Read(workspace.Snapshot()));
        Assert.True(await restored.EvaluateAsync());
        Assert.Equal("4", restored.Result.Code);
        Assert.Equal("3", restored.Session.DraftPreviousAnswer.Code);
    }

    [Fact]
    public async Task DecimalToggleChangesOnlyTheVisibleValueAndDoesNotConsumeAnUndoStep()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.SetMode(CalculationMode.Symbolic);
        workspace.Edit("1/3");
        Assert.True(await workspace.EvaluateAsync());
        string saved = workspace.Snapshot();
        var content = workspace.Content;
        var answer = workspace.Session.Answer;
        var exact = workspace.Result;

        Assert.True(workspace.CanToggleResultFormat);
        Assert.False(workspace.IsDecimalResult);
        Assert.True(workspace.ToggleResultFormat());
        Assert.True(workspace.IsDecimalResult);
        Assert.Equal(1d / 3d, workspace.DisplayResult.Cmx.Re);
        Assert.Same(exact, workspace.Result);
        Assert.Same(answer, workspace.Session.Answer);
        Assert.Same(content, workspace.Content);
        Assert.Equal(saved, workspace.Snapshot());
        Assert.Equal(CalculationMode.Symbolic, workspace.Session.Mode);
        Assert.True(workspace.ToggleResultFormat());
        Assert.Same(exact, workspace.DisplayResult);
        Assert.True(workspace.ToggleResultFormat());

        workspace.Edit("Ans*3", WorkspaceEditContext.DisplayedResult);
        Assert.Equal("1/3", workspace.Session.DraftPreviousAnswer.Code);
        Assert.Null(workspace.DisplayResult);
        Assert.True(workspace.Undo());
        Assert.True(workspace.IsDecimalResult);
        Assert.Equal(1d / 3d, workspace.DisplayResult.Cmx.Re);
        Assert.True(workspace.Redo());
        Assert.False(workspace.IsDecimalResult);
        Assert.True(await workspace.EvaluateAsync());
        Assert.Equal("1", workspace.Result.Code);
        Assert.False(workspace.CanToggleResultFormat);
        Assert.Equal(2, workspace.Session.History.Count);
    }

    [Fact]
    public async Task DecimalToggleDoesNotChangeTheAnsUsedForRepeatedEquals()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.SetMode(CalculationMode.Symbolic);
        workspace.Edit("1/3");
        await workspace.EvaluateAsync();
        workspace.Edit("Ans+1/3");
        await workspace.EvaluateAsync();
        Assert.True(workspace.ToggleResultFormat());
        Assert.True(await workspace.EvaluateAsync());
        Assert.Equal("1", workspace.Result.Code);
        Assert.Equal("2/3", workspace.Session.DraftPreviousAnswer.Code);
        Assert.False(workspace.IsDecimalResult);
    }

    [Fact]
    public async Task RecalledResultCanBeFormattedWithoutRecalculatingOrReplacingTheCurrentAnswer()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.SetMode(CalculationMode.Symbolic);
        workspace.SetRadians(false);
        workspace.Edit("sin(30)");
        await workspace.EvaluateAsync();
        var half = workspace.Session.History.Last();
        workspace.Edit("99");
        await workspace.EvaluateAsync();
        workspace.Recall(half);
        var saved = workspace.Snapshot();
        Assert.True(workspace.ToggleResultFormat());
        Assert.Equal("0.5", workspace.DisplayResult.Code);
        Assert.Equal("1/2", workspace.Result.Code);
        Assert.Equal("99", workspace.Session.Answer.Code);
        Assert.Null(workspace.Content);
        Assert.Equal(saved, workspace.Snapshot());
    }

    [Fact]
    public void FormattingPreservesConditionsAndDoesNotBindUnknownVariables()
    {
        var exact = CalculationValue.FromSymbolic(new ExpressionParser().Parse("sqrt(2)").Root, new[] { "x!=0" });
        Assert.True(ResultFormatting.TryCreateDecimal(exact, true, out var approximation));
        Assert.Equal(System.Math.Sqrt(2), approximation.Cmx.Re);
        Assert.Equal(exact.Conditions, approximation.Conditions);
        var variable = CalculationValue.FromSymbolic(ExpressionNode.Symbol("x"));
        Assert.False(ResultFormatting.TryCreateDecimal(variable, true, out var unavailable));
        Assert.Null(unavailable);
        Assert.False(ResultFormatting.TryCreateDecimal(new MathValue(0.3333333333333333), true, out unavailable));
        Assert.Null(unavailable);
    }

    [Fact]
    public async Task ErrorLocationClearsOnEditingAndReturnsWithUndo()
    {
        using var workspace = new CalculatorWorkspace();
        const string invalid = "12/(3-3)";
        var expected = Assert.Throws<MathException>(() => new CalculationEngine().Evaluate(invalid));
        workspace.Edit(invalid);
        Assert.False(await workspace.EvaluateAsync());
        Assert.NotNull(workspace.Error);
        Assert.InRange(workspace.ErrorStart, 0, invalid.Length);
        Assert.Equal(expected.SourceStart, workspace.ErrorStart);
        Assert.Equal(expected.SourceLength, workspace.ErrorLength);

        workspace.Edit("12/3", WorkspaceEditContext.Preserve);
        Assert.Null(workspace.Error);
        Assert.Equal(-1, workspace.ErrorStart);
        Assert.Equal(0, workspace.ErrorLength);
        Assert.True(workspace.Undo());
        Assert.NotNull(workspace.Error);
        Assert.Equal(expected.SourceStart, workspace.ErrorStart);
        Assert.Equal(expected.SourceLength, workspace.ErrorLength);
        Assert.True(workspace.Redo());
        Assert.Equal(-1, workspace.ErrorStart);
        Assert.True(await workspace.EvaluateAsync());
        Assert.Equal(-1, workspace.ErrorStart);
        Assert.Equal(0, workspace.ErrorLength);
    }
}
