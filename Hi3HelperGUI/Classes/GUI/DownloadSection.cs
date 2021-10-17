using System.Windows;
using System.Windows.Threading;
using Hi3HelperGUI.Preset;
using Hi3HelperGUI.Data;

using static Hi3HelperGUI.Logger;
using static Hi3HelperGUI.Data.ConverterTool;

namespace Hi3HelperGUI
{
    public partial class MainWindow : Window
    {
        readonly HttpClientTool UpdateHttpClient = new(),
                                BlockHttpClient = new();

        void BlockDictDownloadProgressChanged(object sender, DownloadProgressChanged e)
        {
            string BytesReceived = SummarizeSizeSimple(e.BytesReceived);
            string CurrentSpeed = SummarizeSizeSimple(e.CurrentSpeed);
            Dispatcher.Invoke(() =>
            {
                ConfigStore.UpdateFilesTotalDownloaded += e.CurrentReceived;

                BlockFetchingProgressLabel.Text = $"Fetch {BytesReceived}";
                BlockFetchingProgressBar.Value = GetPercentageNumber(e.ProgressPercentage, 100);
            }, DispatcherPriority.Background);
#if DEBUG
            LogWrite($"{e.Message} \u001b[33;1m{(byte)e.ProgressPercentage}%"
             + $"\u001b[0m ({BytesReceived}) (\u001b[32;1m{CurrentSpeed}/s\u001b[0m)", LogType.NoTag, false, true);
#endif
        }

        void UpdateDownloadProgressChanged(object sender, DownloadProgressChanged e)
        {
            string BytesReceived = SummarizeSizeSimple(e.BytesReceived);
            string CurrentSpeed = SummarizeSizeSimple(e.CurrentSpeed);
            Dispatcher.Invoke(() =>
            {
                ConfigStore.UpdateFilesTotalDownloaded += e.CurrentReceived;

                UpdateProgressLabel.Content = $"{(byte)e.ProgressPercentage}% ({BytesReceived}) ({CurrentSpeed}/s)";
                UpdateProgressBar.Value = GetPercentageNumber(ConfigStore.UpdateFilesTotalDownloaded, ConfigStore.UpdateFilesTotalSize);
            }, DispatcherPriority.Background);
#if DEBUG
            LogWrite($"{e.Message} \u001b[33;1m{(byte)e.ProgressPercentage}%"
             + $"\u001b[0m ({BytesReceived}) (\u001b[32;1m{CurrentSpeed}/s\u001b[0m)", LogType.NoTag, false, true);
#endif
        }

        void RemoveUpdateDownloadHandler()
        {
            UpdateHttpClient.ProgressChanged -= UpdateDownloadProgressChanged;
            UpdateHttpClient.Completed -= DownloadProgressCompleted;
        }

        void RemoveBlockDictDownloadHandler()
        {
            BlockHttpClient.ProgressChanged -= BlockDictDownloadProgressChanged;
            BlockHttpClient.Completed -= DownloadProgressCompleted;
        }

        void DownloadProgressCompleted(object sender, DownloadProgressCompleted e)
        {
#if DEBUG
            if (e.DownloadCompleted)
            {
                LogWrite($" Done!", LogType.Empty);
                LogWriteLine();
            }
#endif
        }
    }
}
