using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace NaturplayCalculator;

sealed partial class App : Application
{
    public static MainWindow MainWindow { get; private set; }

    public App()
    {
        UnhandledException += (_, args) => Settings.ReportError("Unhandled", args.Exception);
        InitializeComponent();
        AppInstance.GetCurrent().Activated += OnActivated;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        var frame = new Frame();
        MainWindow.Content = frame;
        frame.NavigationFailed += (_, e) => throw new System.Exception("Failed to load " + e.SourcePageType.FullName, e.Exception);
        frame.Navigate(typeof(MainPage), ActivationParameter(AppInstance.GetCurrent().GetActivatedEventArgs()));
        MainWindow.Activate();
    }

    private void OnActivated(object sender, AppActivationArguments args)
    {
        MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            if (MainWindow.Content is Frame { Content: MainPage page }) page.HandleActivation(ActivationParameter(args));
            MainWindow.Activate();
        });
    }

    private static object ActivationParameter(AppActivationArguments args) =>
        args.Data is IProtocolActivatedEventArgs protocol ? protocol.Uri : null;
}
