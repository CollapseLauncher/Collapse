using CollapseLauncher.Extension;
using CollapseLauncher.Interfaces;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.KianaDispatch;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Hi3Helper.Locale;

namespace CollapseLauncher
{
    internal partial class HonkaiCache : ProgressBase<CacheAsset>, ICache, ICacheBase<HonkaiCache>
    {
        #region Properties

        private const string           CacheRegionalCheckName = "sprite";
        private       string           GameLang         { get; }
        private       byte[]           GameSalt         { get; set; }
        private       KianaDispatch    GameGateway      { get; set; }
        private       List<CacheAsset> UpdateAssetIndex { get; set; }
        private       int              LuckyNumber      { get; set; }
        #endregion

        public HonkaiCache(UIElement parentUI, IGameVersionCheck gameVersionManager)
            : base(
                  parentUI,
                  gameVersionManager!,
                  gameVersionManager!.GameDirAppDataPath,
                  null,
                  gameVersionManager.GetGameVersionAPI()?.VersionString)
        {
            GameLang = _gameVersionManager!.GamePreset!.GetGameLanguage() ?? "en";
        }

        ~HonkaiCache() => Dispose();

        public async Task<bool> StartCheckRoutine(bool useFastCheck)
        {
            _useFastMethod = useFastCheck;
            return await TryRunExamineThrow(CheckRoutine());
        }

        private async Task<bool> CheckRoutine()
        {
            // Reset status and progress
            ResetStatusAndProgress();

            // Initialize _updateAssetIndex
            UpdateAssetIndex = [];

            // Reset status and progress
            // ResetStatusAndProgress();
            _token = new CancellationTokenSourceWrapper();

            // Step 1: Fetch asset indexes
            _assetIndex = await Fetch(_token!.Token);

            // Step 2: Start assets checking
            UpdateAssetIndex = await Check(_assetIndex, _token!.Token);

            // Step 3: Summarize and returns true if the assetIndex count != 0 indicates caches needs to be update.
            //         either way, returns false.
            return SummarizeStatusAndProgress(
                UpdateAssetIndex,
                string.Format(Lang!._CachesPage!.CachesStatusNeedUpdate!, _progressAllCountFound, ConverterTool.SummarizeSizeSimple(_progressAllSizeFound)),
                Lang._CachesPage.CachesStatusUpToDate);
        }

        public async Task StartUpdateRoutine(bool showInteractivePrompt = false)
        {
            if (UpdateAssetIndex!.Count == 0) throw new InvalidOperationException("There's no cache file need to be update! You can't do the update process!");

            _ = await TryRunExamineThrow(UpdateRoutine());
        }

        private async Task<bool> UpdateRoutine()
        {
            // Assign update task
            Task<bool> updateTask = Update(UpdateAssetIndex, _assetIndex, _token!.Token);

            // Run update process
            bool updateTaskSuccess = await TryRunExamineThrow(updateTask);

            // Reset status and progress
            ResetStatusAndProgress();

            // Set as completed
            _status.IsCompleted = true;
            _status.IsCanceled = false;
            _status.ActivityStatus = Lang!._CachesPage!.CachesStatusUpToDate;

            // Update status and progress
            UpdateAll();

            // Clean up _updateAssetIndex
            UpdateAssetIndex!.Clear();

            return updateTaskSuccess;
        }

        public HonkaiCache AsBaseType() => this;

        public void CancelRoutine()
        {
            _token!.Cancel();
        }

        public void Dispose()
        {
            CancelRoutine();
        }
    }
}
