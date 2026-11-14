using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace NaturplayCalculator.Controls;

/// <summary>Keep the native Slider behavior and replace only its thumb artwork.</summary>
public sealed partial class CenteredSlider : Slider
{
    public static readonly DependencyProperty ThumbTemplateProperty = DependencyProperty.Register(
        nameof(ThumbTemplate), typeof(ControlTemplate), typeof(CenteredSlider),
        new PropertyMetadata(null, (sender, _) => ((CenteredSlider)sender).ApplyThumbTemplate()));

    public ControlTemplate ThumbTemplate
    {
        get => (ControlTemplate)GetValue(ThumbTemplateProperty);
        set => SetValue(ThumbTemplateProperty, value);
    }

    public CenteredSlider() => DefaultStyleKey = typeof(Slider);

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        ApplyThumbTemplate();
    }

    private void ApplyThumbTemplate()
    {
        if (ThumbTemplate is null) return;
        if (GetTemplateChild("HorizontalThumb") is Thumb horizontal) horizontal.Template = ThumbTemplate;
        if (GetTemplateChild("VerticalThumb") is Thumb vertical) vertical.Template = ThumbTemplate;
    }
}
