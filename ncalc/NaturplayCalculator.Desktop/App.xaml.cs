using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Microsoft.UI.Windowing;

namespace NaturplayCalculator.Desktop;

public partial class App : Application
{
    private Window? window;

    public App()
    {
        UnhandledException += (_, e) =>
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Logs", "Naturplay Calculator");
            try
            {
                Directory.CreateDirectory(folder);
                File.AppendAllText(Path.Combine(folder, "errors.log"), DateTimeOffset.Now + " " + e.Exception + Environment.NewLine);
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException) { }
        };
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        window = new Window
        {
            Title = "Naturplay Calculator",
            SystemBackdrop = new DesktopAcrylicBackdrop()
        };
        // AppWindow uses physical pixels; presenter minimums use logical points.
        // Size the native window before attaching the first XAML layout.
        double scale = MacWindowIntegration.DisplayScale;
        window.AppWindow.Resize(new SizeInt32((int)(500 * scale), (int)(748 * scale)));
        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 300;
            presenter.PreferredMinimumHeight = 360;
        }
        var view = new MainView(window);
        window.Content = view;
        bool saved = false, closing = false;
        window.AppWindow.Closing += async (_, e) =>
        {
            if (saved) return;
            e.Cancel = true;
            if (closing) return;
            closing = true;
            view.PrepareToClose();
            saved = await view.SaveAsync();
            closing = false;
            if (saved) { MacWindowIntegration.SetAlwaysOnTop(false); window.Close(); }
        };
        window.Activate();
        MacWindowIntegration.Activate();
        view.DispatcherQueue.TryEnqueue(() =>
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Logs", "Naturplay Calculator");
            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "window.log"), $"Scale={view.XamlRoot?.RasterizationScale}; Size={window.Bounds}; {MacWindowIntegration.DescribeBackdrop()}; {MacWindowIntegration.DescribeWindowBehavior()}\n");
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException) { }
        });
    }
}
