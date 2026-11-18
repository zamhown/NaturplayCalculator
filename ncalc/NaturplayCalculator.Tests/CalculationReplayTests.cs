using System.Threading.Tasks;
using NaturplayCalculator.Presentation;
using Xunit;

namespace NaturplayCalculator.Tests;

public sealed class CalculationReplayTests
{
    [Fact]
    public async Task SolvedUpSkipsCurrentResultAndDownReturnsWithoutChangingAnsOrHistory()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.Edit("2+2"); await workspace.EvaluateAsync();
        workspace.Edit("3+3"); await workspace.EvaluateAsync();
        var replay = new CalculationReplay();
        Assert.True(replay.Move(workspace, -1));
        Assert.Equal("2+2", workspace.Session.Draft);
        Assert.Equal("4", workspace.Result.Code);
        Assert.False(replay.Move(workspace, -1));
        Assert.True(replay.Move(workspace, 1));
        Assert.Equal("3+3", workspace.Session.Draft);
        Assert.False(replay.Move(workspace, 1));
        Assert.Equal("6", workspace.Session.Answer.Code);
        Assert.Equal(2, workspace.Session.History.Count);
    }

    [Fact]
    public async Task BlankDraftUpRecallsNewestAndDownRestoresBlankAndEditorState()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.Edit("7"); await workspace.EvaluateAsync();
        workspace.Edit("", WorkspaceEditContext.NewCalculation);
        object caret = new(); workspace.EditorState = caret;
        string before = workspace.Snapshot();
        var replay = new CalculationReplay();
        Assert.True(replay.Move(workspace, -1));
        Assert.Equal("7", workspace.Session.Draft);
        Assert.True(replay.Move(workspace, 1));
        Assert.Equal(before, workspace.Snapshot());
        Assert.Same(caret, workspace.EditorState);
    }

    [Fact]
    public async Task EditingARecalledExpressionEndsHistoryReplayAndPreservesItsAns()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.Edit("2"); await workspace.EvaluateAsync();
        workspace.Edit("Ans+3"); await workspace.EvaluateAsync();
        workspace.Edit("9"); await workspace.EvaluateAsync();
        var replay = new CalculationReplay();
        Assert.True(replay.Move(workspace, -1));
        workspace.Edit("Ans+4", WorkspaceEditContext.Preserve);
        Assert.False(replay.Move(workspace, 1));
        Assert.Equal("Ans+4", workspace.Session.Draft);
        Assert.Equal("2", workspace.Session.DraftPreviousAnswer.Code);
        await workspace.EvaluateAsync();
        Assert.Equal("6", workspace.Result.Code);
        Assert.True(replay.Move(workspace, -1));
        Assert.Equal("9", workspace.Session.Draft);
    }

    [Fact]
    public async Task UnfinishedInputNeverGetsOverwrittenByHistoryArrows()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.Edit("1"); await workspace.EvaluateAsync();
        workspace.Edit("sqrt(");
        var replay = new CalculationReplay();
        Assert.False(replay.Move(workspace, -1));
        Assert.Equal("sqrt(", workspace.Session.Draft);
    }
}
