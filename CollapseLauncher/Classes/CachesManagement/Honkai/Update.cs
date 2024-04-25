using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    internal partial class HonkaiCache
    {
        private async Task<bool> Update(List<CacheAsset> updateAssetIndex, List<CacheAsset> assetIndex, CancellationToken token)
        {
            // Assign Http client
            Http httpClient = new Http(true, 5, 1000, _userAgent);
            try
            {
                // Set IsProgressTotalIndetermined as false and update the status 
                _status!.IsProgressTotalIndetermined = true;
                UpdateStatus();

                // Subscribe the event listener
                httpClient.DownloadProgress += _httpClient_UpdateAssetProgress;
                // Iterate the asset index and do update operation
                foreach (CacheAsset asset in updateAssetIndex!)
                {
                    await UpdateCacheAsset(asset, httpClient, token);
                }

                // Reindex the asset index in Verify.txt
                UpdateCacheVerifyList(assetIndex);

                return true;
            }
            catch (TaskCanceledException) { throw; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LogWriteLine($"An error occured while updating cache file!\r\n{ex}", LogType.Error, true);
                throw;
            }
            finally
            {
                // Unsubscribe the event listener and dispose Http client
                httpClient.DownloadProgress -= _httpClient_UpdateAssetProgress;
                httpClient.Dispose();
            }
        }

        private void UpdateCacheVerifyList(List<CacheAsset> assetIndex)
        {
            // Get listFile path
            string listFile = Path.Combine(_gamePath!, "Data", "Verify.txt");

            // Initialize listFile File Stream
            using (FileStream fs = new FileStream(listFile, FileMode.Create, FileAccess.Write))
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    // Iterate asset index and generate the path for the cache path
                    foreach (CacheAsset asset in assetIndex!)
                    {
                        // Yes, the path is written in this way. Idk why miHoYo did this...
                        // Update 6.8: They finally notices that they use "//" instead of "/"
                        string basePath = GetAssetBasePathByType(asset!.DataType)!.Replace('\\', '/');
                        string path     = basePath + "/" + asset.ConcatN;
                        sw.WriteLine(path);
                    }
                }
        }

        private async Task UpdateCacheAsset(CacheAsset asset, Http httpClient, CancellationToken token)
        {
            // Increment total count and update the status
            _progressTotalCountCurrent++;
            _status!.ActivityStatus = string.Format(Lang!._Misc!.Downloading + " {0}: {1}", asset!.DataType, asset.N);
            UpdateAll();

            // This is a action for Unused asset.
            if (asset.DataType == CacheAssetType.Unused)
            {
                FileInfo fileInfo = new FileInfo(asset.ConcatPath!);
                if (fileInfo.Exists)
                {
                    fileInfo.IsReadOnly = false;
                    fileInfo.Delete();
                }

                LogWriteLine($"Deleted unused file: {fileInfo.FullName}", LogType.Default, true);
            }
            // Other than unused file, do this action
            else
            {
                // Assign and check the path of the asset directory
                string assetDir = Path.GetDirectoryName(asset.ConcatPath);
                if (!Directory.Exists(assetDir))
                {
                    Directory.CreateDirectory(assetDir!);
                }

#if DEBUG
                LogWriteLine($"Downloading cache [T: {asset.DataType}]: {asset.N} at URL: {asset.ConcatURL}", LogType.Debug, true);
#endif

                // Do multi-session download for asset that has applicable size
                if (asset.CS >= _sizeForMultiDownload)
                {
                    await httpClient!.Download(asset.ConcatURL, asset.ConcatPath, _downloadThreadCount, true, token);
                    await httpClient.Merge(token);
                }
                // Do single-session download for others
                else
                {
                    await httpClient!.Download(asset.ConcatURL, asset.ConcatPath, true, null, null, token);
                }

#if !DEBUG
                LogWriteLine($"Downloaded cache [T: {asset.DataType}]: {asset.N}", LogType.Default, true);
#endif
            }

            // Remove Asset Entry display
            Dispatch(() => { if (AssetEntry!.Count > 0) AssetEntry.RemoveAt(0); });
        }

        private async void _httpClient_UpdateAssetProgress(object sender, DownloadEvent e)
        {
            // Update current progress percentages and speed
            _progress!.ProgressTotalPercentage = _progressTotalSizeCurrent != 0 ?
                ConverterTool.GetPercentageNumber(_progressTotalSizeCurrent, _progressTotalSize) :
                0;

            if (e!.State != DownloadState.Merging)
            {
                _progressTotalSizeCurrent += e.Read;
            }
            long speed = (long)(_progressTotalSizeCurrent / _stopwatch!.Elapsed.TotalSeconds);

            if (await CheckIfNeedRefreshStopwatch())
            {
                // Update current activity status
                _status!.IsProgressTotalIndetermined = false;
                string timeLeftString = string.Format(Lang!._Misc!.TimeRemainHMSFormat!, TimeSpan.FromSeconds((_progressTotalSizeCurrent - _progressTotalSize) / ConverterTool.Unzeroed(speed)));
                _status.ActivityTotal = string.Format(Lang!._Misc!.Downloading + ": {0}/{1} ", _progressTotalCountCurrent, _progressTotalCount)
                                       + string.Format($"({Lang._Misc.SpeedPerSec})", ConverterTool.SummarizeSizeSimple(speed))
                                       + $" | {timeLeftString}";

                // Trigger update
                UpdateAll();
            }
        }
    }
}
