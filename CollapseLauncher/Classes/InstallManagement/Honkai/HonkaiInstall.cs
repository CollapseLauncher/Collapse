using CollapseLauncher.GameVersioning;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Pages;
using Hi3Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static Hi3Helper.Logger;

namespace CollapseLauncher.InstallManager.Honkai
{
    internal class HonkaiInstall : InstallManagerBase<GameTypeHonkaiVersion>, IGameInstallManager
    {
        #region Override Properties
        protected override bool _canDeltaPatch { get => _gameVersionManager.IsGameHasDeltaPatch(); }
        protected override DeltaPatchProperty _gameDeltaPatchProperty { get => _gameVersionManager.GetDeltaPatchInfo(); }
        #endregion

        #region Private Properties
        private HonkaiCache _gameCacheManager { get; set; }
        private HonkaiRepair _gameRepairManager { get; set; }
        #endregion

        public HonkaiInstall(UIElement parentUI, IGameVersionCheck GameVersionManager, ICache GameCacheManager, IGameSettings GameSettings)
            : base(parentUI, GameVersionManager)
        {
            _gameSettings = GameSettings;
            _gameCacheManager = GameCacheManager as HonkaiCache;
        }

        #region Public Methods
        public override async ValueTask<int> StartPackageVerification(List<GameInstallPackage> gamePackage)
        {
            IsRunning = true;

            // Get the delta patch confirmation if the property is not null
            if (_gameDeltaPatchProperty != null)
            {
                // If the confirm is 1 (verified) or -1 (cancelled), then return the code
                int deltaPatchConfirm = await ConfirmDeltaPatchDialog(_gameDeltaPatchProperty, _gameRepairManager = new HonkaiRepair(_parentUI, _gameVersionManager, _gameCacheManager, _gameSettings, true, _gameDeltaPatchProperty.SourceVer));
                if (deltaPatchConfirm == -1 || deltaPatchConfirm == 1) return deltaPatchConfirm;
            }

            // If no delta patch is happening as deltaPatchConfirm returns 0 (normal update), then do the base verification
            return await base.StartPackageVerification(gamePackage);
        }

        protected override async Task StartPackageInstallationInner(List<GameInstallPackage> gamePackage = null, bool isOnlyInstallPackage = false)
        {
            // If the delta patch is performed, then return
            if (!isOnlyInstallPackage && await StartDeltaPatch(_gameRepairManager, true)) return;

            // If no delta patch is happening, then do the base installation
            await base.StartPackageInstallationInner(gamePackage);
        }

        public override async ValueTask<bool> TryShowFailedGameConversionState()
        {
            // Get the target and source path
            string GamePath = _gameVersionManager.GameDirPath;
            string GamePathIngredients = GetFailedGameConversionFolder(GamePath);
            // If path doesn't exist or null, then return false
            if (GamePathIngredients is null || !Directory.Exists(GamePathIngredients)) return false;

            // Get the size of entire folder and check if it's below 1 MB, then return false
            long FileSize = Directory.EnumerateFiles(GamePathIngredients).Sum(x => new FileInfo(x).Length);
            if (FileSize < 1 << 20) return false;

            LogWriteLine($"Previous failed game conversion has been detected on Game: {_gameVersionManager.GamePreset.ZoneFullname} ({GamePathIngredients})", LogType.Warning, true);
            // Show action dialog
            switch (await Dialog_PreviousGameConversionFailed(_parentUI))
            {
                // If primary button clicked, then move the folder and get back to HomePage
                case ContentDialogResult.Primary:
                    MoveFolderContent(GamePathIngredients, GamePath);
                    MainFrameChanger.ChangeMainFrame(typeof(HomePage));
                    break;
            }

            // Then reinitialize the version manager
            _gameVersionManager.Reinitialize();
            return true;
        }

        public override void Flush()
        {
            // Flush the base
            base.Flush();

            // Reset _forceIgnoreDeltaPatch state to false
            _forceIgnoreDeltaPatch = false;
        }
        #endregion

        #region Private Methods - Utilities
        private string GetFailedGameConversionFolder(string basepath)
        {
            try
            {
                // Step back once from the game directory
                string ParentPath = Path.GetDirectoryName(basepath);
                // Get the ingredient path
                string IngredientPath = Directory.EnumerateDirectories(ParentPath, $"{_gameVersionManager.GamePreset.GameDirectoryName}*_ConvertedTo-*_Ingredients", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();
                // If the path is not null, then return
                if (IngredientPath is not null) return IngredientPath;
            }
#if DEBUG
            catch (DirectoryNotFoundException)
            {
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex, ErrorType.Unhandled);
#else
            catch
            {
#endif
            }

            // If not, then return null (not found)
            return null;
        }
        #endregion

        #region Override Methods - UninstallGame
        protected override UninstallGameProperty AssignUninstallFolders() => new UninstallGameProperty()
        {
            gameDataFolderName = "BH3_Data",
            foldersToDelete = new string[] { "BH3_Data", "AntiCheatExpert" },
            filesToDelete = new string[] { "ACE-BASE.sys", "bugtrace.dll", "pkg_version", "UnityPlayer.dll", "config.ini" },
            foldersToKeepInData = Array.Empty<string>()
        };
        #endregion
    }
}
