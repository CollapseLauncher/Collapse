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
            _status.RepairActivityStatus = Lang._GameRepairPage.Status11;
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
                        _ => RepairAssetTypeGenericAudio(asset, _httpClient)
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

        #region GenericAudioRepair
        private async Task RepairAssetTypeGenericAudio(FilePropertiesRemote asset, Http _httpClient)
        {
            // Increment total count current
            _progressTotalCountCurrent++;
            // Set repair activity status
            UpdateRepairStatus(
                string.Format(Lang._GameRepairPage.Status8, asset.N),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle2, _progressTotalCountCurrent, _progressTotalCount),
                true);

            // Set URL of the asset
            string assetURL = _gameRepoURL + '/' + asset.N;
            string assetPath = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.N));

            if (asset.FT == FileType.Unused)
            {
                // Remove unused asset
                RemoveUnusedAssetTypeGeneric(assetPath);
            }
            else
            {
                // Start asset download task
                await RunDownloadTask(asset.S, assetPath, assetURL, _httpClient);
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
                RepairAssetEntry.RemoveAt(0);
            }
            catch { }
        });

        private void UpdateRepairStatus(string activityStatus, string activityTotal, bool isPerFileIndetermined)
        {
            // Set repair activity status
            _status.RepairActivityStatus = activityStatus;
            _status.RepairActivityTotal = activityTotal;
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
                _status.RepairActivityPerFile = string.Format(Lang._Misc.Speed, ConverterTool.SummarizeSizeSimple(_progress.ProgressTotalSpeed));
                _status.RepairActivityTotal = string.Format(Lang._GameRepairPage.PerProgressSubtitle2, _progressTotalCountCurrent, _progressTotalCount);

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
                await _httpClient.Download(assetURL, assetPath, (byte)_repairThread, true, _token.Token);
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
