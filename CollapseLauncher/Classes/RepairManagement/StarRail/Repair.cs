using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    internal partial class StarRailRepair
    {
        private async Task<bool> Repair(List<FilePropertiesRemote> repairAssetIndex, CancellationToken token)
        {
            // Set total activity string as "Waiting for repair process to start..."
            _status.ActivityStatus = Lang._GameRepairPage.Status11;
            _status.IsProgressAllIndetermined = true;
            _status.IsProgressPerFileIndetermined = true;

            // Update status
            UpdateStatus();

            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder<SocketsHttpHandler>()
                .UseLauncherConfig(_downloadThreadCount + _downloadThreadCountReserved)
                .SetUserAgent(_userAgent)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Use the new DownloadClient instance
            DownloadClient downloadClient = DownloadClient.CreateInstance(client);

            // Iterate repair asset and check it using different method for each type
            ObservableCollection<IAssetProperty> assetProperty = new ObservableCollection<IAssetProperty>(AssetEntry);
            var runningTask = new ConcurrentDictionary<(FilePropertiesRemote, IAssetProperty), byte>();
            if (_isBurstDownloadEnabled)
            {
                await Parallel.ForEachAsync(
                    PairEnumeratePropertyAndAssetIndexPackage(
#if ENABLEHTTPREPAIR
                    EnforceHTTPSchemeToAssetIndex(repairAssetIndex)
#else
                    repairAssetIndex
#endif
                    , assetProperty),
                    new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = _downloadThreadCount },
                    async (asset, innerToken) =>
                    {
                        if (!runningTask.TryAdd(asset, 0))
                        {
                            LogWriteLine($"Found duplicated task for {asset.AssetProperty.Name}! Skipping...", LogType.Warning, true);
                            return;
                        }
                        // Assign a task depends on the asset type
                        Task assetTask = asset.AssetIndex.FT switch
                        {
                            FileType.Block => RepairAssetTypeGeneric(asset, downloadClient, _httpClient_RepairAssetProgress, innerToken),
                            FileType.Audio => RepairAssetTypeGeneric(asset, downloadClient, _httpClient_RepairAssetProgress, innerToken),
                            FileType.Video => RepairAssetTypeGeneric(asset, downloadClient, _httpClient_RepairAssetProgress, innerToken),
                            _ => RepairAssetTypeGeneric(asset, downloadClient, _httpClient_RepairAssetProgress, innerToken)
                        };

                        // Await the task
                        await assetTask;
                        runningTask.Remove(asset, out _);
                    });
            }
            else
            {
                foreach ((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset in
                    PairEnumeratePropertyAndAssetIndexPackage(
#if ENABLEHTTPREPAIR
                    EnforceHTTPSchemeToAssetIndex(repairAssetIndex)
#else
                    repairAssetIndex
#endif
                    , assetProperty))
                {
                    if (!runningTask.TryAdd(asset, 0))
                    {
                        LogWriteLine($"Found duplicated task for {asset.AssetProperty.Name}! Skipping...", LogType.Warning, true);
                        break;
                    }
                    // Assign a task depends on the asset type
                    Task assetTask = asset.AssetIndex.FT switch
                    {
                        FileType.Block => RepairAssetTypeGeneric(asset, downloadClient, _httpClient_RepairAssetProgress, token),
                        FileType.Audio => RepairAssetTypeGeneric(asset, downloadClient, _httpClient_RepairAssetProgress, token),
                        FileType.Video => RepairAssetTypeGeneric(asset, downloadClient, _httpClient_RepairAssetProgress, token),
                        _ => RepairAssetTypeGeneric(asset, downloadClient, _httpClient_RepairAssetProgress, token)
                    };

                    // Await the task
                    await assetTask;
                    runningTask.Remove(asset, out _);
                }
            }

            return true;
        }

        #region GenericRepair
        private async Task RepairAssetTypeGeneric((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset, DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, CancellationToken token)
        {
            // Increment total count current
            _progressAllCountCurrent++;
            // Set repair activity status
            string timeLeftString = string.Format(Lang!._Misc!.TimeRemainHMSFormat!, _progress.ProgressAllTimeLeft);
            UpdateRepairStatus(
                string.Format(Lang._GameRepairPage.Status8, Path.GetFileName(asset.AssetIndex.N)),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle2, ConverterTool.SummarizeSizeSimple(_progressAllSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressAllSizeTotal)) + $" | {timeLeftString}",
                true);

            FileInfo fileInfo = new FileInfo(asset.AssetIndex.N!).EnsureNoReadOnly();

            // If asset type is unused, then delete it
            if (asset.AssetIndex.FT == FileType.Unused)
            {
                if (fileInfo.Exists)
                {
                    fileInfo.Delete();
                    LogWriteLine($"File [T: {asset.AssetIndex.FT}] {(asset.AssetIndex.FT == FileType.Block ? asset.AssetIndex.CRC : asset.AssetIndex.N)} deleted!", LogType.Default, true);
                }
                RemoveHashMarkFile(asset.AssetIndex.N, out _, out _);
            }
            else
            {
                // Start asset download task
                await RunDownloadTask(asset.AssetIndex.S, fileInfo, asset.AssetIndex.RN, downloadClient, downloadProgress, token);
                LogWriteLine($"File [T: {asset.AssetIndex.FT}] {(asset.AssetIndex.FT == FileType.Block ? asset.AssetIndex.CRC : asset.AssetIndex.N)} has been downloaded!", LogType.Default, true);
            }

            // Pop repair asset display entry
            PopRepairAssetEntry(asset.AssetProperty);
        }
        #endregion
    }
}
