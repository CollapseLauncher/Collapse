using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.EncTool.Parser.YSDispatchHelper;
using Hi3Helper.Http;
using Hi3Helper.Sophon;
using System;
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
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

namespace CollapseLauncher
{
    internal partial class GenshinRepair
    {
        private async Task<bool> Repair(List<PkgVersionProperties> repairAssetIndex, CancellationToken token)
        {
            // Set total activity string as "Waiting for repair process to start..."
            Status.ActivityStatus = Lang._GameRepairPage.Status11;
            Status.IsProgressAllIndetermined = true;
            Status.IsProgressPerFileIndetermined = true;

            // Update status
            UpdateStatus();

            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder<SocketsHttpHandler>()
                .UseLauncherConfig(DownloadThreadWithReservedCount)
                .SetUserAgent(UserAgent)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Use the new DownloadClient instance
            DownloadClient downloadClient = DownloadClient.CreateInstance(client);

            // Get the Dispatcher Query
            QueryProperty queryProperty = await GetCachedDispatcherQuery(downloadClient.GetHttpClient(), token);

            // Iterate repair asset
            ObservableCollection<IAssetProperty> assetProperty = [.. AssetEntry];
            if (IsBurstDownloadEnabled)
            {
                ConcurrentDictionary<(PkgVersionProperties, IAssetProperty), byte> processingAsset = new();
                await Parallel.ForEachAsync(
                    PairEnumeratePropertyAndAssetIndexPackage(
#if ENABLEHTTPREPAIR
                    EnforceHttpSchemeToAssetIndex(repairAssetIndex)
#else
                    repairAssetIndex
#endif
                    , assetProperty),
                    new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = DownloadThreadCount },
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
                ConcurrentDictionary<(PkgVersionProperties, IAssetProperty), byte> processingAsset = new();
                foreach ((PkgVersionProperties AssetIndex, IAssetProperty AssetProperty) asset in
                    PairEnumeratePropertyAndAssetIndexPackage(
#if ENABLEHTTPREPAIR
                    EnforceHttpSchemeToAssetIndex(repairAssetIndex)
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

            await SavePersistentRevision(queryProperty, token);

            // Duplicate ctable.dat to ctable_streaming.dat
            string   streamingAssetsPath = Path.Combine(GamePath,            $"{ExecPrefix}_Data", "StreamingAssets");
            string   ctablePath          = Path.Combine(streamingAssetsPath, "ctable.dat");
            FileInfo ctableFileInfo      = new FileInfo(ctablePath).EnsureCreationOfDirectory().StripAlternateDataStream().EnsureNoReadOnly();
            string   ctableStreamingPath = Path.Combine(streamingAssetsPath, "ctable_streaming.dat");

            // ReSharper disable once InvertIf
            if (ctableFileInfo.Exists)
            {
                new FileInfo(ctableStreamingPath).EnsureCreationOfDirectory().StripAlternateDataStream().EnsureNoReadOnly();
                ctableFileInfo.CopyTo(ctableStreamingPath, true);
                LogWriteLine($"File [T: {RepairAssetType.Generic}] {ctableStreamingPath} has been copied!", LogType.Default, true);
            }

            return true;
        }

        #region GenericRepair
        private async Task RepairAssetTypeGeneric((PkgVersionProperties AssetIndex, IAssetProperty AssetProperty) asset, DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, CancellationToken token)
        {
            ConverterTool.NormalizePathInplaceNoTrim(asset.AssetIndex.remoteName);

            // Increment total count current
            ProgressAllCountCurrent++;
            // Set repair activity status
            string timeLeftString = string.Format(Lang!._Misc!.TimeRemainHMSFormat!, Progress.ProgressAllTimeLeft);
            UpdateRepairStatus(
                string.Format(Lang._GameRepairPage.Status8, asset.AssetIndex.remoteName),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle2, ConverterTool.SummarizeSizeSimple(ProgressAllSizeCurrent), ConverterTool.SummarizeSizeSimple(ProgressAllSizeTotal)) + $" | {timeLeftString}",
                true);

            string   assetPath     = Path.Combine(GamePath, ConverterTool.NormalizePath(asset.AssetIndex.remoteName));
            FileInfo assetFileInfo = new FileInfo(assetPath).EnsureCreationOfDirectory().StripAlternateDataStream().EnsureNoReadOnly();
            bool     isSuccess     = false;

            try
            {
                // If file is unused, then delete
                if (asset.AssetProperty.AssetTypeString.Equals("Unused", StringComparison.OrdinalIgnoreCase))
                {
                    // Delete the file
                    assetFileInfo.Delete();
                    return;
                }

                bool isUseSophonDownload = string.IsNullOrEmpty(asset.AssetIndex.remoteURL);
                if (isUseSophonDownload)
                {
                    ReadOnlySpan<char> splittedPath = asset.AssetIndex.remoteName.TrimStart('\\');
                    if (!SophonAssetDictRefLookup.TryGetValue(splittedPath, out SophonAsset downloadAsSophon))
                    {
                        throw new InvalidOperationException($"Asset {splittedPath} is marked as \"SophonGeneric\" but it wasn't included in the manifest");
                    }

                    DownloadProgress downloadStatus = new()
                    {
                        BytesTotal      = asset.AssetIndex.fileSize,
                        BytesDownloaded = 0
                    };

                    await using FileStream outputStreamAsSophon = assetFileInfo.Create();
                    await downloadAsSophon.WriteToStreamAsync(downloadClient.GetHttpClient(),
                                                              outputStreamAsSophon,
                                                              x =>
                                                              {
                                                                  Interlocked.Add(ref downloadStatus.BytesDownloaded, x);
                                                                  downloadProgress((int)x, downloadStatus);
                                                              },
                                                              token: token);
                    isSuccess = true;
                    return;
                }

                // or start asset download task
                await RunDownloadTask(asset.AssetIndex.fileSize,
                                      assetFileInfo,
                                      asset.AssetIndex.remoteURL,
                                      asset.AssetIndex.remoteURLAlternative,
                                      downloadClient,
                                      downloadProgress,
                                      token);
                isSuccess = true;
            }
            finally
            {
                // Pop repair asset display entry
                PopRepairAssetEntry(asset.AssetProperty);
                if (isSuccess)
                {
                    LogWriteLine($"File [T: {asset.AssetProperty.AssetTypeString}] {asset.AssetIndex.remoteName} has been downloaded!", LogType.Default, true);
                }
            }
        }
        #endregion
    }
}
