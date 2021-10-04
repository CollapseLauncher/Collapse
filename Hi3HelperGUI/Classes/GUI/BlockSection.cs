using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using Hi3HelperGUI.Preset;
using Hi3HelperGUI.Data;

using static Hi3HelperGUI.Logger;
using static Hi3HelperGUI.Data.ConverterTool;

namespace Hi3HelperGUI
{
    public partial class MainWindow
    {
        public void BlockCheckStart(object sender, RoutedEventArgs e) => FetchDictionaryData();

        public async void FetchDictionaryData()
        {
            await Task.Run(() =>
            {
                LogWriteLine($"Bruh");
                RefreshBlockCheckProgressBar();
            }).ConfigureAwait(false);
        }

        private void RefreshBlockCheckProgressBar(double cur = 0, double max = 100) => Dispatcher.Invoke(() => BlockProgressBar.Value = Math.Round((100 * cur) / max, 2), DispatcherPriority.Background);

        private void ChangeBlockRepair(bool a) =>
            Dispatcher.Invoke(() =>
            {
                UpdateDownloadBtn.IsEnabled = a;
                UpdateListView.IsEnabled = a;
            });

        private void ChangeBlockRepairStatus(string s, bool b) =>
            Dispatcher.Invoke(() =>
            {
                UpdateCheckStatus.Content = s;
                UpdateCheckBtn.IsEnabled = b;
            });
    }
}
