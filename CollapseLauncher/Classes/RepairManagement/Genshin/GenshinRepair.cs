using CollapseLauncher.Interfaces;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.EncTool.Parser.YSDispatchHelper;
using Hi3Helper.Sophon;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Global

namespace CollapseLauncher
{
    public enum GenshinAudioLanguage
    {
        English = 0,
        Chinese = 1,
        Japanese = 2,
        Korean = 3
    }

    internal partial class GenshinRepair(
        UIElement     parentUI,
        IGameVersion  gameVersionManager,
        IGameSettings gameSettings)
        : ProgressBase<PkgVersionProperties>(parentUI,
                                             gameVersionManager,
                                             gameSettings,
                                             null,
                                             null)
        , IRepair
    {
        #region ExtensionProperties
        private           string               ExecPrefix              { get => Path.GetFileNameWithoutExtension(GameVersionManager.GamePreset.GameExecutableName); }
        private           int                  DispatcherRegionID      { get; } = gameVersionManager.GamePreset.GetRegServerNameID();
        private           string               DispatcherURL           { get => GameVersionManager.GamePreset.GameDispatchURL ?? ""; }
        private protected string               DispatcherKey           { get => GameVersionManager.GamePreset.DispatcherKey ?? ""; }
        private protected int                  DispatcherKeyLength     { get => GameVersionManager.GamePreset.DispatcherKeyBitLength ?? 0x100; }
        private           QueryProperty        DispatchQuery           { get; set; }
        private           DateTime             LastDispatchQueryTime   { get; set; }
        private           string               GamePersistentPath      { get => Path.Combine(GamePath, $"{Path.GetFileNameWithoutExtension(GameVersionManager.GamePreset.GameExecutableName)}_Data", "Persistent"); }
        private           string               GameStreamingAssetsPath { get => Path.Combine(GamePath, $"{Path.GetFileNameWithoutExtension(GameVersionManager.GamePreset.GameExecutableName)}_Data", "StreamingAssets"); }
        private protected GenshinAudioLanguage AudioLanguage           { get; init; } = (GenshinAudioLanguage)gameVersionManager.GamePreset.GetVoiceLanguageID();

        public override string GamePath
        {
            get => GameVersionManager.GameDirPath;
            set => GameVersionManager.GameDirPath = value;
        }

        #endregion

        #region Properties
        private Dictionary<string, SophonAsset>.AlternateLookup<ReadOnlySpan<char>>? _sophonAssetDictRefLookup;
        private Dictionary<string, SophonAsset> SophonAssetDictRef { get; } = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, SophonAsset>.AlternateLookup<ReadOnlySpan<char>> SophonAssetDictRefLookup { get => _sophonAssetDictRefLookup ??= SophonAssetDictRef.GetAlternateLookup<ReadOnlySpan<char>>(); }
        private bool IsParsePersistentManifestSuccess { get; set; }
        protected override string UserAgent => "UnityPlayer/2017.4.30f1 (UnityWebRequest/1.0, libcurl/7.51.0-DEV)";
        #endregion

        ~GenshinRepair() => Dispose();

        public async Task<bool> StartCheckRoutine(bool useFastCheck)
        {
            UseFastMethod = useFastCheck;
            return await TryRunExamineThrow(CheckRoutine());
        }

        public async Task StartRepairRoutine(bool showInteractivePrompt = false, Action actionIfInteractiveCancel = null)
        {
            if (AssetIndex.Count == 0) throw new InvalidOperationException("There's no broken file being reported! You can't do the repair process!");

            if (showInteractivePrompt)
            {
                await SpawnRepairDialog(AssetIndex, actionIfInteractiveCancel);
            }

            _ = await TryRunExamineThrow(RepairRoutine());
        }

        private async Task<bool> CheckRoutine()
        {
            // Reset status and progress
            ResetStatusAndProgress();
            AssetIndex.Clear();
            SophonAssetDictRef.Clear();

            // Step 1: Ensure that every file are not read-only
            TryUnassignReadOnlyFiles(GamePath);

            // Step 2: Fetch asset index
            AssetIndex = await Fetch(AssetIndex, Token.Token);

            // Step 3: Calculate all the size and count in total
            CountAssetIndex(AssetIndex);

            // Step 4: Check for the asset indexes integrity
            await Check(AssetIndex, Token.Token);

            // Step 5: Summarize and returns true if the assetIndex count != 0 indicates broken file was found.
            //         either way, returns false.
            return SummarizeStatusAndProgress(
                AssetIndex,
                string.Format(Lang._GameRepairPage.Status3, ProgressAllCountFound, SummarizeSizeSimple(ProgressAllSizeFound)),
                Lang._GameRepairPage.Status4);
        }

        private async Task<bool> RepairRoutine()
        {
            // Assign repair task
            Task<bool> repairTask = Repair(AssetIndex, Token.Token);

            // Run repair process
            bool repairTaskSuccess = await TryRunExamineThrow(repairTask);

            // Reset status and progress
            ResetStatusAndProgress();

            // Set as completed
            Status.IsCompleted = true;
            Status.IsCanceled = false;
            Status.ActivityStatus = Lang._GameRepairPage.Status7;

            // Update status and progress
            UpdateAll();

            return repairTaskSuccess;
        }

        public void CancelRoutine()
        {
            // Trigger token cancellation
            Token?.Cancel();
            Token?.Dispose();
            Token = null;
        }

        private async Task<QueryProperty> GetCachedDispatcherQuery(HttpClient client, CancellationToken token)
        {
            if (DispatchQuery != null && DateTime.Now <= LastDispatchQueryTime)
            {
                return DispatchQuery;
            }

            LastDispatchQueryTime = DateTime.Now.AddMinutes(5);
            DispatchQuery         = await GetDispatcherQuery(client, token);
            return DispatchQuery;
        }

        public void Dispose()
        {
            CancelRoutine();
            GC.SuppressFinalize(this);
        }
    }
}
