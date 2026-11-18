using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace NaturplayCalculator.Desktop;

public sealed partial class MainView
{
    private readonly Dictionary<string, Color> systemColors = new(StringComparer.Ordinal);
    private string appearanceFingerprint = "";

    private Color SystemColor(string name)
    {
        if (!systemColors.TryGetValue(name, out var color))
            systemColors[name] = color = MacWindowIntegration.ResolveSystemColor(name, theme);
        return color;
    }

    private SolidColorBrush SystemBrush(string name) => new(SystemColor(name));
    private SolidColorBrush TransparentBrush() => SystemBrush("clearColor");

    private void UpdateSystemPalette()
    {
        systemColors.Clear();
        appearanceFingerprint = MacWindowIntegration.AppearanceFingerprint(theme);

        // Give Uno's menus, dialogs and text controls the same AppKit palette
        // as the custom canvas. Keep NSColor's alpha, including system fills.
        void Map(string color, params string[] keys)
        {
            foreach (string key in keys) Application.Current.Resources[key] = SystemBrush(color);
        }
        Map("labelColor", "TextFillColorPrimaryBrush", "SystemControlForegroundBaseHighBrush");
        Map("secondaryLabelColor", "TextFillColorSecondaryBrush", "SystemControlForegroundBaseMediumBrush");
        Map("tertiaryLabelColor", "TextFillColorTertiaryBrush");
        Map("disabledControlTextColor", "TextFillColorDisabledBrush", "SystemControlDisabledBaseMediumLowBrush");
        Map("windowBackgroundColor", "SolidBackgroundFillColorBaseBrush", "ContentDialogBackground",
            "ContentDialogTopOverlay", "MenuFlyoutPresenterBackground");
        Map("controlBackgroundColor", "SolidBackgroundFillColorSecondaryBrush", "SolidBackgroundFillColorTertiaryBrush",
            "LayerFillColorDefaultBrush", "LayerFillColorAltBrush", "ControlFillColorInputActiveBrush");
        Map("controlColor", "ControlFillColorDefaultBrush", "ControlFillColorSecondaryBrush", "CardBackgroundFillColorDefaultBrush");
        Map("secondarySystemFillColor", "ControlFillColorTertiaryBrush", "SubtleFillColorSecondaryBrush", "InstrumentHoverBrush");
        Map("systemFillColor", "SubtleFillColorTertiaryBrush", "InstrumentPressedBrush");
        Map("separatorColor", "ControlStrokeColorDefaultBrush", "ControlStrokeColorSecondaryBrush",
            "SurfaceStrokeColorDefaultBrush", "DividerStrokeColorDefaultBrush", "MenuFlyoutSeparatorBackground",
            "MenuFlyoutPresenterBorderBrush", "ContentDialogBorderBrush");
        Map("selectedTextBackgroundColor", "TextControlSelectionHighlightColor");
    }
}
