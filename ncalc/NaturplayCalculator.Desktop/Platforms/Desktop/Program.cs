using Uno.UI.Hosting;

namespace NaturplayCalculator.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseMacOS()
            .Build()
            .Run();
    }
}
