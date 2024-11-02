using CollapseLauncher.GameSettings.Honkai;
using CollapseLauncher.Interfaces;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.EncTool.Parser.KianaDispatch;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Hi3Helper.Locale;

namespace CollapseLauncher
{
    internal partial class HonkaiRepair : ProgressBase<FilePropertiesRemote>, IRepair, IRepairAssetIndex
    {
        #region Properties
        private const string _assetBasePath = "BH3_Data/StreamingAssets/";
        private readonly string[] _skippableAssets = new string[] { "CG_Temp.usm$Generic", "BlockMeta.xmf$Generic", "Blocks.xmf$Generic" };
        private HonkaiCache _cacheUtil { get; init; }
        private string _assetBaseURL { get; set; }
        private string _blockBaseURL { get => ConverterTool.CombineURLFromString(_assetBaseURL, $"StreamingAsb/{string.Join('_', _gameVersion.VersionArray)}/pc/HD"); }
        private string _blockAsbBaseURL { get => ConverterTool.CombineURLFromString(_blockBaseURL, "/asb"); }
        private string _blockPatchBaseURL { get => ConverterTool.CombineURLFromString(_blockBaseURL, "/patch"); }
        private string _blockPatchDiffBaseURL { get => ConverterTool.CombineURLFromString(_blockPatchBaseURL, "/{0}"); }
        private string _blockPatchDiffPath { get => ConverterTool.CombineURLFromString(_assetBasePath, "Asb/pc/Patch"); }
        private string _blockBasePath { get => ConverterTool.CombineURLFromString(_assetBasePath, "Asb/pc/"); }
        private bool _isOnlyRecoverMain { get; set; }
        private KianaDispatch _gameServer { get; set; }
        #endregion

        #region ExtensionProperties
        private protected AudioLanguageType _audioLanguage { get; set; }
        private protected string _audioBaseLocalPath { get => ConverterTool.CombineURLFromString(_assetBasePath, "Audio/GeneratedSoundBanks/Windows/"); }
        private protected string _audioBaseRemotePath { get => ConverterTool.CombineURLFromString(_assetBaseURL, "Audio/Windows/{0}/{1}/"); }
        private protected string _audioPatchBaseLocalPath { get => ConverterTool.CombineURLFromString(_audioBaseLocalPath, "Patch/"); }
        private protected string _audioPatchBaseRemotePath { get => ConverterTool.CombineURLFromString(_audioBaseRemotePath, "Patch/"); }
        private protected string _videoBaseLocalPath { get => ConverterTool.CombineURLFromString(_assetBasePath, "Video/"); }
        private protected List<FilePropertiesRemote> _originAssetIndex { get; set; }
        #endregion

        public HonkaiRepair(UIElement parentUI, IGameVersionCheck gameVersionManager, ICache gameCacheManager, IGameSettings gameSettings, bool onlyRecoverMainAsset = false, string versionOverride = null)
            : base(parentUI, gameVersionManager, gameSettings, null, "", versionOverride)
        {
            _cacheUtil = (gameCacheManager as ICacheBase<HonkaiCache>)?.AsBaseType();

            // Get flag to only recover main assets
            _isOnlyRecoverMain = onlyRecoverMainAsset;

            // Initialize audio asset language
            string audioLanguage = (gameSettings as HonkaiSettings)?.SettingsAudio._userCVLanguage;
            _audioLanguage = audioLanguage switch
                             {
                                 "Chinese(PRC)" => AudioLanguageType.Chinese,
                                 _ => _gameVersionManager.GamePreset.GameDefaultCVLanguage
                             };
        }

        ~HonkaiRepair() => Dispose();

        public List<FilePropertiesRemote> GetAssetIndex() => _originAssetIndex;

        public async Task<bool> StartCheckRoutine(bool useFastCheck)
        {
            _useFastMethod = useFastCheck;
            return await TryRunExamineThrow(CheckRoutine());
        }

        public async Task StartRepairRoutine(bool showInteractivePrompt = false, Action actionIfInteractiveCancel = null)
        {
            if (_assetIndex.Count == 0) throw new InvalidOperationException("There's no broken file being reported! You can't do the repair process!");

            if (showInteractivePrompt && actionIfInteractiveCancel != null)
            {
                await SpawnRepairDialog(_assetIndex, actionIfInteractiveCancel);
            }

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

            // Copy list to _originAssetIndex
            _originAssetIndex = [.._assetIndex];

            // Step 3: Check for the asset indexes integrity
            await Check(_assetIndex, _token.Token);

            // Step 4: Summarize and returns true if the assetIndex count != 0 indicates broken file was found.
            //         either way, returns false.
            return SummarizeStatusAndProgress(
                _assetIndex,
                string.Format(Lang._GameRepairPage.Status3, _progressAllCountFound, ConverterTool.SummarizeSizeSimple(_progressAllSizeFound)),
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
            if (_status != null)
            {
                _status.IsCompleted    = true;
                _status.IsCanceled     = false;
                _status.ActivityStatus = Lang._GameRepairPage.Status7;
            }

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
