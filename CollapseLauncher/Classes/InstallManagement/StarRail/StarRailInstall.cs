using CollapseLauncher.GameVersioning;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static CollapseLauncher.Dialogs.SimpleDialogs;
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
        private string _execName { get => Path.GetFileNameWithoutExtension(_gameVersionManager.GamePreset.GameExecutableName); }
        protected override string _gameDataPersistentPath { get => Path.Combine(_gamePath, $"{_execName}_Data", "Persistent"); }
        protected override string _gameAudioLangListPath
        {
            get
            {
                // If the persistent folder is not exist, then return null
                if (!Directory.Exists(_gameDataPersistentPath)) return null;

                // Set the file list path
                string audioRecordPath = Path.Combine(_gameDataPersistentPath, "AudioLaucherRecord.txt");

                // Check if the file exist. If not, return null
                if (!File.Exists(audioRecordPath)) return null;

                // If it exists, then return the path
                return audioRecordPath;
            }
        }
        protected override string _gameAudioLangListPathStatic { get => Path.Combine(_gameDataPersistentPath, "AudioLaucherRecord.txt"); }
        private StarRailRepair _gameRepairManager { get; set; }
        #endregion

        public StarRailInstall(UIElement parentUI, IGameVersionCheck GameVersionManager)
            : base(parentUI, GameVersionManager) { }

        #region Public Methods
        public override async ValueTask<int> StartPackageVerification(List<GameInstallPackage> gamePackage)
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
            return await base.StartPackageVerification(gamePackage);
        }

        protected override async Task StartPackageInstallationInner(List<GameInstallPackage> gamePackage = null, bool isOnlyInstallPackage = false, bool doNotDeleteZipExplicit = false)
        {
            // If the delta patch is performed, then return
            if (!isOnlyInstallPackage && await StartDeltaPatch(_gameRepairManager, false, true))
            {
                // Update the audio package list after delta patch has been initiated
                WriteAudioLangList(_gameDeltaPatchPreReqList);
                return;
            }

            // Run the base installation process
            await base.StartPackageInstallationInner(gamePackage, isOnlyInstallPackage, doNotDeleteZipExplicit);

            // Then start on processing hdifffiles list and deletefiles list
            await ApplyHdiffListPatch();
            ApplyDeleteFileAction();

            // Update the audio lang list if not in isOnlyInstallPackage mode
            if (!isOnlyInstallPackage)
                WriteAudioLangList(_assetIndex);
        }

        protected override string GetLanguageStringByID(int id) => id switch
        {
            0 => "Chinese",
            1 => "Chinese",
            2 => "English(US)",
            3 => "Japanese",
            4 => "Korean",
            _ => throw new KeyNotFoundException($"ID: {id} is not supported!")
        };
        #endregion

        #region Override Methods - GetInstallationPath
        protected override async ValueTask<bool> TryAddResourceVersionList(RegionResourceVersion asset, List<GameInstallPackage> packageList, bool isSkipMainPackage = false)
        {
            // Do action from base method first and add the main package
            if (!await base.TryAddResourceVersionList(asset, packageList, isSkipMainPackage)) return false;

            // Initialize langID
            int langID;

            // Get available language names
            List<string> langStrings = GetAudioLanguageStringList();
            GameInstallPackage package;

            // Skip if the asset doesn't have voice packs
            if (asset.voice_packs == null || asset.voice_packs.Count == 0)
            {
                LogWriteLine($"Asset for version: {asset.version} doesn't have voice packs! Skipping!", LogType.Warning, true);
                return true;
            }

            if (!_canSkipAudio)
            {
                // If the game has already installed or in preload, then try get Voice language ID from registry
                GameInstallStateEnum gameState = await _gameVersionManager.GetGameState();
                if (gameState == GameInstallStateEnum.InstalledHavePreload
                    || gameState == GameInstallStateEnum.NeedsUpdate)
                {
                    // Try get the voice language ID from the registry
                    langID = _gameVoiceLanguageID;
                    // Since zh-CN (0) and zh-TW (1) have the same resource, then move the index forward
                    if (langID == 1) langID = 0; // Force to use zh-CN if zh-TW is used
                    package = new GameInstallPackage(asset.voice_packs[langID], _gamePath, asset.version)
                        { LanguageID = langID, PackageType = GameInstallPackageType.Audio };
                    packageList.Add(package);

                    // Also try add another voice pack that already been installed
                    TryAddOtherInstalledVoicePacks(asset.voice_packs, packageList, asset.version);
                }
                // Else, show dialog to choose the language ID to be installed
                else
                {
                    (List<int> addedVO, int setAsDefaultVO) = await Dialog_ChooseAudioLanguageChoice(_parentUI, langStrings, 2);
                    if (addedVO == null || setAsDefaultVO < 0)
                        throw new TaskCanceledException();

                    for (int i = 0; i < addedVO.Count; i++)
                    {
                        langID = addedVO[i];
                        // Since zh-CN (0) and zh-TW (1) have the same resource, then move the index forward
                        langID += langID > 0 && asset.voice_packs.Count > 4 ? 1 : 0;

                        package = new GameInstallPackage(asset.voice_packs[langID], _gamePath, asset.version)
                        { LanguageID = langID, PackageType = GameInstallPackageType.Audio };
                        packageList.Add(package);

                        LogWriteLine($"Adding primary {package.LanguageName} audio package: {package.Name} to the list (Hash: {package.HashString})",
                                     LogType.Default, true);
                    }

                    // Set the voice language ID to value given
                    _gameVersionManager.GamePreset.SetVoiceLanguageID(setAsDefaultVO);
                }
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
                        "English(US)" => new KeyValuePair<string, int>("en-us", 2),
                        "Japanese" => new KeyValuePair<string, int>("ja-jp", 3),
                        "Korean" => new KeyValuePair<string, int>("ko-kr", 4),
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
        #endregion

        #region Override Methods - UninstallGame
        protected override UninstallGameProperty AssignUninstallFolders() => new()
        {
            gameDataFolderName = "StarRail_Data",
            foldersToDelete = new[] { "AntiCheatExpert" },
            filesToDelete = new[] { "ACE-BASE.sys", "GameAssembly.dll", "pkg_version", "config.ini", "^StarRail.*", "^Unity.*" },
            foldersToKeepInData = new[] { "ScreenShots" }
        };
        #endregion

        #region Override Methods - Sophon
        protected override string GetLangIdToSophonVOLangName(int id)
            => id switch
            {
                0 => "zh-cn",
                1 => "zh-cn",
                2 => "en-us",
                3 => "ja-jp",
                4 => "ko-kr",
                _ => throw new NotSupportedException($"This lang id: {id} is not supported")
            };
        #endregion
    }
}
