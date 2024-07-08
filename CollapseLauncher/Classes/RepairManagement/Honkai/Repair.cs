using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
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
            _status.IsProgressTotalIndetermined = true;
            _status.IsProgressPerFileIndetermined = true;

            // Update status
            UpdateStatus();

            // Reset stopwatch
            RestartStopwatch();

            // Use HttpClient instance on fetching
            Http _httpClient = new Http(true, 5, 1000, _userAgent);

            // Try running instance
            try
            {
                // Assign downloader event
                _httpClient.DownloadProgress += _httpClient_RepairAssetProgress;

                // Iterate repair asset and check it using different method for each type
                foreach (FilePropertiesRemote asset in
#if ENABLEHTTPREPAIR
                    EnforceHTTPSchemeToAssetIndex(repairAssetIndex)
#else
                    repairAssetIndex
#endif
                    )
                {
                    // Assign a task depends on the asset type
                    Task assetTask = (asset.FT switch
                    {
                        FileType.Blocks => RepairAssetTypeBlocks(asset, _httpClient, token),
                        FileType.Audio => RepairOrPatchTypeAudio(asset, _httpClient, token),
                        FileType.Video => RepairAssetTypeVideo(asset, _httpClient, token),
                        _ => RepairAssetTypeGeneric(asset, _httpClient, token)
                    });

                    // Await the task
                    await assetTask;
                }

                return true;
            }
            finally
            {
                // Dispose _httpClient
                _httpClient.Dispose();

                // Unassign downloader event
                _httpClient.DownloadProgress -= _httpClient_RepairAssetProgress;
            }
        }

        #region VideoRepair
        private async Task RepairAssetTypeVideo(FilePropertiesRemote asset, Http _httpClient, CancellationToken token) => await RepairAssetTypeGeneric(asset, _httpClient, token, asset.RN);
        #endregion

        #region AudioRepairOrPatch
        private async Task RepairOrPatchTypeAudio(FilePropertiesRemote asset, Http _httpClient, CancellationToken token)
        {
            if (asset.IsPatchApplicable)
            {
                await RepairTypeAudioActionPatching(asset, _httpClient, token);
            }
            else
            {
                string audioURL = ConverterTool.CombineURLFromString(string.Format(_audioBaseRemotePath, $"{_gameVersion.Major}_{_gameVersion.Minor}", _gameServer.Manifest.ManifestAudio.ManifestAudioRevision), asset.RN);
                await RepairAssetTypeGeneric(asset, _httpClient, token, audioURL);
            }
        }

        private async Task RepairTypeAudioActionPatching(FilePropertiesRemote asset, Http _httpClient, CancellationToken token)
        {
            // Increment total count current
            _progressTotalCountCurrent++;

            // Declare variables for patch file and URL and new file path
            string patchURL = ConverterTool.CombineURLFromString(string.Format(_audioPatchBaseRemotePath, $"{_gameVersion.Major}_{_gameVersion.Minor}", _gameServer.Manifest.ManifestAudio.ManifestAudioRevision), asset.AudioPatchInfo.Value.PatchFilename);
            string patchPath = Path.Combine(_gamePath, ConverterTool.NormalizePath(_audioPatchBaseLocalPath), asset.AudioPatchInfo.Value.PatchFilename);
            string inputFilePath = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.N));
            string outputFilePath = inputFilePath + "_tmp";

            // Set downloading patch status
            UpdateRepairStatus(
                string.Format(Lang._GameRepairPage.Status12, asset.N),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle4, ConverterTool.SummarizeSizeSimple(_progressTotalSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressTotalSize)),
                true);

            // Run patching task
            await RunPatchTask(_httpClient, token, asset.AudioPatchInfo.Value.PatchFileSize, asset.AudioPatchInfo.Value.PatchMD5Array,
                patchURL, patchPath, inputFilePath, outputFilePath, true);

            LogWriteLine($"File [T: {asset.FT}] {asset.N} has been updated!", LogType.Default, true);

            // Pop repair asset display entry
            PopRepairAssetEntry();
        }
        #endregion

        #region GenericRepair
        private async Task RepairAssetTypeGeneric(FilePropertiesRemote asset, Http _httpClient, CancellationToken token, string customURL = null)
        {
            // Increment total count current
            _progressTotalCountCurrent++;
            // Set repair activity status
            UpdateRepairStatus(
                string.Format(asset.FT == FileType.Blocks ? Lang._GameRepairPage.Status9 : Lang._GameRepairPage.Status8, asset.FT == FileType.Blocks ? asset.CRC : asset.N),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle2, ConverterTool.SummarizeSizeSimple(_progressTotalSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressTotalSize)),
                true);

            // Set URL of the asset
            string assetURL = customURL != null ? customURL : asset.RN;
            string assetPath = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.N));

            if (asset.FT == FileType.Unused && !_isOnlyRecoverMain)
            {
                // Remove unused asset
                RemoveUnusedAssetTypeGeneric(assetPath);
                LogWriteLine($"Unused {asset.N} has been deleted!", LogType.Default, true);
            }
            else
            {
                // Start asset download task
                await RunDownloadTask(asset.S, assetPath, assetURL, _httpClient, token);
                LogWriteLine($"File [T: {asset.FT}] {(asset.FT == FileType.Blocks ? asset.CRC : asset.N)} has been downloaded!", LogType.Default, true);
            }

            // Pop repair asset display entry
            PopRepairAssetEntry();
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
        private async Task RepairAssetTypeBlocks(FilePropertiesRemote asset, Http _httpClient, CancellationToken token)
        {
            // If patching is applicable, do patching
            if (asset.IsPatchApplicable)
            {
                // Increment total count current and update the status
                _progressTotalCountCurrent++;

                // Do patching
                await RepairTypeBlocksActionPatching(asset, _httpClient, token);

                return;
            }

            // Initialize URL of the block, then run repair generic task
            string blockURL = asset.RN;
            await RepairAssetTypeGeneric(asset, _httpClient, token, blockURL);
        }

        private async Task RepairTypeBlocksActionPatching(FilePropertiesRemote asset, Http _httpClient, CancellationToken token)
        {
            // Declare variables for patch file and URL and new file path
            string patchURL = ConverterTool.CombineURLFromString(string.Format(_blockPatchDiffBaseURL, asset.BlockPatchInfo.Value.PatchPairs[0].OldVersionDir), asset.BlockPatchInfo.Value.PatchPairs[0].PatchHashStr + ".wmv");
            string patchPath = Path.Combine(_gamePath, ConverterTool.NormalizePath(_blockPatchDiffPath), asset.BlockPatchInfo.Value.PatchPairs[0].PatchHashStr + ".wmv");
            string inputFilePath = Path.Combine(_gamePath, ConverterTool.NormalizePath(_blockBasePath), asset.BlockPatchInfo.Value.PatchPairs[0].OldHashStr + ".wmv");
            string outputFilePath = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.N));

            // Set downloading patch status
            UpdateRepairStatus(
                string.Format(Lang._GameRepairPage.Status13, asset.CRC),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle4, ConverterTool.SummarizeSizeSimple(_progressTotalSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressTotalSize)),
                true);

            // Run patching task
            await RunPatchTask(_httpClient, token, asset.BlockPatchInfo.Value.PatchPairs[0].PatchSize, asset.BlockPatchInfo.Value.PatchPairs[0].PatchHash,
                patchURL, patchPath, inputFilePath, outputFilePath);

            LogWriteLine($"File [T: {asset.FT}] {asset.BlockPatchInfo.Value.PatchPairs[0].OldHashStr} has been updated with new block {asset.BlockPatchInfo.Value.NewBlockName}!", LogType.Default, true);

            // Pop repair asset display entry
            PopRepairAssetEntry();
        }
        #endregion
    }
}
