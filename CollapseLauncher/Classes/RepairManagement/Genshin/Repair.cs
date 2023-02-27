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
                foreach (PkgVersionProperties asset in repairAssetIndex)
                {
                    // Assign a task depends on the asset type
                    // ConfiguredTaskAwaitable assetTask = (asset.FT switch
                    // {
                        // FileType.Blocks => RepairAssetTypeBlocks(asset, _httpClient, token),
                        // FileType.Audio => RepairOrPatchTypeAudio(asset, _httpClient, token),
                        // _ => RepairAssetTypeGeneric(asset, _httpClient, token)
                    // }).ConfigureAwait(false);

                    // Await the task
                    // await assetTask;
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

            // Calculate speed
            long speed = (long)(_progressTotalSizeCurrent / _stopwatch.Elapsed.TotalSeconds);

            if (await CheckIfNeedRefreshStopwatch())
            {
                // Update current activity status
                _status.IsProgressTotalIndetermined = false;
                _status.IsProgressPerFileIndetermined = false;

                // Set time estimation string
                string timeLeftString = string.Format(Lang._Misc.TimeRemainHMSFormat, TimeSpan.FromSeconds((_progressTotalSizeCurrent - _progressTotalSize) / ConverterTool.Unzeroed(speed)));

                _status.ActivityPerFile = string.Format(Lang._Misc.Speed, ConverterTool.SummarizeSizeSimple(_progress.ProgressTotalSpeed));
                _status.ActivityTotal = string.Format(Lang._GameRepairPage.PerProgressSubtitle2, _progressTotalCountCurrent, _progressTotalCount) + $" | {timeLeftString}";

                // Trigger update
                UpdateAll();
            }
        }

        private async Task RunDownloadTask(long assetSize, string assetPath, string assetURL, Http _httpClient, CancellationToken token)
        {
            // Check for directory availability
            if (!Directory.Exists(Path.GetDirectoryName(assetPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            }

            // Start downloading asset
            if (assetSize >= _sizeForMultiDownload)
            {
                await _httpClient.Download(assetURL, assetPath, _downloadThreadCount, true, token);
                await _httpClient.Merge();
            }
            else
            {
                await _httpClient.Download(assetURL, assetPath, true, null, null, token);
            }
        }
        #endregion
    }
}
