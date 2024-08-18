using CollapseLauncher.GameVersioning;
using CollapseLauncher.Interfaces;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Hi3Helper.Locale;

namespace CollapseLauncher
{
    internal partial class StarRailCache : ProgressBase<SRAsset>, ICache
    {
        #region Properties
        private GameTypeStarRailVersion _innerGameVersionManager { get; set; }
        private List<SRAsset> _updateAssetIndex { get; set; }
        protected override string _userAgent { get; set; } = "UnityPlayer/2019.4.34f1 (UnityWebRequest/1.0, libcurl/7.75.0-DEV)";
        #endregion

        public StarRailCache(UIElement parentUI, IGameVersionCheck GameVersionManager)
            : base(
                  parentUI,
                  GameVersionManager,
                  GameVersionManager.GameDirPath,
                  null,
                  GameVersionManager.GetGameVersionAPI()?.VersionString)
        {
            _innerGameVersionManager = GameVersionManager as GameTypeStarRailVersion;
        }

        ~StarRailCache() => Dispose();

        public async Task<bool> StartCheckRoutine(bool useFastCheck)
        {
            _useFastMethod = useFastCheck;
            return await TryRunExamineThrow(CheckRoutine());
        }

        private async Task<bool> CheckRoutine()
        {
            // Initialize _updateAssetIndex
            _updateAssetIndex = new List<SRAsset>();

            // Reset status and progress
            ResetStatusAndProgress();

            // Step 1: Fetch asset indexes
            _assetIndex = await Fetch(_token.Token);

            // Step 2: Start assets checking
            _updateAssetIndex = await Check(_assetIndex, _token.Token);

            // Step 3: Summarize and returns true if the assetIndex count != 0 indicates caches needs to be update.
            //         either way, returns false.
            return SummarizeStatusAndProgress(
                _updateAssetIndex,
                string.Format(Lang._CachesPage.CachesStatusNeedUpdate, _progressAllCountFound, ConverterTool.SummarizeSizeSimple(_progressAllSizeFound)),
                Lang._CachesPage.CachesStatusUpToDate);
        }

        public async Task StartUpdateRoutine(bool showInteractivePrompt = false)
        {
            if (_updateAssetIndex.Count == 0) throw new InvalidOperationException("There's no cache file need to be update! You can't do the update process!");

            _ = await TryRunExamineThrow(UpdateRoutine());
        }

        private async Task<bool> UpdateRoutine()
        {
            // Restart stopwatch
            RestartStopwatch();

            // Assign update task
            Task<bool> updateTask = Update(_updateAssetIndex, _assetIndex, _token.Token);

            // Run update process
            bool updateTaskSuccess = await TryRunExamineThrow(updateTask);

            // Reset status and progress
            ResetStatusAndProgress();

            // Set as completed
            _status.IsCompleted = true;
            _status.IsCanceled = false;
            _status.ActivityStatus = Lang._CachesPage.CachesStatusUpToDate;

            // Update status and progress
            UpdateAll();

            // Clean up _updateAssetIndex
            _updateAssetIndex.Clear();

            return updateTaskSuccess;
        }

        public StarRailCache AsBaseType() => this;

        public void CancelRoutine()
        {
            _token.Cancel();
        }

        public void Dispose()
        {
            CancelRoutine();
        }
    }
}
