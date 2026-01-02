using CollapseLauncher.GameVersioning;
using CollapseLauncher.InstallManager.StarRail;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Statics;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
// ReSharper disable StringLiteralTypo

#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher
{
    internal partial class StarRailRepairV2 : ProgressBase<FilePropertiesRemote>, IRepair, IRepairAssetIndex
    {
        #region Properties

        public override string GamePath
        {
            get => GameVersionManager.GameDirPath;
            set => GameVersionManager.GameDirPath = value;
        }

        private GameTypeStarRailVersion InnerGameVersionManager { get; }
        private StarRailInstall? InnerGameInstaller
        {
            get => field ??= GamePropertyVault.GetCurrentGameProperty().GameInstall as StarRailInstall;
        }

        private bool                       IsCacheUpdateMode       { get; }
        private bool                       IsOnlyRecoverMain { get; }
        private List<FilePropertiesRemote> OriginAssetIndex  { get; set; } = [];
        private string                     ExecName          { get; }

        private string GameDataPersistentPathRelative { get => Path.Combine($"{ExecName}_Data", "Persistent"); }
        private string GameDataPersistentPath { get => Path.Combine(GamePath, GameDataPersistentPathRelative); }

        private string GameAudioLangListPathStatic { get => Path.Combine(GameDataPersistentPath, "AudioLaucherRecord.txt"); }

        protected override string UserAgent => "UnityPlayer/2019.4.34f1 (UnityWebRequest/1.0, libcurl/7.75.0-DEV)";
        #endregion

        public StarRailRepairV2(
            UIElement     parentUI,
            IGameVersion  gameVersionManager,
            IGameSettings gameSettings,
            bool          onlyRecoverMainAsset = false,
            string?       versionOverride      = null,
            bool          isCacheUpdateMode          = false)
            : base(parentUI,
                   gameVersionManager,
                   gameSettings,
                   "",
                   versionOverride)
        {
            // Get flag to only recover main assets
            IsOnlyRecoverMain = onlyRecoverMainAsset;
            InnerGameVersionManager = (gameVersionManager as GameTypeStarRailVersion)!;
            ExecName = Path.GetFileNameWithoutExtension(InnerGameVersionManager.GamePreset.GameExecutableName) ?? "";
            IsCacheUpdateMode = isCacheUpdateMode;
        }

        ~StarRailRepairV2() => Dispose();

        public List<FilePropertiesRemote> GetAssetIndex() => OriginAssetIndex;

        public async Task<bool> StartCheckRoutine(bool useFastCheck)
        {
            UseFastMethod = useFastCheck;
            return await TryRunExamineThrow(CheckRoutine());
        }

        private async Task<bool> CheckRoutine()
        {
            // Always clear the asset index list
            AssetIndex.Clear();

            // Reset status and progress
            ResetStatusAndProgress();

            // Step 1: Fetch asset indexes
            await Fetch(AssetIndex, Token!.Token);

            // Step 2: Remove blacklisted files from asset index (borrow function from StarRailInstall)
            await InnerGameInstaller!.FilterAssetList(AssetIndex, x => x.N, Token.Token);

            // Step 3: Calculate the total size and count of the files
            CountAssetIndex(AssetIndex);

            // Step 4: Check for the asset indexes integrity
            await Check(AssetIndex, Token.Token);

            // Step 5: Summarize and returns true if the assetIndex count != 0 indicates broken file was found.
            //         either way, returns false.
            return SummarizeStatusAndProgress(
                AssetIndex,
                string.Format(Lang._GameRepairPage.Status3, ProgressAllCountFound, ConverterTool.SummarizeSizeSimple(ProgressAllSizeFound)),
                Lang._GameRepairPage.Status4);
        }

        public void CancelRoutine()
        {
            // Trigger token cancellation
            Token?.Cancel();
            Token?.Dispose();
            Token = null;
        }

        public void Dispose()
        {
            CancelRoutine();
            GC.SuppressFinalize(this);
        }
    }
}
