using CollapseLauncher.Interfaces;
using Hi3Helper.Preset;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;

namespace CollapseLauncher
{
    public enum GenshinAudioLanguage : int
    {
        English = 0,
        Chinese = 1,
        Japanese = 2,
        Korean = 3
    }

    internal partial class GenshinRepair :
        ProgressBase<RepairAssetType, PkgVersionProperties>, IRepair
    {
        #region ExtensionProperties
        private protected string _execPrefix { get => Path.GetFileNameWithoutExtension(_gameVersionManager.GamePreset.GameExecutableName); }
        private protected int _dispatcherRegionID { get; init; }
        private protected string _dispatcherURL { get => _gameVersionManager.GamePreset.GameDispatchURL ?? ""; }
        private protected string _dispatcherKey { get => _gameVersionManager.GamePreset.DispatcherKey ?? ""; }
        private protected int _dispatcherKeyLength { get => _gameVersionManager.GamePreset.DispatcherKeyBitLength ?? 0x100; }
        private protected string _gamePersistentPath { get => Path.Combine(_gamePath, $"{Path.GetFileNameWithoutExtension(_gameVersionManager.GamePreset.GameExecutableName)}_Data", "Persistent"); }
        private protected string _gameStreamingAssetsPath { get => Path.Combine(_gamePath, $"{Path.GetFileNameWithoutExtension(_gameVersionManager.GamePreset.GameExecutableName)}_Data", "StreamingAssets"); }
        private protected GenshinAudioLanguage _audioLanguage { get; init; }
        #endregion

        #region Properties
        private bool _isParsePersistentManifestSuccess { get; set; }
        #endregion

        public GenshinRepair(UIElement parentUI, IGameVersionCheck GameVersionManager, string gameRepoURL)
            : base(parentUI, GameVersionManager, null, gameRepoURL, null)
        {
            _audioLanguage = (GenshinAudioLanguage)_gameVersionManager.GamePreset.GetVoiceLanguageID();
            _dispatcherRegionID = _gameVersionManager.GamePreset.GetRegServerNameID();
        }

        ~GenshinRepair() => Dispose();

        public async Task<bool> StartCheckRoutine(bool useFastCheck)
        {
            _useFastMethod = useFastCheck;
            return await TryRunExamineThrow(CheckRoutine());
        }

        public async Task StartRepairRoutine(bool showInteractivePrompt = false, Action actionIfInteractiveCancel = null)
        {
            if (_assetIndex.Count == 0) throw new InvalidOperationException("There's no broken file being reported! You can't do the repair process!");

            if (showInteractivePrompt)
            {
                await SpawnRepairDialog(_assetIndex, actionIfInteractiveCancel);
            }

            _ = await TryRunExamineThrow(RepairRoutine());
        }

        private async Task<bool> CheckRoutine()
        {
            // Reset status and progress
            ResetStatusAndProgress();
            _assetIndex.Clear();

            // Step 1: Ensure that every files are not read-only
            TryUnassignReadOnlyFiles(_gamePath);

            // Step 2: Fetch asset index
            _assetIndex = await Fetch(_assetIndex, _token.Token);

            // Step 3: Calculate all the size and count in total
            CountAssetIndex(_assetIndex);

            // Step 4: Check for the asset indexes integrity
            await Check(_assetIndex, _token.Token);

            // Step 5: Summarize and returns true if the assetIndex count != 0 indicates broken file was found.
            //         either way, returns false.
            return SummarizeStatusAndProgress(
                _assetIndex,
                string.Format(Lang._GameRepairPage.Status3, _progressTotalCountFound, SummarizeSizeSimple(_progressTotalSizeFound)),
                Lang._GameRepairPage.Status4);
        }

        private async Task<bool> RepairRoutine()
        {
            // Restart Stopwatch
            RestartStopwatch();

            // Assign repair task
            Task<bool> repairTask = Repair(_assetIndex, _token.Token);

            // Run repair process
            bool repairTaskSuccess = await TryRunExamineThrow(repairTask);

            // Reset status and progress
            ResetStatusAndProgress();

            // Set as completed
            _status.IsCompleted = true;
            _status.IsCanceled = false;
            _status.ActivityStatus = Lang._GameRepairPage.Status7;

            // Update status and progress
            UpdateAll();

            return repairTaskSuccess;
        }

        public void CancelRoutine()
        {
            // Trigger token cancellation
            _token.Cancel();
        }

        public void Dispose()
        {
            CancelRoutine();
        }
    }
}
