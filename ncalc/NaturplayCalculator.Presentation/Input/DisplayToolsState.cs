using System.Collections.Generic;

namespace NaturplayCalculator.Presentation.Input;

/// <summary>Mouse hover is automatic; an explicit choice stays put across touch contacts.</summary>
public sealed class DisplayToolsState
{
    private readonly HashSet<object> hoveredRegions = new();
    private bool? manualVisibility;
    private bool keyboardFocus;

    public bool IsVisible => keyboardFocus || (manualVisibility ?? hoveredRegions.Count > 0);

    public void SetMouseHover(object region, bool inside)
    {
        if (inside) hoveredRegions.Add(region);
        else hoveredRegions.Remove(region);
    }

    // Freeze an already visible toolbar before dropping mouse hover, so switching
    // from mouse to a finger cannot remove a slider/button from under that finger.
    public void BeginTouchInteraction()
    {
        if (manualVisibility is null && hoveredRegions.Count > 0) manualVisibility = true;
        hoveredRegions.Clear();
    }

    public void SetKeyboardFocus(bool inside) => keyboardFocus = inside;

    public void Toggle() => manualVisibility = !IsVisible;

    public void ClearTransientInput()
    {
        hoveredRegions.Clear();
        keyboardFocus = false;
    }
}
