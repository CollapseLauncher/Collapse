using CollapseLauncher.GameSettings.Zenless;
using CollapseLauncher.GameVersioning;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher
{
    internal partial class ZenlessRepair : ProgressBase<FilePropertiesRemote>, IRepair, IRepairAssetIndex
    {
        #region Properties
        internal const string _assetGamePersistentPath = @"{0}_Data\Persistent";
        internal const string _assetGameStreamingPath = @"{0}_Data\StreamingAssets";

        // ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
        private bool IsOnlyRecoverMain { get; set; }
        private bool IsCacheUpdateMode { get; set; }
        private string? ExecutableName { get; set; }
        // ReSharper restore AutoPropertyCanBeMadeGetOnly.Local
        
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        private List<FilePropertiesRemote>? OriginAssetIndex { get; set; }
        private GameTypeZenlessVersion? GameVersionManagerCast { get => _gameVersionManager as GameTypeZenlessVersion; }
        private ZenlessSettings? GameSettings { get; init; }

        private string GameDataPersistentPath { get => Path.Combine(_gamePath, string.Format(_assetGamePersistentPath, ExecutableName)); }
        private string GameDataStreamingPath { get => Path.Combine(_gamePath, string.Format(_assetGameStreamingPath, ExecutableName)); }

        protected string? _gameAudioLangListPath
        {
            get
            {
                // If the persistent folder is not exist, then return null
                if (!Directory.Exists(GameDataPersistentPath))
                    return null;

                // Get the audio lang path index
                string audioLangPath = _gameAudioLangListPathStatic;
                return File.Exists(audioLangPath) ? audioLangPath : null;
            }
        }

        private string? _gameAudioLangListPathAlternate
        {
            get
            {
                // If the persistent folder is not exist, then return null
                if (!Directory.Exists(GameDataPersistentPath))
                    return null;

                // Get the audio lang path index
                string audioLangPath = _gameAudioLangListPathAlternateStatic;
                return File.Exists(audioLangPath) ? audioLangPath : null;
            }
        }

        private string _gameAudioLangListPathStatic =>
            Path.Combine(GameDataPersistentPath, "audio_lang_launcher");
        private string _gameAudioLangListPathAlternateStatic =>
            Path.Combine(GameDataPersistentPath, "audio_lang");

        protected override string _userAgent { get; set; } = "UnityPlayer/2019.4.40f1 (UnityWebRequest/1.0, libcurl/7.80.0-DEV)";

        public ZenlessRepair(UIElement parentUI, IGameVersionCheck gameVersionManager, ZenlessSettings gameSettings, bool isOnlyRecoverMain = false, string? versionOverride = null, bool isCacheUpdateMode = false)
            : base(parentUI, gameVersionManager, null, "", versionOverride)
        {
            // Use IsOnlyRecoverMain for future delta-patch or main game only files
            IsOnlyRecoverMain = isOnlyRecoverMain;
            // We are merging cache functionality with cache update
            IsCacheUpdateMode = isCacheUpdateMode;
            ExecutableName = Path.GetFileNameWithoutExtension(gameVersionManager.GamePreset.GameExecutableName);
            GameSettings = gameSettings;
        }
        #endregion

        #region Public Methods

        ~ZenlessRepair() => Dispose();

        public List<FilePropertiesRemote> GetAssetIndex() => OriginAssetIndex!;

        public async Task<bool> StartCheckRoutine(bool useFastCheck)
        {
            _useFastMethod = useFastCheck;
            return await TryRunExamineThrow(CheckRoutine());
        }

        public async Task StartRepairRoutine(bool showInteractivePrompt = false, Action? actionIfInteractiveCancel = null)
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
                string.Format(Locale.Lang._GameRepairPage.Status3, _progressAllCountFound, ConverterTool.SummarizeSizeSimple(_progressAllSizeFound)),
                Locale.Lang._GameRepairPage.Status4);
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
            _status.ActivityStatus = Locale.Lang._GameRepairPage.Status7;

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
        #endregion
    }
}
