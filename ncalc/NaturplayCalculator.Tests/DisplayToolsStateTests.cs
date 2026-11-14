using NaturplayCalculator.Presentation.Input;
using Xunit;

namespace NaturplayCalculator.Tests;

public class DisplayToolsStateTests
{
    [Fact]
    public void MouseCanCrossOverlappingRegionsWithoutClosingTheTools()
    {
        var state = new DisplayToolsState();
        Assert.False(state.IsVisible);
        state.SetMouseHover("display", true);
        state.SetMouseHover("input", true);
        state.SetMouseHover("display", false);
        Assert.True(state.IsVisible);
        state.SetMouseHover("input", false);
        Assert.False(state.IsVisible);
    }

    [Fact]
    public void RepeatedTouchContactsDoNotFlashOrCloseExplicitlyOpenedTools()
    {
        var state = new DisplayToolsState();
        for (int i = 0; i < 3; i++) state.BeginTouchInteraction();
        Assert.False(state.IsVisible);
        state.Toggle();
        for (int i = 0; i < 3; i++) state.BeginTouchInteraction();
        Assert.True(state.IsVisible);
        state.Toggle();
        state.BeginTouchInteraction();
        Assert.False(state.IsVisible);
    }

    [Fact]
    public void SwitchingFromMouseToTouchDoesNotHideTheControlUnderTheFinger()
    {
        var state = new DisplayToolsState();
        state.SetMouseHover("display", true);
        state.BeginTouchInteraction();
        Assert.True(state.IsVisible);
        state.SetMouseHover("display", true);
        state.SetMouseHover("display", false);
        Assert.True(state.IsVisible);
        state.Toggle();
        state.BeginTouchInteraction();
        Assert.False(state.IsVisible);
    }

    [Fact]
    public void ExplicitCloseWinsOverSubsequentHoverAndTouch()
    {
        var state = new DisplayToolsState();
        state.SetMouseHover("display", true);
        state.Toggle();
        state.SetMouseHover("display", false);
        state.SetMouseHover("display", true);
        state.BeginTouchInteraction();
        Assert.False(state.IsVisible);
        state.Toggle();
        Assert.True(state.IsVisible);
    }

    [Fact]
    public void KeyboardFocusKeepsToolsReachableUntilFocusLeaves()
    {
        var state = new DisplayToolsState();
        state.SetMouseHover("display", true);
        state.SetKeyboardFocus(true);
        state.SetMouseHover("display", false);
        Assert.True(state.IsVisible);
        state.SetKeyboardFocus(false);
        Assert.False(state.IsVisible);
    }

    [Fact]
    public void UnloadingClearsTransientInputButPreservesExplicitChoice()
    {
        var state = new DisplayToolsState();
        state.SetMouseHover("display", true);
        state.SetKeyboardFocus(true);
        state.ClearTransientInput();
        Assert.False(state.IsVisible);
        state.Toggle();
        state.ClearTransientInput();
        Assert.True(state.IsVisible);
    }
}
