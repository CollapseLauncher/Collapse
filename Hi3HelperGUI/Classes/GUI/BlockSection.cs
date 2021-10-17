using System;
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
        readonly BlockData blockData = new();

        private void BlockCheckCancel(object sender, RoutedEventArgs e)
        {
            LogWriteLine($"Cancelling Block Checking...", LogType.Warning);
            BlockCheckTokenSource.Cancel();
        }

        public void BlockCheckStart(object sender, RoutedEventArgs e) => DoBlockCheck();

        public void BlockRepairStart(object sender, RoutedEventArgs e) => DoBlockRepair();

        public async void DoBlockCheck()
        {
            BlockCheckTokenSource = new CancellationTokenSource();
            CancellationToken token = BlockCheckTokenSource.Token;

            try
            {
                await FetchBlockDictionary(token);
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Block Checking Cancelled!", LogType.Warning, true);
                ChangeBlockRepairStatus($"Block Checking Cancelled!", true);
            }
        }

        public async void DoBlockRepair()
        {
            BlockCheckTokenSource = new CancellationTokenSource();
            CancellationToken token = BlockCheckTokenSource.Token;

            try
            {
                ChangeBlockRepairStatus($"Preparing repair...", false, false, true, false);
                await RunBlockRepair(token);
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Block Repairing Cancelled!", LogType.Warning, true);
                ChangeBlockRepairStatus($"Block Repairing Cancelled!", true, false);
            }
        }

        public async Task FetchBlockDictionary(CancellationToken token)
        {
            string remotePath,
                   message;

            await Task.Run(() =>
            {
                blockData.DisposeAll();
                Dispatcher.Invoke(() =>
                {
                    BlockChunkTreeView.ItemsSource = blockData.BrokenBlocksRegion;
                });
                foreach (PresetConfigClasses i in ConfigStore.Config)
                {
                    blockDictStream = new MemoryStream();

                    RemoveBlockDictDownloadHandler();
                    RefreshBlockCheckProgressBar();
                    LogWriteLine($"Fetching block data for \u001b[34;1m{i.ZoneName}\u001b[0m (\u001b[32;1m{Path.GetFileName(i.InstallRegistryLocation)}\u001b[0m) version... ");

                    remotePath = i.DictionaryHost + i.BlockDictionaryAddress
                    + $"StreamingAsb/{i.GameVersion}/pc/HD/asb/index.dict";

                    BlockHttpClient.ProgressChanged += BlockDictDownloadProgressChanged;
                    BlockHttpClient.Completed += DownloadProgressCompleted;

                    message = $"Fetching Block Data {i.ZoneName} {Path.GetFileName(i.InstallRegistryLocation)}";
                    ChangeBlockRepairStatus(message, false, false, false, false);

                    while (!BlockHttpClient.DownloadStream(remotePath, blockDictStream, token, -1, -1, message))
                    {
                        LogWriteLine($"Retrying...", LogType.Warning);
                    }
                    blockData.Init(blockDictStream);

                    blockData.CheckingProgressChanged += BlockProgressChanged;
                    blockData.CheckingProgressChangedStatus += BlockProgressChanged;
                    blockData.CheckIntegrity(i, token);

                    if (blockData.BrokenBlocksRegion.Count > 0)
                        ChangeBlockRepairStatus($"Broken blocks have been found. Click Repair Block to start repairing!", true, true, true);
                    else
                        ChangeBlockRepairStatus($"No broken blocks found!", true, false, true);

                    blockData.CheckingProgressChanged -= BlockProgressChanged;
                    blockData.CheckingProgressChangedStatus -= BlockProgressChanged;

                    blockDictStream.Dispose();
                }

                blockData.DisposeAssets();

                Dispatcher.Invoke(() =>
                {
                    BlockChunkTreeView.ItemsSource = blockData.BrokenBlocksRegion;
                });
            });
        }

        public async Task RunBlockRepair(CancellationToken token)
        {
            await Task.Run(() =>
            {
                blockData.RepairingProgressChanged += BlockProgressChanged;
                blockData.RepairingProgressChangedStatus += BlockProgressChanged;
                blockData.BlockRepair(ConfigStore.Config, token);
                ChangeBlockRepairStatus($"Block repair is finished!", true, false, true);

                blockData.RepairingProgressChanged -= BlockProgressChanged;
                blockData.RepairingProgressChangedStatus -= BlockProgressChanged;
            });
        }
        private void BlockProgressChanged(object sender, CheckingBlockProgressChangedStatus e)
        {
            Dispatcher.Invoke(() =>
            {
                BlockCheckStatus.Content = $"Checking Block: [{e.CurrentBlockPos}/{e.BlockCount}] {e.BlockHash}";
            });
        }

        private void BlockProgressChanged(object sender, CheckingBlockProgressChanged e)
        {
            Dispatcher.Invoke(() =>
            {
                BlockProgressLabel.Text = $"Read: {SummarizeSizeSimple(e.BytesRead)}\t Total Size: {SummarizeSizeSimple(e.TotalBlockSize)}";
                BlockProgressBar.Value = GetPercentageNumber(e.BytesRead, e.TotalBlockSize);
            }, DispatcherPriority.Background);
        }

        private void BlockProgressChanged(object sender, RepairingBlockProgressChangedStatus e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.Downloading && e.DownloadingBlock)
                {
                    BlockCheckStatus.Content = $"Downloading: [{e.CurrentBlockPos}/{e.BlockCount}] {e.BlockHash}\tSize: {SummarizeSizeSimple(e.DownloadTotalSize)}";
                }
                else
                {
                    BlockCheckStatus.Content = $"Repairing: [{e.CurrentBlockPos}/{e.BlockCount}] {e.BlockHash}\tOffset: 0x{NumberToHexString(e.ChunkOffset)}\tPos: [{e.CurrentChunkPos}/{e.ChunkCount}]";
                }
            });
        }

        private void BlockProgressChanged(object sender, RepairingBlockProgressChanged e)
        {
            Dispatcher.Invoke(() =>
            {
                BlockProgressLabel.Text = $"Read: {SummarizeSizeSimple(e.TotalBytesRead)}\t Total Size: {SummarizeSizeSimple(e.TotalRepairableSize)}";
                BlockProgressBar.Value = Math.Round(e.ProgressPercentage, 2);
                BlockFetchingProgressLabel.Text = $"{SummarizeSizeSimple(e.DownloadReceivedBytes)} ({SummarizeSizeSimple(e.DownloadSpeed)}/s)";
                BlockFetchingProgressBar.Value = Math.Round(e.DownloadProgressPercentage, 2);
            }, DispatcherPriority.Background);
        }

        private void RefreshBlockCheckProgressBar(double cur = 0, double max = 100)
        {
            Dispatcher.Invoke(() => {
                BlockProgressBar.Value = Math.Round((100 * cur) / max, 2);
                BlockProgressLabel.Text = "none";
            }, DispatcherPriority.Background);
        }

        private void ChangeBlockRepairStatus(string s,bool b, bool c = false,
            bool resetprogress = false, bool hideCancelBtn = true) =>
            Dispatcher.Invoke(() =>
            {
                BlockCheckStatus.Content = s;
                BlockCheckBtn.IsEnabled = b;
                BlockCheckCancelBtn.Visibility = hideCancelBtn ? Visibility.Collapsed : Visibility.Visible;
                BlockRepairBtn.IsEnabled = c;
                BlockChunkTreeView.IsEnabled = b;
                if (resetprogress)
                {
                    BlockFetchingProgressBar.Value = 0;
                    BlockFetchingProgressLabel.Text = "Fetch";
                    BlockProgressBar.Value = 0;
                    BlockProgressLabel.Text = "none";
                }
            });
    }
}
