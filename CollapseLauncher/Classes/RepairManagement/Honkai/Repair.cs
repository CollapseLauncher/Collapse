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
                                FileType.Blocks => RepairAssetTypeBlocks(asset, _httpClient, innerToken),
                                FileType.Audio => RepairOrPatchTypeAudio(asset, _httpClient, innerToken),
                                FileType.Video => RepairAssetTypeVideo(asset, _httpClient, innerToken),
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
                            FileType.Blocks => RepairAssetTypeBlocks(asset, _httpClient, token),
                            FileType.Audio => RepairOrPatchTypeAudio(asset, _httpClient, token),
                            FileType.Video => RepairAssetTypeVideo(asset, _httpClient, token),
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

        #region VideoRepair
        private async Task RepairAssetTypeVideo((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset, Http _httpClient, CancellationToken token) => await RepairAssetTypeGeneric(asset, _httpClient, token, asset.AssetIndex.RN);
        #endregion

        #region AudioRepairOrPatch
        private async Task RepairOrPatchTypeAudio((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset, Http _httpClient, CancellationToken token)
        {
            if (asset.AssetIndex.IsPatchApplicable)
            {
                await RepairTypeAudioActionPatching(asset, _httpClient, token);
            }
            else
            {
                string audioURL = ConverterTool.CombineURLFromString(string.Format(_audioBaseRemotePath, $"{_gameVersion.Major}_{_gameVersion.Minor}", _gameServer.Manifest.ManifestAudio.ManifestAudioRevision), asset.AssetIndex.RN);
                await RepairAssetTypeGeneric(asset, _httpClient, token, audioURL);
            }
        }

        private async Task RepairTypeAudioActionPatching((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset, Http _httpClient, CancellationToken token)
        {
            // Increment total count current
            _progressAllCountCurrent++;

            // Declare variables for patch file and URL and new file path
            string patchURL = ConverterTool.CombineURLFromString(string.Format(_audioPatchBaseRemotePath, $"{_gameVersion.Major}_{_gameVersion.Minor}", _gameServer.Manifest.ManifestAudio.ManifestAudioRevision), asset.AssetIndex.AudioPatchInfo.Value.PatchFilename);
            string patchPath = Path.Combine(_gamePath, ConverterTool.NormalizePath(_audioPatchBaseLocalPath), asset.AssetIndex.AudioPatchInfo.Value.PatchFilename);
            string inputFilePath = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.AssetIndex.N));
            string outputFilePath = inputFilePath + "_tmp";

            // Set downloading patch status
            UpdateRepairStatus(
                string.Format(Lang._GameRepairPage.Status12, asset.AssetIndex.N),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle4, ConverterTool.SummarizeSizeSimple(_progressAllSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressAllSizeTotal)),
                true);

            // Run patching task
            await RunPatchTask(_httpClient, token, asset.AssetIndex.AudioPatchInfo.Value.PatchFileSize, asset.AssetIndex.AudioPatchInfo.Value.PatchMD5Array,
                patchURL, patchPath, inputFilePath, outputFilePath, true);

            LogWriteLine($"File [T: {asset.AssetIndex.FT}] {asset.AssetIndex.N} has been updated!", LogType.Default, true);

            // Pop repair asset display entry
            PopRepairAssetEntry(asset.AssetProperty);
        }
        #endregion

        #region GenericRepair
        private async Task RepairAssetTypeGeneric((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset, Http _httpClient, CancellationToken token, string customURL = null)
        {
            // Increment total count current
            _progressAllCountCurrent++;
            // Set repair activity status
            UpdateRepairStatus(
                string.Format(asset.AssetIndex.FT == FileType.Blocks ? Lang._GameRepairPage.Status9 : Lang._GameRepairPage.Status8, asset.AssetIndex.FT == FileType.Blocks ? asset.AssetIndex.CRC : asset.AssetIndex.N),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle2, ConverterTool.SummarizeSizeSimple(_progressAllSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressAllSizeTotal)),
                true);

            // Set URL of the asset
            string assetURL = customURL != null ? customURL : asset.AssetIndex.RN;
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
                await RunDownloadTask(asset.AssetIndex.S, assetPath, assetURL, _httpClient, token);
                LogWriteLine($"File [T: {asset.AssetIndex.FT}] {(asset.AssetIndex.FT == FileType.Blocks ? asset.AssetIndex.CRC : asset.AssetIndex.N)} has been downloaded!", LogType.Default, true);
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
        private async Task RepairAssetTypeBlocks((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset, Http _httpClient, CancellationToken token)
        {
            // If patching is applicable, do patching
            if (asset.AssetIndex.IsPatchApplicable)
            {
                // Increment total count current and update the status
                _progressAllCountCurrent++;

                // Do patching
                await RepairTypeBlocksActionPatching(asset, _httpClient, token);

                return;
            }

            // Initialize URL of the block, then run repair generic task
            string blockURL = asset.AssetIndex.RN;
            await RepairAssetTypeGeneric(asset, _httpClient, token, blockURL);
        }

        private async Task RepairTypeBlocksActionPatching((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset, Http _httpClient, CancellationToken token)
        {
            // Declare variables for patch file and URL and new file path
            string patchURL = ConverterTool.CombineURLFromString(string.Format(_blockPatchDiffBaseURL, asset.AssetIndex.BlockPatchInfo.Value.PatchPairs[0].OldVersionDir), asset.AssetIndex.BlockPatchInfo.Value.PatchPairs[0].PatchHashStr + ".wmv");
            string patchPath = Path.Combine(_gamePath, ConverterTool.NormalizePath(_blockPatchDiffPath), asset.AssetIndex.BlockPatchInfo.Value.PatchPairs[0].PatchHashStr + ".wmv");
            string inputFilePath = Path.Combine(_gamePath, ConverterTool.NormalizePath(_blockBasePath), asset.AssetIndex.BlockPatchInfo.Value.PatchPairs[0].OldHashStr + ".wmv");
            string outputFilePath = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.AssetIndex.N));

            // Set downloading patch status
            UpdateRepairStatus(
                string.Format(Lang._GameRepairPage.Status13, asset.AssetIndex.CRC),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle4, ConverterTool.SummarizeSizeSimple(_progressAllSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressAllSizeTotal)),
                true);

            // Run patching task
            await RunPatchTask(_httpClient, token, asset.AssetIndex.BlockPatchInfo.Value.PatchPairs[0].PatchSize, asset.AssetIndex.BlockPatchInfo.Value.PatchPairs[0].PatchHash,
                patchURL, patchPath, inputFilePath, outputFilePath);

            LogWriteLine($"File [T: {asset.AssetIndex.FT}] {asset.AssetIndex.BlockPatchInfo.Value.PatchPairs[0].OldHashStr} has been updated with new block {asset.AssetIndex.BlockPatchInfo.Value.NewBlockName}!", LogType.Default, true);

            // Pop repair asset display entry
            PopRepairAssetEntry(asset.AssetProperty);
        }
        #endregion
    }
}
