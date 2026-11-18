using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;

namespace NaturplayCalculator.Desktop.Controls;

/// <summary>Centers a standalone symbol's ink, rather than its font's line box.</summary>
internal sealed class CenteredSymbol : TextBlock
{
    private (double Height, double Baseline, double Size, string Family, ushort Weight, string Text) measured;
    public double InkWidth { get; private set; }

    public CenteredSymbol()
    {
        TextAlignment = Microsoft.UI.Xaml.TextAlignment.Center;
        LayoutUpdated += (_, _) => AlignInk();
    }

    private void AlignInk()
    {
        if (ActualHeight <= 0 || BaselineOffset <= 0 || string.IsNullOrEmpty(Text)) return;
        var current = (ActualHeight, BaselineOffset, FontSize, FontFamily.Source, FontWeight.Weight, Text);
        if (current == measured) return;
        measured = current;

        using var typeface = SKTypeface.FromFamilyName(FontFamily.Source,
            new SKFontStyle((SKFontStyleWeight)FontWeight.Weight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright));
        using var font = new SKFont(typeface, (float)FontSize) { Subpixel = true };
        float advance = font.MeasureText(Text, out var ink);
        InkWidth = ink.Width;
        // BaselineOffset comes from Uno's arranged text. Viewbox subsequently
        // scales both the glyph and this correction, including in narrow keys.
        RenderTransform = new TranslateTransform
        {
            X = (advance - ink.Left - ink.Right) / 2,
            Y = ActualHeight / 2 - BaselineOffset - (ink.Top + ink.Bottom) / 2
        };
    }
}
