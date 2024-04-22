using CollapseLauncher.GameVersioning;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CollapseLauncher.InstallManager.Zenless
{
    internal class ZenlessInstall : InstallManagerBase<GameTypeZenlessVersion>, IGameInstallManager
    {
        #region Override Properties
        #endregion

        #region Properties
        private string _execName { get; set; }
        #endregion

        public ZenlessInstall(UIElement parentUI, IGameVersionCheck GameVersionManager)
            : base(parentUI, GameVersionManager)
        {
            _execName = Path.GetFileNameWithoutExtension(GameVersionManager.GamePreset.GameExecutableName);
        }

        #region Public Methods
        public override async ValueTask<int> StartPackageVerification(List<GameInstallPackage> gamePackage)
        {
            IsRunning = true;

            /*
            // Get the delta patch confirmation if the property is not null
            if (_gameDeltaPatchProperty != null)
            {
                // If the confirm is 1 (verified) or -1 (cancelled), then return the code
                int deltaPatchConfirm = await ConfirmDeltaPatchDialog(_gameDeltaPatchProperty, _gameRepairManager = new StarRailRepair(_parentUI, _gameVersionManager, true, _gameDeltaPatchProperty.SourceVer));
                if (deltaPatchConfirm == -1 || deltaPatchConfirm == 1) return deltaPatchConfirm;
            }
            */

            // Call base method for now
            return await base.StartPackageVerification(gamePackage);
        }

        protected override async Task StartPackageInstallationInner(List<GameInstallPackage> gamePackage = null, bool isOnlyInstallPackage = false, bool doNotDeleteZipExplicit = false)
        {
            /*
            // If the delta patch is performed, then return
            if (!isOnlyInstallPackage && await StartDeltaPatch(_gameRepairManager, false))
            {
                // Update the audio package list after delta patch has been initiated
                WriteAudioLangList(_gameDeltaPatchPreReqList);
                return;
            }
            */

            // Call base method for now
            await base.StartPackageInstallationInner(gamePackage, isOnlyInstallPackage, doNotDeleteZipExplicit);

            /*
            // Then start on processing hdifffiles list and deletefiles list
            await ApplyHdiffListPatch();
            ApplyDeleteFileAction();

            // Update the audio lang list if not in isOnlyInstallPackage mode
            if (!isOnlyInstallPackage)
                WriteAudioLangList(_assetIndex);
            */
        }
        #endregion

        #region Override Methods - UninstallGame
        protected override UninstallGameProperty AssignUninstallFolders() => new UninstallGameProperty()
        {
            gameDataFolderName = "ZZZ_Data",
            foldersToDelete = new string[] { "APMCrashReporter" },
            filesToDelete = new string[] { "mhypbase.dll", "HoYoKProtect.sys", "GameAssembly.dll", "pkg_version", "config.ini", "^ZZZ.*", "^Unity.*" },
            foldersToKeepInData = new string[] { "ScreenShots" }
        };
        #endregion
    }
}
