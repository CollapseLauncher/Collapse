using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Hi3HelperGUI.Preset;
using Hi3HelperGUI.Data;

using static Hi3HelperGUI.Logger;
using static Hi3HelperGUI.Data.ConverterTool;

namespace Hi3HelperGUI
{
    public partial class MainWindow
    {
        public async Task<bool> DownloadUpdateFilesAsync(CancellationTokenSource tokenSource, CancellationToken token)
        {
            for (ushort p = 0; p < ConfigStore.UpdateFiles.Count; p++)
            {
                DownloadUtils client = new DownloadUtils();
                if (!Directory.Exists(Path.GetDirectoryName(ConfigStore.UpdateFiles[p].ActualPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(ConfigStore.UpdateFiles[p].ActualPath));

                client.DownloadProgressChanged += Client_UpdateFilesDownloadProgressChanged($"Down: [{ConfigStore.UpdateFiles[p].ZoneName} > {ConfigStore.UpdateFiles[p].DataType}] ({p+1}/{ConfigStore.UpdateFiles.Count}) {Path.GetFileName(ConfigStore.UpdateFiles[p].N)}");
                client.DownloadCompleted += Client_UpdateFilesDownloadFileCompleted();

                ChangeUpdateStatus($"Down: [{ConfigStore.UpdateFiles[p].ZoneName} > {ConfigStore.UpdateFiles[p].DataType}] ({p+1}/{ConfigStore.UpdateFiles.Count}) {Path.GetFileName(ConfigStore.UpdateFiles[p].N)}", false);
                await Task.Run(async () => {
                    token.ThrowIfCancellationRequested();
                    while (!await client.DownloadFileAsync(ConfigStore.UpdateFiles[p].RemotePath, ConfigStore.UpdateFiles[p].ActualPath, token))
                    {
                        if (token.IsCancellationRequested)
                            token.ThrowIfCancellationRequested();

                        LogWriteLine($"Retrying...", LogType.Warning);
                    }
                }, token);
            }

            return false;
        }

        private EventHandler<DownloadProgressChangedEventArgs> Client_UpdateFilesDownloadProgressChanged(string customMessage = "")
        {
            Action<object, DownloadProgressChangedEventArgs> action = (sender, e) =>
            {
                ConfigStore.UpdateFilesTotalDownloaded += e.CurrentReceivedBytes;

                RefreshUpdateProgressLabel($"{(byte)e.ProgressPercentage}% ({SummarizeSizeSimple(e.BytesReceived)}) ({SummarizeSizeSimple(e.CurrentSpeed)}/s)");

                RefreshTotalProgressBar(ConfigStore.UpdateFilesTotalDownloaded, ConfigStore.UpdateFilesTotalSize);
                LogWrite($"{customMessage} \u001b[33;1m{(e.NoProgress ? "Unknown" : $"{(byte)e.ProgressPercentage}%")}"
                 + $"\u001b[0m ({SummarizeSizeSimple(e.BytesReceived)}) (\u001b[32;1m{SummarizeSizeSimple(e.CurrentSpeed)}/s\u001b[0m)", LogType.NoTag, false, true);
            };
            return new EventHandler<DownloadProgressChangedEventArgs>(action);
        }

        private static EventHandler Client_UpdateFilesDownloadFileCompleted()
        {
            Action<object, EventArgs> action = (sender, e) =>
            {
                LogWrite($" Done!", LogType.Empty);
                Console.WriteLine();
            };
            return new EventHandler(action);
        }
    }
}
