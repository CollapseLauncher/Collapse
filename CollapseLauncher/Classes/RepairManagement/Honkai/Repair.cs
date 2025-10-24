using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
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

namespace CollapseLauncher
{
    internal partial class HonkaiRepair
    {
        private async Task<bool> Repair(List<FilePropertiesRemote> repairAssetIndex, CancellationToken token)
        {
            // Set total activity string as "Waiting for repair process to start..."
            Status.ActivityStatus                = Lang._GameRepairPage.Status11;
            Status.IsProgressAllIndetermined     = true;
            Status.IsProgressPerFileIndetermined = true;
            
            // Update status
            UpdateStatus();

            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder()
                .UseLauncherConfig(DownloadThreadWithReservedCount)
                .SetUserAgent(UserAgent)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Initialize the new DownloadClient instance
            DownloadClient downloadClient = DownloadClient.CreateInstance(client);

            // Iterate repair asset and check it using different method for each type
            ObservableCollection<IAssetProperty>                               assetProperty = [.. AssetEntry];
            ConcurrentDictionary<(FilePropertiesRemote, IAssetProperty), byte> runningTask   = new();
            if (IsBurstDownloadEnabled)
            {
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
                        if (!runningTask.TryAdd(asset, 0))
                        {
                            LogWriteLine($"Found duplicated task for {asset.AssetProperty.Name}! Skipping...", LogType.Warning, true);
                            return;
                        }
                        // Assign a task depends on the asset type
                        Task assetTask = asset.AssetIndex.FT switch
                        {
                            FileType.Block => RepairAssetTypeBlocks(asset, downloadClient, _httpClient_RepairAssetProgress, innerToken),
                            FileType.Audio => RepairOrPatchTypeAudio(asset, downloadClient, _httpClient_RepairAssetProgress, innerToken),
                            FileType.Video => RepairAssetTypeVideo(asset, downloadClient, _httpClient_RepairAssetProgress, innerToken),
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
                    EnforceHttpSchemeToAssetIndex(repairAssetIndex)
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

                    Task assetTask = asset.AssetIndex.AssociatedObject is SophonAsset asSophonAsset
                        ? RepairAssetTypeSophon(asSophonAsset,
                                                asset.AssetIndex,
                                                asset.AssetProperty,
                                                downloadClient,
                                                _httpClient_RepairAssetProgress,
                                                token)
                        : asset.AssetIndex.FT switch
                          {
                              FileType.Block => RepairAssetTypeBlocks(asset, downloadClient, _httpClient_RepairAssetProgress, token),
                              FileType.Audio => RepairOrPatchTypeAudio(asset, downloadClient, _httpClient_RepairAssetProgress, token),
                              FileType.Video => RepairAssetTypeVideo(asset, downloadClient, _httpClient_RepairAssetProgress, token),
                              _ => RepairAssetTypeGeneric(asset, downloadClient, _httpClient_RepairAssetProgress, token)
                          };

                    // Await the task
                    await assetTask;
                    runningTask.Remove(asset, out _);
                }
            }

            return true;
        }

        #region VideoRepair
        private async Task RepairAssetTypeVideo((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset, DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, CancellationToken token) =>
            await RepairAssetTypeGeneric(asset, downloadClient, downloadProgress, token, asset.AssetIndex.RN);
        #endregion

        #region AudioRepairOrPatch
        private async Task RepairOrPatchTypeAudio((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset, DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, CancellationToken token)
        {
            if (asset.AssetIndex.IsPatchApplicable)
            {
                await RepairTypeAudioActionPatching(asset, downloadClient, downloadProgress, token);
            }
            else
            {
                string audioURL = string.Format(AudioBaseRemotePath, $"{GameVersion.Major}_{GameVersion.Minor}", GameServer.Manifest.ManifestAudio.ManifestAudioRevision).CombineURLFromString(asset.AssetIndex.RN);
                await RepairAssetTypeGeneric(asset, downloadClient, downloadProgress, token, audioURL);
            }
        }

        private async Task RepairTypeAudioActionPatching((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset, DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, CancellationToken token)
        {
            // Increment total count current
            ProgressAllCountCurrent++;

            // Declare variables for patch file and URL and new file path
            if (asset.AssetIndex.AudioPatchInfo != null)
            {
                string patchURL       = string.Format(AudioPatchBaseRemotePath, $"{GameVersion.Major}_{GameVersion.Minor}", GameServer.Manifest.ManifestAudio.ManifestAudioRevision).CombineURLFromString(asset.AssetIndex.AudioPatchInfo.PatchFilename);
                string patchPath      = Path.Combine(GamePath, ConverterTool.NormalizePath(AudioPatchBaseLocalPath), asset.AssetIndex.AudioPatchInfo.PatchFilename);
                string inputFilePath  = Path.Combine(GamePath, ConverterTool.NormalizePath(asset.AssetIndex.N));
                string outputFilePath = inputFilePath + "_tmp";

                // Set downloading patch status
                string timeLeftString = string.Format(Lang!._Misc!.TimeRemainHMSFormat!, Progress.ProgressAllTimeLeft);
                UpdateRepairStatus(
                                   string.Format(Lang._GameRepairPage.Status12,             asset.AssetIndex.N),
                                   string.Format(Lang._GameRepairPage.PerProgressSubtitle4, ConverterTool.SummarizeSizeSimple(ProgressAllSizeCurrent), ConverterTool.SummarizeSizeSimple(ProgressAllSizeTotal)) + $" | {timeLeftString}",
                                   true);

                // Run patching task
                await RunPatchTask(downloadClient,
                                   downloadProgress,
                                   asset.AssetIndex.AudioPatchInfo.PatchFileSize,
                                   asset.AssetIndex.AudioPatchInfo.PatchMD5Array,
                                   patchURL,
                                   patchPath,
                                   inputFilePath,
                                   outputFilePath,
                                   true, token);
            }

            LogWriteLine($"File [T: {asset.AssetIndex.FT}] {asset.AssetIndex.N} has been updated!", LogType.Default, true);

            // Pop repair asset display entry
            PopRepairAssetEntry(asset.AssetProperty);
        }
        #endregion

        #region GenericRepair

        private async Task RepairAssetTypeSophon(
            SophonAsset              sophonAsset,
            FilePropertiesRemote     assetIndex,
            IAssetProperty           assetEntry,
            DownloadClient           downloadClient,
            DownloadProgressDelegate downloadProgress,
            CancellationToken        token)
        {
            // Increment total count current
            ProgressAllCountCurrent++;
            // Set repair activity status
            string timeLeftString = string.Format(Lang!._Misc!.TimeRemainHMSFormat!, Progress.ProgressAllTimeLeft);
            UpdateRepairStatus(string.Format(assetIndex.FT == FileType.Block ? Lang._GameRepairPage.Status9 : Lang._GameRepairPage.Status8, assetIndex.FT == FileType.Block ? assetIndex.CRC : assetIndex.N),
                               string.Format(Lang._GameRepairPage.PerProgressSubtitle2, ConverterTool.SummarizeSizeSimple(ProgressAllSizeCurrent), ConverterTool.SummarizeSizeSimple(ProgressAllSizeTotal)) + $" | {timeLeftString}",
                               true);

            string   assetPath     = Path.Combine(GamePath, ConverterTool.NormalizePath(sophonAsset.AssetName));
            FileInfo assetFileInfo = new FileInfo(assetPath)
                                    .StripAlternateDataStream()
                                    .EnsureNoReadOnly();

            try
            {
                if (assetIndex.FT == FileType.Unused && !IsOnlyRecoverMain)
                {
                    // Remove unused asset
                    RemoveUnusedAssetTypeGeneric(assetFileInfo);
                    LogWriteLine($"Unused {assetIndex.N} has been deleted!", LogType.Default, true);

                    return;
                }

                HttpClient client = downloadClient.GetHttpClient();
                DownloadProgress simulatedDownloadProgress = new()
                {
                    BytesDownloaded = 0,
                    BytesTotal      = sophonAsset.AssetSize
                };

                await using FileStream outFileStream = assetFileInfo.Create();
                await sophonAsset.WriteToStreamAsync(client,
                                                     outFileStream,
                                                     read =>
                                                     {
                                                         simulatedDownloadProgress.BytesDownloaded += read;
                                                         downloadProgress.Invoke((int)read, simulatedDownloadProgress);
                                                     },
                                                     token: token);
                LogWriteLine($"File [T: {assetIndex.FT}] {(assetIndex.FT == FileType.Block ? assetIndex.CRC : assetIndex.N)} has been downloaded!", LogType.Default, true);
            }
            finally
            {
                // Pop repair asset display entry
                PopRepairAssetEntry(assetEntry);
            }

        }

        private async Task RepairAssetTypeGeneric((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset, DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, CancellationToken token, string customURL = null)
        {
            // Increment total count current
            ProgressAllCountCurrent++;
            // Set repair activity status
            string timeLeftString = string.Format(Lang!._Misc!.TimeRemainHMSFormat!, Progress.ProgressAllTimeLeft);
            UpdateRepairStatus(string.Format(asset.AssetIndex.FT == FileType.Block ? Lang._GameRepairPage.Status9 : Lang._GameRepairPage.Status8, asset.AssetIndex.FT == FileType.Block ? asset.AssetIndex.CRC : asset.AssetIndex.N),
                               string.Format(Lang._GameRepairPage.PerProgressSubtitle2, ConverterTool.SummarizeSizeSimple(ProgressAllSizeCurrent), ConverterTool.SummarizeSizeSimple(ProgressAllSizeTotal)) + $" | {timeLeftString}",
                               true);

            // Set URL of the asset
            string assetURL  = customURL ?? asset.AssetIndex.RN;
            string assetPath = Path.Combine(GamePath, ConverterTool.NormalizePath(asset.AssetIndex.N));
            FileInfo assetFileInfo = new FileInfo(assetPath).StripAlternateDataStream().EnsureNoReadOnly();

            if (asset.AssetIndex.FT == FileType.Unused && !IsOnlyRecoverMain)
            {
                // Remove unused asset
                RemoveUnusedAssetTypeGeneric(assetFileInfo);
                LogWriteLine($"Unused {asset.AssetIndex.N} has been deleted!", LogType.Default, true);
            }
            else
            {
                // Start asset download task
                await RunDownloadTask(asset.AssetIndex.S, assetFileInfo, assetURL, downloadClient, downloadProgress, token);
                LogWriteLine($"File [T: {asset.AssetIndex.FT}] {(asset.AssetIndex.FT == FileType.Block ? asset.AssetIndex.CRC : asset.AssetIndex.N)} has been downloaded!", LogType.Default, true);
            }

            // Pop repair asset display entry
            PopRepairAssetEntry(asset.AssetProperty);
        }

        private void RemoveUnusedAssetTypeGeneric(FileInfo filePath)
        {
            try
            {
                // Unassign Read only attribute and delete the file.
                if (filePath.Exists)
                {
                    filePath.Delete();
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Unable to delete unused asset: {filePath}\r\n{ex}", LogType.Warning, true);
            }
        }
        #endregion

        #region BlocksRepair
        private async Task RepairAssetTypeBlocks((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset, DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, CancellationToken token)
        {
            // If patching is applicable, do patching
            if (asset.AssetIndex.IsPatchApplicable)
            {
                // Increment total count current and update the status
                ProgressAllCountCurrent++;

                // Do patching
                await RepairTypeBlocksActionPatching(asset, downloadClient, downloadProgress, token);

                return;
            }

            // Initialize URL of the block, then run repair generic task
            string blockURL = asset.AssetIndex.RN;
            await RepairAssetTypeGeneric(asset, downloadClient, downloadProgress, token, blockURL);
        }

        private async Task RepairTypeBlocksActionPatching((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset, DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, CancellationToken token)
        {
            // Declare variables for patch file and URL and new file path
            if (asset.AssetIndex.BlockPatchInfo != null)
            {
                string patchURL       = string.Format(BlockPatchDiffBaseURL, asset.AssetIndex.BlockPatchInfo.PatchPairs[0].OldVersionDir).CombineURLFromString(asset.AssetIndex.BlockPatchInfo.PatchPairs[0].PatchName);
                string patchPath      = Path.Combine(GamePath, ConverterTool.NormalizePath(BlockPatchDiffPath), asset.AssetIndex.BlockPatchInfo.PatchPairs[0].PatchName);
                string inputFilePath  = Path.Combine(GamePath, ConverterTool.NormalizePath(BlockBasePath),      asset.AssetIndex.BlockPatchInfo.PatchPairs[0].OldName);
                string outputFilePath = Path.Combine(GamePath, ConverterTool.NormalizePath(asset.AssetIndex.N));

                // Set downloading patch status
                string timeLeftString = string.Format(Lang!._Misc!.TimeRemainHMSFormat!, Progress.ProgressAllTimeLeft);
                UpdateRepairStatus(
                                   string.Format(Lang._GameRepairPage.Status13,             asset.AssetIndex.CRC),
                                   string.Format(Lang._GameRepairPage.PerProgressSubtitle4, ConverterTool.SummarizeSizeSimple(ProgressAllSizeCurrent), ConverterTool.SummarizeSizeSimple(ProgressAllSizeTotal)) + $" | {timeLeftString}",
                                   true);

                // Run patching task
                await RunPatchTask(downloadClient,
                                   downloadProgress,
                                   asset.AssetIndex.BlockPatchInfo.PatchPairs[0].PatchSize,
                                   asset.AssetIndex.BlockPatchInfo.PatchPairs[0].PatchHash,
                                   patchURL,
                                   patchPath,
                                   inputFilePath,
                                   outputFilePath,
                                   token: token);
            }

            if (asset.AssetIndex.BlockPatchInfo != null)
            {
                LogWriteLine($"File [T: {asset.AssetIndex.FT}] {asset.AssetIndex.BlockPatchInfo.PatchPairs[0].OldName} has been updated with new block {asset.AssetIndex.BlockPatchInfo.NewName}!",
                             LogType.Default, true);
            }

            // Pop repair asset display entry
            PopRepairAssetEntry(asset.AssetProperty);
        }
        #endregion
    }
}
