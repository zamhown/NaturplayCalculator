using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using NaturplayCalculator.Services;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace NaturplayCalculator
{
    public sealed partial class MainPage
    {
        private readonly DialogCoordinator dialogCoordinator = new DialogCoordinator();
        private bool showingInitialDialog;
        private bool exportingImage;

        public Task ShowDialogAsync(ContentDialog dialog) => dialogCoordinator.ShowAsync(dialog);
        private Task ShowAlertAsync(int type) => dialogCoordinator.ShowAsync(AlertWindow, () => AlertWindow.SetContent(type));

        public async Task LaunchLinkAsync(string link, bool review = false)
        {
            try
            {
                if (review) link = "ms-windows-store:reviewapp?PFN=" + Windows.ApplicationModel.Package.Current.Id.FamilyName;
                await Launcher.LaunchUriAsync(new Uri(link, UriKind.Absolute));
            }
            catch (Exception ex) { Settings.ReportError("Open link", ex); }
        }

        private async void SaveAsPicAsync(UIElement content)
        {
            if (exportingImage || content == null) return;
            exportingImage = true;
            try
            {
                var bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(content);
                var pixels = (await bitmap.GetPixelsAsync()).ToArray();
                var picker = new FileSavePicker
                {
                    CommitButtonText = AppResources.GetString("save_button"),
                    SuggestedFileName = AppResources.GetString("save_name") + saveCount + ".png"
                };
                picker.FileTypeChoices.Add(AppResources.GetString("save_type"), new[] { ".png" });
                WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
                var file = await picker.PickSaveFileAsync();
                if (file == null) return;
                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    stream.Size = 0;
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                    double dpi = 96 * XamlRoot.RasterizationScale;
                    encoder.SetPixelData(BitmapPixelFormat.Bgra8, White ? BitmapAlphaMode.Ignore : BitmapAlphaMode.Premultiplied,
                        (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, dpi, dpi, pixels);
                    await encoder.FlushAsync();
                }
                saveCount++;
                await ShowAlertAsync(1);
            }
            catch (Exception ex)
            {
                Settings.ReportError("Export image", ex);
                await ShowAlertAsync(2);
            }
            finally { exportingImage = false; }
        }
    }
}
