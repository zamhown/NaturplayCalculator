using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace NaturplayCalculator.Controls;

// Each surface owns its controller. XAML supplies activation, theme and accessibility policy.
public sealed partial class TintedAcrylicBackdrop : SystemBackdrop
{
    private readonly Color tint;
    private readonly float opacity;
    private DesktopAcrylicController controller;

    public TintedAcrylicBackdrop(Color tint, float opacity)
    {
        this.tint = tint;
        this.opacity = opacity;
    }

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target, XamlRoot root)
    {
        base.OnTargetConnected(target, root);
        controller = new DesktopAcrylicController
        {
            TintColor = tint,
            TintOpacity = opacity,
            FallbackColor = tint
        };
        controller.SetSystemBackdropConfiguration(GetDefaultSystemBackdropConfiguration(target, root));
        controller.AddSystemBackdropTarget(target);
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        controller?.RemoveSystemBackdropTarget(target);
        controller?.Dispose();
        controller = null;
        base.OnTargetDisconnected(target);
    }
}
