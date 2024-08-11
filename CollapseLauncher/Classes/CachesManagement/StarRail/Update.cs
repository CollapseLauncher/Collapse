using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset;
using Hi3Helper.Http;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
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
                _status.IsProgressAllIndetermined = true;
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
                    foreach ((SRAsset, IAssetProperty) asset in
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

        private async Task UpdateCacheAsset((SRAsset AssetIndex, IAssetProperty AssetProperty) asset, Http httpClient, CancellationToken token)
        {
            // Increment total count and update the status
            _progressAllCountCurrent++;
            _status.ActivityStatus = string.Format(Lang._Misc.Downloading + " {0}: {1}", asset.AssetIndex.AssetType, Path.GetFileName(asset.AssetIndex.LocalName));
            UpdateAll();

            // Assign and check the path of the asset directory
            string assetDir = Path.GetDirectoryName(asset.AssetIndex.LocalName);
            if (!Directory.Exists(assetDir))
            {
                Directory.CreateDirectory(assetDir);
            }

            // Do multi-session download for asset that has applicable size
            if (asset.AssetIndex.Size >= _sizeForMultiDownload && !_isBurstDownloadEnabled)
            {
                await httpClient.Download(asset.AssetIndex.RemoteURL, asset.AssetIndex.LocalName, _downloadThreadCount, true, token);
                await httpClient.Merge(token);
            }
            // Do single-session download for others
            else
            {
                await httpClient.Download(asset.AssetIndex.RemoteURL, asset.AssetIndex.LocalName, true, null, null, token);
            }

            LogWriteLine($"Downloaded cache [T: {asset.AssetIndex.AssetType}]: {Path.GetFileName(asset.AssetIndex.LocalName)}", LogType.Default, true);


            // Remove Asset Entry display
            PopRepairAssetEntry(asset.AssetProperty);
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
