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
    public sealed partial class HelpDialog : ContentDialog
    {
        MainPage mainPage;

        public HelpDialog(MainPage main)
        {
            this.InitializeComponent();
            mainPage = main;

            textBlock.Text = "";
            textBlock.Inlines.Add(new Run() { Text = $"{AppResources.GetString("msg_help_title")}\n\n", FontSize = 32 });
            textBlock.Inlines.Add(new Run() { Text = $"{AppResources.GetString("msg_help_head")}\n\n\n" });
            textBlock.Inlines.Add(new Run() { Text = AppResources.GetString("mode_symbolic/Text") + "\n\n", FontSize = 22 });
            textBlock.Inlines.Add(new Run() { Text = AppResources.GetString("symbolic_help") + "\n\n\n" });
            textBlock.Inlines.Add(new Run() { Text = $"{AppResources.GetString("msg_help_topic_1")}\n\n", FontSize = 22 });
            textBlock.Inlines.Add(new Run() { Text = $"{AppResources.GetString("msg_help_content_1")}\n\n\n" });
            textBlock.Inlines.Add(new Run() { Text = $"{AppResources.GetString("msg_help_topic_2")}\n\n", FontSize = 22 });
            var sb = new StringBuilder();
            for (int i = 1; i < 12; i++)
                sb.Append($"{AppResources.GetString($"msg_help_content_2_{i}")}\n\n");
            textBlock.Inlines.Add(new Run() { Text = $"{sb.ToString()}\n" });
            textBlock.Inlines.Add(new Run() { Text = $"{AppResources.GetString("msg_help_topic_3")}\n\n", FontSize = 22 });
            textBlock.Inlines.Add(new Run() { Text = $"{AppResources.GetString("msg_help_content_3")}\n\n\n" });
            textBlock.Inlines.Add(new Run() { Text = $"{AppResources.GetString("msg_help_topic_4")}\n\n", FontSize = 22 });
            textBlock.Inlines.Add(new Run() { Text = $"{AppResources.GetString("msg_help_content_4")}" });
        }

        private void bclose_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }
    }
}
