using Hi3Helper.Preset;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using static Hi3Helper.Locale;
using Hi3Helper.Data;

namespace CollapseLauncher.Interfaces
{
    internal class ProgressBase<T1, T2> :
        GamePropertyBase<T1, T2> where T1 : Enum
    {
        public ProgressBase(UIElement parentUI, string gameVersion, string gamePath,
            string gameRepoURL, PresetConfigV2 gamePreset, byte repairThread, byte downloadThread)
            : base(parentUI, gameVersion, gamePath, gameRepoURL, gamePreset, repairThread, downloadThread)
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

        protected void ResetStatusAndProgress()
        {
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
    }
}
