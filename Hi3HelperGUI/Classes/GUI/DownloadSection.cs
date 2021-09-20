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
using static Hi3HelperGUI.ConverterTools;

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

                client.DownloadProgressChanged += Client_UpdateFilesDownloadProgressChanged(p, $"Down: [{ConfigStore.UpdateFiles[p].ZoneName} > {ConfigStore.UpdateFiles[p].DataType}] ({p+1}/{ConfigStore.UpdateFiles.Count}) {Path.GetFileName(ConfigStore.UpdateFiles[p].N)}");
                client.DownloadCompleted += Client_UpdateFilesDownloadFileCompleted(p);

                ChangeUpdateStatus($"Down: [{ConfigStore.UpdateFiles[p].ZoneName} > {ConfigStore.UpdateFiles[p].DataType}] ({p+1}/{ConfigStore.UpdateFiles.Count}) {Path.GetFileName(ConfigStore.UpdateFiles[p].N)}", false);
                await Task.Run(async () => {
                    token.ThrowIfCancellationRequested();
                    while (!await client.DownloadFileAsync(ConfigStore.UpdateFiles[p].RemotePath, ConfigStore.UpdateFiles[p].ActualPath, token))
                    {
                        if (token.IsCancellationRequested)
                        {
                            // Clean up here, then...
                            token.ThrowIfCancellationRequested();
                        }
                        LogWriteLine($"Retrying...", LogType.Warning);
                    }
                }, token);
            }

            return false;
        }

        public bool DownloadToBuffer(string input, in MemoryStream output)
        {
            DownloadUtils client = new DownloadUtils();

            client.DownloadProgressChanged += Client_DownloadProgressChanged($"Downloading to buffer");
            client.DownloadCompleted += Client_DownloadFileCompleted();

            client.DownloadFileToBuffer(input, output);

            return false;
        }

        public bool DownloadUpdateFiles(List<UpdateDataProperties> input)
        {
            ushort o = 1;
            foreach (UpdateDataProperties i in input)
            {
                DownloadUtils client = new DownloadUtils();
                if (!Directory.Exists(Path.GetDirectoryName(i.ActualPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(i.ActualPath));

                client.DownloadProgressChanged += Client_DownloadProgressChanged($"Down: ({o}/{input.Count}) {Path.GetFileName(i.N)}");
                client.DownloadCompleted += Client_DownloadFileCompleted();

                ChangeUpdateStatus($"Down: [{i.DataType}] ({o}/{input.Count}) {Path.GetFileName(i.N)}", false);
                client.DownloadFile(i.RemotePath, i.ActualPath);
                o += 1;
            }

            return false;
        }

        static long curRecBytes = 0;
        static long prevRecBytes = 0;
        static long bytesInterval;
        static DateTime beginTimeDownload = DateTime.Now;

        private EventHandler<DownloadProgressChangedEventArgs> Client_UpdateFilesDownloadProgressChanged(ushort index, string customMessage = "")
        {
            Action<object, DownloadProgressChangedEventArgs> action = (sender, e) =>
            {
                curRecBytes = e.BytesReceived;
                //if ((DateTime.Now - beginTime).TotalMilliseconds > 250)
                //{
                if ((curRecBytes - prevRecBytes) < 0)
                    bytesInterval = 0;
                if ((DateTime.Now - beginTimeDownload).TotalMilliseconds > 500)
                {
                    bytesInterval = ((curRecBytes - prevRecBytes) < 0 ? 0 : curRecBytes - prevRecBytes) * 2;
                    prevRecBytes = curRecBytes;
                    beginTimeDownload = DateTime.Now;
                }

                ConfigStore.UpdateFilesTotalDownloaded += e.CurrentReceivedBytes;

                if ((DateTime.Now - beginTimeDownload).TotalMilliseconds > 100)
                    RefreshUpdateProgressLabel($"{(byte)e.ProgressPercentage}% ({SummarizeSizeSimple(e.BytesReceived)}) ({SummarizeSizeSimple(bytesInterval)}/s)");

                /* TODO
                 * Realtime change Download Status for specific item.
                if ((DateTime.Now - beginTimeDownload).TotalMilliseconds > 1000)
                {
                    RefreshUpdateListView();
                    ConfigStore.UpdateFiles[index].DownloadStatus = $"{(e.NoProgress ? "Unknown" : $"{(byte)e.ProgressPercentage}%")} ({(bytesInterval == 0 || e.NoProgress ? "n/a" : (SummarizeSize(bytesInterval)))}/s) {SummarizeSize(e.BytesReceived)}";
                }
                */
                RefreshTotalProgressBar(ConfigStore.UpdateFilesTotalDownloaded, ConfigStore.UpdateFilesTotalSize);
                LogWrite($"{customMessage} \u001b[33;1m{(e.NoProgress ? "Unknown" : $"{(byte)e.ProgressPercentage}%")}"
                 + $"\u001b[0m ({SummarizeSizeSimple(e.BytesReceived)}) (\u001b[32;1m{SummarizeSizeSimple(bytesInterval)}/s\u001b[0m)", LogType.NoTag, false, true);
                //}
            };
            return new EventHandler<DownloadProgressChangedEventArgs>(action);
        }

        private static EventHandler Client_UpdateFilesDownloadFileCompleted(ushort index)
        {
            Action<object, EventArgs> action = (sender, e) =>
            {
                //ConfigStore.UpdateFiles[index].DownloadStatus = "Done!";
                LogWrite($" Done!", LogType.Empty);
                Console.WriteLine();
            };
            return new EventHandler(action);
        }

        private EventHandler<DownloadProgressChangedEventArgs> Client_DownloadProgressChanged(string customMessage = "")
        {
            Action<object, DownloadProgressChangedEventArgs> action = (sender, e) =>
            {
                curRecBytes = e.BytesReceived;
                //if ((DateTime.Now - beginTime).TotalMilliseconds > 250)
                //{
                if ((curRecBytes - prevRecBytes) < 0)
                        bytesInterval = 0;
                if ((DateTime.Now - beginTimeDownload).TotalMilliseconds > 500)
                {
                    bytesInterval = ((curRecBytes - prevRecBytes) < 0 ? 0 : curRecBytes - prevRecBytes) * 2;
                    prevRecBytes = curRecBytes;
                    beginTimeDownload = DateTime.Now;
                }
                ConfigStore.UpdateFilesTotalDownloaded += e.CurrentReceivedBytes;
                RefreshTotalProgressBar(ConfigStore.UpdateFilesTotalDownloaded, ConfigStore.UpdateFilesTotalSize);
                LogWrite($"{customMessage} \u001b[33;1m{(e.NoProgress ? "Unknown" : $"{(byte)e.ProgressPercentage}%")}"
                 + $"\u001b[0m ({SummarizeSizeSimple(e.BytesReceived)}) (\u001b[32;1m{(bytesInterval == 0 || e.NoProgress ? "n/a" : (SummarizeSizeSimple(bytesInterval) + "/s"))}\u001b[0m)", LogType.NoTag, false, true);
                //}
            };
            return new EventHandler<DownloadProgressChangedEventArgs>(action);
        }

        private static EventHandler Client_DownloadFileCompleted()
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
