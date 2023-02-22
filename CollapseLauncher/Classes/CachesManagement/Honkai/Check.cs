using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
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
    internal partial class HonkaiCache
    {
        private async Task<List<CacheAsset>> Check(List<CacheAsset> assetIndex, CancellationToken token)
        {
            // Initialize asset index for the return
            List<CacheAsset> returnAsset = new List<CacheAsset>();

            // Set Indetermined status as false
            _status.IsProgressTotalIndetermined = false;

            // Show the asset entry panel
            _status.IsAssetEntryPanelShow = true;

            // Reset stopwatch
            RestartStopwatch();

            await Task.Run(() =>
            {
                try
                {
                    // Create the cache directory if it doesn't exist
                    if (!Directory.Exists(_gamePath))
                    {
                        Directory.CreateDirectory(_gamePath);
                    }

                    // Check for unused files
                    CheckUnusedAssets(assetIndex, returnAsset);

                    // Do check in parallelization.
                    Parallel.ForEach(assetIndex, new ParallelOptions { MaxDegreeOfParallelism = _threadCount }, (asset) =>
                    {
                        CheckAsset(asset, returnAsset, token);
                    });
                }
                catch (AggregateException ex)
                {
                    throw ex.Flatten().InnerExceptions.First();
                }
            });

            // Return the asset index
            return returnAsset;
        }

        private void CheckUnusedAssets(List<CacheAsset> assetIndex, List<CacheAsset> returnAsset)
        {
            // Iterate the file contained in the _gamePath
            foreach (string filePath in Directory.EnumerateFiles(_gamePath, "*", SearchOption.AllDirectories))
            {
                if (!filePath.Contains("output_log") && !filePath.Contains("Crashes") && !filePath.Contains("Verify.txt") && !filePath.Contains("APM") && !assetIndex.Exists(x => x.ConcatPath == filePath))
                {
                    // Increment the total found count
                    _progressTotalCountFound++;

                    // Add asset to the returnAsset
                    FileInfo fileInfo = new FileInfo(filePath);
                    CacheAsset asset = new CacheAsset()
                    {
                        BasePath = Path.GetDirectoryName(filePath),
                        N = Path.GetFileName(filePath),
                        DataType = CacheAssetType.Unused,
                        CS = fileInfo.Length,
                        CRC = null,
                        Status = CacheAssetStatus.Unused,
                        IsUseLocalPath = true
                    };
                    returnAsset.Add(asset);

                    // Add to asset entry display
                    Dispatch(() => AssetEntry.Add(new AssetProperty<CacheAssetType>(
                            asset.N,
                            asset.DataType,
                            asset.BasePath,
                            asset.CS,
                            null,
                            null
                        ))
                    );

                    LogWriteLine($"File: {asset.ConcatPath} is unused!", LogType.Warning, true);
                }
            }
        }

        private void CheckAsset(CacheAsset asset, List<CacheAsset> returnAsset, CancellationToken token)
        {
            // Increment the count and update the status
            lock (this)
            {
                _progressTotalCountCurrent++;
                _status.ActivityStatus = string.Format(Lang._CachesPage.CachesStatusChecking, asset.DataType, asset.N);
                _status.ActivityTotal = string.Format(Lang._CachesPage.CachesTotalStatusChecking, _progressTotalCountCurrent, _progressTotalCount);
            }

            // Assign the file info.
            FileInfo fileInfo = new FileInfo(asset.ConcatPath);

            // Check if the file exist. If not, then add it to asset index.
            if (!fileInfo.Exists)
            {
                AddGenericCheckAsset(asset, CacheAssetStatus.New, returnAsset, null, asset.CRCArray);
                return;
            }

            // Check if the file size matches. if not, then add it to asset index.
            if (fileInfo.Length != asset.CS)
            {
                AddGenericCheckAsset(asset, CacheAssetStatus.New, returnAsset, null, asset.CRCArray);
                return;
            }

            // If above passes, then run the CRC check
            using (FileStream fs = new FileStream(asset.ConcatPath, FileMode.Open, FileAccess.Read))
            {
                // Calculate the asset CRC (SHA1)
                byte[] hashArray = TryCheckCRC(fs, token);

                // If the asset CRC doesn't match, then add the file to asset index.
                if (!IsCRCArrayMatch(asset.CRCArray, hashArray))
                {
                    AddGenericCheckAsset(asset, CacheAssetStatus.Obsolete, returnAsset, hashArray, asset.CRCArray);
                    return;
                }
            }
        }

        private void AddGenericCheckAsset(CacheAsset asset, CacheAssetStatus assetStatus, List<CacheAsset> returnAsset, byte[] localCRC, byte[] remoteCRC)
        {
            // Increment the count and total size
            lock (this)
            {
                // Set Indetermined status as false
                _status.IsProgressTotalIndetermined = false;
                _progressTotalCountFound++;
                _progressTotalSizeFound += asset.CS;
            }

            // Add file into asset index
            lock (returnAsset)
            {
                asset.Status = assetStatus;
                returnAsset.Add(asset);

                LogWriteLine($"[T: {asset.DataType}]: {asset.N} found to be \"{assetStatus}\"", LogType.Warning, true);
            }

            // Add to asset entry display
            Dispatch(() => AssetEntry.Add(new AssetProperty<CacheAssetType>(
                    Path.GetFileName(asset.N),
                    asset.DataType,
                    Path.GetDirectoryName(asset.N),
                    asset.CS,
                    localCRC,
                    remoteCRC
                ))
            );

            // Update the progress and status
            UpdateProgressCRC();
        }

        private byte[] TryCheckCRC(Stream fs, CancellationToken token)
        {
            // Initialize buffer and put the chunk into the buffer using stack
            byte[] buffer = new byte[_bufferBigLength];
            int read;

            // Initialize HMACSHA1 hash
            HMACSHA1 _hash = new HMACSHA1(_gameSalt);

            // Do read activity
            while ((read = fs.Read(buffer)) > 0)
            {
                // Throw Cancellation exception if detected
                token.ThrowIfCancellationRequested();

                // Calculate the hash block
                _hash.TransformBlock(buffer, 0, read, buffer, 0);

                // Increment the _progressTotalSize
                lock (this)
                {
                    _progressTotalSizeCurrent += read;
                }

                // Update the CRC progress
                UpdateProgressCRC();
            }

            // Do final transform
            _hash.TransformFinalBlock(buffer, 0, read);

            // Return as hash array
            return _hash.Hash;
        }

        private bool IsCRCArrayMatch(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target) => source.SequenceEqual(target);

        private async void UpdateProgressCRC()
        {
            if (await CheckIfNeedRefreshStopwatch())
            {
                // Update current progress percentages
                _progress.ProgressTotalPercentage = _progressTotalSizeCurrent != 0 ?
                    ConverterTool.GetPercentageNumber(_progressTotalSizeCurrent, _progressTotalSize) :
                    0;

                // Calculate current speed and update the status and progress speed
                long speed = (long)(_progressTotalSizeCurrent / _stopwatch.Elapsed.TotalSeconds);
                _progress.ProgressTotalSpeed = speed;

                // Update current activity status
                _status.ActivityTotal = string.Format(Lang._CachesPage.CachesTotalStatusChecking, _progressTotalCountCurrent, _progressTotalCount)
                                      + string.Format($" ({Lang._Misc.SpeedPerSec})", ConverterTool.SummarizeSizeSimple(speed));

                // Trigger update
                UpdateAll();
            }
        }
    }
}
