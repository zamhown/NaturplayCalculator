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
    public sealed partial class AlertDialog : ContentDialog
    {
        MainPage mainPage;

        public AlertDialog(MainPage main)
        {
            this.InitializeComponent();
            mainPage = main;
        }

        public void SetContent(int type)
        {
            switch(type)
            {
                case 1:
                    textBlock.Text = AppResources.GetString("msg_savesuccess");
                    break;
                case 2:
                    textBlock.Text = AppResources.GetString("msg_savefailed");
                    break;
                case 3:
                    textBlock.Text = AppResources.GetString("msg_saveHistoryFailed");
                    break;
                case 4:
                    textBlock.Text = AppResources.GetString("msg_restoreSettings");
                    break;
            }
        }

        private void bclose_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }
    }
}
