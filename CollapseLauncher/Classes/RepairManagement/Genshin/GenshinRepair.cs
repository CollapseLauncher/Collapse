using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CollapseLauncher.Interfaces;
using static Hi3Helper.Locale;

namespace CollapseLauncher
{
    internal partial class GenshinRepair : IRepair
    {
        public ObservableCollection<RepairAssetProperty> RepairAssetEntry { get; set; }
        public event EventHandler<RepairProgress> ProgressChanged;
        public event EventHandler<RepairStatus> StatusChanged;

        private const string _userAgent = "UnityPlayer/2017.4.18f1 (UnityWebRequest/1.0, libcurl/7.51.0-DEV)";
        private UIElement _parentUI { get; init; }
        private RepairStatus _status;
        private RepairProgress _progress;
        private CancellationTokenSource _token { get; set; }
        private GameVersion _gameVersion { get; init; }
        private string _gamePath { get; init; }
        private PresetConfigV2 _gamePreset { get; set; }

        public GenshinRepair(UIElement parentUI, string gameVersion, string gamePath, PresetConfigV2 gamePreset)
        {
            _parentUI = parentUI;
            _status = new RepairStatus();
            _progress = new RepairProgress();
            _token = new CancellationTokenSource();
            _gameVersion = new GameVersion(gameVersion);
            _gamePath = gamePath;
            _gamePreset = gamePreset;
        }

        ~GenshinRepair() => Dispose();

        public async Task<bool> StartCheckRoutine()
        {
            // Reset status and progress
            ResetStatusAndProgress();

            // Step 1: Fetch asset indexes
            Memory<FilePropertiesRemote> assetIndex = await FetchCacheAssetIndex();

            return true;
        }

        public async Task StartRepairRoutine(bool showInteractivePrompt = false) { }
        public void CancelRoutine()
        {
            // Trigger token cancellation
            _token.Cancel();
        }
        public void Dispose()
        {
        }

        private void ResetStatusAndProgress()
        {
            // Reset cancellation token
            _token = new CancellationTokenSource();

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

            // Update the status
            UpdateStatus();
        }

        private void UpdateProgress() => ProgressChanged?.Invoke(this, _progress);
        private void UpdateStatus() => StatusChanged?.Invoke(this, _status);
    }
}
