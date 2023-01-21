using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;

namespace CollapseLauncher
{
    internal partial class HonkaiRepair : IRepair
    {
        public ObservableCollection<RepairAssetProperty> RepairAssetEntry { get; set; }
        public event EventHandler<RepairProgress> ProgressChanged;
        public event EventHandler<RepairStatus> StatusChanged;

        private const int _refreshInterval = 33;
        private const int _bufferLength = 4 << 10;
        private const int _bufferBigLength = 1 << 20;
        private const int _sizeForMultiDownload = 10 << 20;
        private const string _userAgent = "UnityPlayer/2017.4.18f1 (UnityWebRequest/1.0, libcurl/7.51.0-DEV)";
        private const string _blockBasePath = "BH3_Data/StreamingAssets/Asb/pc/";
        private UIElement _parentUI { get; init; }
        private RepairStatus _status;
        private RepairProgress _progress;
        private byte _repairThread { get; set; }
        private CancellationTokenSource _token { get; set; }
        private GameVersion _gameVersion { get; init; }
        private string _gamePath { get; init; }
        private string _gameRepoURL { get; init; }
        private PresetConfigV2 _gamePreset { get; set; }
        private Stopwatch _stopwatch { get; set; }
        private Stopwatch _refreshStopwatch { get; set; }
        private List<FilePropertiesRemote> _assetIndex { get; set; }

        private int _progressTotalCountCurrent;
        private int _progressTotalCountFound;
        private int _progressTotalCount;
        private long _progressTotalSizeCurrent;
        private long _progressTotalSizeFound;
        private long _progressTotalSize;
        private long _progressPerFileSizeCurrent;
        private long _progressPerFileSize;

        public HonkaiRepair(UIElement parentUI, string gameVersion, string gamePath,
            string gameRepoURL, PresetConfigV2 gamePreset, byte repairThread)
        {
            _parentUI = parentUI;
            _status = new RepairStatus();
            _progress = new RepairProgress();
            _token = new CancellationTokenSource();
            _gameVersion = new GameVersion(gameVersion);
            _gamePath = gamePath;
            _gamePreset = gamePreset;
            _gameRepoURL = gameRepoURL;
            _repairThread = repairThread;
            _stopwatch = Stopwatch.StartNew();
            _refreshStopwatch = Stopwatch.StartNew();
            RepairAssetEntry = new ObservableCollection<RepairAssetProperty>();
        }

        ~HonkaiRepair() => Dispose();

        public async Task<bool> StartCheckRoutine()
        {
            return await TryRunExamineThrow(CheckRoutine());
        }

        public async Task StartRepairRoutine(bool showInteractivePrompt = false)
        {
            if (_assetIndex.Count == 0) throw new InvalidOperationException("There's no broken file being reported! You can't do the repair process!");

            _ = await TryRunExamineThrow(RepairRoutine());
        }

        private async Task<bool> CheckRoutine()
        {
            // Reset status and progress
            ResetStatusAndProgress();

            // Step 1: Fetch asset indexes
            _assetIndex = await Fetch();

            // Step 2: Calculate the total size and count of the files
            CountAssetIndex(_assetIndex);

            // Step 3: Check for the asset integrity
            _assetIndex = await Check(_assetIndex);

            // Step 4: Summarize and returns true if the assetIndex count != 0 indicates broken file was found.
            //         either way, returns false.
            return SummarizeStatusAndProgress(_assetIndex);
        }

        private async Task<bool> RepairRoutine()
        {
            // Assign repair task
            Task<bool> repairTask = Repair(_assetIndex);

            // Run repair process
            bool repairTaskSuccess = await TryRunExamineThrow(repairTask);

            // Reset status and progress
            ResetStatusAndProgress();

            // Set as completed
            _status.IsCompleted = true;
            _status.IsCanceled = false;
            _status.RepairActivityStatus = Lang._GameRepairPage.Status7;

            // Update status and progress
            UpdateAll();

            return repairTaskSuccess;
        }

        private async Task<bool> TryRunExamineThrow(Task<bool> action)
        {
            try
            {
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
            }
        }

        public void CancelRoutine()
        {
            // Trigger token cancellation
            _token.Cancel();
        }

        public void Dispose()
        {
        }

        private bool SummarizeStatusAndProgress(List<FilePropertiesRemote> assetIndex)
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
            _status.RepairActivityStatus = IsBrokenFound ? string.Format(Lang._GameRepairPage.Status3, _progressTotalCount, ConverterTool.SummarizeSizeSimple(_progressTotalSize))
                : Lang._GameRepairPage.Status4;

            // Update status and progress
            UpdateAll();

            // Return broken asset check
            return IsBrokenFound;
        }

        private void ResetStatusAndProgress()
        {
            // Reset RepairAssetProperty list
            RepairAssetEntry.Clear();

            // Reset status and progress properties
            ResetStatusAndProgressProperty();

            // Update the status and progress
            UpdateAll();
        }

        private void ResetStatusAndProgressProperty()
        {
            // Reset cancellation token
            _token = new CancellationTokenSource();

            // Show the asset entry panel
            _status.IsAssetEntryPanelShow = false;

            // Reset all total activity status
            _status.RepairActivityStatus = Lang._GameRepairPage.StatusNone;
            _status.RepairActivityTotal = Lang._GameRepairPage.StatusNone;
            _status.IsProgressTotalIndetermined = false;

            // Reset all per-file activity status
            _status.RepairActivityPerFile = Lang._GameRepairPage.StatusNone;
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

        private void Dispatch(DispatcherQueueHandler handler) => _parentUI.DispatcherQueue.TryEnqueue(handler);
        private async Task<bool> CheckIfNeedRefreshStopwatch()
        {
            if (_refreshStopwatch.ElapsedMilliseconds > _refreshInterval)
            {
                _refreshStopwatch.Restart();
                return true;
            }

            await Task.Delay(33);
            return false;
        }
        private void RestartStopwatch() => _stopwatch.Restart();
        private void UpdateAll()
        {
            UpdateStatus();
            UpdateProgress();
        }
        private void UpdateProgress() => ProgressChanged?.Invoke(this, _progress);
        private void UpdateStatus() => StatusChanged?.Invoke(this, _status);
    }
}
