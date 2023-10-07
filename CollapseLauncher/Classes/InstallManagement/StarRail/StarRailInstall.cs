using CollapseLauncher.GameVersioning;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
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
        #endregion

        public StarRailInstall(UIElement parentUI, IGameVersionCheck GameVersionManager)
            : base(parentUI, GameVersionManager)
        {

        }

        #region Public Methods
        protected override async Task StartPackageInstallationInner()
        {
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

            // If the game is not installed or broken, then try set Voice language ID to registry
            if (_gameInstallationStatus == GameInstallStateEnum.NotInstalled
                  || _gameInstallationStatus == GameInstallStateEnum.GameBroken)
            {
                langID = await Dialog_ChooseAudioLanguage(_parentUI, langStrings);

                // Set the voice language ID to value given
                _gameVersionManager.GamePreset.SetVoiceLanguageID(langID);
            }

            LogWriteLine($"Setting audio ID to: {_gameVersionManager.GamePreset.GetStarRailVoiceLanguageFullNameByID(_gameVoiceLanguageID)}", LogType.Default, true);
            return true;
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
