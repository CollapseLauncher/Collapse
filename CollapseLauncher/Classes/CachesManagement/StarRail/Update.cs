using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset;
using Hi3Helper.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    internal partial class StarRailCache
    {
        // ReSharper disable once UnusedParameter.Local
        private async Task<bool> Update(List<SRAsset> updateAssetIndex, List<SRAsset> assetIndex, CancellationToken token)
        {
            // Assign Http client
            Http httpClient = new Http(true, 5, 1000, _userAgent);
            try
            {
                // Set IsProgressAllIndetermined as false and update the status 
                _status.IsProgressAllIndetermined = true;
                UpdateStatus();

                // Subscribe the event listener
                httpClient.DownloadProgress += _httpClient_UpdateAssetProgress;
                // Iterate the asset index and do update operation
                foreach (SRAsset asset in updateAssetIndex)
                {
                    await UpdateCacheAsset(asset, httpClient, token);
                }

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

        private async Task UpdateCacheAsset(SRAsset asset, Http httpClient, CancellationToken token)
        {
            // Increment total count and update the status
            _progressAllCountCurrent++;
            _status.ActivityStatus = string.Format(Lang._Misc.Downloading + " {0}: {1}", asset.AssetType, Path.GetFileName(asset.LocalName));
            UpdateAll();

            // Assign and check the path of the asset directory
            string assetDir = Path.GetDirectoryName(asset.LocalName);
            if (!Directory.Exists(assetDir))
            {
                Directory.CreateDirectory(assetDir);
            }

            // Do multi-session download for asset that has applicable size
            if (asset.Size >= _sizeForMultiDownload)
            {
                await httpClient.Download(asset.RemoteURL, asset.LocalName, _downloadThreadCount, true, token);
                await httpClient.Merge(token);
            }
            // Do single-session download for others
            else
            {
                await httpClient.Download(asset.RemoteURL, asset.LocalName, true, null, null, token);
            }

            LogWriteLine($"Downloaded cache [T: {asset.AssetType}]: {Path.GetFileName(asset.LocalName)}", LogType.Default, true);


            // Remove Asset Entry display
            Dispatch(() => { if (AssetEntry.Count != 0) AssetEntry.RemoveAt(0); });
        }

        private async void _httpClient_UpdateAssetProgress(object sender, DownloadEvent e)
        {
            // Update current progress percentages and speed
            _progress.ProgressAllPercentage = _progressAllSizeCurrent != 0 ?
                ConverterTool.GetPercentageNumber(_progressAllSizeCurrent, _progressAllSizeTotal) :
                0;

            if (e.State != DownloadState.Merging)
            {
                _progressAllSizeCurrent += e.Read;
            }
            long speed = (long)(_progressAllSizeCurrent / _stopwatch.Elapsed.TotalSeconds);

            if (await CheckIfNeedRefreshStopwatch())
            {
                // Update current activity status
                _status.IsProgressAllIndetermined = false;
                string timeLeftString = string.Format(Lang._Misc.TimeRemainHMSFormat, ((_progressAllSizeCurrent - _progressAllSizeTotal) / ConverterTool.Unzeroed(speed)).ToTimeSpanNormalized());
                _status.ActivityAll = string.Format(Lang._Misc.Downloading + ": {0}/{1} ", _progressAllCountCurrent, _progressAllCountTotal)
                                       + string.Format($"({Lang._Misc.SpeedPerSec})", ConverterTool.SummarizeSizeSimple(speed))
                                       + $" | {timeLeftString}";

                // Trigger update
                UpdateAll();
            }
        }
    }
}
