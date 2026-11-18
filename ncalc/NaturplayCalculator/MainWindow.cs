using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Graphics;

namespace NaturplayCalculator;

public sealed partial class MainWindow : Window
{
    private bool closing;
    private bool saved;
    private readonly OverlappedPresenter regularPresenter;
    public bool IsFullScreenMode => AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;

    public MainWindow()
    {
        Title = AppResources.AppName;
        ExtendsContentIntoTitleBar = true;
        regularPresenter = (OverlappedPresenter)AppWindow.Presenter;
        regularPresenter.PreferredMinimumWidth = 300;
        regularPresenter.PreferredMinimumHeight = 200;
        AppWindow.Closing += OnClosing;
    }

    public void ResizeContent(Size size)
    {
        double scale = Content?.XamlRoot?.RasterizationScale
            ?? GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.ResizeClient(new SizeInt32((int)(Math.Max(300, size.Width) * scale), (int)(Math.Max(200, size.Height) * scale)));
    }

    public void EnterFullScreen() => AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);

    public void SetCompactOverlay(bool enabled, Size size)
    {
        if (enabled) AppWindow.SetPresenter(AppWindowPresenterKind.CompactOverlay);
        else AppWindow.SetPresenter(regularPresenter);
        if (enabled) ResizeContent(size);
    }

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint window);

    private async void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (saved) return;
        args.Cancel = true;
        if (closing) return;
        closing = true;
        try
        {
            if (Content is Frame { Content: MainPage page }) await page.SuspendAsync();
        }
        catch (Exception ex) { Settings.ReportError("Close", ex); }
        finally { saved = true; Close(); }
    }
}
