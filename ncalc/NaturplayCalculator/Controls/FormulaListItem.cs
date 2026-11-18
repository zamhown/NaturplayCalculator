using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace NaturplayCalculator.Controls
{
    /// <summary>Hover feedback independent of ListView selection and item invocation.</summary>
    public sealed partial class FormulaListItem : ListViewItem
    {
        public static readonly DependencyProperty HoverOpacityProperty = DependencyProperty.Register(
            nameof(HoverOpacity), typeof(double), typeof(FormulaListItem), new PropertyMetadata(0d));
        public double HoverOpacity { get => (double)GetValue(HoverOpacityProperty); private set => SetValue(HoverOpacityProperty, value); }
        public static readonly DependencyProperty ResultOpacityProperty = DependencyProperty.Register(
            nameof(ResultOpacity), typeof(double), typeof(FormulaListItem), new PropertyMetadata(0d));
        public double ResultOpacity { get => (double)GetValue(ResultOpacityProperty); private set => SetValue(ResultOpacityProperty, value); }
        // Preview emphasis is programmatic and is independent of user selection.
        public bool IsPreviewResult { get => ResultOpacity > 0; set => ResultOpacity = value ? .18 : 0; }
        public FormulaListItem() { Unloaded += (s, e) => HoverOpacity = 0; }
        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            base.OnPointerEntered(e);
            HoverOpacity = e.Pointer.PointerDeviceType == PointerDeviceType.Touch ? 0 : 1;
        }
        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);
            HoverOpacity = 0;
        }
    }
}
