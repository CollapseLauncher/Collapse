using CollapseLauncher.GameVersioning;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using Microsoft.UI.Xaml;

namespace CollapseLauncher.InstallManager.Zenless
{
    internal class ZenlessInstall : InstallManagerBase<GameTypeZenlessVersion>
    {
        #region Override Properties
        #endregion

        public ZenlessInstall(UIElement parentUI, IGameVersionCheck GameVersionManager)
            : base(parentUI, GameVersionManager) { }

        #region Override Methods - UninstallGame
        protected override UninstallGameProperty AssignUninstallFolders() => new UninstallGameProperty()
        {
            gameDataFolderName = "ZenlessZoneZero_Data",
            foldersToDelete = new string[] { "APMCrashReporter" },
            filesToDelete = new string[] { "mhypbase.dll", "HoYoKProtect.sys", "GameAssembly.dll", "pkg_version", "config.ini", "^ZZZ.*", "^Unity.*" },
            foldersToKeepInData = new string[] { "ScreenShots" }
        };
        #endregion
    }
}