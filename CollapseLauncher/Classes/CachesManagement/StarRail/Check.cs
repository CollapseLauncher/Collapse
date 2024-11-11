using Hi3Helper;
using Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset;
using Hi3Helper.SentryHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    internal partial class StarRailCache
    {
        private async Task<List<SRAsset>> Check(List<SRAsset> assetIndex, CancellationToken token)
        {
            // Initialize asset index for the return
            List<SRAsset> returnAsset = new List<SRAsset>();

            // Set Indetermined status as false
            _status!.IsProgressAllIndetermined = false;

            // Show the asset entry panel
            _status.IsAssetEntryPanelShow = true;

            // Reset stopwatch
            RestartStopwatch();

            // Get persistent and streaming paths
            string execName = Path.GetFileNameWithoutExtension(_innerGameVersionManager!.GamePreset!.GameExecutableName);
            string baseDesignDataPathPersistent = Path.Combine(_gamePath!, @$"{execName}_Data\Persistent\DesignData\Windows");
            string baseDesignDataPathStreaming = Path.Combine(_gamePath!, @$"{execName}_Data\StreamingAssets\DesignData\Windows");

            string baseLuaPathPersistent = Path.Combine(_gamePath!, @$"{execName}_Data\Persistent\Lua\Windows");
            string baseLuaPathStreaming = Path.Combine(_gamePath!, @$"{execName}_Data\StreamingAssets\Lua\Windows");

            string baseIFixPathPersistent = Path.Combine(_gamePath!, @$"{execName}_Data\Persistent\IFix\Windows");
            string baseIFixPathStreaming = Path.Combine(_gamePath!, @$"{execName}_Data\StreamingAssets\IFix\Windows");

            try
            {
                // Do check in parallelization.
                await Parallel.ForEachAsync(assetIndex!, new ParallelOptions
                {
                    MaxDegreeOfParallelism = _threadCount,
                    CancellationToken = token
                }, async (asset, threadToken) =>
                {
                    switch (asset!.AssetType)
                    {
                        case SRAssetType.DesignData:
                            await CheckAsset(asset, returnAsset, baseDesignDataPathPersistent, baseDesignDataPathStreaming, threadToken);
                            break;
                        case SRAssetType.Lua:
                            await CheckAsset(asset, returnAsset, baseLuaPathPersistent, baseLuaPathStreaming, threadToken);
                            break;
                        case SRAssetType.IFix:
                            await CheckAsset(asset, returnAsset, baseIFixPathPersistent, baseIFixPathStreaming, threadToken);
                            break;
                    }
                });
            }
            catch (AggregateException ex)
            {
                var exFlattened = ex.Flatten().InnerExceptions.First();
                if (exFlattened is not (TaskCanceledException or OperationCanceledException))
                    await SentryHelper.ExceptionHandlerAsync(exFlattened);
                throw exFlattened;
            }

            // Return the asset index
            return returnAsset;
        }

        private async ValueTask CheckAsset(SRAsset asset, List<SRAsset> returnAsset, string basePersistent, string baseStreaming, CancellationToken token)
        {
            // Increment the count and update the status
            lock (this)
            {
                _progressAllCountCurrent++;
                _status!.ActivityStatus = string.Format(Lang!._CachesPage!.CachesStatusChecking!, asset!.AssetType, asset.LocalName);
                _status.ActivityAll = string.Format(Lang!._CachesPage!.CachesTotalStatusChecking!, _progressAllCountCurrent, _progressAllCountTotal);
            }

            // Get persistent and streaming paths
            FileInfo fileInfoPersistent = new FileInfo(Path.Combine(basePersistent!, asset.LocalName!));
            FileInfo fileInfoStreaming = new FileInfo(Path.Combine(baseStreaming!, asset.LocalName!));

            bool UsePersistent = !fileInfoStreaming.Exists;
            bool IsPersistentExist = fileInfoPersistent.Exists && fileInfoPersistent.Length == asset.Size;
            bool IsStreamingExist = fileInfoStreaming.Exists && fileInfoStreaming.Length == asset.Size;
            asset.LocalName = UsePersistent ? fileInfoPersistent.FullName : fileInfoStreaming.FullName;

            // Check if the file exist. If not, then add it to asset index.
            if (UsePersistent && !IsPersistentExist && !IsStreamingExist)
            {
                AddGenericCheckAsset(asset, CacheAssetStatus.New, returnAsset, null, asset.Hash);
                return;
            }

            // Skip CRC check if fast method is used
            if (_useFastMethod)
            {
                return;
            }

            // If above passes, then run the CRC check
            await using FileStream fs = await NaivelyOpenFileStreamAsync(UsePersistent ? fileInfoPersistent : fileInfoStreaming,
                                                                         FileMode.Open, FileAccess.Read, FileShare.Read);
            // Calculate the asset CRC (MD5)
            byte[] hashArray = await CheckHashAsync(fs, MD5.Create(), token);

            // If the asset CRC doesn't match, then add the file to asset index.
            if (!IsArrayMatch(asset.Hash, hashArray))
            {
                AddGenericCheckAsset(asset, CacheAssetStatus.Obsolete, returnAsset, hashArray, asset.Hash);
            }
        }

        private void AddGenericCheckAsset(SRAsset asset, CacheAssetStatus assetStatus, List<SRAsset> returnAsset, byte[] localCRC, byte[] remoteCRC)
        {
            // Increment the count and total size
            lock (this)
            {
                // Set Indetermined status as false
                _status!.IsProgressAllIndetermined = false;
                _progressAllCountFound++;
                _progressAllSizeFound += asset!.Size;
            }

            // Add file into asset index
            lock (returnAsset!)
            {
                returnAsset.Add(asset);

                LogWriteLine($"[T: {asset.AssetType}]: {asset.LocalName} found to be \"{assetStatus}\"", LogType.Warning, true);
            }

            // Add to asset entry display
            Dispatch(() => AssetEntry!.Add(new AssetProperty<CacheAssetType>(
                    Path.GetFileName(asset.LocalName),
                    ConvertCacheAssetTypeEnum(asset.AssetType),
                    $"{asset.AssetType}",
                    asset.Size,
                    localCRC,
                    remoteCRC
                ))
            );

            // Update the progress and status
            UpdateProgressCRC();
        }
    }
}
