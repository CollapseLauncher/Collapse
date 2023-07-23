using CollapseLauncher.GameVersioning;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Pages;
using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.SharpHDiffPatch;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static Hi3Helper.Locale;
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
        private bool _forceIgnoreDeltaPatch = false;
        #endregion

        public HonkaiInstall(UIElement parentUI)
            : base(parentUI)
        {

        }

        #region Public Methods
        public override async ValueTask<int> StartPackageVerification()
        {
            DeltaPatchProperty patchProperty = _gameDeltaPatchProperty;

            // Check if the game has delta patch and in NeedsUpdate status. If true, then
            // proceed with the delta patch update
            if (_canDeltaPatch && _gameInstallationStatus == GameInstallStateEnum.NeedsUpdate && !_forceIgnoreDeltaPatch)
            {
                switch (await Dialog_DeltaPatchFileDetected(_parentUI, patchProperty.SourceVer, patchProperty.TargetVer))
                {
                    // If no, then proceed with normal update (0)
                    // Also set ignore delta patch process if this method is re-called
                    case ContentDialogResult.Secondary:
                        _forceIgnoreDeltaPatch = true;
                        return 0;
                    // If cancel. then proceed to cancel (-1)
                    case ContentDialogResult.None:
                        return -1;
                }

                // Always reset the token
                ResetToken();

                // Initialize repair tool
                _gameRepairTool = new HonkaiRepair(_parentUI, true, patchProperty.SourceVer);
                try
                {
                    // Set the activity
                    _status.ActivityStatus = string.Format(Lang._GameRepairPage.Status2);
                    _status.IsIncludePerFileIndicator = false;
                    _status.IsProgressTotalIndetermined = true;
                    UpdateStatus();

                    // Start the check routine and get the state if download needed
                    _gameRepairTool.ProgressChanged += DeltaPatchCheckProgress;
                    bool isDownloadNeeded = await _gameRepairTool.StartCheckRoutine();
                    if (isDownloadNeeded)
                    {
                        _status.ActivityStatus = string.Format(Lang._GameRepairPage.Status8, "").Replace(": ", "");
                        _progressTotalSizeCurrent = 0;
                        _progressTotalCountCurrent = 1;
                        _progressTotalReadCurrent = 0;
                        UpdateStatus();

                        // If download needed, then start the repair (download) routine
                        await _gameRepairTool.StartRepairRoutine(true);
                    }
                }
                catch { throw; }
                finally
                {
                    // Unsubscribe the progress event
                    _gameRepairTool.ProgressChanged -= DeltaPatchCheckProgress;
                }

                // Then return 1 as continue to other steps
                return 1;
            }

            // If no delta patch is happening, then do the base verification
            return await base.StartPackageVerification();
        }

        public override async Task StartPackageInstallation()
        {
            if (_canDeltaPatch && _gameInstallationStatus == GameInstallStateEnum.NeedsUpdate && !_forceIgnoreDeltaPatch)
            {
                DeltaPatchProperty patchProperty = _gameDeltaPatchProperty;

                string previousPath = _gamePath;
                string ingredientPath = previousPath.TrimEnd('\\') + "_Ingredients";

                try
                {
                    List<FilePropertiesRemote> localAssetIndex = ((HonkaiRepair)_gameRepairTool).GetAssetIndex();
                    MoveFileToIngredientList(localAssetIndex, previousPath, ingredientPath);

                    // Get the sum of uncompressed size and
                    // Set progress count to beginning
                    _progressTotalSize = localAssetIndex.Sum(x => x.S);
                    _progressTotalSizeCurrent = 0;
                    _progressTotalCountCurrent = 1;
                    _status.IsIncludePerFileIndicator = false;
                    _status.IsProgressTotalIndetermined = true;
                    _status.ActivityStatus = Lang._Misc.ApplyingPatch;
                    UpdateStatus();
                    RestartStopwatch();

                    // Start the patching process
                    EventListener.PatchEvent += DeltaPatchCheckProgress;
                    await Task.Run(() =>
                    {
                        HDiffPatch patch = new HDiffPatch();
                        patch.Initialize(patchProperty.PatchPath);
                        patch.Patch(ingredientPath, previousPath, false, _token.Token);
                    });

                    // Remove ingredient folder
                    Directory.Delete(ingredientPath, true);

                    if (_canDeleteZip)
                    {
                        File.Delete(patchProperty.PatchPath);
                    }

                    // Then return
                    return;
                }
                catch (Exception ex)
                {
                    LogWriteLine($"Error has occurred while performing delta-patch!\r\n{ex}", LogType.Error, true);
                    throw;
                }
                finally
                {
                    EventListener.PatchEvent -= DeltaPatchCheckProgress;
                }
            }

            // If no delta patch is happening, then do the base installation
            await base.StartPackageInstallation();
        }

        public override async ValueTask<bool> TryShowFailedDeltaPatchState()
        {
            // Get the target and source path
            string GamePath = _gameVersionManager.GameDirPath;
            string GamePathIngredients = GamePath + "_Ingredients";
            // If path doesn't exist, then return false
            if (!Directory.Exists(GamePathIngredients)) return false;

            LogWriteLine($"Previous failed delta patch has been detected on Game {_gamePreset.ZoneFullname} ({GamePathIngredients})", LogType.Warning, true);
            // Show action dialog
            switch (await Dialog_PreviousDeltaPatchInstallFailed(_parentUI))
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

            LogWriteLine($"Previous failed game conversion has been detected on Game: {_gamePreset.ZoneFullname} ({GamePathIngredients})", LogType.Warning, true);
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

        #region Private Methods - StartPackageInstallation
        private void MoveFileToIngredientList(List<FilePropertiesRemote> assetIndex, string sourcePath, string targetPath)
        {
            string inputPath;
            string outputPath;
            string outputFolder;

            // Iterate the asset
            FileInfo fileInfo;
            foreach (FilePropertiesRemote index in assetIndex)
            {
                // Get the combined path from the asset name
                inputPath = Path.Combine(sourcePath, index.N);
                outputPath = Path.Combine(targetPath, index.N);
                outputFolder = Path.GetDirectoryName(outputPath);

                // Create directory of the output path if not exist
                if (!Directory.Exists(outputFolder))
                {
                    Directory.CreateDirectory(outputFolder);
                }

                // Sanity Check: If the file is still missing even after the process, then throw
                fileInfo = new FileInfo(inputPath);
                if (!fileInfo.Exists)
                {
                    throw new AccessViolationException($"File: {inputPath} isn't found!");
                }

                // Move the file to the target directory
                fileInfo.IsReadOnly = false;
                fileInfo.MoveTo(outputPath, true);
                LogWriteLine($"Moving from: {inputPath} to {outputPath}", LogType.Default, true);
            }

            // TODO: Make it automatic
            // Move block file to ingredient path
            string baseBlockPath = @"BH3_Data\StreamingAssets\Asb\pc\Blocks.xmf";
            inputPath = Path.Combine(sourcePath, baseBlockPath);
            outputPath = Path.Combine(targetPath, baseBlockPath);

            // Sanity Check: If the block manifest (xmf) is still missing even after the process, then throw
            fileInfo = new FileInfo(inputPath);
            if (!fileInfo.Exists)
            {
                throw new AccessViolationException($"Block file: {inputPath} isn't found!");
            }

            // Move the block manifest (xmf) to the target directory
            fileInfo.IsReadOnly = false;
            fileInfo.MoveTo(outputPath, true);
            LogWriteLine($"Moving from: {inputPath} to {outputPath}", LogType.Default, true);
        }
        #endregion

        #region Event Methods
        private async void DeltaPatchCheckProgress(object sender, PatchEvent e)
        {
            _progress.ProgressTotalPercentage = e.ProgressPercentage;

            _progress.ProgressTotalTimeLeft = e.TimeLeft;
            _progress.ProgressTotalSpeed = e.Speed;

            _progress.ProgressTotalSizeToDownload = e.TotalSizeToBePatched;
            _progress.ProgressTotalDownload = e.CurrentSizePatched;

            if (await CheckIfNeedRefreshStopwatch())
            {
                _status.IsProgressTotalIndetermined = false;
                UpdateProgressBase();
                UpdateStatus();
            }
        }

        private async void DeltaPatchCheckProgress(object sender, TotalPerfileProgress e)
        {
            _progress.ProgressTotalPercentage = e.ProgressTotalPercentage == 0 ? e.ProgressPerFilePercentage : e.ProgressTotalPercentage;

            _progress.ProgressTotalTimeLeft = e.ProgressTotalTimeLeft;
            _progress.ProgressTotalSpeed = e.ProgressTotalSpeed;

            _progress.ProgressTotalSizeToDownload = e.ProgressTotalSizeToDownload;
            _progress.ProgressTotalDownload = e.ProgressTotalDownload;

            if (await CheckIfNeedRefreshStopwatch())
            {
                _status.IsProgressTotalIndetermined = false;
                UpdateProgressBase();
                UpdateStatus();
            }
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
                string IngredientPath = Directory.EnumerateDirectories(ParentPath, $"{PageStatics._GameVersion.GamePreset.GameDirectoryName}*_ConvertedTo-*_Ingredients", SearchOption.TopDirectoryOnly)
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
    }
}
