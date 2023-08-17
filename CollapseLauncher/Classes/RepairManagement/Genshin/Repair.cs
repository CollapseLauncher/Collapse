using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using System.Collections.Generic;
using System.IO;
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

                // Iterate repair asset
                foreach (PkgVersionProperties asset in repairAssetIndex)
                {
                    await RepairAssetTypeGeneric(asset, _httpClient, token).ConfigureAwait(false);
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

        #region GenericRepair
        private async Task RepairAssetTypeGeneric(PkgVersionProperties asset, Http _httpClient, CancellationToken token)
        {
            // Increment total count current
            _progressTotalCountCurrent++;
            // Set repair activity status
            UpdateRepairStatus(
                string.Format(Lang._GameRepairPage.Status8, asset.remoteName),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle2, _progressTotalCountCurrent, _progressTotalCount),
                true);

            // If file is unused, then delete
            if (asset.type == "Unused")
            {
                string assetPath = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.localName));

                // Delete the file
                TryDeleteReadOnlyFile(assetPath);
            }
            else
            {
                string assetPath = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.remoteName));

                // or start asset download task
                await RunDownloadTask(asset.fileSize, assetPath, asset.remoteURL, _httpClient, token);
                LogWriteLine($"File [T: {RepairAssetType.General}] {asset.remoteName} has been downloaded!", LogType.Default, true);
            }

            // Pop repair asset display entry
            PopRepairAssetEntry();
        }
        #endregion
    }
}
