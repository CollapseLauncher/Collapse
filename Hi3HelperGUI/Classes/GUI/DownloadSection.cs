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
        MemoryStream blockDictStream;
        BlockData blockData;

        public async Task<bool> DownloadUpdateFilesAsync(CancellationTokenSource tokenSource, CancellationToken token)
        {
            int updateFilesCount = ConfigStore.UpdateFiles.Count;
            string message;
            for (ushort p = 0; p < updateFilesCount; p++)
            {
                RemoveUpdateDownloadHandler();
                if (!Directory.Exists(Path.GetDirectoryName(ConfigStore.UpdateFiles[p].ActualPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(ConfigStore.UpdateFiles[p].ActualPath));

                client.ProgressChanged += UpdateDownloadProgressChanged;
                client.Completed += DownloadProgressCompleted;

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

        public async Task FetchBlockDictionary(CancellationTokenSource tokenSource, CancellationToken token)
        {
            string remotePath,
                   message;

            await Task.Run(async () =>
            {
                foreach (PresetConfigClasses i in ConfigStore.Config)
                {
                    blockDictStream = new MemoryStream();

                    RemoveBlockDictDownloadHandler();
                    RefreshBlockCheckProgressBar();
                    LogWriteLine($"Fetching update data for \u001b[34;1m{i.ZoneName}\u001b[0m (\u001b[32;1m{Path.GetFileName(i.InstallRegistryLocation)}\u001b[0m) version... ");
                    
                    remotePath = i.DictionaryHost + i.BlockDictionaryAddress
                    + $"StreamingAsb/{i.GameVersion}/pc/HD/asb/index.dict";

                    client.ProgressChanged += BlockDictDownloadProgressChanged;
                    client.Completed += DownloadProgressCompleted;

                    message = $"Fetching Block Data... {i.ZoneName} {Path.GetFileName(i.InstallRegistryLocation)}";

                    while (!client.DownloadFileToStream(remotePath, blockDictStream, -1, -1, message))
                    {
                        LogWriteLine($"Retrying...", LogType.Warning);
                        await Task.Delay(3000);
                    }
                    blockData = new BlockData(blockDictStream);
                    blockData.CheckIntegrity();
                    await blockDictStream.DisposeAsync();
                }
            });
        }

        void RemoveUpdateDownloadHandler()
        {
            client.ProgressChanged -= UpdateDownloadProgressChanged;
            client.Completed -= DownloadProgressCompleted;
        }

        void RemoveBlockDictDownloadHandler()
        {
            client.ProgressChanged -= BlockDictDownloadProgressChanged;
            client.Completed -= DownloadProgressCompleted;
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

        void BlockDictDownloadProgressChanged(object sender, DownloadProgressChanged e)
        {
            string BytesReceived = SummarizeSizeSimple(e.BytesReceived);
            string CurrentSpeed = SummarizeSizeSimple(e.CurrentSpeed);
            Dispatcher.Invoke(() =>
            {
                ConfigStore.UpdateFilesTotalDownloaded += e.CurrentReceived;

                BlockProgressLabel.Content = $"{(byte)e.ProgressPercentage}% ({BytesReceived}) ({CurrentSpeed}/s)";
                BlockProgressBar.Value = GetPercentageNumber(e.ProgressPercentage, 100);
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
    }
}
