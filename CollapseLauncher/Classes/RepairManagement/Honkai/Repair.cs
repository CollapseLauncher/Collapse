using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using System;
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
            if (_status != null)
            {
                _status.ActivityStatus                = Lang._GameRepairPage.Status11;
                _status.IsProgressAllIndetermined     = true;
                _status.IsProgressPerFileIndetermined = true;
            }

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

            // Initialize the new DownloadClient instance
            DownloadClient downloadClient = DownloadClient.CreateInstance(client);

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
                            FileType.Block => RepairAssetTypeBlocks(asset, downloadClient, _httpClient_RepairAssetProgress, innerToken),
                            FileType.Audio => RepairOrPatchTypeAudio(asset, downloadClient, _httpClient_RepairAssetProgress, innerToken),
                            FileType.Video => RepairAssetTypeVideo(asset, downloadClient, _httpClient_RepairAssetProgress, innerToken),
                            _ => RepairAssetTypeGeneric(asset, downloadClient, _httpClient_RepairAssetProgress, innerToken)
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
                        FileType.Block => RepairAssetTypeBlocks(asset, downloadClient, _httpClient_RepairAssetProgress, token),
                        FileType.Audio => RepairOrPatchTypeAudio(asset, downloadClient, _httpClient_RepairAssetProgress, token),
                        FileType.Video => RepairAssetTypeVideo(asset, downloadClient, _httpClient_RepairAssetProgress, token),
                        _ => RepairAssetTypeGeneric(asset, downloadClient, _httpClient_RepairAssetProgress, token)
                    };

                    // Await the task
                    await assetTask;
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
                string audioURL = ConverterTool.CombineURLFromString(string.Format(_audioBaseRemotePath, $"{_gameVersion.Major}_{_gameVersion.Minor}", _gameServer.Manifest.ManifestAudio.ManifestAudioRevision), asset.AssetIndex.RN);
                await RepairAssetTypeGeneric(asset, downloadClient, downloadProgress, token, audioURL);
            }
        }

        private async Task RepairTypeAudioActionPatching((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset, DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, CancellationToken token)
        {
            // Increment total count current
            _progressAllCountCurrent++;

            // Declare variables for patch file and URL and new file path
            if (asset.AssetIndex.AudioPatchInfo != null)
            {
                string patchURL       = ConverterTool.CombineURLFromString(string.Format(_audioPatchBaseRemotePath, $"{_gameVersion.Major}_{_gameVersion.Minor}", _gameServer.Manifest.ManifestAudio.ManifestAudioRevision), asset.AssetIndex.AudioPatchInfo.Value.PatchFilename);
                string patchPath      = Path.Combine(_gamePath, ConverterTool.NormalizePath(_audioPatchBaseLocalPath), asset.AssetIndex.AudioPatchInfo.Value.PatchFilename);
                string inputFilePath  = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.AssetIndex.N));
                string outputFilePath = inputFilePath + "_tmp";

                // Set downloading patch status
                UpdateRepairStatus(
                                   string.Format(Lang._GameRepairPage.Status12,             asset.AssetIndex.N),
                                   string.Format(Lang._GameRepairPage.PerProgressSubtitle4, ConverterTool.SummarizeSizeSimple(_progressAllSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressAllSizeTotal)),
                                   true);

                // Run patching task
                await RunPatchTask(downloadClient, downloadProgress, token,         asset.AssetIndex.AudioPatchInfo.Value.PatchFileSize, asset.AssetIndex.AudioPatchInfo.Value.PatchMD5Array,
                                   patchURL,       patchPath,        inputFilePath, outputFilePath,                                      true);
            }

            LogWriteLine($"File [T: {asset.AssetIndex.FT}] {asset.AssetIndex.N} has been updated!", LogType.Default, true);

            // Pop repair asset display entry
            PopRepairAssetEntry(asset.AssetProperty);
        }
        #endregion

        #region GenericRepair
        private async Task RepairAssetTypeGeneric((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset, DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, CancellationToken token, string customURL = null)
        {
            // Increment total count current
            _progressAllCountCurrent++;
            // Set repair activity status
            UpdateRepairStatus(
                string.Format(asset.AssetIndex.FT == FileType.Block ? Lang._GameRepairPage.Status9 : Lang._GameRepairPage.Status8, asset.AssetIndex.FT == FileType.Block ? asset.AssetIndex.CRC : asset.AssetIndex.N),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle2, ConverterTool.SummarizeSizeSimple(_progressAllSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressAllSizeTotal)),
                true);

            // Set URL of the asset
            string assetURL  = customURL ?? asset.AssetIndex.RN;
            string assetPath = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.AssetIndex.N));

            if (asset.AssetIndex.FT == FileType.Unused && !_isOnlyRecoverMain)
            {
                // Remove unused asset
                RemoveUnusedAssetTypeGeneric(assetPath);
                LogWriteLine($"Unused {asset.AssetIndex.N} has been deleted!", LogType.Default, true);
            }
            else
            {
                // Start asset download task
                await RunDownloadTask(asset.AssetIndex.S, assetPath, assetURL, downloadClient, downloadProgress, token);
                LogWriteLine($"File [T: {asset.AssetIndex.FT}] {(asset.AssetIndex.FT == FileType.Block ? asset.AssetIndex.CRC : asset.AssetIndex.N)} has been downloaded!", LogType.Default, true);
            }

            // Pop repair asset display entry
            PopRepairAssetEntry(asset.AssetProperty);
        }

        private void RemoveUnusedAssetTypeGeneric(string filePath)
        {
            try
            {
                // Unassign Read only attribute and delete the file.
                FileInfo fInfo = new FileInfo(filePath);
                if (fInfo.Exists)
                {
                    fInfo.IsReadOnly = false;
                    fInfo.Delete();
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
                _progressAllCountCurrent++;

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
                string patchURL       = ConverterTool.CombineURLFromString(string.Format(_blockPatchDiffBaseURL, asset.AssetIndex.BlockPatchInfo.Value.PatchPairs[0].OldVersionDir), asset.AssetIndex.BlockPatchInfo.Value.PatchPairs[0].PatchHashStr + ".wmv");
                string patchPath      = Path.Combine(_gamePath, ConverterTool.NormalizePath(_blockPatchDiffPath), asset.AssetIndex.BlockPatchInfo.Value.PatchPairs[0].PatchHashStr + ".wmv");
                string inputFilePath  = Path.Combine(_gamePath, ConverterTool.NormalizePath(_blockBasePath),      asset.AssetIndex.BlockPatchInfo.Value.PatchPairs[0].OldHashStr + ".wmv");
                string outputFilePath = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.AssetIndex.N));

                // Set downloading patch status
                UpdateRepairStatus(
                                   string.Format(Lang._GameRepairPage.Status13,             asset.AssetIndex.CRC),
                                   string.Format(Lang._GameRepairPage.PerProgressSubtitle4, ConverterTool.SummarizeSizeSimple(_progressAllSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressAllSizeTotal)),
                                   true);

                // Run patching task
                await RunPatchTask(downloadClient, downloadProgress, token,         asset.AssetIndex.BlockPatchInfo.Value.PatchPairs[0].PatchSize, asset.AssetIndex.BlockPatchInfo.Value.PatchPairs[0].PatchHash,
                                   patchURL,       patchPath,        inputFilePath, outputFilePath);
            }

            if (asset.AssetIndex.BlockPatchInfo != null)
            {
                LogWriteLine($"File [T: {asset.AssetIndex.FT}] {asset.AssetIndex.BlockPatchInfo.Value.PatchPairs[0].OldHashStr} has been updated with new block {asset.AssetIndex.BlockPatchInfo.Value.NewBlockName}!",
                             LogType.Default, true);
            }

            // Pop repair asset display entry
            PopRepairAssetEntry(asset.AssetProperty);
        }
        #endregion
    }
}
