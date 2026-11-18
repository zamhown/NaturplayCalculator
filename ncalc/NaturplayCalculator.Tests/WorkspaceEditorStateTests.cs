using NaturplayCalculator.History;
using NaturplayCalculator.Presentation;
using Xunit;

namespace NaturplayCalculator.Tests;

public sealed class WorkspaceEditorStateTests
{
    private sealed record Marker(string Source, string Scope, int Caret);

    [Fact]
    public void UndoAndRedoRestoreTheExactImmutableEditorSnapshot()
    {
        using var workspace = new CalculatorWorkspace();
        workspace.Edit("8/2^2");
        var squaredOperand = new Marker("8/2^2", "completed right operand", 5);
        workspace.EditorState = squaredOperand;
        workspace.Edit("8/2^2*3");
        var appendedFactor = new Marker("8/2^2*3", "root", 7);
        workspace.EditorState = appendedFactor;

        Assert.True(workspace.Undo());
        Assert.Equal(squaredOperand.Source, workspace.Session.Draft);
        Assert.Same(squaredOperand, workspace.EditorState);
        Assert.True(workspace.Redo());
        Assert.Equal(appendedFactor.Source, workspace.Session.Draft);
        Assert.Same(appendedFactor, workspace.EditorState);
    }

    [Fact]
    public void IdenticalDraftsCanRestoreDifferentEditorStatesAcrossARealSettingEdit()
    {
        using var workspace = new CalculatorWorkspace(new CalculatorSession { Draft = "8/2^2" });
        var operandEnd = new Marker("8/2^2", "completed right operand", 5);
        workspace.EditorState = operandEnd;
        bool originalRadians = workspace.Session.DraftUseRadians;
        workspace.SetRadians(!originalRadians);
        var denominator = new Marker("8/2^2", "exponent", 5);
        workspace.EditorState = denominator;

        Assert.True(workspace.Undo());
        Assert.Equal("8/2^2", workspace.Session.Draft);
        Assert.Equal(originalRadians, workspace.Session.DraftUseRadians);
        Assert.Same(operandEnd, workspace.EditorState);
        Assert.True(workspace.Redo());
        Assert.Equal("8/2^2", workspace.Session.Draft);
        Assert.Equal(!originalRadians, workspace.Session.DraftUseRadians);
        Assert.Same(denominator, workspace.EditorState);
    }

    [Fact]
    public void NavigationAloneNeitherAddsAnUndoStepNorChangesTheSavedSession()
    {
        using var workspace = new CalculatorWorkspace(new CalculatorSession { Draft = "8/2^2" });
        string original = workspace.Snapshot();
        workspace.EditorState = new Marker("8/2^2", "root", 5);
        workspace.EditorState = new Marker("8/2^2", "exponent", 5);
        workspace.Edit("8/2^2", WorkspaceEditContext.Preserve);
        Assert.False(workspace.CanUndo);
        Assert.False(workspace.CanRedo);
        Assert.Equal(original, workspace.Snapshot());
        using var restored = new CalculatorWorkspace(SessionCodec.Read(workspace.Snapshot()));
        Assert.Null(restored.EditorState);
        Assert.Equal(workspace.Session.Draft, restored.Session.Draft);
    }
}
