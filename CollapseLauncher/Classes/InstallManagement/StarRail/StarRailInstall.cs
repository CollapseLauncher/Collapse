using CollapseLauncher.GameVersioning;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher.InstallManager.StarRail
{
    internal class StarRailInstall : InstallManagerBase<GameTypeStarRailVersion>, IGameInstallManager
    {
        #region Override Properties
        protected override int _gameVoiceLanguageID { get => _gameVersionManager.GamePreset.GetVoiceLanguageID(); }
        protected override bool _canDeltaPatch { get => _gameVersionManager.IsGameHasDeltaPatch(); }
        protected override DeltaPatchProperty _gameDeltaPatchProperty { get => _gameVersionManager.GetDeltaPatchInfo(); }
        #endregion

        #region Properties
        // TODO: Need to look how the audio lang list works on HSR 1.5
        private string _gameAudioLangListPath { get => null; }
        private StarRailRepair _gameRepairManager { get; set; }
        #endregion

        public StarRailInstall(UIElement parentUI, IGameVersionCheck GameVersionManager)
            : base(parentUI, GameVersionManager)
        {

        }

        #region Public Methods
        public override async ValueTask<int> StartPackageVerification()
        {
            IsRunning = true;

            // Get the delta patch confirmation if the property is not null
            if (_gameDeltaPatchProperty != null)
            {
                // If the confirm is 1 (verified) or -1 (cancelled), then return the code
                int deltaPatchConfirm = await ConfirmDeltaPatchDialog(_gameDeltaPatchProperty, _gameRepairManager = new StarRailRepair(_parentUI, _gameVersionManager, true, _gameDeltaPatchProperty.SourceVer));
                if (deltaPatchConfirm == -1 || deltaPatchConfirm == 1) return deltaPatchConfirm;
            }

            // If no delta patch is happening as deltaPatchConfirm returns 0 (normal update), then do the base verification
            return await base.StartPackageVerification();
        }

        protected override async Task StartPackageInstallationInner()
        {
            // If the delta patch is performed, then return
            if (await StartDeltaPatch(_gameRepairManager, false)) return;

            // Run the base installation process
            await base.StartPackageInstallationInner();

            // Then start on processing hdifffiles list and deletefiles list
            await ApplyHdiffListPatch();
            ApplyDeleteFileAction();
        }
        #endregion

        #region Override Methods - GetInstallationPath
        protected override async ValueTask<bool> TryAddResourceVersionList(RegionResourceVersion asset, List<GameInstallPackage> packageList)
        {
            // Do action from base method first
            if (!await base.TryAddResourceVersionList(asset, packageList)) return false;

            // Initialize langID
            int langID;

            // Get available language names
            List<string> langStrings = EnumerateAudioLanguageString();
            GameInstallPackage package;

            // Skip if the asset doesn't have voice packs
            if (asset.voice_packs == null || asset.voice_packs.Count == 0)
            {
                LogWriteLine($"Asset for version: {asset.version} doesn't have voice packs! Skipping!", LogType.Warning, true);
                return true;
            }

            // If the game has already installed or in preload, then try get Voice language ID from registry
            if (_gameInstallationStatus == GameInstallStateEnum.InstalledHavePreload
             || _gameInstallationStatus == GameInstallStateEnum.NeedsUpdate)
            {
                // Try get the voice language ID from the registry
                langID = _gameVoiceLanguageID;
                // Since zh-CN (0) and zh-TW (1) have the same resource, then move the index forward
                langID += langID > 0 && asset.voice_packs.Count > 4 ? 1 : 0;
                package = new GameInstallPackage(asset.voice_packs[langID], _gamePath, asset.version) { LanguageID = langID, PackageType = GameInstallPackageType.Audio };
                packageList.Add(package);

                // Also try add another voice pack that already been installed
                TryAddOtherInstalledVoicePacks(asset.voice_packs, packageList, asset.version);
            }
            // Else, show dialog to choose the language ID to be installed
            else
            {
                langID = await Dialog_ChooseAudioLanguage(_parentUI, langStrings);
                // Since zh-CN (0) and zh-TW (1) have the same resource, then move the index forward
                langID += langID > 0 && asset.voice_packs.Count > 4 ? 1 : 0;

                package = new GameInstallPackage(asset.voice_packs[langID], _gamePath, asset.version) { LanguageID = langID, PackageType = GameInstallPackageType.Audio };
                packageList.Add(package);

                // Set the voice language ID to value given
                _gameVersionManager.GamePreset.SetVoiceLanguageID(langID);

                LogWriteLine($"Adding primary {package.LanguageName} audio package: {package.Name} to the list (Hash: {package.HashString})", LogType.Default, true);
            }

            return true;
        }


        private void TryAddOtherInstalledVoicePacks(IList<RegionResourceVersion> packs, List<GameInstallPackage> packageList, string assetVersion)
        {
            // If not found (null), then return
            if (_gameAudioLangListPath == null) return;

            // Start read the file
            using (StreamReader sw = new StreamReader(_gameAudioLangListPath))
            {
                while (!sw.EndOfStream)
                {
                    // Get the line
                    string langStr = sw.ReadLine();

                    // Get the key value pair for the respective value
                    KeyValuePair<string, int> langKey = langStr switch
                    {
                        "Chinese" => new KeyValuePair<string, int>("zh-cn", 0),
                        "English(US)" => new KeyValuePair<string, int>("en-us", 1),
                        "Japanese" => new KeyValuePair<string, int>("ja-jp", 2),
                        "Korean" => new KeyValuePair<string, int>("ko-kr", 3),
                        _ => throw new KeyNotFoundException($"Key: {langStr} is not supported!")
                    };

                    // Add the voice language to the list
                    TryAddOtherVoicePacksDictionary(langKey.Key, packs[langKey.Value], langKey.Value, packageList, assetVersion);
                }
            }
        }

        private void TryAddOtherVoicePacksDictionary(string key, RegionResourceVersion value, int langID, List<GameInstallPackage> packageList, string assetVersion)
        {
            // Try check if the package list matches the key
            if (!packageList.Any(x => x.LanguageName == key))
            {
                // Then add to the package list
                value.languageID = langID;
                GameInstallPackage package = new GameInstallPackage(value, _gamePath, assetVersion) { LanguageID = langID, PackageType = GameInstallPackageType.Audio };
                packageList.Add(package);

                LogWriteLine($"Adding additional {package.LanguageName} audio package: {package.Name} to the list (Hash: {package.HashString})", LogType.Default, true);
            }
        }


        private List<string> EnumerateAudioLanguageString()
        {
            return new List<string>
            {
                Lang._Misc.LangNameCN,
                Lang._Misc.LangNameENUS,
                Lang._Misc.LangNameJP,
                Lang._Misc.LangNameKR
            };
        }
        #endregion

        #region Override Methods - UninstallGame
        protected override UninstallGameProperty AssignUninstallFolders() => new UninstallGameProperty()
        {
            gameDataFolderName = "StarRail_Data",
            foldersToDelete = new string[] { "AntiCheatExpert" },
            filesToDelete = new string[] { "ACE-BASE.sys", "GameAssembly.dll", "pkg_version", "config.ini", "^StarRail.*", "^Unity.*" },
            foldersToKeepInData = new string[] { "ScreenShots" }
        };
        #endregion
    }
}
