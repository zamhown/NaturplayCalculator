using System;
using Windows.Foundation;
using Windows.UI;
using Colors = Microsoft.UI.Colors;
using Windows.UI.ViewManagement;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Markup;

namespace Naturplay.Tex.WinUI
{
    /// <summary>A reusable insertion caret. It owns no text, selection or input state.</summary>
    public sealed partial class BlinkingCaret : UserControl
    {
        private readonly DispatcherTimer timer = new DispatcherTimer();
        private readonly UISettings settings = new UISettings();
        private XamlRoot root;
        private Window hostWindow;
        public Window HostWindow
        {
            get => hostWindow;
            set
            {
                if (hostWindow == value) return;
                if (loaded && hostWindow != null) hostWindow.Activated -= OnActivated;
                hostWindow = value;
                if (loaded && hostWindow != null) hostWindow.Activated += OnActivated;
            }
        }
        private bool requested, loaded, active = true;
        private bool isBlinking = true;
        public bool IsBlinking
        {
            get => isBlinking;
            set { if (isBlinking == value) return; isBlinking = value; Restart(); }
        }
        public BlinkingCaret()
        {
            IsHitTestVisible = false; IsTabStop = false; Opacity = 0;
            Content = (Rectangle)XamlReader.Load("<Rectangle xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Fill=\"{ThemeResource SystemControlHighlightAccentBrush}\"/>");
            timer.Tick += (s, e) => Opacity = Opacity == 0 ? 1 : 0;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }
        public void ShowAt(Rect bounds, bool restart = true, bool centered = false)
        {
            Width = Math.Max(2, settings.CaretWidth); Height = Math.Max(1, bounds.Height);
            Canvas.SetLeft(this, bounds.X - (centered ? Width / 2 : 0)); Canvas.SetTop(this, bounds.Y);
            bool wasRequested = requested; requested = true;
            if (restart || !wasRequested) Restart();
        }
        public void Hide() { requested = false; timer.Stop(); Opacity = 0; }
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            loaded = true; root = XamlRoot;
            if (hostWindow != null) hostWindow.Activated += OnActivated;
            if (root != null) { root.Changed += OnRootChanged; active = root.IsHostVisible; }
            Restart();
        }
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            loaded = false; timer.Stop(); Opacity = 0;
            if (hostWindow != null) hostWindow.Activated -= OnActivated;
            if (root != null) root.Changed -= OnRootChanged;
            root = null;
        }
        private void OnActivated(object sender, WindowActivatedEventArgs args)
        { active = args.WindowActivationState != WindowActivationState.Deactivated; Restart(); }
        private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs e)
        { Restart(); }
        private void Restart()
        {
            timer.Stop(); Opacity = requested && loaded && active && (root?.IsHostVisible ?? true) ? 1 : 0;
            uint milliseconds = settings.CaretBlinkRate;
            // Windows uses INFINITE when caret blinking is disabled by the user.
            if (Opacity == 0 || !IsBlinking || milliseconds == 0 || milliseconds == uint.MaxValue) return;
            timer.Interval = TimeSpan.FromMilliseconds(milliseconds); timer.Start();
        }
    }
}
