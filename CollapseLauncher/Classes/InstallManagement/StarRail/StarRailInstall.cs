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
        protected override int _gameVoiceLanguageID { get => _gamePreset.GetVoiceLanguageID(); }
        #endregion

        public StarRailInstall(UIElement parentUI)
            : base(parentUI)
        {

        }

        #region Public Methods
        public override async Task StartPackageInstallation()
        {
            // Run the base installation process
            await base.StartPackageInstallation();

            // Then start on processing hdifffiles list and deletefiles list
            await ApplyHdiffListPatch();
            ApplyDeleteFileAction();
        }
        #endregion

        #region Override Methods - GetInstallationPath
        protected override async Task TryAddResourceVersionList(RegionResourceVersion asset, List<GameInstallPackage> packageList)
        {
            // Do action from base method first
            await base.TryAddResourceVersionList(asset, packageList);

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
                _gamePreset.SetVoiceLanguageID(langID);
            }

            LogWriteLine($"Setting audio ID to: {_gamePreset.GetStarRailVoiceLanguageFullNameByID(_gameVoiceLanguageID)}", LogType.Default, true);
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
    }
}
