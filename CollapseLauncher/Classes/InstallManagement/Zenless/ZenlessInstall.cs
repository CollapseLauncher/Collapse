using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using Microsoft.UI.Xaml;

// ReSharper disable GrammarMistakeInComment
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace
// ReSharper disable ArrangeObjectCreationWhenTypeEvident
// ReSharper disable EmptyRegion
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor

namespace CollapseLauncher.InstallManager.Zenless
{
    internal class ZenlessInstall : InstallManagerBase
    {
        #region Override Properties

        #endregion

        public ZenlessInstall(UIElement parentUI, IGameVersionCheck GameVersionManager)
            : base(parentUI, GameVersionManager)
        {
        }

        #region Override Methods - UninstallGame

        protected override UninstallGameProperty AssignUninstallFolders()
        {
            return new UninstallGameProperty
            {
                gameDataFolderName = "ZenlessZoneZero_Data",
                foldersToDelete    = ["APMCrashReporter"],
                filesToDelete =
                [
                    "mhypbase.dll", "HoYoKProtect.sys", "GameAssembly.dll", "pkg_version", "config.ini", "^ZZZ.*",
                    "^Unity.*"
                ],
                foldersToKeepInData = ["ScreenShots"]
            };
        }

        #endregion
    }
}