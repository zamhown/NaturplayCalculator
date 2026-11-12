using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“内容对话框”项模板

namespace NaturplayCalculator
{
    public sealed partial class AboutDialog : ContentDialog
    {
        MainPage mainPage;

        public AboutDialog(MainPage main)
        {
            this.InitializeComponent();
            mainPage = main;

            string version = Settings.GetVersion();
            titleBlock.Text = AppResources.AppName;
            versionBlock.Text = AppResources.GetString("msg_about_version") + version;

            textBlock.Text = "";
            textBlock.Inlines.Add(new Run() { Text = $"\n{AppResources.GetString("msg_about")}" });
            textBlock.Inlines.Add(new Run() { Text = $"\n\n{ AppResources.GetString("msg_about_tail")}\nCopyright © 2015-{ DateTime.Now.Year } by Howard Zhang" });
            
            bupdate.Content = AppResources.GetString("button_update_content_1");
        }

        private void bclose_Click(object sender, RoutedEventArgs e)
        {
            mainPage.changeEmoji();
            this.Hide();
        }

        public async void bdonate_Click(object sender, RoutedEventArgs e)
        {
            await mainPage.LaunchLinkAsync("https://ko-fi.com/zamhown");
        }

        private async void bhelp_Click(object sender, RoutedEventArgs e)
        {
            mainPage.changeEmoji();
            this.Hide();
            await mainPage.ShowDialogAsync(mainPage.HelpWindow);
        }

        private async void bupdate_Click(object sender, RoutedEventArgs e)
        {
            mainPage.changeEmoji();
            this.Hide();
            await mainPage.ShowDialogAsync(mainPage.WhatsNewWindow);
        }

        public async void breview_Click(object sender, RoutedEventArgs e)
        {
            await mainPage.LaunchLinkAsync(null, review: true);
        }
    }
}
