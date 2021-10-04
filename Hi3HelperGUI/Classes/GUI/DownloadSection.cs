using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Hi3HelperGUI.Preset;
using Hi3HelperGUI.Data;

using static Hi3HelperGUI.Logger;
using static Hi3HelperGUI.Data.ConverterTool;

namespace Hi3HelperGUI
{
    public partial class MainWindow : Window
    {
        readonly HttpClientTool client = new HttpClientTool();
        public async Task<bool> DownloadUpdateFilesAsync(CancellationTokenSource tokenSource, CancellationToken token)
        {
            int updateFilesCount = ConfigStore.UpdateFiles.Count;
            string message;
            for (ushort p = 0; p < updateFilesCount; p++)
            {
                RemoveDownloadHandler();
                if (!Directory.Exists(Path.GetDirectoryName(ConfigStore.UpdateFiles[p].ActualPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(ConfigStore.UpdateFiles[p].ActualPath));

                client.ProgressChanged += new EventHandler<DownloadProgressChanged>(DownloadProgressChanged);
                client.Completed += new EventHandler<DownloadProgressCompleted>(DownloadProgressCompleted);

                message = $"Down: [{ConfigStore.UpdateFiles[p].ZoneName} > {ConfigStore.UpdateFiles[p].DataType}] ({p + 1}/{ConfigStore.UpdateFiles.Count}) {Path.GetFileName(ConfigStore.UpdateFiles[p].N)}";
                //client.ProgressChanged += DownloadProgressChanges($"Down: [{ConfigStore.UpdateFiles[p].ZoneName} > {ConfigStore.UpdateFiles[p].DataType}] ({p + 1}/{ConfigStore.UpdateFiles.Count}) {Path.GetFileName(ConfigStore.UpdateFiles[p].N)}");
                //client.Completed += DownloadProgressCompleted();

                ChangeUpdateStatus(message, false);
                await Task.Run(async () => {
                    while (!await client.DownloadFile(ConfigStore.UpdateFiles[p].RemotePath, ConfigStore.UpdateFiles[p].ActualPath, token, -1, -1, message))
                    {
                        LogWriteLine($"Retrying...", LogType.Warning);
                        await Task.Delay(3000);
                    }
                }, token);
            }

            return false;
        }

        void RemoveDownloadHandler()
        {
            client.ProgressChanged -= new EventHandler<DownloadProgressChanged>(DownloadProgressChanged);
            client.Completed -= new EventHandler<DownloadProgressCompleted>(DownloadProgressCompleted);
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

        void DownloadProgressChanged(object sender, DownloadProgressChanged e)
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
    }
}
