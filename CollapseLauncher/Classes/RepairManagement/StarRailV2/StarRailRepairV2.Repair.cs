using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.RepairManagement;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Sophon;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher
{
    internal partial class StarRailRepairV2
    {
        private static ReadOnlySpan<byte> HashMarkFileContent => [0x20];

        public async Task StartRepairRoutine(
            bool showInteractivePrompt = false,
            Action? actionIfInteractiveCancel = null)
        {
            await TryRunExamineThrow(StartRepairRoutineCoreAsync(showInteractivePrompt, actionIfInteractiveCancel));

            // Reset status and progress
            ResetStatusAndProgress();

            // Set as completed
            Status.ActivityStatus = IsCacheUpdateMode ? Locale.Lang._CachesPage.CachesStatusUpToDate : Locale.Lang._GameRepairPage.Status7;

            // Update status and progress
            UpdateAll();
        }

        private async Task StartRepairRoutineCoreAsync(bool showInteractivePrompt = false,
                                                       Action? actionIfInteractiveCancel = null)
        {
            if (AssetIndex.Count == 0) throw new InvalidOperationException("There's no broken file being reported! You can't perform repair process!");

            // Swap current found all size to per file size
            ProgressPerFileSizeTotal = ProgressAllSizeTotal;
            ProgressAllSizeTotal = AssetIndex.Where(x => x.FT != FileType.Unused).Sum(x => x.S);

            // Reset progress counter
            ResetProgressCounter();

            if (showInteractivePrompt &&
                actionIfInteractiveCancel != null)
            {
                await SpawnRepairDialog(AssetIndex, actionIfInteractiveCancel);
            }

            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder()
                                     .UseLauncherConfig(DownloadThreadWithReservedCount)
                                     .SetUserAgent(UserAgent)
                                     .SetAllowedDecompression(DecompressionMethods.None)
                                     .Create();

            int threadNum = IsBurstDownloadEnabled
                ? 1
                : ThreadForIONormalized;

            await Parallel.ForEachAsync(AssetIndex,
                                        new ParallelOptions
                                        {
                                            CancellationToken = Token!.Token,
                                            MaxDegreeOfParallelism = threadNum
                                        },
                                        Impl);

            return;

            async ValueTask Impl(FilePropertiesRemote asset, CancellationToken token)
            {
                await (asset switch
                {
                    { AssociatedObject: SophonAsset } => RepairAssetGenericSophonType(asset, token),
                    // ReSharper disable once AccessToDisposedClosure
                    _ => RepairAssetGenericType(client, asset, token)
                });

                if (!asset.IsHasHashMark)
                {
                    return;
                }

                string fileDir       = Path.Combine(GamePath, Path.GetDirectoryName(asset.N) ?? "");
                string fileNameNoExt = Path.GetFileNameWithoutExtension(asset.N);
                string markPath      = Path.Combine(fileDir, $"{fileNameNoExt}_{asset.CRC}.hash");

                File.WriteAllBytes(markPath, HashMarkFileContent);
            }
        }

        private async ValueTask RepairAssetGenericSophonType(
            FilePropertiesRemote asset,
            CancellationToken token)
        {
            // Update repair status to the UI
            this.UpdateCurrentRepairStatus(asset);

            string assetPath = Path.Combine(GamePath, asset.N);
            FileInfo assetFileInfo = new FileInfo(assetPath)
                                    .StripAlternateDataStream()
                                    .EnsureCreationOfDirectory()
                                    .EnsureNoReadOnly();

            try
            {
                await using FileStream assetFileStream = assetFileInfo
                   .Open(FileMode.Create,
                         FileAccess.Write,
                         FileShare.Write,
                         asset.S.GetFileStreamBufferSize());

                if (asset.AssociatedObject is not SophonAsset sophonAsset)
                {
                    throw new
                        InvalidOperationException("Invalid operation! This asset shouldn't have been here! It's not a sophon-based asset!");
                }

                // Download as Sophon asset
                await sophonAsset
                   .WriteToStreamAsync(FallbackCDNUtil.GetGlobalHttpClient(true),
                                       assetFileStream,
                                       readBytes => UpdateProgressCounter(readBytes, readBytes),
                                       token: token);
            }
            finally
            {
                this.PopBrokenAssetFromList(asset);
                assetFileInfo.Directory?.DeleteEmptyDirectory(true);
            }
        }

        private async ValueTask RepairAssetGenericType(
            HttpClient downloadHttpClient,
            FilePropertiesRemote asset,
            CancellationToken token)
        {
            // Update repair status to the UI
            this.UpdateCurrentRepairStatus(asset, IsCacheUpdateMode);

            string assetPath = Path.Combine(GamePath, asset.N);
            FileInfo assetFileInfo = new FileInfo(assetPath)
                                    .StripAlternateDataStream()
                                    .EnsureNoReadOnly();

            try
            {
                if (asset.FT == FileType.Unused)
                {
                    if (assetFileInfo.TryDeleteFile())
                    {
                        Logger.LogWriteLine($"[StarRailRepairV2::RepairAssetGenericType] Unused asset {asset} has been deleted!",
                                            LogType.Default,
                                            true);
                    }

                    return;
                }

                // Use Hi3Helper.Http module to download the file.
                DownloadClient downloadClient = DownloadClient
                   .CreateInstance(downloadHttpClient);

                // Perform download
                await RunDownloadTask(asset.S,
                                      assetFileInfo,
                                      asset.RN,
                                      downloadClient,
                                      ProgressRepairAssetGenericType,
                                      token);

                Logger.LogWriteLine($"[StarRailRepairV2::RepairAssetGenericType] Asset {asset.N} has been downloaded!",
                                    LogType.Default,
                                    true);
            }
            finally
            {
                this.PopBrokenAssetFromList(asset);
                assetFileInfo.Directory?.DeleteEmptyDirectory(true);
            }
        }

        // Note for future me @neon-nyan:
        // This is intended that we ignore DownloadProgress for now as the download size for "per-file" progress
        // is now being handled by this own class progress counter.
        private void ProgressRepairAssetGenericType(int read, DownloadProgress progress) => UpdateProgressCounter(read, read);

        private double _downloadReadLastSpeed;
        private long _downloadReadLastReceivedBytes;
        private long _downloadReadLastTick;

        private double _dataWriteLastSpeed;
        private long _dataWriteLastReceivedBytes;
        private long _dataWriteLastTick;

        private void UpdateProgressCounter(long dataWrite, long downloadRead)
        {
            double speedAll = CalculateSpeed(dataWrite, // dataWrite used as All Progress overall speed.
                                             ref _dataWriteLastSpeed,
                                             ref _dataWriteLastReceivedBytes,
                                             ref _dataWriteLastTick);

            double speedPerFile = CalculateSpeed(downloadRead, // downloadRead used as Per File Progress overall speed.
                                                 ref _downloadReadLastSpeed,
                                                 ref _downloadReadLastReceivedBytes,
                                                 ref _downloadReadLastTick);

            Interlocked.Add(ref ProgressAllSizeCurrent, dataWrite);
            Interlocked.Add(ref ProgressPerFileSizeCurrent, downloadRead);

            if (!CheckIfNeedRefreshStopwatch())
            {
                return;
            }

            double speedClamped = speedAll.ClampLimitedSpeedNumber();
            TimeSpan timeLeftSpan = ConverterTool.ToTimeSpanRemain(ProgressAllSizeTotal,
                                                                   ProgressAllSizeCurrent,
                                                                   speedClamped);

            double percentPerFile = ProgressPerFileSizeCurrent != 0
                ? ConverterTool.ToPercentage(ProgressPerFileSizeTotal, ProgressPerFileSizeCurrent)
                : 0;
            double percentAll = ProgressAllSizeCurrent != 0
                ? ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent)
                : 0;

            lock (Progress)
            {
                Progress.ProgressPerFilePercentage = percentPerFile;
                Progress.ProgressPerFileSizeCurrent = ProgressPerFileSizeCurrent;
                Progress.ProgressPerFileSizeTotal = ProgressPerFileSizeTotal;
                Progress.ProgressAllSizeCurrent = ProgressAllSizeCurrent;
                Progress.ProgressAllSizeTotal = ProgressAllSizeTotal;

                // Calculate speed
                Progress.ProgressAllSpeed = speedClamped;
                Progress.ProgressAllTimeLeft = timeLeftSpan;

                // Update current progress percentages
                Progress.ProgressAllPercentage = percentAll;
            }

            lock (Status)
            {
                // Update current activity status
                Status.IsProgressAllIndetermined = false;
                Status.IsProgressPerFileIndetermined = false;

                // Set time estimation string
                string timeLeftString = string.Format(Locale.Lang._Misc.TimeRemainHMSFormat, Progress.ProgressAllTimeLeft);

                Status.ActivityPerFile = string.Format(Locale.Lang._Misc.Speed, ConverterTool.SummarizeSizeSimple(speedPerFile));
                Status.ActivityAll = string.Format(Locale.Lang._GameRepairPage.PerProgressSubtitle2,
                                                   ConverterTool.SummarizeSizeSimple(ProgressAllSizeCurrent),
                                                   ConverterTool.SummarizeSizeSimple(ProgressAllSizeTotal))
                                     + $" | {timeLeftString}"
                                     + $" ({string.Format(Locale.Lang._Misc.Speed, ConverterTool.SummarizeSizeSimple(speedAll))})";

                // Trigger update
                UpdateAll();
            }
        }

        private void ResetProgressCounter()
        {
            _dataWriteLastSpeed = 0;
            _dataWriteLastReceivedBytes = 0;
            _dataWriteLastTick = 0;

            _downloadReadLastSpeed = 0;
            _downloadReadLastReceivedBytes = 0;
            _downloadReadLastTick = 0;

            ProgressAllSizeCurrent = 0;
            ProgressPerFileSizeCurrent = 0;
        }
    }
}
