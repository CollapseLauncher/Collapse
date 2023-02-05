using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    internal partial class HonkaiCache
    {
        private async Task<List<CacheAsset>> Check(List<CacheAsset> assetIndex)
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
                    // Check for unused files
                    CheckUnusedAssets(assetIndex, returnAsset);

                    // Do check in parallelization.
                    Parallel.ForEach(assetIndex, new ParallelOptions { MaxDegreeOfParallelism = _threadCount }, (asset) =>
                    {
                        CheckAsset(asset, returnAsset);
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

        private void CheckAsset(CacheAsset asset, List<CacheAsset> returnAsset)
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
                byte[] hashArray = TryCheckCRC(fs);

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

        private byte[] TryCheckCRC(Stream fs)
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
                _token.Token.ThrowIfCancellationRequested();

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

        private bool IsCRCArrayMatch(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target) =>
                     source[0] == target[0] && source[1] == target[1] && source[2] == target[2] && source[3] == target[3] && source[4] == target[4]
                  && source[5] == target[5] && source[6] == target[6] && source[7] == target[7] && source[8] == target[8] && source[9] == target[9]
                  && source[10] == target[10] && source[11] == target[11] && source[12] == target[12] && source[13] == target[13] && source[14] == target[14]
                  && source[15] == target[15] && source[16] == target[16] && source[17] == target[17] && source[18] == target[18] && source[19] == target[19];

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
