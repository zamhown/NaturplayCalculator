using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“内容对话框”项模板

namespace NaturplayCalculator
{
    public sealed partial class WhatsNewDialog : ContentDialog
    {
        MainPage mainPage;

        public WhatsNewDialog(MainPage main)
        {
            this.InitializeComponent();
            mainPage = main;

            string version = Settings.GetVersion();
            titleBlock.Text = AppResources.AppName;
            versionBlock.Text = AppResources.GetString("msg_about_version") + version + "  2021/10/18";

            textBlock.Text = "";
            textBlock.Inlines.Add(new Run() { Text = $"\n{AppResources.GetString("button_update_content_1")}\n\n", FontSize = 18 });
            var sb = new StringBuilder();
            for (int i = 1; i <= 4; i++)
                sb.Append($"{i}, {AppResources.GetString($"msg_update_content_{i}")}\n");
            textBlock.Inlines.Add(new Run() { Text = sb.ToString() });
            sb.Clear();
            textBlock.Inlines.Add(new Run() { Text = $"\n{AppResources.GetString("msg_past_update_title")}\n\n", FontSize = 18 });
            for (int i = 1; i <= 3; i++)
                sb.Append($"{i}, {AppResources.GetString($"msg_past_update_content_{i}")}\n");
            textBlock.Inlines.Add(new Run() { Text = sb.ToString() });

            bclose.Content = AppResources.GetString("button_close_content_2");
        }

        private void bclose_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private async void bhelp_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            await mainPage.ShowDialogAsync(mainPage.HelpWindow);
        }
    }
}
