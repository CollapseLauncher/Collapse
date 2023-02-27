using Force.Crc32;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
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
    internal partial class GenshinRepair
    {
        private async Task Check(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            List<FilePropertiesRemote> brokenAssetIndex = new List<FilePropertiesRemote>();

            // Set Indetermined status as false
            _status.IsProgressTotalIndetermined = false;
            _status.IsProgressPerFileIndetermined = false;

            // Show the asset entry panel
            _status.IsAssetEntryPanelShow = true;

            // Reset stopwatch
            RestartStopwatch();

            /*

            // Find unused assets
            CheckUnusedAsset(assetIndex, brokenAssetIndex);

            // Await the task for parallel processing
            await Task.Run(() =>
            {
                try
                {
                    // Check for skippable assets to skip the check
                    RemoveSkippableAssets(assetIndex);

                    // Iterate assetIndex and check it using different method for each type and run it in parallel
                    Parallel.ForEach(assetIndex, new ParallelOptions { MaxDegreeOfParallelism = _threadCount }, (asset) =>
                    {
                        // Assign a task depends on the asset type
                        switch (asset.FT)
                        {
                            case FileType.Blocks:
                                CheckAssetTypeBlocks(asset, brokenAssetIndex, token);
                                break;
                            case FileType.Audio:
                                CheckAssetTypeAudio(asset, brokenAssetIndex, token);
                                break;
                            default:
                                CheckAssetTypeGeneric(asset, brokenAssetIndex, token);
                                break;
                        }
                    });
                }
                catch (AggregateException ex)
                {
                    throw ex.Flatten().InnerExceptions.First();
                }
            }).ConfigureAwait(false);

            // Re-add the asset index with a broken asset index
            assetIndex.Clear();
            assetIndex.AddRange(brokenAssetIndex);
            */
        }


        #region Tools
        private bool IsArrayMatch(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target) => source.SequenceEqual(target);

        private byte[] TryCheckCRCFromStackalloc(Stream fs, int bufferSize)
        {
            // Initialize buffer and put the chunk into the buffer using stack
            Span<byte> bufferStackalloc = stackalloc byte[bufferSize];

            // Read from filesystem
            fs.Read(bufferStackalloc);

            // Check the CRC of the chunk buffer
            return CheckCRCThreadChild(bufferStackalloc);
        }

        private byte[] CheckCRCThreadChild(ReadOnlySpan<byte> buffer)
        {
            lock (this)
            {
                // Increment total size counter
                _progressTotalSizeCurrent += buffer.Length;
                // Increment per file size counter
                _progressPerFileSizeCurrent += buffer.Length;
            }

            // Update status and progress for CRC calculation
            UpdateProgressCRC();

            // Return computed hash byte
            Crc32Algorithm _crcInstance = new Crc32Algorithm();
            lock (_crcInstance)
            {
                return _crcInstance.ComputeHashByte(buffer);
            }
        }

        private byte[] CheckCRC(Stream stream, CancellationToken token)
        {
            // Reset CRC instance and assign buffer
            Crc32Algorithm _crcInstance = new Crc32Algorithm();
            Span<byte> buffer = stackalloc byte[_bufferBigLength];

            using (stream)
            {
                int read;
                while ((read = stream.Read(buffer)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    _crcInstance.Append(buffer.Slice(0, read));

                    lock (this)
                    {
                        // Increment total size counter
                        _progressTotalSizeCurrent += read;
                        // Increment per file size counter
                        _progressPerFileSizeCurrent += read;
                    }

                    // Update status and progress for CRC calculation
                    UpdateProgressCRC();
                }
            }

            // Return computed hash byte
            return _crcInstance.Hash;
        }

        private byte[] CheckMD5(Stream stream, CancellationToken token)
        {
            // Initialize MD5 instance and assign buffer
            MD5 md5Instance = MD5.Create();
            byte[] buffer = new byte[_bufferBigLength];

            using (stream)
            {
                int read;
                while ((read = stream.Read(buffer)) >= _bufferBigLength)
                {
                    token.ThrowIfCancellationRequested();
                    // Append buffer into hash block
                    md5Instance.TransformBlock(buffer, 0, buffer.Length, buffer, 0);

                    lock (this)
                    {
                        // Increment total size counter
                        _progressTotalSizeCurrent += read;
                        // Increment per file size counter
                        _progressPerFileSizeCurrent += read;
                    }

                    // Update status and progress for MD5 calculation
                    UpdateProgressCRC();
                }

                // Finalize the hash calculation
                md5Instance.TransformFinalBlock(buffer, 0, read);
            }

            // Return computed hash byte
            return md5Instance.Hash;
        }

        private async void UpdateProgressCRC()
        {
            if (await CheckIfNeedRefreshStopwatch())
            {
                // Update current progress percentages
                _progress.ProgressPerFilePercentage = _progressPerFileSizeCurrent != 0 ?
                    ConverterTool.GetPercentageNumber(_progressPerFileSizeCurrent, _progressPerFileSize) :
                    0;
                _progress.ProgressTotalPercentage = _progressTotalSizeCurrent != 0 ?
                    ConverterTool.GetPercentageNumber(_progressTotalSizeCurrent, _progressTotalSize) :
                    0;

                // Calculate speed
                long speed = (long)(_progressTotalSizeCurrent / _stopwatch.Elapsed.TotalSeconds);

                // Calculate current speed and update the status and progress speed
                _progress.ProgressTotalSpeed = (long)(_progressTotalSizeCurrent / _stopwatch.Elapsed.TotalSeconds);
                _status.ActivityPerFile = string.Format(Lang._Misc.Speed, ConverterTool.SummarizeSizeSimple(_progress.ProgressTotalSpeed));

                // Set time estimation string
                string timeLeftString = string.Format(Lang._Misc.TimeRemainHMSFormat, TimeSpan.FromSeconds((_progressTotalSizeCurrent - _progressTotalSize) / ConverterTool.Unzeroed(speed)));

                // Update current activity status
                _status.ActivityTotal = string.Format(Lang._GameRepairPage.PerProgressSubtitle2, _progressTotalCountCurrent, _progressTotalCount) + $" | {timeLeftString}";

                // Trigger update
                UpdateAll();
            }
        }
        #endregion
    }
}
