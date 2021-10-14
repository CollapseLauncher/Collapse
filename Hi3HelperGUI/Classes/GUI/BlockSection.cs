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
        CancellationTokenSource BlockCheckTokenSource;
        MemoryStream blockDictStream;
        BlockData blockData = new BlockData();

        private void BlockCheckCancel(object sender, RoutedEventArgs e)
        {
            LogWriteLine($"Cancelling Block Checking...", LogType.Warning);
            BlockCheckTokenSource.Cancel();
        }

        public void BlockCheckStart(object sender, RoutedEventArgs e) => DoBlockCheck();

        public async void DoBlockCheck()
        {
            BlockCheckTokenSource = new CancellationTokenSource();
            CancellationToken token = BlockCheckTokenSource.Token;

            try
            {
                await FetchBlockDictionary(BlockCheckTokenSource, token);
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Block Checking Cancelled!", LogType.Warning, true);
                ChangeBlockRepairStatus($"Block Checking Cancelled!", true);
            }
        }

        public async Task FetchBlockDictionary(CancellationTokenSource tokenSource, CancellationToken token)
        {
            string remotePath,
                   message;

            await Task.Run(async () =>
            {
                blockData.FlushProp();
                foreach (PresetConfigClasses i in ConfigStore.Config)
                {
                    blockDictStream = new MemoryStream();

                    RemoveBlockDictDownloadHandler();
                    RefreshBlockCheckProgressBar();
                    LogWriteLine($"Fetching update data for \u001b[34;1m{i.ZoneName}\u001b[0m (\u001b[32;1m{Path.GetFileName(i.InstallRegistryLocation)}\u001b[0m) version... ");

                    remotePath = i.DictionaryHost + i.BlockDictionaryAddress
                    + $"StreamingAsb/{i.GameVersion}/pc/HD/asb/index.dict";

                    BlockHttpClient.ProgressChanged += BlockDictDownloadProgressChanged;
                    BlockHttpClient.Completed += DownloadProgressCompleted;

                    message = $"Fetching Block Data {i.ZoneName} {Path.GetFileName(i.InstallRegistryLocation)}";
                    ChangeBlockRepairStatus(message, false);

                    while (!BlockHttpClient.DownloadStream(remotePath, blockDictStream, token, -1, -1, message))
                    {
                        LogWriteLine($"Retrying...", LogType.Warning);
                        await Task.Delay(3000);
                    }
                    blockData.Init(blockDictStream, i);

                    blockData.ProgressChanged -= BlockCheckProgressChanged;
                    blockData.ProgressChanged += BlockCheckProgressChanged;
                    blockData.CheckIntegrity(i, token);

                    if (blockData.BrokenBlocksRegion.Count > 0)
                        ChangeBlockRepairStatus($"Broken blocks have been found. Click Repair Block to start repairing!", true, true);
                    else
                        ChangeBlockRepairStatus($"No broken blocks found!", true, false);

                    blockDictStream.Dispose();
                }

                Dispatcher.Invoke(() =>
                {
                    BlockChunkTreeView.ItemsSource = blockData.BrokenBlocksRegion;
                });
            });
        }

        private void BlockCheckProgressChanged(object sender, ReadingBlockProgressChanged e)
        {
            string BytesReceived = SummarizeSizeSimple(e.BytesRead);
            double Percentage = GetPercentageNumber(e.BytesRead, e.TotalBlockSize);
            Dispatcher.Invoke(() =>
            {
                BlockCheckStatus.Content = $"Checking Block: {e.BlockHash}";
                BlockProgressLabel.Content = $"Read: {SummarizeSizeSimple(e.BytesRead)}\t Total Size: {SummarizeSizeSimple(e.TotalBlockSize)}";
                BlockProgressBar.Value = Percentage;
            }, DispatcherPriority.Background);
        }

        private void RefreshBlockCheckProgressBar(double cur = 0, double max = 100) => Dispatcher.Invoke(() => BlockProgressBar.Value = Math.Round((100 * cur) / max, 2), DispatcherPriority.Background);

        private void ChangeBlockRepair(bool a) =>
            Dispatcher.Invoke(() =>
            {
                BlockRepairBtn.IsEnabled = a;
                UpdateListView.IsEnabled = a;
            });

        private void ChangeBlockRepairStatus(string s, bool b, bool c = false) =>
            Dispatcher.Invoke(() =>
            {
                BlockCheckStatus.Content = s;
                BlockCheckBtn.IsEnabled = b;
                BlockCheckCancelBtn.Visibility = b ? Visibility.Collapsed : Visibility.Visible;
                BlockRepairBtn.IsEnabled = c;
            });
    }
}
