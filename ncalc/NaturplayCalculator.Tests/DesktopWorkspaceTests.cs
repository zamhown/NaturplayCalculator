using System;
using System.IO;
using System.Threading.Tasks;
using NaturplayCalculator.History;
using NaturplayCalculator.Model;
using NaturplayCalculator.Presentation;
using Xunit;

namespace NaturplayCalculator.Tests;

public sealed class DesktopWorkspaceTests
{
    [Fact]
    public async Task EditingARecalledExpressionPreservesItsOriginalAnsThroughUndoAndRedo()
    {
        using var workspace = RecalledAnsWorkspace();
        workspace.Edit("Ans*3", WorkspaceEditContext.Preserve);
        Assert.Equal("5", workspace.Session.DraftPreviousAnswer.Code);
        Assert.Equal("99", workspace.Session.Answer.Code);
        Assert.True(await workspace.EvaluateAsync());
        Assert.Equal("15", workspace.Result.Code);

        Assert.True(workspace.Undo());
        Assert.Equal("Ans*2", workspace.Session.Draft);
        Assert.Equal("10", workspace.Result.Code);
        Assert.Equal("5", workspace.Session.DraftPreviousAnswer.Code);
        Assert.Equal("15", workspace.Session.Answer.Code);
        Assert.True(workspace.Redo());
        Assert.Equal("15", workspace.Result.Code);
        Assert.Equal("5", workspace.Session.DraftPreviousAnswer.Code);
        Assert.Equal(2, workspace.Session.History.Count);
    }

    [Fact]
    public async Task DisplayedResultContinuationKeepsItsContextAcrossUndoRedoAndSessionRestore()
    {
        using var workspace = RecalledAnsWorkspace();
        workspace.Edit("Ans^2", WorkspaceEditContext.DisplayedResult);
        Assert.Equal("10", workspace.Session.DraftPreviousAnswer.Code);
        Assert.Equal("99", workspace.Session.Answer.Code);
        Assert.Single(workspace.Session.History);

        Assert.True(workspace.Undo());
        Assert.Equal("10", workspace.Result.Code);
        Assert.Equal("5", workspace.Session.DraftPreviousAnswer.Code);
        Assert.True(workspace.Redo());
        Assert.False(workspace.Session.DraftIsSolved);
        Assert.Equal("10", workspace.Session.DraftPreviousAnswer.Code);

        using var restored = new CalculatorWorkspace(SessionCodec.Read(workspace.Snapshot()));
        Assert.Equal("99", restored.Session.Answer.Code);
        Assert.Equal("10", restored.Session.DraftPreviousAnswer.Code);
        Assert.True(await restored.EvaluateAsync());
        Assert.Equal("100", restored.Result.Code);
        Assert.Equal("100", restored.Session.Answer.Code);
        Assert.Equal(2, restored.Session.History.Count);
    }

    [Fact]
    public async Task FreshCalculationUsesLatestGlobalAnsInsteadOfRecalledExpressionContext()
    {
        using var workspace = RecalledAnsWorkspace();
        workspace.Edit("", WorkspaceEditContext.NewCalculation);
        Assert.Equal("99", workspace.Session.DraftPreviousAnswer.Code);
        workspace.Edit("Ans+1");
        Assert.True(await workspace.EvaluateAsync());
        Assert.Equal("100", workspace.Result.Code);
    }

    [Fact]
    public async Task ModeAndAngleChangesKeepTheAnsContextOfTheCurrentExpression()
    {
        using var workspace = RecalledAnsWorkspace();
        workspace.SetMode(CalculationMode.Symbolic);
        workspace.SetRadians(false);
        Assert.Equal("5", workspace.Session.DraftPreviousAnswer.Code);
        Assert.True(await workspace.EvaluateAsync());
        Assert.Equal("10", workspace.Result.Code);
        Assert.Equal(CalculationMode.Symbolic, workspace.Session.DraftMode);
        Assert.False(workspace.Session.DraftUseRadians);
    }

    [Fact]
    public async Task EmptyRestoredDraftStartsWithTheCurrentGlobalAns()
    {
        using var workspace = new CalculatorWorkspace(new CalculatorSession { Answer = new MathValue(7) });
        workspace.Edit("Ans+1");
        Assert.Equal("7", workspace.Session.DraftPreviousAnswer.Code);
        Assert.True(await workspace.EvaluateAsync());
        Assert.Equal("8", workspace.Result.Code);
    }

    [Fact]
    public void ChangingOnlyAnsContextIsUndoableButRepeatingItDoesNotCreateAnotherStep()
    {
        using var workspace = RecalledAnsWorkspace();
        workspace.Edit("Ans", WorkspaceEditContext.Preserve);
        workspace.Edit("Ans", WorkspaceEditContext.NewCalculation);
        workspace.Edit("Ans", WorkspaceEditContext.NewCalculation);
        Assert.Equal("99", workspace.Session.DraftPreviousAnswer.Code);
        Assert.True(workspace.Undo());
        Assert.Equal("Ans", workspace.Session.Draft);
        Assert.Equal("5", workspace.Session.DraftPreviousAnswer.Code);
        workspace.Edit("Ans", WorkspaceEditContext.Preserve);
        Assert.True(workspace.CanRedo);
        Assert.True(workspace.Redo());
        Assert.Equal("99", workspace.Session.DraftPreviousAnswer.Code);
    }

    private static CalculatorWorkspace RecalledAnsWorkspace()
    {
        var session = new CalculatorSession { Answer = new MathValue(99) };
        var record = new HistoryRecord
        {
            Input = "Ans*2", PreviousAnswer = new MathValue(5), Answer = new MathValue(10)
        };
        session.History.Add(record);
        var workspace = new CalculatorWorkspace(session);
        workspace.Recall(record);
        return workspace;
    }

    [Fact]
    public async Task UndoClearRestoresSolvedExpressionAndContentWithoutRewindingAnsOrHistory()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.Edit("10");
        Assert.True(await workspace.EvaluateAsync());
        workspace.Edit("Ans+2");
        Assert.True(await workspace.EvaluateAsync());
        var content = workspace.Content;
        string previousAnswer = workspace.Session.DraftPreviousAnswer.Code;

        workspace.Edit("");
        Assert.Null(workspace.Content);
        Assert.Null(workspace.Result);
        Assert.True(workspace.Undo());

        Assert.Equal("Ans+2", workspace.Session.Draft);
        Assert.True(workspace.Session.DraftIsSolved);
        Assert.Equal("12", workspace.Result.Code);
        Assert.Equal(previousAnswer, workspace.Session.DraftPreviousAnswer.Code);
        Assert.Same(content, workspace.Content);
        Assert.Equal("12", workspace.Session.Answer.Code);
        Assert.Equal(2, workspace.Session.History.Count);

        Assert.True(workspace.Redo());
        Assert.Equal("", workspace.Session.Draft);
        Assert.False(workspace.Session.DraftIsSolved);
        Assert.Null(workspace.Result);
        Assert.Null(workspace.Content);
        Assert.Equal("12", workspace.Session.DraftPreviousAnswer.Code);
        Assert.Equal("12", workspace.Session.Answer.Code);
        Assert.Equal(2, workspace.Session.History.Count);
        Assert.False(workspace.CanRedo);
    }

    [Fact]
    public async Task UndoRestoresStructuredContentWithoutReevaluatingIt()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.Edit("{\"answer\":42}");
        Assert.True(await workspace.EvaluateAsync(), workspace.Error);
        var content = workspace.Content;
        Assert.Equal("json", content.Kind);
        workspace.Edit("");

        Assert.True(workspace.Undo());
        Assert.Same(content, workspace.Content);
        Assert.True(workspace.Session.DraftIsSolved);
        Assert.Null(workspace.Result);
        Assert.Single(workspace.Session.History);
    }

    [Fact]
    public void NewEditAfterUndoDiscardsRedoBranchAndNoOpEditsDoNotCreateSteps()
    {
        using var workspace = new CalculatorWorkspace();
        Assert.False(workspace.Undo());
        Assert.False(workspace.Redo());
        workspace.Edit("1");
        workspace.Edit("1");
        workspace.SetMode(CalculationMode.Numeric);
        workspace.SetRadians(true);
        workspace.Edit("12");
        Assert.True(workspace.Undo());
        Assert.Equal("1", workspace.Session.Draft);
        Assert.True(workspace.CanRedo);
        workspace.Edit("1");
        Assert.True(workspace.CanRedo);

        workspace.Edit("13");
        Assert.False(workspace.CanRedo);
        Assert.False(workspace.Redo());
        Assert.True(workspace.Undo());
        Assert.Equal("1", workspace.Session.Draft);
        Assert.True(workspace.Undo());
        Assert.Equal("", workspace.Session.Draft);
        Assert.False(workspace.CanUndo);
    }

    [Fact]
    public async Task UndoModeAndAngleChangesRestoresSolvedStateAndSameSettingsAreNoOps()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.Edit("sin(30)");
        workspace.SetMode(CalculationMode.Symbolic);
        workspace.SetRadians(false);
        Assert.True(await workspace.EvaluateAsync());
        var content = workspace.Content;
        workspace.SetMode(CalculationMode.Symbolic);
        workspace.SetRadians(false);
        Assert.Same(content, workspace.Content);
        Assert.Equal("1/2", workspace.Result.Code);

        workspace.SetMode(CalculationMode.Numeric);
        workspace.SetRadians(true);
        Assert.True(workspace.Undo());
        Assert.False(workspace.Session.DraftUseRadians);
        Assert.Equal(CalculationMode.Numeric, workspace.Session.Mode);
        Assert.Null(workspace.Result);
        Assert.True(workspace.Undo());
        Assert.Equal(CalculationMode.Symbolic, workspace.Session.Mode);
        Assert.Equal(CalculationMode.Symbolic, workspace.Session.DraftMode);
        Assert.False(workspace.Session.DraftUseRadians);
        Assert.True(workspace.Session.DraftIsSolved);
        Assert.Equal("1/2", workspace.Result.Code);
        Assert.Same(content, workspace.Content);

        Assert.True(workspace.Redo());
        Assert.Equal(CalculationMode.Numeric, workspace.Session.Mode);
        Assert.False(workspace.Session.DraftUseRadians);
        Assert.True(workspace.Redo());
        Assert.True(workspace.Session.DraftUseRadians);
        Assert.Equal("1/2", workspace.Session.Answer.Code);
        Assert.Single(workspace.Session.History);
    }

    [Fact]
    public async Task ResumingSolvedTextIsUndoableEvenWhenItsCharactersDoNotChange()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.Edit("6*7");
        Assert.True(await workspace.EvaluateAsync());
        workspace.Edit("6*7");
        Assert.False(workspace.Session.DraftIsSolved);
        Assert.Null(workspace.Result);

        Assert.True(workspace.Undo());
        Assert.True(workspace.Session.DraftIsSolved);
        Assert.Equal("42", workspace.Result.Code);
        Assert.NotNull(workspace.Content);
        Assert.True(workspace.Redo());
        Assert.False(workspace.Session.DraftIsSolved);
        Assert.Equal("6*7", workspace.Session.Draft);
    }

    [Fact]
    public async Task UndoRecallRestoresPriorResultButNeverRewindsGlobalAnswerOrHistory()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.SetMode(CalculationMode.Symbolic);
        workspace.SetRadians(false);
        workspace.Edit("sin(30)");
        Assert.True(await workspace.EvaluateAsync());
        var record = workspace.Session.History[0];
        workspace.SetMode(CalculationMode.Numeric);
        workspace.SetRadians(true);
        workspace.Edit("99");
        Assert.True(await workspace.EvaluateAsync());
        var latestContent = workspace.Content;

        workspace.Recall(record);
        workspace.Recall(record); // Repeated browsing must not create another step.
        Assert.Equal("1/2", workspace.Result.Code);
        Assert.True(workspace.Undo());
        Assert.Equal("99", workspace.Session.Draft);
        Assert.Equal("99", workspace.Result.Code);
        Assert.Same(latestContent, workspace.Content);
        Assert.Equal(CalculationMode.Numeric, workspace.Session.Mode);
        Assert.True(workspace.Session.DraftUseRadians);
        Assert.True(workspace.Redo());
        Assert.Equal("sin(30)", workspace.Session.Draft);
        Assert.Equal("1/2", workspace.Result.Code);
        Assert.Equal(record.PreviousAnswer.Code, workspace.Session.DraftPreviousAnswer.Code);
        Assert.False(workspace.Session.DraftUseRadians);
        Assert.Equal("99", workspace.Session.Answer.Code);
        Assert.Equal(2, workspace.Session.History.Count);
    }

    [Fact]
    public async Task UndoAndRedoInvalidatePendingCalculationsBeforeTheyCanCommit()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.Edit("2+3");
        workspace.Edit("7+8");
        var first = workspace.EvaluateAsync();
        Assert.True(workspace.Undo());
        Assert.False(await first);
        Assert.Equal("2+3", workspace.Session.Draft);
        Assert.False(workspace.IsBusy);
        Assert.Null(workspace.Content);
        Assert.Null(workspace.Error);

        var second = workspace.EvaluateAsync();
        Assert.True(workspace.Redo());
        Assert.False(await second);
        Assert.Equal("7+8", workspace.Session.Draft);
        Assert.Null(workspace.Result);
        Assert.Equal("0", workspace.Session.Answer.Code);
        Assert.Empty(workspace.Session.History);
        Assert.False(workspace.IsBusy);
    }

    [Fact]
    public void UndoHistoryBoundsBothEditCountAndRetainedLargeDrafts()
    {
        using var shortDrafts = new CalculatorWorkspace();
        for (int i = 0; i < 200; i++) shortDrafts.Edit(i.ToString());
        int shortUndos = 0;
        while (shortDrafts.Undo()) shortUndos++;
        Assert.InRange(shortUndos, 1, 100);
        Assert.NotEqual("", shortDrafts.Session.Draft);

        using var longDrafts = new CalculatorWorkspace();
        string prefix = new string('x', 256 * 1024 - 2);
        for (int i = 0; i < 20; i++) longDrafts.Edit(prefix + i.ToString("D2"));
        int longUndos = 0;
        while (longDrafts.Undo()) longUndos++;
        Assert.InRange(longUndos, 1, 8);
        Assert.True(longDrafts.CanRedo);
    }

    [Fact]
    public async Task EditingWhileWorkerRunsCannotCommitStaleResult()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.Edit("2+3");
        var pending = workspace.EvaluateAsync();
        workspace.Edit("7+8");
        Assert.False(await pending);
        Assert.Null(workspace.Result);
        Assert.Empty(workspace.Session.History);
        Assert.Equal("7+8", workspace.Session.Draft);
        Assert.True(await workspace.EvaluateAsync());
        Assert.Equal("15", workspace.Result.Code);
    }

    [Fact]
    public async Task HistoryRestoresExactResultAndAngleWithoutChangingAns()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.SetMode(CalculationMode.Symbolic);
        workspace.SetRadians(false);
        workspace.Edit("sin(30)");
        Assert.True(await workspace.EvaluateAsync());
        var first = workspace.Session.History[0];
        workspace.Edit("sqrt(8)");
        Assert.True(await workspace.EvaluateAsync());
        string ans = workspace.Session.Answer.Code;
        workspace.Recall(first);
        Assert.Equal("1/2", workspace.Result.Code);
        Assert.False(workspace.Session.DraftUseRadians);
        Assert.Equal(CalculationMode.Symbolic, workspace.Session.Mode);
        Assert.Equal(ans, workspace.Session.Answer.Code);
        Assert.Equal(2, workspace.Session.History.Count);
        var restored = new CalculatorWorkspace(SessionCodec.Read(workspace.Snapshot()));
        Assert.True(restored.Session.DraftIsSolved);
        Assert.Equal("1/2", restored.Result.Code);
        Assert.Equal(ans, restored.Session.Answer.Code);
    }

    [Fact]
    public async Task InvalidInputDoesNotReplaceAnsOrEnterHistory()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.Edit("42");
        await workspace.EvaluateAsync();
        workspace.Edit("1/0");
        Assert.False(await workspace.EvaluateAsync());
        Assert.NotNull(workspace.Error);
        Assert.Equal("42", workspace.Session.Answer.Code);
        Assert.Single(workspace.Session.History);
    }

    [Theory]
    [InlineData("lg(100)", "2")]
    [InlineData("Abs((1,2,,3,4))", "-2")]
    [InlineData("Ex((1,2,3))", "2")]
    [InlineData("5 nCr 2", "10")]
    [InlineData("50/100", "0.5")]
    public async Task MacKeypadExpressionsUseSharedGrammar(string input, string expected)
    {
        using var workspace = new CalculatorWorkspace();
        workspace.Edit(input);
        Assert.True(await workspace.EvaluateAsync(), workspace.Error);
        Assert.Equal(expected, workspace.Result.Code);
    }

    [Fact]
    public async Task DamagedPrimaryRecoversBackupAndPreservesDamagedBytes()
    {
        string directory = Path.Combine(Path.GetTempPath(), "naturplay-session-" + Guid.NewGuid());
        try
        {
            var store = new FileSessionStore(directory);
            await store.LoadAsync();
            var first = new CalculatorSession { Draft = "1/3" };
            await store.SaveAsync(SessionCodec.Write(first));
            await store.SaveAsync(SessionCodec.Write(new CalculatorSession { Draft = "sqrt(8)" }));
            await File.WriteAllTextAsync(Path.Combine(directory, FileSessionStore.FileName), "{broken");
            var recovery = new FileSessionStore(directory);
            var restored = await recovery.LoadAsync();
            Assert.True(recovery.HadReadError);
            Assert.True(recovery.RecoveredFromBackup);
            Assert.Equal("1/3", restored.Draft);
            await recovery.SaveAsync(SessionCodec.Write(restored));
            string corrupt = Assert.Single(Directory.GetFiles(directory, "calculator-session-corrupt-*.json"));
            Assert.Equal("{broken", await File.ReadAllTextAsync(corrupt));
            Assert.Equal("1/3", (await new FileSessionStore(directory).LoadAsync()).Draft);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
