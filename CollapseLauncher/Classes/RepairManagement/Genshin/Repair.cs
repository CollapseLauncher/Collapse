using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.Http;
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
    internal partial class GenshinRepair
    {
        private async Task<bool> Repair(List<PkgVersionProperties> repairAssetIndex, CancellationToken token)
        {
            // Set total activity string as "Waiting for repair process to start..."
            _status!.ActivityStatus = Lang._GameRepairPage.Status11;
            _status.IsProgressAllIndetermined = true;
            _status.IsProgressPerFileIndetermined = true;

            // Update status
            UpdateStatus();

            // Reset stopwatch
            RestartStopwatch();

            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder<SocketsHttpHandler>()
                .UseLauncherConfig(_downloadThreadCount + _downloadThreadCountReserved)
                .SetUserAgent(_userAgent)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Use the new DownloadClient instance
            DownloadClient downloadClient = DownloadClient.CreateInstance(client);

            // Iterate repair asset
            ObservableCollection<IAssetProperty> assetProperty = new ObservableCollection<IAssetProperty>(AssetEntry);
            if (_isBurstDownloadEnabled)
            {
                var processingAsset = new ConcurrentDictionary<(PkgVersionProperties, IAssetProperty), byte>();
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
                        if (!processingAsset.TryAdd(asset, 0))
                        {
                            LogWriteLine($"Asset {asset.AssetIndex.localName} is already being processed, skipping...",
                                         LogType.Warning, true);
                            return;
                        }
                        await RepairAssetTypeGeneric(asset, downloadClient, _httpClient_RepairAssetProgress, innerToken);
                        processingAsset.TryRemove(asset, out _);
                    });
            }
            else
            {
                var processingAsset = new ConcurrentDictionary<(PkgVersionProperties, IAssetProperty), byte>();
                foreach ((PkgVersionProperties AssetIndex, IAssetProperty AssetProperty) asset in
                    PairEnumeratePropertyAndAssetIndexPackage(
#if ENABLEHTTPREPAIR
                    EnforceHTTPSchemeToAssetIndex(repairAssetIndex)
#else
                    repairAssetIndex
#endif
                    , assetProperty))
                {
                    if (!processingAsset.TryAdd(asset, 0))
                    {
                        LogWriteLine($"Asset {asset.AssetIndex.localName} is already being processed, skipping...",
                                     LogType.Warning, true);
                        continue;
                    }
                    await RepairAssetTypeGeneric(asset, downloadClient, _httpClient_RepairAssetProgress, token);
                    processingAsset.TryRemove(asset, out _);
                }
            }

            return true;
        }

        #region GenericRepair
        private async Task RepairAssetTypeGeneric((PkgVersionProperties AssetIndex, IAssetProperty AssetProperty) asset, DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, CancellationToken token)
        {
            // Increment total count current
            _progressAllCountCurrent++;
            // Set repair activity status
            UpdateRepairStatus(
                string.Format(Lang._GameRepairPage.Status8, asset.AssetIndex.remoteName),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle2, _progressAllCountCurrent, _progressAllCountTotal),
                true);

            string   assetPath     = ConverterTool.NormalizePath(asset.AssetIndex.localName);
            FileInfo assetFileInfo = new FileInfo(assetPath).EnsureCreationOfDirectory().EnsureNoReadOnly();

            // If file is unused, then delete
            if (asset.AssetIndex.type == "Unused")
            {
                // Delete the file
                assetFileInfo.Delete();
            }
            else
            {
                // or start asset download task
                await RunDownloadTask(asset.AssetIndex.fileSize, assetFileInfo, asset.AssetIndex.remoteURL, downloadClient, downloadProgress, token);
                LogWriteLine($"File [T: {RepairAssetType.Generic}] {asset.AssetIndex.remoteName} has been downloaded!", LogType.Default, true);
            }

            // Pop repair asset display entry
            PopRepairAssetEntry(asset.AssetProperty);
        }
        #endregion
    }
}
