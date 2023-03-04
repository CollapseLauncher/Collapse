using Force.Crc32;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;

namespace CollapseLauncher.Interfaces
{
    internal class ProgressBase<T1, T2> :
        GamePropertyBase<T1, T2> where T1 : Enum
    {
        public ProgressBase(UIElement parentUI, string gamePath, string gameRepoURL, PresetConfigV2 gamePreset)
            : base(parentUI, gamePath, gameRepoURL, gamePreset)
        {
            _status = new TotalPerfileStatus() { IsIncludePerFileIndicator = true };
            _progress = new TotalPerfileProgress();
            _stopwatch = Stopwatch.StartNew();
            _refreshStopwatch = Stopwatch.StartNew();
            _assetIndex = new List<T2>();
        }

        public event EventHandler<TotalPerfileProgress> ProgressChanged;
        public event EventHandler<TotalPerfileStatus> StatusChanged;

        protected TotalPerfileStatus _status;
        protected TotalPerfileProgress _progress;
        protected int _progressTotalCountCurrent;
        protected int _progressTotalCountFound;
        protected int _progressTotalCount;
        protected long _progressTotalSizeCurrent;
        protected long _progressTotalSizeFound;
        protected long _progressTotalSize;
        protected long _progressPerFileSizeCurrent;
        protected long _progressPerFileSize;

        #region ProgressEventHandlers - Fetch
        protected virtual void _httpClient_FetchAssetProgress(object sender, DownloadEvent e)
        {
            // Update fetch status
            _status.IsProgressPerFileIndetermined = false;
            _status.ActivityPerFile = string.Format(Lang._GameRepairPage.PerProgressSubtitle3, ConverterTool.SummarizeSizeSimple(e.Speed));

            // Update fetch progress
            _progress.ProgressPerFilePercentage = e.ProgressPercentage;

            // Push status and progress update
            UpdateStatus();
            UpdateProgress();
        }
        #endregion

        #region ProgressEventHandlers - Repair
        protected virtual async void _httpClient_RepairAssetProgress(object sender, DownloadEvent e)
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
        #endregion

        #region BaseTools
        protected virtual void ResetStatusAndProgress()
        {
            // Reset the cancellation token
            _token = new CancellationTokenSource();

            // Reset RepairAssetProperty list
            AssetEntry.Clear();

            // Reset status and progress properties
            ResetStatusAndProgressProperty();

            // Update the status and progress
            UpdateAll();
        }

        protected void ResetStatusAndProgressProperty()
        {
            // Reset cancellation token
            _token = new CancellationTokenSource();

            // Show the asset entry panel
            _status.IsAssetEntryPanelShow = false;

            // Reset all total activity status
            _status.ActivityStatus = Lang._GameRepairPage.StatusNone;
            _status.ActivityTotal = Lang._GameRepairPage.StatusNone;
            _status.IsProgressTotalIndetermined = false;

            // Reset all per-file activity status
            _status.ActivityPerFile = Lang._GameRepairPage.StatusNone;
            _status.IsProgressPerFileIndetermined = false;

            // Reset all status indicators
            _status.IsAssetEntryPanelShow = false;
            _status.IsCompleted = false;
            _status.IsCanceled = false;

            // Reset all total activity progress
            _progress.ProgressPerFilePercentage = 0;
            _progress.ProgressTotalPercentage = 0;
            _progress.ProgressTotalEntryCount = 0;
            _progress.ProgressTotalSpeed = 0;

            // Reset all inner counter
            _progressTotalCountCurrent = 0;
            _progressTotalCount = 0;
            _progressTotalSizeCurrent = 0;
            _progressTotalSize = 0;
            _progressPerFileSizeCurrent = 0;
            _progressPerFileSize = 0;
        }

        protected async Task<bool> TryRunExamineThrow(Task<bool> action)
        {
            try
            {
                // Define if the status is still running
                _status.IsRunning = true;

                // Run the task
                return await action;
            }
            catch (TaskCanceledException)
            {
                // If a cancellation was thrown, then set IsCanceled as true
                _status.IsCompleted = false;
                _status.IsCanceled = true;
                throw;
            }
            catch (OperationCanceledException)
            {
                // If a cancellation was thrown, then set IsCanceled as true
                _status.IsCompleted = false;
                _status.IsCanceled = true;
                throw;
            }
            catch (Exception)
            {
                // Except, if the other exception was thrown, then set both IsCompleted
                // and IsCanceled as false.
                _status.IsCompleted = false;
                _status.IsCanceled = false;
                throw;
            }
            finally
            {
                // Clear the _assetIndex after that
                if (!_status.IsCompleted)
                {
                    _assetIndex.Clear();
                }

                // Define that the status is not running
                _status.IsRunning = false;
            }
        }

        protected void SetFoundToTotalValue()
        {
            // Assign found count and size to total count and size
            _progressTotalCount = _progressTotalCountFound;
            _progressTotalSize = _progressTotalSizeFound;

            // Reset found count and size
            _progressTotalCountFound = 0;
            _progressTotalSizeFound = 0;
        }

        protected bool SummarizeStatusAndProgress(List<T2> assetIndex, string msgIfFound, string msgIfClear)
        {
            // Reset status and progress properties
            ResetStatusAndProgressProperty();

            // Assign found value to total value
            SetFoundToTotalValue();

            // Set check if broken asset is found or not
            bool IsBrokenFound = assetIndex.Count > 0;

            // Set status
            _status.IsAssetEntryPanelShow = IsBrokenFound;
            _status.IsCompleted = true;
            _status.IsCanceled = false;
            _status.ActivityStatus = IsBrokenFound ? msgIfFound : msgIfClear;

            // Update status and progress
            UpdateAll();

            // Return broken asset check
            return IsBrokenFound;
        }

        protected virtual bool IsArrayMatch(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target) => source.SequenceEqual(target);

        protected virtual async Task RunDownloadTask(long assetSize, string assetPath, string assetURL, Http _httpClient, CancellationToken token)
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

        #region HashTools
        protected virtual byte[] TryCheckCRCFromStackalloc(Stream fs, int bufferSize)
        {
            // Initialize buffer and put the chunk into the buffer using stack
            Span<byte> bufferStackalloc = stackalloc byte[bufferSize];

            // Read from filesystem
            fs.Read(bufferStackalloc);

            // Check the CRC of the chunk buffer
            return CheckCRCThreadChild(bufferStackalloc);
        }

        protected virtual byte[] CheckCRCThreadChild(ReadOnlySpan<byte> buffer)
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

        protected virtual byte[] CheckSHA1(Stream fs, byte[] gameSalt, CancellationToken token)
        {
            // Initialize buffer and put the chunk into the buffer using stack
            byte[] buffer = new byte[_bufferBigLength];
            int read;

            // Initialize HMACSHA1 hash
            HMACSHA1 _hash = new HMACSHA1(gameSalt);

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

        protected virtual byte[] CheckCRC(Stream stream, CancellationToken token)
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

        protected virtual byte[] CheckMD5(Stream stream, CancellationToken token)
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
        #endregion

        #region HandlerUpdaters
        protected virtual void PopRepairAssetEntry() => Dispatch(() =>
        {
            try
            {
                AssetEntry.RemoveAt(0);
            }
            catch { }
        });

        protected virtual async void UpdateProgressCRC()
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

        protected virtual void UpdateRepairStatus(string activityStatus, string activityTotal, bool isPerFileIndetermined)
        {
            // Set repair activity status
            _status.ActivityStatus = activityStatus;
            _status.ActivityTotal = activityTotal;
            _status.IsProgressPerFileIndetermined = isPerFileIndetermined;

            // Update status
            UpdateStatus();
        }

        protected void Dispatch(DispatcherQueueHandler handler) => _parentUI.DispatcherQueue.TryEnqueue(handler);
        protected async Task<bool> CheckIfNeedRefreshStopwatch()
        {
            if (_refreshStopwatch.ElapsedMilliseconds > _refreshInterval)
            {
                _refreshStopwatch.Restart();
                return true;
            }

            await Task.Delay(33);
            return false;
        }
        protected void RestartStopwatch() => _stopwatch.Restart();
        protected void UpdateAll()
        {
            UpdateStatus();
            UpdateProgress();
        }
        protected void UpdateProgress() => ProgressChanged?.Invoke(this, _progress);
        protected void UpdateStatus() => StatusChanged?.Invoke(this, _status);
        #endregion
    }
}
