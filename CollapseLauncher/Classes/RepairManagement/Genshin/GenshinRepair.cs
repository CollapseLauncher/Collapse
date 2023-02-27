using CollapseLauncher.GameSettings.Genshin;
using CollapseLauncher.Interfaces;
using Hi3Helper.Data;
using Hi3Helper.EncTool.KianaManifest;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

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
        private protected string _execPrefix { get => Path.GetFileNameWithoutExtension(_gamePreset.GameExecutableName); }
        private protected GenshinAudioLanguage _audioLanguage { get => (GenshinAudioLanguage)_gamePreset.GetRegServerNameID(); }
        private protected int _dispatcherRegionID { get => _gamePreset.GetRegServerNameID(); }
        private protected string _dispatcherURL { get => _gamePreset.GameDispatchURL ?? "" ; }
        private protected string _dispatcherKey { get => _gamePreset.DispatcherKey ?? "" ; }
        private protected int _dispatcherKeyLength { get => _gamePreset.DispatcherKeyBitLength ?? 0x100; }
        #endregion

        public GenshinRepair(UIElement parentUI, string gameVersion, string gamePath,
            string gameRepoURL, PresetConfigV2 gamePreset, byte repairThread, byte downloadThread)
            : base(parentUI, gameVersion, gamePath, gameRepoURL, gamePreset, repairThread, downloadThread)
        {
        }

        ~GenshinRepair() => Dispose();

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
            await Fetch(_assetIndex, _token.Token);

            // Step 4: Summarize and returns true if the assetIndex count != 0 indicates broken file was found.
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
