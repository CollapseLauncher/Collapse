using CollapseLauncher.GameSettings.Honkai;
using CollapseLauncher.Interfaces;
using Hi3Helper.Data;
using Hi3Helper.EncTool.KianaManifest;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    internal partial class HonkaiRepair :
        ProgressBase<RepairAssetType, FilePropertiesRemote>, IRepair
    {
        #region Properties
        private const string _assetBasePath = "BH3_Data/StreamingAssets/";
        private string _assetBaseURL { get; set; }
        private string _blockBasePath { get => _assetBasePath + "Asb/pc/"; }
        private readonly string[] _skippableAssets = new string[] { "CG_Temp.usm" };
        private readonly string[] _audioPersistentAssets = new string[] { "AUDIO_Ex_Dorm_CN", "AUDIO_Ex_Event_CN", "AUDIO_Ex_Rogue_CN" };
        #endregion

        #region ExtensionProperties
        private protected AssetLanguage _audioLanguage { get; set; }
        private protected string _audioBaseLocalPath { get => _assetBasePath + "Audio/GeneratedSoundBanks/Windows/"; }
        private protected string _audioBaseRemotePath { get => _assetBaseURL + "Audio/{0}/Windows/"; }
        private protected string _audioPatchBaseLocalPath { get => _audioBaseLocalPath + "Patch/"; }
        private protected string _audioPatchBaseRemotePath { get => _audioBaseRemotePath + "Patch/"; }
        #endregion

        public HonkaiRepair(UIElement parentUI, string gameRepoURL, PresetConfigV2 gamePreset)
            : base(parentUI, null, gameRepoURL, gamePreset)
        {
            // Initialize audio asset language
            string audioLanguage = (Statics.PageStatics._GameSettings as HonkaiSettings).SettingsAudio.CVLanguage;
            switch (audioLanguage)
            {
                case "Chinese(PRC)":
                    _audioLanguage = AssetLanguage.Chinese;
                    break;
                default:
                    _audioLanguage = gamePreset.GameDefaultCVLanguage;
                    break;
            }
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
            // Always clear the asset index list
            _assetIndex.Clear();

            // Reset status and progress
            ResetStatusAndProgress();

            // Step 1: Fetch asset indexes
            await Fetch(_assetIndex, _token.Token);

            LogWriteLine($"Before adding generic files: {_progressTotalCount} assets {SummarizeSizeSimple(_progressTotalSize)} ({_progressTotalSize} bytes)");
            long beforeCount = _progressTotalCount;
            long beforeSize = _progressTotalSize;

            // Step 2: Calculate the total size and count of the files
            CountAssetIndex(_assetIndex);

            long afterCount = _progressTotalCount - beforeCount;
            long afterSize = _progressTotalSize - beforeSize;
            LogWriteLine($"After adding generic files: {_progressTotalCount} assets {SummarizeSizeSimple(_progressTotalSize)} ({_progressTotalSize} bytes)");
            LogWriteLine($"Adding size: {afterCount} assets {SummarizeSizeSimple(afterSize)} ({afterSize} bytes)");

            CountAudioIndex(_assetIndex);

            afterCount = _progressTotalCount - afterCount;
            afterSize = _progressTotalSize - afterSize;
            LogWriteLine($"After adding audio files: {_progressTotalCount} assets {SummarizeSizeSimple(_progressTotalSize)} ({_progressTotalSize} bytes)");
            LogWriteLine($"Adding size: {afterCount} assets {SummarizeSizeSimple(afterSize)} ({afterSize} bytes)");

            // Step 3: Check for the asset indexes integrity
            await Check(_assetIndex, _token.Token);

            // Step 4: Summarize and returns true if the assetIndex count != 0 indicates broken file was found.
            //         either way, returns false.
            return SummarizeStatusAndProgress(
                _assetIndex,
                string.Format(Lang._GameRepairPage.Status3, _progressTotalCountFound, ConverterTool.SummarizeSizeSimple(_progressTotalSizeFound)),
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
