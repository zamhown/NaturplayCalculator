using System;
using System.Collections.Generic;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NaturplayCalculator.Controls;
using Windows.UI;

namespace NaturplayCalculator;

public sealed partial class MainPage
{
    private readonly Dictionary<Grid, SystemBackdropElement> acrylicSurfaces = new();

    public bool Acrylic
    {
        get => acrylic;
        private set
        {
            acrylic = value;
            displayRectangle.Fill = null;
            SetAcrylicSurface(displayMaterial, Gray(White ? 23 : 230), 0.6f);
            SetAcrylicSurface(conButtons, Gray(White ? 28 : 220), 0.9f);
            foreach (var grid in new[] { numButtons, numButtons_1, smallnumButtons })
                SetAcrylicSurface(grid, Gray(White ? 36 : 230), 0.9f);
        }
    }

    private static Color Gray(int component) => Color.FromArgb(255, (byte)component, (byte)component, (byte)component);

    private void SetAcrylicSurface(Grid panel, Color tint, float opacity)
    {
        if (!acrylicSurfaces.TryGetValue(panel, out var backdrop))
        {
            backdrop = new SystemBackdropElement { IsHitTestVisible = false };
            Grid.SetRowSpan(backdrop, Math.Max(1, panel.RowDefinitions.Count));
            Grid.SetColumnSpan(backdrop, Math.Max(1, panel.ColumnDefinitions.Count));
            panel.Children.Insert(0, backdrop);
            acrylicSurfaces.Add(panel, backdrop);
        }

        bool enabled = Acrylic && DesktopAcrylicController.IsSupported();
        backdrop.SystemBackdrop = enabled ? new TintedAcrylicBackdrop(tint, opacity) : null;
        backdrop.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        panel.Background = enabled ? null : new SolidColorBrush(tint);
    }
}
