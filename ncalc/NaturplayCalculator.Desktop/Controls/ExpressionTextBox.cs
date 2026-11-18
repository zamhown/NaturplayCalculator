using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NaturplayCalculator.Desktop.Controls;

internal sealed class ExpressionTextBox : TextBox
{
    public bool SuppressClearButton { get; set; }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (SuppressClearButton && GetTemplateChild("DeleteButton") is Button delete)
        {
            // Keep Uno's native editor and template, but remove its destructive
            // action when the host supplies a scope-navigation button instead.
            // Width and hit testing stay disabled even if visual states reveal it.
            delete.Width = delete.MinWidth = delete.MaxWidth = 0;
            delete.Margin = new Thickness(0); delete.Command = null;
            delete.Opacity = 0; delete.IsHitTestVisible = false; delete.IsTabStop = false;
        }
        // Uno's scroller stretches its text child even when content alignment
        // is Center. Center the child itself; the delete button already is.
        if (GetTemplateChild("ContentElement") is ScrollViewer content)
        {
            content.VerticalContentAlignment = VerticalAlignment.Center;
            if (content.Content is TextBlock text)
            {
                text.VerticalAlignment = VerticalAlignment.Center;
                // Use the cap-height top while retaining the full descender
                // space for parentheses and commas. The baseline stays stable
                // while editing, and caret/selection use the same line metrics.
                text.TextLineBounds = TextLineBounds.TrimToCapHeight;
            }
        }
        if (GetTemplateChild("PlaceholderTextContentPresenter") is TextBlock placeholder)
        {
            placeholder.VerticalAlignment = VerticalAlignment.Center;
            placeholder.TextLineBounds = TextLineBounds.TrimToCapHeight;
        }
    }
}
