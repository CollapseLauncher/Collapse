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
                foreach (FilePropertiesRemote asset in repairAssetIndex)
                {
                    // Assign a task depends on the asset type
                    ConfiguredTaskAwaitable assetTask = (asset.FT switch
                    {
                        FileType.Blocks => RepairAssetTypeBlocks(asset, _httpClient, token),
                        FileType.Audio => RepairOrPatchTypeAudio(asset, _httpClient, token),
                        FileType.Video => RepairAssetTypeVideo(asset, _httpClient, token),
                        _ => RepairAssetTypeGeneric(asset, _httpClient, token)
                    }).ConfigureAwait(false);

                    // Await the task
                    await assetTask;
                }

                return true;
            }
            finally
            {
                // Dispose _httpClient
                _httpClient.Dispose();
                await _httpClient.WaitUntilInstanceDisposed();

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
                string audioURL = string.Format(_audioBaseRemotePath, $"{_gameVersion.Major}_{_gameVersion.Minor}") + asset.RN;
                await RepairAssetTypeGeneric(asset, _httpClient, token, audioURL);
            }
        }

        private async Task RepairTypeAudioActionPatching(FilePropertiesRemote asset, Http _httpClient, CancellationToken token)
        {
            // Increment total count current
            _progressTotalCountCurrent++;

            // Declare variables for patch file and URL and new file path
            string patchURL = string.Format(_audioPatchBaseRemotePath, $"{_gameVersion.Major}_{_gameVersion.Minor}") + asset.AudioPatchInfo.Value.PatchFilename;
            string patchPath = Path.Combine(_gamePath, ConverterTool.NormalizePath(_audioPatchBaseLocalPath), asset.AudioPatchInfo.Value.PatchFilename);
            string inputFilePath = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.N));
            string outputFilePath = inputFilePath + "_tmp";

            // Set downloading patch status
            UpdateRepairStatus(
                string.Format(Lang._GameRepairPage.Status12, asset.N),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle4, ConverterTool.SummarizeSizeSimple(_progressTotalSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressTotalSize)),
                true);

            // Download patch File first
            await RunDownloadTask(asset.AudioPatchInfo.Value.PatchFileSize, patchPath, patchURL, _httpClient, token);

            // Start patching process
            BinaryPatchUtility patchUtil = new BinaryPatchUtility();
            try
            {
                // Subscribe patching progress and start applying patch
                patchUtil.ProgressChanged += RepairTypeActionPatching_ProgressChanged;
                patchUtil.Initialize(inputFilePath, patchPath, outputFilePath);
                await Task.Run(() => patchUtil.Apply(token)).ConfigureAwait(false);

                // Delete old file and rename the new file
                File.Delete(inputFilePath);
                File.Move(outputFilePath, inputFilePath);

                LogWriteLine($"File [T: {asset.FT}] {asset.N} has been updated!", LogType.Default, true);
            }
            finally
            {
                // Delete the patch file and unsubscribe the patching progress
                FileInfo fileInfo = new FileInfo(patchPath);
                if (fileInfo.Exists)
                {
                    fileInfo.IsReadOnly = false;
                    fileInfo.Delete();
                }
                patchUtil.ProgressChanged -= RepairTypeActionPatching_ProgressChanged;
            }

            // Pop repair asset display entry
            PopRepairAssetEntry();
        }

        private async void RepairTypeActionPatching_ProgressChanged(object sender, BinaryPatchProgress e)
        {
            _progress.ProgressPerFilePercentage = e.ProgressPercentage;
            _progress.ProgressTotalSpeed = e.Speed;

            // Update current progress percentages
            _progress.ProgressTotalPercentage = _progressTotalSizeCurrent != 0 ?
                ConverterTool.GetPercentageNumber(_progressTotalSizeCurrent, _progressTotalSize) :
                0;

            if (await CheckIfNeedRefreshStopwatch())
            {
                // Update current activity status
                _status.IsProgressTotalIndetermined = false;
                _status.IsProgressPerFileIndetermined = false;
                _status.ActivityPerFile = string.Format(Lang._GameRepairPage.PerProgressSubtitle5, ConverterTool.SummarizeSizeSimple(_progress.ProgressTotalSpeed));
                _status.ActivityTotal = string.Format(Lang._GameRepairPage.PerProgressSubtitle2, ConverterTool.SummarizeSizeSimple(_progressTotalSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressTotalSize));

                // Trigger update
                UpdateAll();
            }
        }
        #endregion

        #region GenericRepair
        private async Task RepairAssetTypeGeneric(FilePropertiesRemote asset, Http _httpClient, CancellationToken token, string customURL = null)
        {
            // Increment total count current
            _progressTotalCountCurrent++;
            // Set repair activity status
            UpdateRepairStatus(
                string.Format(Lang._GameRepairPage.Status8, asset.N),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle2, ConverterTool.SummarizeSizeSimple(_progressTotalSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressTotalSize)),
                true);

            // Set URL of the asset
            string assetURL = customURL != null ? customURL : asset.RN;
            string assetPath = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.N));

            if (asset.FT == FileType.Unused)
            {
                // Remove unused asset
                RemoveUnusedAssetTypeGeneric(assetPath);
                LogWriteLine($"Unused {asset.N} has been deleted!", LogType.Default, true);
            }
            else
            {
                // Start asset download task
                await RunDownloadTask(asset.S, assetPath, assetURL, _httpClient, token);
                LogWriteLine($"File [T: {asset.FT}] {asset.N} has been downloaded!", LogType.Default, true);
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

                // Pop repair asset display entry
                PopRepairAssetEntry();

                // Increase the total current size
                _progressTotalSizeCurrent += asset.BlockPatchInfo.Value.PatchSize;

                return;
            }

            // Increment total count current and update the status
            _progressTotalCountCurrent++;

            // Set repair activity status
            UpdateRepairStatus(
                string.Format(Lang._GameRepairPage.Status9, asset.CRC),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle2, ConverterTool.SummarizeSizeSimple(_progressTotalSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressTotalSize)),
                true);

            // Initialize paths and URL of the block
            string assetPath = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.N));
            string assetURL = asset.RN;

            // Start asset download task
            await RunDownloadTask(asset.S, assetPath, assetURL, _httpClient, token);

            // Pop repair asset display entry
            PopRepairAssetEntry();
        }

        private async Task RepairTypeBlocksActionPatching(FilePropertiesRemote asset, Http _httpClient, CancellationToken token)
        {
            // Declare variables for patch file and URL and new file path
            string patchURL = _blockPatchDiffBaseURL + "/" + asset.BlockPatchInfo.Value.PatchName + ".wmv";
            string patchPath = Path.Combine(_gamePath, ConverterTool.NormalizePath(_blockPatchDiffPath), asset.BlockPatchInfo.Value.PatchName + ".wmv");
            string inputFilePath = Path.Combine(_gamePath, ConverterTool.NormalizePath(_blockBasePath), asset.BlockPatchInfo.Value.OldBlockName + ".wmv");
            string outputFilePath = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.N));

            // Set downloading patch status
            UpdateRepairStatus(
                string.Format(Lang._GameRepairPage.Status13, asset.N),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle4, ConverterTool.SummarizeSizeSimple(_progressTotalSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressTotalSize)),
                true);

            // Get info about patch file
            FileInfo patchInfo = new FileInfo(patchPath);

            // If file doesn't exist, then download the patch first
            if (!patchInfo.Exists || patchInfo.Length != asset.BlockPatchInfo.Value.PatchSize)
            {
                // Download patch File first
                await RunDownloadTask(asset.BlockPatchInfo.Value.PatchSize, patchPath, patchURL, _httpClient, token);
            }

            while (true)
            {
                // Verify the patch file and if it doesn't match, then redownload it
                byte[] patchCRC = await Task.Run(() => CheckMD5(patchInfo.OpenRead(), token, false)).ConfigureAwait(false);
                if (!IsArrayMatch(patchCRC, asset.BlockPatchInfo.Value.PatchHash))
                {
                    // Redownload the patch file
                    await RunDownloadTask(asset.BlockPatchInfo.Value.PatchSize, patchPath, patchURL, _httpClient, token);
                    continue;
                }

                // else, break and quit from loop
                break;
            }

            // Start patching process
            BinaryPatchUtility patchUtil = new BinaryPatchUtility();
            try
            {
                // Set current per file size
                _progressPerFileSize = asset.S;
                _progressPerFileSizeCurrent = 0;

                // Subscribe patching progress and start applying patch
                patchUtil.ProgressChanged += RepairTypeActionPatching_ProgressChanged;
                patchUtil.Initialize(inputFilePath, patchPath, outputFilePath);
                await Task.Run(() => patchUtil.Apply(token)).ConfigureAwait(false);

                // Delete old block
                File.Delete(inputFilePath);

                LogWriteLine($"File [T: {asset.FT}] {asset.BlockPatchInfo.Value.OldBlockName} has been updated with new block {asset.BlockPatchInfo.Value.NewBlockName}!", LogType.Default, true);
            }
            finally
            {
                // Delete the patch file and unsubscribe the patching progress
                FileInfo fileInfo = new FileInfo(patchPath);
                if (fileInfo.Exists)
                {
                    fileInfo.IsReadOnly = false;
                    fileInfo.Delete();
                }
                patchUtil.ProgressChanged -= RepairTypeActionPatching_ProgressChanged;
            }
        }
        #endregion
    }
}
