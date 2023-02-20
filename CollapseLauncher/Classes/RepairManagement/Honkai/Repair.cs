using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    internal partial class HonkaiRepair
    {
        private async Task<bool> Repair(List<FilePropertiesRemote> repairAssetIndex)
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
                        FileType.Blocks => RepairAssetTypeBlocks(asset, _httpClient),
                        FileType.Audio => RepairOrPatchTypeAudio(asset, _httpClient),
                        _ => RepairAssetTypeGeneric(asset, _httpClient)
                    }).ConfigureAwait(false);

                    // Await the task
                    await assetTask;
                }

                return true;
            }
            catch { throw; }
            finally
            {
                // Dispose _httpClient
                _httpClient.Dispose();
                await _httpClient.WaitUntilInstanceDisposed();

                // Unassign downloader event
                _httpClient.DownloadProgress -= _httpClient_RepairAssetProgress;
            }
        }

        #region AudioRepairOrPatch
        private async Task RepairOrPatchTypeAudio(FilePropertiesRemote asset, Http _httpClient)
        {
            if (asset.IsPatchApplicable)
            {
                await RepairTypeAudioActionPatching(asset, _httpClient);
            }
            else
            {
                string audioURL = string.Format(_audioBaseRemotePath, $"{_gameVersion.Major}_{_gameVersion.Minor}") + asset.RN;
                await RepairAssetTypeGeneric(asset, _httpClient, audioURL);
            }
        }

        private async Task RepairTypeAudioActionPatching(FilePropertiesRemote asset, Http _httpClient)
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
                string.Format(Lang._GameRepairPage.PerProgressSubtitle4, _progressTotalCountCurrent, _progressTotalCount),
                true);

            // Download patch File first
            await RunDownloadTask(asset.AudioPatchInfo.Value.PatchFileSize, patchPath, patchURL, _httpClient);

            // Start patching process
            BinaryPatchUtility patchUtil = new BinaryPatchUtility();
            try
            {
                // Subscribe patching progress and start applying patch
                patchUtil.ProgressChanged += RepairTypeAudioActionPatching_ProgressChanged;
                patchUtil.Initialize(inputFilePath, patchPath, outputFilePath);
                await Task.Run(patchUtil.Apply).ConfigureAwait(false);

                // Delete old file and rename the new file
                File.Delete(inputFilePath);
                File.Move(outputFilePath, inputFilePath);

                LogWriteLine($"File [T: {asset.FT}] {asset.N} has been updated!", LogType.Default, true);
            }
            catch (Exception)
            {
                throw;
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
                patchUtil.ProgressChanged -= RepairTypeAudioActionPatching_ProgressChanged;
            }

            // Pop repair asset display entry
            PopRepairAssetEntry();
        }

        private async void RepairTypeAudioActionPatching_ProgressChanged(object sender, BinaryPatchProgress e)
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
                _status.ActivityTotal = string.Format(Lang._GameRepairPage.PerProgressSubtitle2, _progressTotalCountCurrent, _progressTotalCount);

                // Trigger update
                UpdateAll();
            }
        }
        #endregion

        #region GenericRepair
        private async Task RepairAssetTypeGeneric(FilePropertiesRemote asset, Http _httpClient, string customURL = null)
        {
            // Increment total count current
            _progressTotalCountCurrent++;
            // Set repair activity status
            UpdateRepairStatus(
                string.Format(Lang._GameRepairPage.Status8, asset.N),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle2, _progressTotalCountCurrent, _progressTotalCount),
                true);

            // Set URL of the asset
            string assetURL = customURL != null ? customURL : _gameRepoURL + '/' + asset.N;
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
                await RunDownloadTask(asset.S, assetPath, assetURL, _httpClient);
                LogWriteLine($"File [T: {asset.FT}] {asset.N} has been downloaded!", LogType.Default, true);
            }

            // Pop repair asset display entry
            PopRepairAssetEntry();
        }

        private void RemoveUnusedAssetTypeGeneric(string filePath)
        {
            try
            {
                FileInfo fInfo = new FileInfo(filePath);
                _progressTotalSizeCurrent += fInfo.Length;
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
        private async Task RepairAssetTypeBlocks(FilePropertiesRemote asset, Http _httpClient)
        {
            // Iterate broken block and process the repair
            foreach (XMFBlockList block in asset.BlkC)
            {
                // Initialize paths and URL of the block
                string assetBasePath = _blockBasePath + block.BlockHash + ".wmv";
                string assetURL = _gameRepoURL + '/' + assetBasePath;
                string assetPath = Path.Combine(_gamePath, ConverterTool.NormalizePath(assetBasePath));

                // Run block repair task
                if (block.BlockMissing)
                {
                    await RepairSingleBlock(assetPath, assetURL, block, _httpClient);
                }
                else
                {
                    await RepairChunkBlock(assetPath, assetURL, block, _httpClient);
                }
            }
        }

        private async Task RepairSingleBlock(string blockPath, string blockURL, XMFBlockList block, Http _httpClient)
        {
            // Increment total count current and update the status
            _progressTotalCountCurrent++;
            // Set repair activity status
            UpdateRepairStatus(
                string.Format(Lang._GameRepairPage.Status9, block.BlockHash),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle2, _progressTotalCountCurrent, _progressTotalCount),
                true);

            // Start asset download task
            await RunDownloadTask(block.BlockSize, blockPath, blockURL, _httpClient);

            // Pop repair asset display entry
            PopRepairAssetEntry();
        }

        private async Task RepairChunkBlock(string blockPath, string blockURL, XMFBlockList block, Http _httpClient)
        {
            // Initialize block file into FileStream
            using (FileStream fs = new FileStream(blockPath, FileMode.Open, FileAccess.Write))
            {
                // Iterate chunks from block
                foreach (XMFFileProperty chunk in block.BlockContent)
                {
                    // Increment total count current and update the status
                    _progressTotalCountCurrent++;
                    // Set repair activity status
                    UpdateRepairStatus(
                        string.Format(Lang._GameRepairPage.Status9, $"*{chunk._startoffset:x8} -> {chunk._startoffset + chunk._filesize:x8}"),
                        string.Format(Lang._GameRepairPage.PerProgressSubtitle2, _progressTotalCountCurrent, _progressTotalCount),
                        true);

                    // Start chunk repair task
                    await TryRepairChunk(fs, blockURL, chunk, _httpClient);

                    // Pop repair asset display entry
                    PopRepairAssetEntry();
                }
            }
        }

        private async Task TryRepairChunk(FileStream blockFs, string blockURL, XMFFileProperty chunk, Http _httpClient)
        {
            // Initialize offsets of the chunk
            long oStart = chunk._startoffset;
            long oEnd = chunk._startoffset + chunk._filesize;

            // Initialize block to chunk as ChunkStream
            using (ChunkStream chunkFs = new ChunkStream(blockFs, oStart, oEnd, false))
            {
                // Start downloading task for the chunk
                await _httpClient.Download(blockURL, chunkFs, oStart, oEnd, _token.Token, true);
            }
        }
        #endregion

        #region Tools
        private void PopRepairAssetEntry() => Dispatch(() =>
        {
            try
            {
                AssetEntry.RemoveAt(0);
            }
            catch { }
        });

        private void UpdateRepairStatus(string activityStatus, string activityTotal, bool isPerFileIndetermined)
        {
            // Set repair activity status
            _status.ActivityStatus = activityStatus;
            _status.ActivityTotal = activityTotal;
            _status.IsProgressPerFileIndetermined = isPerFileIndetermined;

            // Update status
            UpdateStatus();
        }

        private async void _httpClient_RepairAssetProgress(object sender, DownloadEvent e)
        {
            _progress.ProgressPerFilePercentage = e.ProgressPercentage;
            _progress.ProgressTotalSpeed = e.Speed;

            // Update current progress percentages
            _progress.ProgressTotalPercentage = _progressTotalSizeCurrent != 0 ?
                ConverterTool.GetPercentageNumber(_progressTotalSizeCurrent, _progressTotalSize) :
                0;

            if (e.State != DownloadState.Merging)
            {
                _progressTotalSizeCurrent += e.Read;
            }

            if (await CheckIfNeedRefreshStopwatch())
            {
                // Update current activity status
                _status.IsProgressTotalIndetermined = false;
                _status.IsProgressPerFileIndetermined = false;
                _status.ActivityPerFile = string.Format(Lang._Misc.Speed, ConverterTool.SummarizeSizeSimple(_progress.ProgressTotalSpeed));
                _status.ActivityTotal = string.Format(Lang._GameRepairPage.PerProgressSubtitle2, _progressTotalCountCurrent, _progressTotalCount);

                // Trigger update
                UpdateAll();
            }
        }

        private async Task RunDownloadTask(long assetSize, string assetPath, string assetURL, Http _httpClient)
        {
            // Check for directory availability
            if (!Directory.Exists(Path.GetDirectoryName(assetPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            }

            // Start downloading asset
            if (assetSize >= _sizeForMultiDownload)
            {
                await _httpClient.Download(assetURL, assetPath, _downloadThreadCount, true, _token.Token);
                await _httpClient.Merge();
            }
            else
            {
                await _httpClient.Download(assetURL, assetPath, true, null, null, _token.Token);
            }
        }
        #endregion
    }
}
