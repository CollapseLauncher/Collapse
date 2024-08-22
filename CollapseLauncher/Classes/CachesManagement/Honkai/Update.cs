using CollapseLauncher.Helper;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http.Legacy;
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
    internal partial class HonkaiCache
    {
        private async Task<bool> Update(List<CacheAsset> updateAssetIndex, List<CacheAsset> assetIndex, CancellationToken token)
        {
            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder<SocketsHttpHandler>()
                .UseLauncherConfig(_downloadThreadCount + 16)
                .SetUserAgent(_userAgent)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Assign Http client
            Http httpClient = new Http(true, 5, 1000, _userAgent, client);
            try
            {
                // Set IsProgressAllIndetermined as false and update the status 
                _status!.IsProgressAllIndetermined = true;
                UpdateStatus();

                // Subscribe the event listener
                httpClient.DownloadProgress += _httpClient_UpdateAssetProgress;
                // Iterate the asset index and do update operation
                ObservableCollection<IAssetProperty> assetProperty = new ObservableCollection<IAssetProperty>(AssetEntry);
                if (_isBurstDownloadEnabled)
                {
                    await Parallel.ForEachAsync(
                        PairEnumeratePropertyAndAssetIndexPackage(
#if ENABLEHTTPREPAIR    
                        EnforceHTTPSchemeToAssetIndex(updateAssetIndex)
#else
                        updateAssetIndex
#endif
                        , assetProperty),
                        new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = _downloadThreadCount },
                        async (asset, innerToken) =>
                        {
                            await UpdateCacheAsset(asset, httpClient, innerToken);
                        });
                }
                else
                {
                    foreach ((CacheAsset, IAssetProperty) asset in
                        PairEnumeratePropertyAndAssetIndexPackage(
#if ENABLEHTTPREPAIR    
                        EnforceHTTPSchemeToAssetIndex(updateAssetIndex)
#else
                        updateAssetIndex
#endif
                        , assetProperty))
                    {
                        await UpdateCacheAsset(asset, httpClient, token);
                    }
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
                        string path = basePath + "/" + asset.ConcatN;
                        sw.WriteLine(path);
                    }
                }
        }

        private async Task UpdateCacheAsset((CacheAsset AssetIndex, IAssetProperty AssetProperty) asset, Http httpClient, CancellationToken token)
        {
            // Increment total count and update the status
            _progressAllCountCurrent++;
            _status!.ActivityStatus = string.Format(Lang!._Misc!.Downloading + " {0}: {1}", asset!.AssetIndex.DataType, asset.AssetIndex.N);
            UpdateAll();

            // This is a action for Unused asset.
            if (asset.AssetIndex.DataType == CacheAssetType.Unused)
            {
                FileInfo fileInfo = new FileInfo(asset.AssetIndex.ConcatPath!);
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
                string assetDir = Path.GetDirectoryName(asset.AssetIndex.ConcatPath);
                if (!Directory.Exists(assetDir))
                {
                    Directory.CreateDirectory(assetDir!);
                }

#if DEBUG
                LogWriteLine($"Downloading cache [T: {asset.AssetIndex.DataType}]: {asset.AssetIndex.N} at URL: {asset.AssetIndex.ConcatURL}", LogType.Debug, true);
#endif

                // Do multi-session download for asset that has applicable size
                if (asset.AssetIndex.CS >= _sizeForMultiDownload && !_isBurstDownloadEnabled)
                {
                    await httpClient!.Download(asset.AssetIndex.ConcatURL, asset.AssetIndex.ConcatPath, _downloadThreadCount, true, token);
                    await httpClient.Merge(token);
                }
                // Do single-session download for others
                else
                {
                    await httpClient!.Download(asset.AssetIndex.ConcatURL, asset.AssetIndex.ConcatPath, true, null, null, token);
                }

#if !DEBUG
                LogWriteLine($"Downloaded cache [T: {asset.AssetIndex.DataType}]: {asset.AssetIndex.N}", LogType.Default, true);
#endif
            }

            // Remove Asset Entry display
            PopRepairAssetEntry(asset.AssetProperty);
        }

        private async void _httpClient_UpdateAssetProgress(object sender, DownloadEvent e)
        {
            // Update current progress percentages and speed
            _progress!.ProgressAllPercentage = _progressAllSizeCurrent != 0 ?
                ConverterTool.GetPercentageNumber(_progressAllSizeCurrent, _progressAllSizeTotal) :
                0;

            if (e!.State != DownloadState.Merging)
            {
                _progressAllSizeCurrent += e.Read;
            }
            long speed = (long)(_progressAllSizeCurrent / _stopwatch!.Elapsed.TotalSeconds);

            if (await CheckIfNeedRefreshStopwatch())
            {
                // Update current activity status
                _status!.IsProgressAllIndetermined = false;
                string timeLeftString = string.Format(Lang!._Misc!.TimeRemainHMSFormat!, ((_progressAllSizeCurrent - _progressAllSizeTotal) / ConverterTool.Unzeroed(speed)).ToTimeSpanNormalized());
                _status.ActivityAll = string.Format(Lang!._Misc!.Downloading + ": {0}/{1} ", _progressAllCountCurrent, _progressAllCountTotal)
                                       + string.Format($"({Lang._Misc.SpeedPerSec})", ConverterTool.SummarizeSizeSimple(speed))
                                       + $" | {timeLeftString}";

                // Trigger update
                UpdateAll();
            }
        }
    }
}
