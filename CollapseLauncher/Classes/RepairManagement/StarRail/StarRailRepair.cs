using CollapseLauncher.GameVersioning;
using CollapseLauncher.Interfaces;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static Hi3Helper.Locale;

namespace CollapseLauncher
{
    internal partial class StarRailRepair : ProgressBase<FilePropertiesRemote>, IRepair, IRepairAssetIndex
    {
        #region Properties
        private GameTypeStarRailVersion _innerGameVersionManager { get; set; }
        private bool _isOnlyRecoverMain { get; set; }
        private List<FilePropertiesRemote> _originAssetIndex { get; set; }
        private string _execName { get; set; }
        private string _gameDataPersistentPath { get => Path.Combine(_gamePath, $"{_execName}_Data", "Persistent"); }
        private string _gameAudioLangListPath
        {
            get
            {
                // If the persistent folder is not exist, then return null
                if (!Directory.Exists(_gameDataPersistentPath)) return null;

                // Set the file list path
                string audioRecordPath = Path.Combine(_gameDataPersistentPath, "AudioLaucherRecord.txt");

                // Check if the file exist. If not, return null
                if (!File.Exists(audioRecordPath)) return null;

                // If exist, then return the path
                return audioRecordPath;
            }
        }
        private string _gameAudioLangListPathStatic { get => Path.Combine(_gameDataPersistentPath, "AudioLaucherRecord.txt"); }

        internal const string _assetGameAudioStreamingPath = @"{0}_Data\StreamingAssets\Audio\AudioPackage\Windows";
        internal const string _assetGameAudioPersistentPath = @"{0}_Data\Persistent\Audio\AudioPackage\Windows";

        internal const string _assetGameBlocksStreamingPath = @"{0}_Data\StreamingAssets\Asb\Windows";
        internal const string _assetGameBlocksPersistentPath = @"{0}_Data\Persistent\Asb\Windows";

        internal const string _assetGameVideoStreamingPath = @"{0}_Data\StreamingAssets\Video\Windows";
        internal const string _assetGameVideoPersistentPath = @"{0}_Data\Persistent\Video\Windows";
        protected override string _userAgent { get; set; } = "UnityPlayer/2019.4.34f1 (UnityWebRequest/1.0, libcurl/7.75.0-DEV)";
        #endregion

        public StarRailRepair(UIElement parentUI, IGameVersionCheck GameVersionManager, bool onlyRecoverMainAsset = false, string versionOverride = null)
            : base(parentUI, GameVersionManager, null, "", versionOverride)
        {
            // Get flag to only recover main assets
            _isOnlyRecoverMain = onlyRecoverMainAsset;
            _innerGameVersionManager = GameVersionManager as GameTypeStarRailVersion;
            _execName = Path.GetFileNameWithoutExtension(_innerGameVersionManager.GamePreset.GameExecutableName);
        }

        ~StarRailRepair() => Dispose();

        public List<FilePropertiesRemote> GetAssetIndex() => _originAssetIndex;

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
                string.Format(Lang._GameRepairPage.Status3, _progressAllCountFound, ConverterTool.SummarizeSizeSimple(_progressAllSizeFound)),
                Lang._GameRepairPage.Status4);
        }

        private async Task<bool> RepairRoutine()
        {
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
