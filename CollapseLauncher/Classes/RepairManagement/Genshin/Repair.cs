using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.Http;
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

            // Use the new DownloadClient instance
            DownloadClient downloadClient = DownloadClient.CreateInstance(client);

            // Iterate repair asset
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
                        await RepairAssetTypeGeneric(asset, downloadClient, _httpClient_RepairAssetProgress, innerToken);
                    });
            }
            else
            {
                foreach ((PkgVersionProperties AssetIndex, IAssetProperty AssetProperty) asset in
                    PairEnumeratePropertyAndAssetIndexPackage(
#if ENABLEHTTPREPAIR
                    EnforceHTTPSchemeToAssetIndex(repairAssetIndex)
#else
                    repairAssetIndex
#endif
                    , assetProperty))
                {
                    await RepairAssetTypeGeneric(asset, downloadClient, _httpClient_RepairAssetProgress, token);
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

            // If file is unused, then delete
            if (asset.AssetIndex.type == "Unused")
            {
                string assetPath = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.AssetIndex.localName));

                // Delete the file
                TryDeleteReadOnlyFile(assetPath);
            }
            else
            {
                string assetPath = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.AssetIndex.remoteName));

                // or start asset download task
                await RunDownloadTask(asset.AssetIndex.fileSize, assetPath, asset.AssetIndex.remoteURL, downloadClient, downloadProgress, token);
                LogWriteLine($"File [T: {RepairAssetType.General}] {asset.AssetIndex.remoteName} has been downloaded!", LogType.Default, true);
            }

            // Pop repair asset display entry
            PopRepairAssetEntry(asset.AssetProperty);
        }
        #endregion
    }
}
