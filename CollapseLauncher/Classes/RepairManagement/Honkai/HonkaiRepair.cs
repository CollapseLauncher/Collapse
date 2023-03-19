using CollapseLauncher.GameSettings.Honkai;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Statics;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;
using static Hi3Helper.Locale;

namespace CollapseLauncher
{
    internal partial class HonkaiRepair :
        ProgressBase<RepairAssetType, FilePropertiesRemote>, IRepair
    {
        #region Properties
        private HonkaiCache _cacheUtil = (PageStatics._GameCache as ICacheBase<HonkaiCache>).AsBaseType();

        private const ulong _assetIndexSignature = 0x657370616C6C6F43; // 657370616C6C6F43 is "Collapse"
        private const string _assetBasePath = "BH3_Data/StreamingAssets/";
        private readonly string[] _skippableAssets = new string[] { "CG_Temp.usm" };
        private string _assetBaseURL { get; set; }
        private string _blockBaseURL { get => ConverterTool.CombineURLFromString(_assetBaseURL, $"StreamingAsb/{string.Join('_', _gameVersion.VersionArray)}/pc/HD"); }
        private string _blockAsbBaseURL { get => ConverterTool.CombineURLFromString(_blockBaseURL, "/asb"); }
        private string _blockPatchBaseURL { get => ConverterTool.CombineURLFromString(_blockBaseURL, "/patch"); }
        private string _blockPatchDiffBaseURL { get => ConverterTool.CombineURLFromString(_blockPatchBaseURL, $"/{string.Join('_', _gameVersion.VersionArrayManifest)}"); }
        private string _blockPatchDiffPath { get => ConverterTool.CombineURLFromString(_assetBasePath, "Asb/pc/Patch"); }
        private string _blockBasePath { get => ConverterTool.CombineURLFromString(_assetBasePath, "Asb/pc/"); }
        private bool _isOnlyRecoverMain { get; set; }
        #endregion

        #region ExtensionProperties
        private protected AudioLanguageType _audioLanguage { get; set; }
        private protected string _audioBaseLocalPath { get => ConverterTool.CombineURLFromString(_assetBasePath, "Audio/GeneratedSoundBanks/Windows/"); }
        private protected string _audioBaseRemotePath { get => ConverterTool.CombineURLFromString(_assetBaseURL, "Audio/{0}/Windows/"); }
        private protected string _audioPatchBaseLocalPath { get => ConverterTool.CombineURLFromString(_audioBaseLocalPath, "Patch/"); }
        private protected string _audioPatchBaseRemotePath { get => ConverterTool.CombineURLFromString(_audioBaseRemotePath, "Patch/"); }
        private protected string _videoBaseLocalPath { get => ConverterTool.CombineURLFromString(_assetBasePath, "Video/"); }
        #endregion

        public HonkaiRepair(UIElement parentUI, string gameRepoURL, PresetConfigV2 gamePreset, bool onlyRecoverMainAsset = false)
            : base(parentUI, null, gameRepoURL, gamePreset)
        {
            // Get flag to only recover main assets
            _isOnlyRecoverMain = onlyRecoverMainAsset;

            // Initialize audio asset language
            string audioLanguage = (PageStatics._GameSettings as HonkaiSettings).SettingsAudio._userCVLanguage;
            switch (audioLanguage)
            {
                case "Chinese(PRC)":
                    _audioLanguage = AudioLanguageType.Chinese;
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

            // Step 2: Calculate the total size and count of the files
            CountAssetIndex(_assetIndex);

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
