using System.ComponentModel;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using NaturplayCalculator.Presentation.Input;

namespace NaturplayCalculator;

public sealed partial class MainPage
{
    public event PropertyChangedEventHandler PropertyChanged;
    public Style KeyStyle { get; private set; }
    public Style ConstantKeyStyle { get; private set; }

    private readonly DisplayToolsState displayToolsState = new();
    public string DisplayToolsToggleLabel => AppResources.GetString("display_tools_toggle");

    private void ApplyAppearance(bool dark)
    {
        string theme = dark ? "Dark" : "Light";
        KeyStyle = (Style)Resources[$"Calculator{theme}KeyStyle"];
        ConstantKeyStyle = dark ? (Style)Resources["CalculatorDarkConstantKeyStyle"] : KeyStyle;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KeyStyle)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConstantKeyStyle)));
        BgBrush = (SolidColorBrush)Resources[$"Calculator{theme}BackgroundBrush"];
        VisualStateManager.GoToState(this, $"Calculator{theme}", false);
        // Dialogs are separate visual roots and do not inherit the page's state.
        foreach (var dialog in Dialogs)
            dialog.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
    }

    private void RefreshVisualStates()
    {
        ApplyAppearance(White);
        VisualStateManager.GoToState(this, Shift ? "ShiftedOperators" : "PrimaryOperators", false);
        VisualStateManager.GoToState(this, TreeViewState ? "DocumentResults" : "FormulaResults", false);
        VisualStateManager.GoToState(this, TapFirst ? "TouchInput" : "PointerInput", false);
        UpdateDisplayToolsState();
    }

    private void DisplayRegion_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            displayToolsState.SetMouseHover(sender, true);
        else
            displayToolsState.BeginTouchInteraction();
        UpdateDisplayToolsState();
    }

    private void DisplayRegion_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse) return;
        displayToolsState.SetMouseHover(sender, false);
        UpdateDisplayToolsState();
    }

    private void DisplayToolsToggle_Click(object sender, RoutedEventArgs e)
    {
        displayToolsState.SetKeyboardFocus(false);
        displayToolsState.Toggle();
        UpdateDisplayToolsState();
    }

    private void DisplayToolsToggle_Tapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;

    private void DisplayTools_FocusChanged(object sender, RoutedEventArgs e)
    {
        var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        bool keyboard = focused is Control { FocusState: FocusState.Keyboard };
        while (focused != null && focused != toolgrid) focused = VisualTreeHelper.GetParent(focused);
        displayToolsState.SetKeyboardFocus(keyboard && focused == toolgrid);
        UpdateDisplayToolsState();
    }

    private void DisplayRegion_Unloaded(object sender, RoutedEventArgs e)
    {
        displayToolsState.ClearTransientInput();
        UpdateDisplayToolsState();
    }

    private void UpdateDisplayToolsState() =>
        VisualStateManager.GoToState(this, displayToolsState.IsVisible ? "DisplayToolsVisible" : "DisplayToolsHidden", false);
}
