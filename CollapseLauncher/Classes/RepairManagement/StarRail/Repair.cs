using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
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

            // Reset stopwatch
            RestartStopwatch();

            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder<SocketsHttpHandler>()
                .UseLauncherConfig(_downloadThreadCount + 16)
                .SetUserAgent(_userAgent)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Use HttpClient instance on fetching
            using Http _httpClient = new Http(true, 5, 1000, _userAgent, client);

            // Try running instance
            try
            {
                // Assign downloader event
                _httpClient.DownloadProgress += _httpClient_RepairAssetProgress;

                // Iterate repair asset and check it using different method for each type
                ObservableCollection<IAssetProperty> assetProperty = new ObservableCollection<IAssetProperty>(AssetEntry);
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
                            // Assign a task depends on the asset type
                            Task assetTask = asset.AssetIndex.FT switch
                            {
                                FileType.Blocks => RepairAssetTypeGeneric(asset, _httpClient, innerToken),
                                FileType.Audio => RepairAssetTypeGeneric(asset, _httpClient, innerToken),
                                FileType.Video => RepairAssetTypeGeneric(asset, _httpClient, innerToken),
                                _ => RepairAssetTypeGeneric(asset, _httpClient, innerToken)
                            };

                            // Await the task
                            await assetTask;
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
                        // Assign a task depends on the asset type
                        Task assetTask = asset.AssetIndex.FT switch
                        {
                            FileType.Blocks => RepairAssetTypeGeneric(asset, _httpClient, token),
                            FileType.Audio => RepairAssetTypeGeneric(asset, _httpClient, token),
                            FileType.Video => RepairAssetTypeGeneric(asset, _httpClient, token),
                            _ => RepairAssetTypeGeneric(asset, _httpClient, token)
                        };

                        // Await the task
                        await assetTask;
                    }
                }

                return true;
            }
            finally
            {
                // Unassign downloader event
                _httpClient.DownloadProgress -= _httpClient_RepairAssetProgress;
            }
        }

        #region GenericRepair
        private async Task RepairAssetTypeGeneric((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset, Http _httpClient, CancellationToken token)
        {
            // Increment total count current
            _progressAllCountCurrent++;
            // Set repair activity status
            UpdateRepairStatus(
                string.Format(Lang._GameRepairPage.Status8, Path.GetFileName(asset.AssetIndex.N)),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle2, ConverterTool.SummarizeSizeSimple(_progressAllSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressAllSizeTotal)),
                true);

            // If asset type is unused, then delete it
            if (asset.AssetIndex.FT == FileType.Unused)
            {
                FileInfo fileInfo = new FileInfo(asset.AssetIndex.N);
                if (fileInfo.Exists)
                {
                    fileInfo.IsReadOnly = false;
                    fileInfo.Delete();
                    LogWriteLine($"File [T: {asset.AssetIndex.FT}] {(asset.AssetIndex.FT == FileType.Blocks ? asset.AssetIndex.CRC : asset.AssetIndex.N)} deleted!", LogType.Default, true);
                }
                RemoveHashMarkFile(asset.AssetIndex.N, out _, out _);
            }
            else
            {
                // Start asset download task
                await RunDownloadTask(asset.AssetIndex.S, asset.AssetIndex.N, asset.AssetIndex.RN, _httpClient, token);
                LogWriteLine($"File [T: {asset.AssetIndex.FT}] {(asset.AssetIndex.FT == FileType.Blocks ? asset.AssetIndex.CRC : asset.AssetIndex.N)} has been downloaded!", LogType.Default, true);
            }

            // Pop repair asset display entry
            PopRepairAssetEntry(asset.AssetProperty);
        }
        #endregion
    }
}
