using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace NaturplayCalculator.Services
{
    // Accessed on the UI thread. WinUI permits one ContentDialog at a time.
    public sealed class DialogCoordinator
    {
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private readonly HashSet<ContentDialog> pending = new HashSet<ContentDialog>();

        public async Task ShowAsync(ContentDialog dialog, Action prepare = null)
        {
            if (!pending.Add(dialog)) return;
            await gate.WaitAsync();
            try
            {
                prepare?.Invoke();
                dialog.XamlRoot = App.MainWindow.Content.XamlRoot;
                await dialog.ShowAsync();
            }
            catch (Exception ex) { Settings.ReportError("Show dialog", ex); }
            finally { pending.Remove(dialog); gate.Release(); }
        }
    }
}
