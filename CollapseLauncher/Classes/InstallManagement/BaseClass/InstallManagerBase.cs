using CollapseLauncher.FileDialogCOM;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Hi3Helper.SharpHDiffPatch;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using CoreCombinedStream = Hi3Helper.EncTool.CombinedStream;

namespace CollapseLauncher.InstallManager.Base
{
    internal abstract class InstallManagerBase<T> : ProgressBase<GameInstallPackageType, GameInstallPackage> where T : IGameVersionCheck
    {
        #region Internal Struct
        protected struct UninstallGameProperty
        {
            public string gameDataFolderName;
            public string[] filesToDelete;
            public string[] foldersToDelete;
            public string[] foldersToKeepInData;
        }
        #endregion

        #region Properties
        protected readonly string _gamePersistentFolderBasePath;
        protected readonly string _gameStreamingAssetsFolderBasePath;
        protected RegionResourceGame _gameRegion { get => _gameVersionManager.GameAPIProp.data; }
        protected GameVersion _gameLatestVersion { get => _gameVersionManager.GetGameVersionAPI(); }
        protected GameVersion? _gameLatestPreloadVersion { get => _gameVersionManager.GetGameVersionAPIPreload(); }
        protected GameVersion? _gameInstalledVersion { get => _gameVersionManager.GetGameExistingVersion(); }
        protected GameInstallStateEnum _gameInstallationStatus { get => _gameVersionManager.GetGameState(); }
        // TODO: Override if the game was supposed to have voice packs (For example: Genshin)
        protected virtual int _gameVoiceLanguageID { get => int.MinValue; }
        protected IRepair _gameRepairTool { get; set; }
        protected Http _httpClient { get; private set; }
        protected bool _canDeleteHdiffReference { get => !File.Exists(Path.Combine(_gamePath, "@NoDeleteHdiffReference")); }
        protected bool _canDeleteZip { get => !File.Exists(Path.Combine(_gamePath, "@NoDeleteZip")); }
        protected bool _canSkipVerif { get => File.Exists(Path.Combine(_gamePath, "@NoVerification")); }
        protected bool _canSkipExtract { get => File.Exists(Path.Combine(_gamePath, "@NoExtraction")); }
        protected bool _canMergeDownloadChunks { get => LauncherConfig.GetAppConfigValue("UseDownloadChunksMerging").ToBool(); }
        protected virtual bool _canDeltaPatch { get => false; }
        protected virtual DeltaPatchProperty _gameDeltaPatchProperty { get => null; }
        protected bool _forceIgnoreDeltaPatch = false;

        private long _totalLastSizeCurrent = 0;
        #endregion

        #region Public Properties
        public bool IsRunning { get; protected set; }
        public event EventHandler FlushingTrigger;
        #endregion

        public InstallManagerBase(UIElement parentUI, IGameVersionCheck GameVersionManager)
            : base(parentUI, GameVersionManager, "", "", null)
        {
            _httpClient = new Http(true);
            _gameVersionManager = GameVersionManager;
            _gamePersistentFolderBasePath = $"{Path.GetFileNameWithoutExtension(_gameVersionManager.GamePreset.GameExecutableName)}_Data\\Persistent";
            _gameStreamingAssetsFolderBasePath = $"{Path.GetFileNameWithoutExtension(_gameVersionManager.GamePreset.GameExecutableName)}_Data\\StreamingAssets";
            UpdateCompletenessStatus(CompletenessStatus.Idle);
        }

        /*
        ~InstallManagerBase()
        {
#if DEBUG
            LogWriteLine($"[~InstallManagerBase()] Deconstructor getting called in {_gameVersionManager}", LogType.Warning, true);
#endif
            Dispose();
        }
        */

        protected void ResetToken() => _token = new CancellationTokenSource();

        public void Dispose()
        {
            _httpClient?.Dispose();
            _gameRepairTool?.Dispose();
            _token?.Cancel();
            IsRunning = false;
            Flush();
        }

        public virtual void Flush()
        {
            UpdateCompletenessStatus(CompletenessStatus.Idle);
            _gameRepairTool?.Dispose();
            _assetIndex.Clear();
            FlushingTrigger?.Invoke(this, EventArgs.Empty);
        }

        #region Public Methods
        protected virtual async ValueTask<int> ConfirmDeltaPatchDialog(DeltaPatchProperty patchProperty, IRepair gameRepair)
        {
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
                _gameRepairTool = gameRepair;
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
                catch
                {
                    IsRunning = false;
                    throw;
                }
                finally
                {
                    // Unsubscribe the progress event
                    _gameRepairTool.ProgressChanged -= DeltaPatchCheckProgress;
                }

                // Then return 1 as continue to other steps
                return 1;
            }

            return 0;
        }

        protected virtual async ValueTask<bool> StartDeltaPatch(IRepairAssetIndex repairGame, bool isHonkai)
        {
            if (_canDeltaPatch && _gameInstallationStatus == GameInstallStateEnum.NeedsUpdate && !_forceIgnoreDeltaPatch)
            {
                DeltaPatchProperty patchProperty = _gameDeltaPatchProperty;

                string previousPath = _gamePath;
                string ingredientPath = previousPath.TrimEnd('\\') + "_Ingredients";

                try
                {
                    List<FilePropertiesRemote> localAssetIndex = repairGame.GetAssetIndex();
                    MoveFileToIngredientList(localAssetIndex, previousPath, ingredientPath, isHonkai);

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
                    HDiffPatch.LogVerbosity = Verbosity.Verbose;
                    EventListener.PatchEvent += DeltaPatchCheckProgress;
                    EventListener.LoggerEvent += DeltaPatchCheckLogEvent;
                    await Task.Run(() =>
                    {
                        HDiffPatch patch = new HDiffPatch();
                        patch.Initialize(patchProperty.PatchPath);
                        patch.Patch(ingredientPath, previousPath, true, _token.Token, false, true);
                    });

                    // Remove ingredient folder
                    Directory.Delete(ingredientPath, true);

                    if (_canDeleteZip)
                    {
                        File.Delete(patchProperty.PatchPath);
                    }

                    // Then return
                    return true;
                }
                catch (Exception ex)
                {
                    LogWriteLine($"Error has occurred while performing delta-patch!\r\n{ex}", LogType.Error, true);
                    throw;
                }
                finally
                {
                    EventListener.PatchEvent -= DeltaPatchCheckProgress;
                    EventListener.LoggerEvent -= DeltaPatchCheckLogEvent;
                }
            }

            // Return false to indicate that there's no delta patch running
            return false;
        }

        // Bool:  0      -> Indicates that the action is completed and no need to step further
        //        1      -> Continue to the next step
        //       -1      -> Cancel the operation
        public virtual async ValueTask<int> GetInstallationPath()
        {
            // Try get the existing Steam path. If it return true, then continue to the next step
            int result = await CheckExistingSteamInstallation();
            if (result > 1) return await CheckExistingOrAskFolderDialog();
            if (result <= 0) return result;

            // Try get the existing Better Hi3 Launcher path. If it return true, then continue to the next step
            result = await CheckExistingBHI3LInstallation();
            if (result > 1) return await CheckExistingOrAskFolderDialog();
            if (result <= 0) return result;

            // Try get the existing Official Launcher path. If it return true, then continue to the next step
            result = await CheckExistingOfficialInstallation();
            if (result > 1) return await CheckExistingOrAskFolderDialog();
            if (result <= 0) return result;

            return await CheckExistingOrAskFolderDialog();
        }

        public virtual async Task StartPackageDownload(bool skipDialog)
        {
            UpdateCompletenessStatus(CompletenessStatus.Running);
            ResetToken();

            // Get the game state and run the action for each of them
            GameInstallStateEnum gameState = _gameVersionManager.GetGameState();
            LogWriteLine($"Gathering packages information for installation (State: {gameState})...", LogType.Default, true);

            try
            {
                switch (gameState)
                {
                    case GameInstallStateEnum.NotInstalled:
                    case GameInstallStateEnum.GameBroken:
                    case GameInstallStateEnum.NeedsUpdate:
                        await GetLatestPackageList(_assetIndex, gameState, false);
                        break;
                    case GameInstallStateEnum.InstalledHavePreload:
                        await GetLatestPackageList(_assetIndex, gameState, true);
                        break;
                }

                // Set the progress bar to indetermined
                _status.IsIncludePerFileIndicator = _assetIndex.Sum(x => x.Segments != null ? x.Segments.Count : 1) > 1;
                _status.IsProgressPerFileIndetermined = true;
                _status.IsProgressTotalIndetermined = true;
                UpdateStatus();

                // Start getting the size of the packages
                await GetPackagesRemoteSize(_assetIndex, _token.Token);

                // Get the remote total size and current total size
                _progressTotalSize = _assetIndex.Sum(x => x.Size);
                _progressTotalSizeCurrent = GetExistingDownloadPackageSize(_assetIndex);

                // Sanitize Check: Check for the free space of the drive and show the dialog if necessary
                await CheckDriveFreeSpace(_parentUI, _assetIndex);

                // Sanitize Check: Show dialog for resuming/reset the existing download
                if (!skipDialog)
                {
                    await CheckExistingDownloadAsync(_parentUI, _assetIndex);
                }

                // Start downloading process
                await InvokePackageDownloadRoutine(_assetIndex, _token.Token);

                UpdateCompletenessStatus(CompletenessStatus.Completed);
            }
            catch
            {
                UpdateCompletenessStatus(CompletenessStatus.Cancelled);
                throw;
            }
        }

        // Bool:  0      -> Indicates that one of the package is failing and need to redownload
        //                  or the verification can't be started because the download never being performed first
        //        1      -> Continue to the next step (all passes)
        //       -1      -> Cancel the operation
        public virtual async ValueTask<int> StartPackageVerification()
        {
            try
            {
                UpdateCompletenessStatus(CompletenessStatus.Running);

                // Get the total asset count
                int assetCount = _assetIndex.Sum(x => x.Segments != null ? x.Segments.Count : 1);

                // If the assetIndex is empty, then skip and return 0
                if (assetCount == 0)
                {
                    return 0;
                }

                // If _canSkipVerif flag is true, then return 1 (skip) the verification;
                if (_canSkipVerif) return 1;

                // Set progress count to beginning
                _progressTotalSize = _assetIndex.Sum(x => x.Size);
                _progressTotalSizeCurrent = 0;
                _progressTotalCountCurrent = 1;
                _progressTotalCount = assetCount;
                _status.IsIncludePerFileIndicator = assetCount > 1;
                RestartStopwatch();

                // Set progress bar to not indetermined
                _status.IsProgressPerFileIndetermined = false;
                _status.IsProgressTotalIndetermined = false;

                // Iterate the asset
                foreach (GameInstallPackage asset in _assetIndex)
                {
                    int returnCode = 0;

                    // Iterate if the package has segment
                    if (asset.Segments != null)
                    {
                        for (int i = 0; i < asset.Segments.Count; i++)
                        {
                            // Run the package verification routine
                            if ((returnCode = await RunPackageVerificationRoutine(asset.Segments[i], _token.Token)) < 1)
                            {
                                return returnCode;
                            }
                        }
                        continue;
                    }

                    // Run the package verification routine as a single package
                    if ((returnCode = await RunPackageVerificationRoutine(asset, _token.Token)) < 1)
                    {
                        return returnCode;
                    }
                }
                UpdateCompletenessStatus(CompletenessStatus.Completed);
            }
            catch
            {
                UpdateCompletenessStatus(CompletenessStatus.Cancelled);
                throw;
            }

            return 1;
        }

        private async ValueTask<int> RunPackageVerificationRoutine(GameInstallPackage asset, CancellationToken token)
        {
            // Reset per size counter
            _progressPerFileSizeCurrent = 0;

            byte[] hashLocal;
            using (Stream fs = asset.GetReadStream(_downloadThreadCount))
            {
                // Reset the per file size
                _progressPerFileSize = fs.Length;
                _status.ActivityStatus = string.Format("{0}: {1}", Lang._Misc.Verifying, string.Format(Lang._Misc.PerFromTo, _progressTotalCountCurrent, _progressTotalCount));
                UpdateStatus();

                // Run the check and assign to hashLocal variable
                hashLocal = await Task.Run(() => base.CheckHash(fs, MD5.Create(), token, true));
            }

            // Check for the hash differences. If found, then show dialog to delete or cancel the process
            if (!IsArrayMatch(hashLocal, asset.Hash))
            {
                switch (await Dialog_GameInstallationFileCorrupt(_parentUI, asset.HashString, HexTool.BytesToHexUnsafe(hashLocal)))
                {
                    case ContentDialogResult.Primary:
                        _progressTotalSizeCurrent -= asset.Size;
                        DeleteDownloadedFile(asset.PathOutput, _downloadThreadCount);
                        return 0;
                    case ContentDialogResult.None:
                        return -1;
                }
            }

            // Increment the total current count
            _progressTotalCountCurrent++;

            // Return 1 as OK
            return 1;
        }

        private long GetAssetIndexTotalUncompressSize(List<GameInstallPackage> assetIndex)
        {
            long returnSize = 0;

            foreach (GameInstallPackage asset in assetIndex)
            {
                using (Stream stream = GetSingleOrSegmentedDownloadStream(asset))
                using (ArchiveFile archiveFile = new ArchiveFile(stream, null, @"Lib\7z.dll"))
                {
                    returnSize += archiveFile.Entries.Sum(x => (long)x.Size);
                }
            }

            return returnSize;
        }

        private Stream GetSingleOrSegmentedDownloadStream(GameInstallPackage asset)
        {
            return asset.Segments != null && asset.Segments.Count != 0 ?
                new CoreCombinedStream(asset.Segments.Select(x => x.GetReadStream(_downloadThreadCount)).ToArray()) :
                asset.GetReadStream(_downloadThreadCount);
        }

        private void DeleteSingleOrSegmentedDownloadStream(GameInstallPackage asset)
        {
            if (asset.Segments != null && asset.Segments.Count != 0)
            {
                asset.Segments.ForEach(x => x.DeleteFile(_downloadThreadCount));
                return;
            }
            asset.DeleteFile(_downloadThreadCount);
        }

        public async Task StartPackageInstallation()
        {
            try
            {
                UpdateCompletenessStatus(CompletenessStatus.Running);

                await StartPackageInstallationInner();

                UpdateCompletenessStatus(CompletenessStatus.Completed);
            }
            catch
            {
                UpdateCompletenessStatus(CompletenessStatus.Cancelled);
                throw;
            }
        }

        protected virtual async Task StartPackageInstallationInner()
        {
            // Get the sum of uncompressed size and
            // Set progress count to beginning
            _progressTotalSize = GetAssetIndexTotalUncompressSize(_assetIndex);

            // Start Async Thread
            // Since the ArchiveFile (especially with the callbacks) can't run under
            // different thread, so the async call will be called at the start
            await Task.Run(() =>
            {
                // Sanity Check: Check if the package list is empty or not
                if (_assetIndex.Count == 0) throw new InvalidOperationException("Package list is empty. Make sure you have ran StartPackageDownload() first.");

                // If _canSkipExtract flag is true, then return (skip) the extraction
                if (_canSkipExtract) return;

                _progressTotalSizeCurrent = 0;
                _progressTotalCountCurrent = 1;
                _progressTotalCount = _assetIndex.Count;
                _status.IsIncludePerFileIndicator = _assetIndex.Count > 1;
                RestartStopwatch();

                // Reset the last size counter
                _totalLastSizeCurrent = 0;

                // Try unassign read-only and redundant diff files
                TryUnassignReadOnlyFiles();
                TryRemoveRedundantHDiffList();

                foreach (GameInstallPackage asset in _assetIndex)
                {
                    // Update the status
                    _status.ActivityStatus = string.Format("{0}: {1}", Lang._Misc.Extracting, string.Format(Lang._Misc.PerFromTo, _progressTotalCountCurrent, _progressTotalCount));
                    _status.IsProgressPerFileIndetermined = false;
                    _status.IsProgressTotalIndetermined = false;
                    UpdateStatus();

                    // Load the zip
                    Stream stream = GetSingleOrSegmentedDownloadStream(asset);
                    ArchiveFile archiveFile = new ArchiveFile(stream, null, @"Lib\7z.dll");

                    try
                    {
                        // Start extraction
                        archiveFile.ExtractProgress += ZipProgressAdapter;
                        archiveFile.Extract(e => Path.Combine(_gamePath, e.FileName), _token.Token);

                        // Get the information about diff and delete list file
                        FileInfo hdiffList = new FileInfo(Path.Combine(_gamePath, "hdifffiles.txt"));
                        FileInfo deleteList = new FileInfo(Path.Combine(_gamePath, "deletefiles.txt"));

                        // If diff list file exist, then rename the file
                        if (hdiffList.Exists)
                        {
                            hdiffList.MoveTo(Path.Combine(_gamePath, $"hdifffiles_{Path.GetFileNameWithoutExtension(asset.PathOutput)}.txt"), true);
                        }

                        // If the delete zip file exist, then rename the file
                        if (deleteList.Exists)
                        {
                            deleteList.MoveTo(Path.Combine(_gamePath, $"deletefiles_{Path.GetFileNameWithoutExtension(asset.PathOutput)}.txt"), true);
                        }

                        // Make sure that the Stream is getting disposed first
                        stream?.Dispose();

                        // If the _canDeleteZip flag is true, then delete the zip
                        if (_canDeleteZip)
                        {
                            DeleteSingleOrSegmentedDownloadStream(asset);
                        }
                    }
                    catch (Exception) { throw; }
                    finally
                    {
                        archiveFile.ExtractProgress -= ZipProgressAdapter;
                        stream?.Dispose();
                        archiveFile?.Dispose();
                    }

                    _progressTotalCountCurrent++;
                }
            });
        }

        public virtual async Task StartPostInstallVerification()
        {
        }

        public virtual void ApplyGameConfig(bool forceUpdateToLatest = false)
        {
            _gameVersionManager.UpdateGamePath(_gamePath);
            if (forceUpdateToLatest)
            {
                _gameVersionManager.UpdateGameVersionToLatest(true);
            }
            _gameVersionManager.Reinitialize();
        }


        public virtual async ValueTask<bool> IsPreloadCompleted(CancellationToken token)
        {
            // Get the latest package list and await
            await GetLatestPackageList(_assetIndex, GameInstallStateEnum.InstalledHavePreload, true);
            // Get the total size of the packages
            await GetPackagesRemoteSize(_assetIndex, token);
            long totalPackageSize = _assetIndex.Sum(x => x.Size);

            // Get the sum of the total size of the single or segmented packages
            return _assetIndex.Sum(asset =>
            {
                // Check if the package is segmented
                if (asset.Segments != null && asset.Segments.Count != 0)
                {
                    // Get the sum of the total size/length for each of its streams
                    return asset.Segments.Sum(segment =>
                    {
                        // Check if the read stream exist
                        if (segment.IsReadStreamExist(_downloadThreadCount))
                        {
                            // Return the size/length of the chunk stream
                            return segment.GetStreamLength(_downloadThreadCount);
                        }
                        // If not, then return 0
                        return 0;
                    });
                }

                // If segment is none, check if the single stream exist
                if (asset.IsReadStreamExist(_downloadThreadCount))
                {
                    // If yes, then return the size of the single stream
                    return asset.GetStreamLength(_downloadThreadCount);
                }

                // If neither of both exist, then return 0
                return 0;
            }) == totalPackageSize; // Then compare if the total package size is equal

            // Note:
            // x.GetReadStream() will check if the single package/zip exist.
            // So checking the fully downloaded single package is unnecessary.
        }

        public async Task MoveGameLocation()
        {

        }

        public async ValueTask<bool> UninstallGame()
        {
            // Get the Game folder
            string GameFolder = ConverterTool.NormalizePath(_gamePath);

            // Check if the dialog result is Okay (Primary). If not, then return false
            ContentDialogResult DialogResult = await Dialog_UninstallGame(_parentUI, GameFolder, _gameVersionManager.GamePreset.ZoneFullname);
            if (DialogResult != ContentDialogResult.Primary) return false;

            try
            {
#nullable enable
                // Assign UninstallProperty from each overrides
                UninstallGameProperty UninstallProperty = AssignUninstallFolders();

                //Preparing paths
                var _DataFolderFullPath = Path.Combine(GameFolder, UninstallProperty.gameDataFolderName);

                string[]? foldersToKeepInDataFullPath = null;
                if (UninstallProperty.foldersToKeepInData != null && UninstallProperty.foldersToKeepInData.Length != 0)
                {
                    foldersToKeepInDataFullPath = new string[UninstallProperty.foldersToKeepInData.Length];
                    for (int i = 0; i < UninstallProperty.foldersToKeepInData.Length; i++)
                    {
                        foldersToKeepInDataFullPath[i] = Path.Combine(_DataFolderFullPath, UninstallProperty.foldersToKeepInData[i]);
                    }
                }
                else foldersToKeepInDataFullPath = Array.Empty<string>();

#pragma warning disable CS8604 // Possible null reference argument.
                LogWriteLine($"Uninstalling game: {_gameVersionManager.GameType} - region: {_gameVersionManager.GamePreset.ZoneName}\r\n" +
                    $"  GameFolder          : {GameFolder}\r\n" +
                    $"  gameDataFolderName  : {UninstallProperty.gameDataFolderName}\r\n" +
                    $"  foldersToDelete     : {string.Join(", ", UninstallProperty.foldersToDelete)}\r\n" +
                    $"  filesToDelete       : {string.Join(", ", UninstallProperty.filesToDelete)}\r\n" +
                    $"  foldersToKeepInData : {string.Join(", ", UninstallProperty.foldersToKeepInData)}\r\n" +
                    $"  _Data folder path   : {_DataFolderFullPath}\r\n" +
                    $"  Excluded full paths : {string.Join(", ", foldersToKeepInDataFullPath)}", LogType.Warning, true);
#pragma warning restore CS8604 // Possible null reference argument.

                // Cleanup Game_Data folder while keeping whatever specified in foldersToKeepInData
                foreach (string folderGameData in Directory.EnumerateFileSystemEntries(_DataFolderFullPath))
                {
                    try
                    {
                        if (UninstallProperty.foldersToKeepInData != null && UninstallProperty.foldersToKeepInData.Length != 0 && !foldersToKeepInDataFullPath.Contains(folderGameData)) // Skip this entire process if foldersToKeepInData is null
                        {
                            // Delete directories inside gameDataFolderName that is not included in foldersToKeepInData
                            if (File.GetAttributes(folderGameData).HasFlag(FileAttributes.Directory))
                            {
                                TryUnassignReadOnlyFiles(folderGameData);
                                Directory.Delete(folderGameData, true);
                                LogWriteLine($"Deleted folder: {folderGameData}", LogType.Default, true);
                            }
                            // Delete files inside gameDataFolderName that is not included in foldersToKeepInData
                            else
                            {
                                TryDeleteReadOnlyFile(folderGameData);
                                LogWriteLine($"Deleted file: {folderGameData}", LogType.Default, true);
                            }
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWriteLine($"An error occurred while deleting object {folderGameData}\r\n{ex}", LogType.Error, true);
                    }
                }
                // Check if _DataFolderPath folder empty after cleaning up 
                if (!Directory.EnumerateFileSystemEntries(_DataFolderFullPath).Any())
                {
                    Directory.Delete(_DataFolderFullPath);
                    LogWriteLine($"Deleted empty game folder: {_DataFolderFullPath}", LogType.Default, true);
                }

                // Cleanup any folders in foldersToDelete
                foreach (string folderNames in Directory.EnumerateDirectories(GameFolder))
                {
                    if (UninstallProperty.foldersToDelete.Length != 0 && UninstallProperty.foldersToDelete.Contains(Path.GetFileName(folderNames)))
                    {
                        try
                        {
                            Directory.Delete(folderNames, true);
                            LogWriteLine($"Deleted {folderNames}", LogType.Default, true);
                        }
                        catch (Exception ex)
                        {
                            LogWriteLine($"An error occurred while deleting folder {folderNames}\r\n{ex}", LogType.Error, true);
                        }
                        continue;
                    }
                }

                // Cleanup any files in filesToDelete
                foreach (string fileNames in Directory.EnumerateFiles(GameFolder))
                {
                    if (UninstallProperty.filesToDelete.Length != 0 && UninstallProperty.filesToDelete.Contains(Path.GetFileName(fileNames)) ||
                        UninstallProperty.filesToDelete.Length != 0 && UninstallProperty.filesToDelete.Any(pattern => Regex.IsMatch(Path.GetFileName(fileNames), pattern,
                        RegexOptions.Compiled | RegexOptions.NonBacktracking
                    )))
                    {
                        TryDeleteReadOnlyFile(fileNames);
                        LogWriteLine($"Deleted {fileNames}", LogType.Default, true);
                        continue;
                    }
                }

                // Cleanup Game App Data
                string appDataPath = _gameVersionManager.GameDirAppDataPath;
                try
                {
                    Directory.Delete(appDataPath, true);
                    LogWriteLine($"Deleted {appDataPath}", LogType.Default, true);
                }
                catch (Exception ex)
                {
                    LogWriteLine($"An error occurred while deleting game AppData folder: {_gameVersionManager.GameDirAppDataPath}\r\n{ex}", LogType.Error, true);
                }

                // Remove the entire folder if nothing is there
                if (Directory.Exists(GameFolder) && !Directory.EnumerateFileSystemEntries(GameFolder).Any())
                {
                    try
                    {
                        Directory.Delete(GameFolder);
                        LogWriteLine($"Deleted empty game folder: {GameFolder}", LogType.Default, true);
                    }
                    catch (Exception ex)
                    {
                        LogWriteLine($"An error occurred while deleting empty game folder: {GameFolder}\r\n{ex}", LogType.Error, true);
                    }
                }
                else
                {
                    LogWriteLine($"Game folder {GameFolder} is not empty, skipping delete root directory...", LogType.Default, true);
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while uninstalling game: {_gameVersionManager.GameType} - Region: {_gameVersionManager.GamePreset.ZoneName}\r\n{ex}", LogType.Error, true);
            }

            _gameVersionManager.UpdateGamePath("", true);
            _gameVersionManager.Reinitialize();
            return true;
#nullable disable
        }

        public void CancelRoutine()
        {
            _gameRepairTool?.CancelRoutine();
            _token.Cancel();
            Flush();
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public virtual async ValueTask<bool> TryShowFailedDeltaPatchState() { return false; }
        public virtual async ValueTask<bool> TryShowFailedGameConversionState() { return false; }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

        public virtual void ApplyDeleteFileAction()
        {
            foreach (string path in Directory.EnumerateFiles(_gamePath, "deletefiles_*", SearchOption.TopDirectoryOnly))
            {
                using (StreamReader sw = new StreamReader(path,
                    new FileStreamOptions
                    {
                        Mode = FileMode.Open,
                        Access = FileAccess.Read,
                        Options = _canDeleteHdiffReference ? FileOptions.DeleteOnClose : FileOptions.None
                    }))
                {
                    while (!sw.EndOfStream)
                    {
                        string deleteFile = GetBasePersistentDirectory(_gamePath, sw.ReadLine());
                        FileInfo fileInfo = new FileInfo(deleteFile);

                        try
                        {
                            if (fileInfo.Exists)
                            {
                                fileInfo.IsReadOnly = false;
                                fileInfo.Delete();
                                LogWriteLine($"Deleting old file: {deleteFile}");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogWriteLine($"Failed deleting old file: {deleteFile}\r\n{ex}", LogType.Warning, true);
                        }
                    }
                }
            }
        }

        private string GetBasePersistentDirectory(string basePath, string input)
        {
            const string streamingAssetsName = "StreamingAssets";

            input = ConverterTool.NormalizePath(input);
            string inStreamingAssetsPath = Path.Combine(basePath, input);

            int baseStreamingAssetsIndex = input.LastIndexOf(streamingAssetsName);
            if (baseStreamingAssetsIndex <= 0) return inStreamingAssetsPath;

            string inputTrimmed = input.AsSpan().Slice(baseStreamingAssetsIndex + streamingAssetsName.Length + 1).ToString();
            string inPersistentPath = Path.Combine(basePath, _gamePersistentFolderBasePath, inputTrimmed);

            if (File.Exists(inPersistentPath)) return inPersistentPath;

            return inStreamingAssetsPath;
        }

        public virtual async ValueTask ApplyHdiffListPatch()
        {
            List<PkgVersionProperties> hdiffEntry = TryGetHDiffList();

            _progress.ProgressTotalSizeToDownload = hdiffEntry.Sum(x => x.fileSize);
            _progress.ProgressTotalDownload = 0;
            _status.IsIncludePerFileIndicator = false;
            RestartStopwatch();

            _progressTotalCount = 1;
            _progressTotalCountFound = hdiffEntry.Count;

            HDiffPatch patcher = new HDiffPatch();
            foreach (PkgVersionProperties entry in hdiffEntry)
            {
                _status.ActivityStatus = string.Format("{0}: {1}", Lang._Misc.Patching, string.Format(Lang._Misc.PerFromTo, _progressTotalCount, _progressTotalCountFound));

                string patchBasePath = Path.Combine(_gamePath, ConverterTool.NormalizePath(entry.remoteName));
                string sourceBasePath = GetBasePersistentDirectory(_gamePath, entry.remoteName);
                string patchPath = patchBasePath + ".hdiff";
                string destPath = sourceBasePath + "_tmp";

                try
                {
                    _token.Token.ThrowIfCancellationRequested();
                    HDiffPatch.LogVerbosity = Verbosity.Verbose;
                    EventListener.LoggerEvent += EventListener_PatchLogEvent;
                    EventListener.PatchEvent += EventListener_PatchEvent;
                    if (File.Exists(sourceBasePath) && File.Exists(patchPath))
                    {
                        LogWriteLine($"Patching file {entry.remoteName}...", LogType.Default, true);
                        UpdateProgressBase();
                        UpdateStatus();

                        await Task.Run(() =>
                        {
                            patcher.Initialize(patchPath);
                            patcher.Patch(sourceBasePath, destPath, true, _token.Token, false, true);
                        }, _token.Token);

                        File.Move(destPath, sourceBasePath, true);
                    }
                }
                catch (OperationCanceledException)
                {
                    _token.Cancel();
                    LogWriteLine($"Cancelling patching process!...", LogType.Warning, true);
                    throw;
                }
                catch (Exception ex)
                {
                    LogWriteLine($"Error while patching file: {entry.remoteName}. Skipping!\r\n{ex}", LogType.Warning, true);

                    _progress.ProgressTotalDownload += entry.fileSize;
                    _progress.ProgressTotalPercentage = Math.Round(((double)_progress.ProgressTotalDownload / _progress.ProgressTotalSizeToDownload) * 100, 2);
                    _progress.ProgressTotalSpeed = _progress.ProgressTotalDownload / _stopwatch.Elapsed.TotalSeconds;

                    _progress.ProgressTotalTimeLeft = TimeSpan.FromSeconds((_progress.ProgressTotalSizeToDownload - _progress.ProgressTotalDownload) / ConverterTool.Unzeroed(_progress.ProgressTotalSpeed));
                    UpdateProgress();
                }
                finally
                {
                    EventListener.PatchEvent -= EventListener_PatchEvent;
                    EventListener.LoggerEvent -= EventListener_PatchLogEvent;
                    try
                    {
                        if (File.Exists(destPath)) File.Delete(destPath);
                    }
                    catch (Exception ex)
                    {
                        LogWriteLine($"Failed while trying to delete temporary file: {destPath}, skipping!\r\n{ex}", LogType.Warning, true);
                    }

                    _progressTotalCount++;
                    FileInfo patchFile = new FileInfo(patchPath);

                    patchFile.IsReadOnly = false;
                    patchFile.Delete();
                }
            }
        }

        private async void EventListener_PatchEvent(object sender, PatchEvent e)
        {
            _progress.ProgressTotalDownload += e.Read;
            if (await base.CheckIfNeedRefreshStopwatch())
            {
                _progress.ProgressTotalPercentage = Math.Round(((double)_progress.ProgressTotalDownload / _progress.ProgressTotalSizeToDownload) * 100, 2);
                _progress.ProgressTotalSpeed = _progress.ProgressTotalDownload / _stopwatch.Elapsed.TotalSeconds;

                _progress.ProgressTotalTimeLeft = TimeSpan.FromSeconds((_progress.ProgressTotalSizeToDownload - _progress.ProgressTotalDownload) / ConverterTool.Unzeroed(_progress.ProgressTotalSpeed));
                UpdateProgress();
            }
        }

        private void EventListener_PatchLogEvent(object sender, LoggerEvent e)
        {
            if (HDiffPatch.LogVerbosity == Verbosity.Quiet
            || (HDiffPatch.LogVerbosity == Verbosity.Debug
            && !(e.LogLevel == Verbosity.Debug ||
                 e.LogLevel == Verbosity.Verbose ||
                 e.LogLevel == Verbosity.Info))
            || (HDiffPatch.LogVerbosity == Verbosity.Verbose
            && !(e.LogLevel == Verbosity.Verbose ||
                 e.LogLevel == Verbosity.Info))
            || (HDiffPatch.LogVerbosity == Verbosity.Info
            && !(e.LogLevel == Verbosity.Info))) return;

            LogType type = e.LogLevel switch
            {
                Verbosity.Verbose => LogType.Debug,
                Verbosity.Debug => LogType.Debug,
                _ => LogType.Default
            };

            LogWriteLine(e.Message, type, true);
        }

        public virtual List<PkgVersionProperties> TryGetHDiffList()
        {
            List<PkgVersionProperties> _out = new List<PkgVersionProperties>();
            PkgVersionProperties prop;
            foreach (string listFile in Directory.EnumerateFiles(_gamePath, "*hdifffiles*", SearchOption.TopDirectoryOnly))
            {
                LogWriteLine($"hdiff File list path: {listFile}", LogType.Default, true);

                try
                {
                    using (StreamReader listReader = new StreamReader(listFile,
                        new FileStreamOptions
                        {
                            Mode = FileMode.Open,
                            Access = FileAccess.Read,
                            Options = _canDeleteHdiffReference ? FileOptions.DeleteOnClose : FileOptions.None
                        }))
                    {
                        while (!listReader.EndOfStream)
                        {
                            prop = listReader.ReadLine().Deserialize<PkgVersionProperties>(CoreLibraryJSONContext.Default);

                            string filePath = Path.Combine(_gamePath, prop.remoteName + ".hdiff");
                            if (File.Exists(filePath))
                            {
                                try
                                {
                                    prop.fileSize = HDiffPatch.GetHDiffNewSize(filePath);
                                    LogWriteLine($"hdiff entry: {prop.remoteName}", LogType.Default, true);

                                    _out.Add(prop);
                                }
                                catch (Exception ex)
                                {
                                    LogWriteLine($"Error while parsing the size of the new file inside of diff: {filePath}\r\n{ex}", LogType.Warning, true);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogWriteLine($"Failed while trying to read hdiff file list: {listFile}\r\n{ex}", LogType.Warning, true);
                }
            }

            return _out;
        }
        #endregion

        #region Private Methods - GetInstallationPath
        private async ValueTask<int> CheckExistingSteamInstallation()
        {
            string pathOnSteam = "";
            if (TryGetExistingSteamPath(ref pathOnSteam))
            {
                switch (await Dialog_ExistingInstallationSteam(_parentUI))
                {
                    // If action to migrate was taken, then update the game path (but don't save it to the config file)
                    // After that, return 0
                    case ContentDialogResult.Primary:
                        _gameVersionManager.UpdateGamePath(pathOnSteam, false);
                        return 0;
                    // If action to fresh install was taken, then return 2 (selecting path)
                    case ContentDialogResult.Secondary:
                        return 2;
                    // If action to cancel was taken, then return -1 (go back)
                    case ContentDialogResult.None:
                        return -1;
                }
            }

            // Return 1 to continue to another check
            return 1;
        }

        private async ValueTask<int> CheckExistingBHI3LInstallation()
        {
            string pathOnBHi3L = "";
            if (TryGetExistingBHI3LPath(ref pathOnBHi3L))
            {
                switch (await Dialog_ExistingInstallationBetterLauncher(_parentUI, pathOnBHi3L))
                {
                    // If action to migrate was taken, then update the game path (but don't save it to the config file)
                    case ContentDialogResult.Primary:
                        _gameVersionManager.UpdateGamePath(pathOnBHi3L, false);
                        return 0;
                    // If action to fresh install was taken, then return 2 (selecting path)
                    case ContentDialogResult.Secondary:
                        return 2;
                    // If action to cancel was taken, then return -1 (go back)
                    case ContentDialogResult.None:
                        return -1;
                }
            }

            // Return 1 to continue to another check
            return 1;
        }

        private async ValueTask<int> CheckExistingOfficialInstallation()
        {
            if (_gameVersionManager.GamePreset.CheckExistingGame())
            {
                switch (await Dialog_ExistingInstallation(_parentUI, _gameVersionManager.GamePreset.ActualGameDataLocation))
                {
                    // If action to migrate was taken, then update the game path (but don't save it to the config file)
                    case ContentDialogResult.Primary:
                        _gameVersionManager.UpdateGamePath(_gameVersionManager.GamePreset.ActualGameDataLocation.Replace('\\', '/'), false);
                        return 0;
                    // If action to fresh install was taken, then return 2 (selecting path)
                    case ContentDialogResult.Secondary:
                        return 2;
                    // If action to cancel was taken, then return -1 (go back)
                    case ContentDialogResult.None:
                        return -1;
                }
            }

            // Return 1 to continue to another check
            return 1;
        }

        private bool TryGetExistingSteamPath(ref string OutputPath)
        {
            // If the game preset doesn't have SteamGameID, then return false
            if (_gameVersionManager.GamePreset.SteamGameID == null) return false;
            // Assign Steam ID
            int steamID = _gameVersionManager.GamePreset.SteamGameID ?? 0;

            // Try get the list of Steam Libs and Apps
            List<string> steamLibsList = SteamTool.GetSteamLibs();
            if (steamLibsList == null) return false;

            List<AppInfo> steamAppList = SteamTool.GetSteamApps(steamLibsList);
#nullable enable
            AppInfo? steamAppInfo = steamAppList.Where(x => x.Id == steamID).FirstOrDefault();

            // If the app info is not null, then assign OutputPath to the game path
            if (steamAppInfo != null)
            {
                OutputPath = steamAppInfo?.GameRoot;
                return true;
            }
#nullable disable

            // If none of them has it, then return false
            return false;
        }

        private bool TryGetExistingBHI3LPath(ref string OutputPath)
        {
#nullable enable
            // If the preset doesn't have BetterHi3Launcher registry ver info, then return false
            if (_gameVersionManager.GamePreset.BetterHi3LauncherVerInfoReg == null) return false;

            // Try open BHI3L registry key
            // If the key doesn't exist, then return false
            RegistryKey? Key = Registry.CurrentUser.OpenSubKey("Software\\Bp\\Better HI3 Launcher");
            if (Key == null) return false;

            // Try get the key value
            // If the key also doesn't exist, then return false
            byte[]? keyValue = (byte[]?)Key?.GetValue(_gameVersionManager.GamePreset.BetterHi3LauncherVerInfoReg);
            if (keyValue == null) return false;

            BHI3LInfo? config;
            string value = "";
            try
            {
                // Try parsing the config
                value = Encoding.UTF8.GetString(keyValue);
                config = value.Deserialize<BHI3LInfo>(CoreLibraryJSONContext.Default);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Registry Value {_gameVersionManager.GamePreset.BetterHi3LauncherVerInfoReg}:\r\n{value}\r\n\r\nException:\r\n{ex}", LogType.Error, true);
                return false;
            }

            // Assign OutputPath to the path provided by the config
            if (config != null && config.game_info.installed
             && !string.IsNullOrEmpty(config.game_info.install_path))
            {
                FileInfo execPath = new FileInfo(Path.Combine(config.game_info.install_path, _gameVersionManager.GamePreset.GameExecutableName));
                OutputPath = config.game_info.install_path;
                return execPath.Exists && execPath.Length > 1 >> 16;
            }

            // If all of those not passed, then return false
            return false;
#nullable disable
        }

        private async Task<string> AskGameFolderDialog()
        {
            // Set initial folder variable as empty
            string folder = "";

            // Do loop and break if choice is already been done
            bool isChoosen = false;
            while (!isChoosen)
            {
                // Show dialog
                switch (await Dialog_InstallationLocation(_parentUI))
                {
                    // If primary button is clicked, then set folder with the default path
                    case ContentDialogResult.Primary:
                        folder = Path.Combine(LauncherConfig.AppGameFolder, _gameVersionManager.GamePreset.ProfileName, _gameVersionManager.GamePreset.GameDirectoryName);
                        isChoosen = true;
                        break;
                    // If secondary, then show folder picker dialog to choose the folder
                    case ContentDialogResult.Secondary:
                        folder = await FileDialogNative.GetFolderPicker();

                        if (!string.IsNullOrEmpty(folder))
                        {
                            // Check for the write permission on the folder
                            if (ConverterTool.IsUserHasPermission(folder))
                            {
                                isChoosen = true;
                            }
                            else
                            {
                                // If not, then show the Insufficient access dialog
                                await Dialog_InsufficientWritePermission(_parentUI, folder);
                            }
                        }
                        else
                        {
                            isChoosen = false;
                        }
                        break;
                    case ContentDialogResult.None:
                        return null;
                }
            }

            return folder;
        }

        private async Task GetLatestPackageList(List<GameInstallPackage> packageList, GameInstallStateEnum gameState, bool usePreload)
        {
            // Clean the package list
            packageList.Clear();

            // Iterate the package resource version and add it into packageList
            foreach (RegionResourceVersion asset in usePreload ?
                _gameVersionManager.GetGamePreloadZip() :
                _gameVersionManager.GetGameLatestZip(gameState))
            {
                await TryAddResourceVersionList(asset, packageList);
            }
        }

        private async ValueTask<int> CheckExistingOrAskFolderDialog()
        {
            // Try run the result and if it's null, then return -1 (Cancel the operation)
            string result = await AskGameFolderDialog();
            if (result == null) return -1;

            // Check for existing installation and if it's found, then override result
            // with pathPossibleExisting value and return 0 to skip the process
            string pathPossibleExisting = _gameVersionManager.FindGameInstallationPath(result);
            if (pathPossibleExisting != null)
            {
                _gameVersionManager.UpdateGamePath(pathPossibleExisting, false);
                return 0;
            }

            // if above passes, start with the new installation
            _gameVersionManager.UpdateGamePath(result, false);
            return 1;
        }
        #endregion
        #region Virtual Methods - GetInstallationPath
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected virtual async ValueTask<bool> TryAddResourceVersionList(RegionResourceVersion asset, List<GameInstallPackage> packageList)
        {
            // Try add the package into
            GameInstallPackage package = new GameInstallPackage(asset, _gamePath) { PackageType = GameInstallPackageType.General };
            packageList.Add(package);

            if (package.Segments != null)
            {
                foreach (GameInstallPackage segment in package.Segments)
                {
                    LogWriteLine($"Adding segmented package: {segment.Name} to the list (Hash: {segment.HashString})", LogType.Default, true);
                }
                return true;
            }
            LogWriteLine($"Adding general package: {package.Name} to the list (Hash: {package.HashString})", LogType.Default, true);

            // If the voice packs don't exist, then skip it
            if (asset.voice_packs == null) return false;

            // Otherwise, return true
            return true;
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        #endregion

        #region Private Methods - StartPackageInstallation
        private void MoveFileToIngredientList(List<FilePropertiesRemote> assetIndex, string sourcePath, string targetPath, bool isHonkai)
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

            // If it's not honkai, then return
            if (!isHonkai) return;

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

        private void TryUnassignReadOnlyFiles()
        {
            foreach (string file in Directory.EnumerateFiles(_gamePath, "*", SearchOption.AllDirectories))
            {
                FileInfo fileInfo = new FileInfo(file);
                if (fileInfo.IsReadOnly)
                    fileInfo.IsReadOnly = false;
            }
        }

        private void TryRemoveRedundantHDiffList()
        {
            foreach (string file in Directory.EnumerateFiles(_gamePath, "*.txt", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(file);
                if (name.StartsWith("deletefiles", StringComparison.OrdinalIgnoreCase)
                 || name.StartsWith("hdifffiles", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        LogWriteLine($"Be careful that the installation process might have some problem since the launcher can't remove HDiff list file: {name}!\r\n{ex}", LogType.Warning, true);
                    }
                }
            }
        }
        #endregion

        #region Private Methods - StartPackageDownload
        private async ValueTask InvokePackageDownloadRoutine(List<GameInstallPackage> packageList, CancellationToken token)
        {
            // Get the package/segment count
            int packageCount = packageList.Sum(x => x.Segments != null ? x.Segments.Count : 1);

            // Set progress count to beginning
            _progressTotalCountCurrent = 1;
            _progressTotalCount = packageCount;
            RestartStopwatch();

            // Subscribe the download progress to the event adapter
            _httpClient.DownloadProgress += HttpClientDownloadProgressAdapter;
            try
            {
                // Iterate the package list
                foreach (GameInstallPackage package in packageList)
                {
                    // If the package is segmented, then iterate and run the routine for segmented packages
                    if (package.Segments != null)
                    {
                        // Iterate the segment list
                        for (int i = 0; i < package.Segments.Count; i++)
                        {
                            await RunPackageDownloadRoutine(package.Segments[i], token, packageCount);
                        }
                        // Skip action below and continue to the next segment
                        continue;
                    }

                    // Else, run the routine as normal
                    await RunPackageDownloadRoutine(package, token, packageCount);
                }
            }
            finally
            {
                // Unsubscribe the download progress from the event adapter
                _httpClient.DownloadProgress -= HttpClientDownloadProgressAdapter;
            }
        }

        private async ValueTask RunPackageDownloadRoutine(GameInstallPackage package, CancellationToken token, int packageCount)
        {
            // Set the activity status
            _status.IsIncludePerFileIndicator = packageCount > 1;
            _status.ActivityStatus = string.Format("{0}: {1}", Lang._Misc.Downloading, string.Format(Lang._Misc.PerFromTo, _progressTotalCountCurrent, _progressTotalCount));
            LogWriteLine($"Downloading package URL {_progressTotalCountCurrent}/{_progressTotalCount} ({ConverterTool.SummarizeSizeSimple(package.Size)}): {package.URL}");

            // Get the directory path
            string pathDir = Path.GetDirectoryName(package.PathOutput);

            // If the directory doesn't exist, then create one
            if (!Directory.Exists(pathDir)) Directory.CreateDirectory(pathDir);

            // If the file exist or package size is unmatched,
            // then start downloading
            long existingPackageFileSize = package.GetStreamLength(_downloadThreadCount);
            bool isExistingPackageFileExist = package.IsReadStreamExist(_downloadThreadCount);

            if (!isExistingPackageFileExist
              || existingPackageFileSize != package.Size)
            {
                // If the package size is more than or equal to 10 MB, then allow to use multi-session.
                // Otherwise, forcefully use single-session.
                bool isCanMultiSession;
                if (isCanMultiSession = package.Size >= (10 << 20))
                    await _httpClient.Download(package.URL, package.PathOutput, _downloadThreadCount, false, token);
                else
                    await _httpClient.Download(package.URL, package.PathOutput, false, null, null, token);

                // Update status to merging
                _status.ActivityStatus = string.Format("{0}: {1}", Lang._Misc.Merging, string.Format(Lang._Misc.PerFromTo, _progressTotalCountCurrent, _progressTotalCount));
                UpdateStatus();
                _stopwatch.Stop();

                // Check if the merge chunk is enabled and the download could perform multisession,
                // then do merge.
                if (_canMergeDownloadChunks && isCanMultiSession)
                    await _httpClient.Merge();
                _stopwatch.Start();
            }

            // Increment the total count
            _progressTotalCountCurrent++;
        }

        private async ValueTask CheckExistingDownloadAsync(UIElement Content, List<GameInstallPackage> packageList)
        {
            // If the _progressTotalSizeCurrent has the size, then
            // display the reset or continue download dialog.
            // UPDATE: Ensure if the downloaded size is not the same as total. If no, then continue
            //         showing the dialog
            if (_progressTotalSizeCurrent > 0 && _progressTotalSize != _progressTotalSizeCurrent)
            {
                switch (await Dialog_ExistingDownload(Content, _progressTotalSizeCurrent, _progressTotalSize))
                {
                    case ContentDialogResult.Primary:
                        break;
                    // Reset the download (delete all existing files) if selected
                    case ContentDialogResult.Secondary:
                        ResetDownload(packageList);
                        break;
                }
            }
        }

        private void ResetDownload(List<GameInstallPackage> packageList)
        {
            // Reset the _progressTotalSizeCurrent to 0
            _progressTotalSizeCurrent = 0;

            // Iterate and start deleting the existing file
            for (int i = 0; i < packageList.Count; i++)
            {
                // Check if the package has segment. If yes, then iterate it per segment
                if (packageList[i].Segments != null)
                {
                    for (int j = 0; j < packageList[i].Segments.Count; j++)
                    {
                        DeleteDownloadedFile(packageList[i].Segments[j].PathOutput, _downloadThreadCount);
                    }
                }

                // Otherwise, delete the package as a single file
                DeleteDownloadedFile(packageList[i].PathOutput, _downloadThreadCount);
            }
        }

        private void DeleteDownloadedFile(string FileOutput, byte Thread)
        {
            // Get the info of the existing file
            FileInfo fileInfo = new FileInfo(FileOutput);

            // If exist, then delete
            if (fileInfo.Exists)
            {
                fileInfo.IsReadOnly = false;
                fileInfo.Delete();
            }

            // Delete the file of the chunk file too
            _httpClient.DeleteMultisessionFiles(FileOutput, Thread);
        }

        private long GetExistingDownloadPackageSize(List<GameInstallPackage> packageList)
        {
            // Initialize total existing size and download thread count
            long totalSize = 0;

            // Iterate the size and find the total, then increment it to totalSize
            for (int i = 0; i < packageList.Count; i++)
            {
                if (packageList[i].Segments != null)
                {
                    long totalSegmentDownloaded = 0;
                    for (int j = 0; j < packageList[i].Segments.Count; j++)
                    {
                        long segmentDownloaded = _httpClient.CalculateExistingMultisessionFilesWithExpctdSize(packageList[i].Segments[j].PathOutput, _downloadThreadCount, packageList[i].Segments[j].Size);
                        totalSize += segmentDownloaded;
                        totalSegmentDownloaded += segmentDownloaded;
                        packageList[i].Segments[j].SizeDownloaded = segmentDownloaded;
                    }
                    packageList[i].SizeDownloaded = totalSegmentDownloaded;
                    continue;
                }

                packageList[i].SizeDownloaded = _httpClient.CalculateExistingMultisessionFilesWithExpctdSize(packageList[i].PathOutput, _downloadThreadCount, packageList[i].Size);
                totalSize += packageList[i].SizeDownloaded;
            }

            // return totalSize
            return totalSize;
        }

        private async ValueTask CheckDriveFreeSpace(UIElement Content, List<GameInstallPackage> packageList)
        {
            // Get the information about the disk
            DriveInfo _DriveInfo = new DriveInfo(_gamePath);

            // Get the total of required size on every package
            long RequiredSpace = packageList.Sum(x => x.SizeRequired);
            long ExistingPackageSize = packageList.Sum(x => x.Segments != null ? x.Segments.Sum(y => GetExistingPartialDownloadLength(y.PathOutput, y.Size)) : GetExistingPartialDownloadLength(x.PathOutput, x.Size));

            // Get the total free space of the disk
            long DiskSpace = _DriveInfo.TotalFreeSpace;
            LogWriteLine($"Total free space required: {ConverterTool.SummarizeSizeSimple(RequiredSpace)} with {_DriveInfo.Name} remaining free space: {ConverterTool.SummarizeSizeSimple(DiskSpace)}", LogType.Default, true);

            // Check if the disk space is insufficient, then show the dialog.
            if (DiskSpace < (RequiredSpace - ExistingPackageSize))
            {
                string errStr = $"Free Space on {_DriveInfo.Name} is not sufficient! (Free space: {DiskSpace}, Req. Space: {RequiredSpace - ExistingPackageSize}, Drive: {_DriveInfo.Name})";
                LogWriteLine(errStr, LogType.Error, true);
                await Dialog_InsufficientDriveSpace(Content, DiskSpace, RequiredSpace - ExistingPackageSize, _DriveInfo.Name);
                throw new TaskCanceledException(errStr);
            }
        }

        private long GetExistingPartialDownloadLength(string fileOutput, long remoteSize)
        {
            // Get the directory path
            string pathDir = Path.GetDirectoryName(fileOutput);

            // If the directory doesn't exist, then return 0
            if (!Directory.Exists(pathDir)) return 0;

            // Try get the status of the full downloaded package output
            FileInfo fileInfo = new FileInfo(fileOutput);

            // If the full downloaded package output is already exist and the size is the same as remote,
            // then return the actual size
            if (fileInfo.Exists && fileInfo.Length == remoteSize)
                return fileInfo.Length;

            // If above not passed, then try enumerate for the chunk
            string[] partPaths = Directory.EnumerateFiles(Path.GetDirectoryName(fileOutput), $"{Path.GetFileName(fileOutput)}.*").Where(x =>
            {
                // Get the extension
                string extension = Path.GetExtension(x).TrimStart('.'); // Removing dot (.)

                // If the extension is a number, then return true. Otherwise, return false.
                return int.TryParse(extension, out int _);
            }).ToArray();

            // If the chunk file doesn't exist, then return 0
            if (partPaths.Length == 0) return 0;

            // If the chunk file might probably exist, then return the sum of the size
            return partPaths.Sum(x => (fileInfo = new FileInfo(x)).Exists ? fileInfo.Length : 0);
        }

        private async Task GetPackagesRemoteSize(List<GameInstallPackage> packageList, CancellationToken token)
        {
            // Iterate and assign the remote size to each package inside the list
            for (int i = 0; i < packageList.Count; i++)
            {
                if (packageList[i].Segments != null)
                {
                    await TryGetSegmentedPackageRemoteSize(packageList[i], token);
                    continue;
                }

                await TryGetPackageRemoteSize(packageList[i], token);
            }
        }
        #endregion
        #region Virtual Methods - StartPackageDownload

        private enum CompletenessStatus { Running, Completed, Cancelled, Idle }
        private void UpdateCompletenessStatus(CompletenessStatus status)
        {
            switch (status)
            {
                case CompletenessStatus.Running:
                    IsRunning = true;
                    _status.IsRunning = true;
                    _status.IsCompleted = false;
                    _status.IsCanceled = false;
                    break;
                case CompletenessStatus.Completed:
                    IsRunning = false;
                    _status.IsRunning = false;
                    _status.IsCompleted = true;
                    _status.IsCanceled = false;
                    // HACK: Fix the progress not achieving 100% while completed
                    _progress.ProgressTotalPercentage = 100f;
                    _progress.ProgressPerFilePercentage = 100f;
                    break;
                case CompletenessStatus.Cancelled:
                    IsRunning = false;
                    _status.IsRunning = false;
                    _status.IsCompleted = false;
                    _status.IsCanceled = true;
                    break;
                case CompletenessStatus.Idle:
                    IsRunning = false;
                    _status.IsRunning = false;
                    _status.IsCompleted = false;
                    _status.IsCanceled = false;
                    break;
            }
            UpdateAll();
        }

        protected async Task TryGetPackageRemoteSize(GameInstallPackage asset, CancellationToken token)
        {
            asset.Size = await _httpClient.TryGetContentLength(asset.URL, token) ?? 0;

            LogWriteLine($"Package: [T: {asset.PackageType}] {asset.Name} has {ConverterTool.SummarizeSizeSimple(asset.Size)} in size with {ConverterTool.SummarizeSizeSimple(asset.SizeRequired)} of free space required", LogType.Default, true);
        }

        protected async Task TryGetSegmentedPackageRemoteSize(GameInstallPackage asset, CancellationToken token)
        {
            long totalSize = 0;
            for (int i = 0; i < asset.Segments.Count; i++)
            {
                long segmentSize = await _httpClient.TryGetContentLength(asset.Segments[i].URL, token) ?? 0;
                totalSize += segmentSize;
                asset.Segments[i].Size = segmentSize;
                LogWriteLine($"Package Segment: [T: {asset.PackageType}] {asset.Segments[i].Name} has {ConverterTool.SummarizeSizeSimple(segmentSize)} in size", LogType.Default, true);
            }

            asset.Size = totalSize;
            LogWriteLine($"Package Segment (count: {asset.Segments.Count}) has {ConverterTool.SummarizeSizeSimple(asset.Size)} in total size with {ConverterTool.SummarizeSizeSimple(asset.SizeRequired)} of free space required", LogType.Default, true);
        }
        #endregion

        #region Virtual Methods - UninstallGame
        protected virtual UninstallGameProperty AssignUninstallFolders() => throw new NotSupportedException($"Cannot uninstall game: {_gameVersionManager.GamePreset.GameType}. Uninstall method is not yet implemented!");
        #endregion

        #region Event Methods
        protected void UpdateProgressBase() => base.UpdateProgress();

        protected async void DeltaPatchCheckProgress(object sender, PatchEvent e)
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

        protected void DeltaPatchCheckLogEvent(object sender, LoggerEvent e)
        {
            if (HDiffPatch.LogVerbosity == Verbosity.Quiet
            || (HDiffPatch.LogVerbosity == Verbosity.Debug
            && !(e.LogLevel == Verbosity.Debug ||
                 e.LogLevel == Verbosity.Verbose ||
                 e.LogLevel == Verbosity.Info))
            || (HDiffPatch.LogVerbosity == Verbosity.Verbose
            && !(e.LogLevel == Verbosity.Verbose ||
                 e.LogLevel == Verbosity.Info))
            || (HDiffPatch.LogVerbosity == Verbosity.Info
            && !(e.LogLevel == Verbosity.Info))) return;

            LogType type = e.LogLevel switch
            {
                Verbosity.Verbose => LogType.Debug,
                Verbosity.Debug => LogType.Debug,
                _ => LogType.Default
            };

            LogWriteLine(e.Message, type, true);
        }

        protected async void DeltaPatchCheckProgress(object sender, TotalPerfileProgress e)
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

        private async void ZipProgressAdapter(object sender, ExtractProgressProp e)
        {
            if (await base.CheckIfNeedRefreshStopwatch())
            {
                // Incrment current total size
                long lastSize = GetLastSize((long)e.TotalRead);
                _progressTotalSizeCurrent += lastSize;

                // Assign per file size
                _progressPerFileSizeCurrent = (long)e.TotalRead;
                _progressPerFileSize = (long)e.TotalSize;

                // Assign local sizes to progress
                _progress.ProgressPerFileDownload = _progressPerFileSizeCurrent;
                _progress.ProgressPerFileSizeToDownload = _progressPerFileSize;
                _progress.ProgressTotalDownload = _progressTotalSizeCurrent;
                _progress.ProgressTotalSizeToDownload = _progressTotalSize;

                // Calculate the speed
                _progress.ProgressTotalSpeed = _progressTotalSizeCurrent / _stopwatch.Elapsed.TotalSeconds;

                // Calculate percentage
                _progress.ProgressTotalPercentage = Math.Round(((double)_progressTotalSizeCurrent / _progressTotalSize) * 100, 2);
                _progress.ProgressPerFilePercentage = Math.Round(((double)_progressPerFileSizeCurrent / _progressPerFileSize) * 100, 2);
                // Calculate the timelapse
                _progress.ProgressTotalTimeLeft = TimeSpan.FromSeconds((_progressTotalSize - _progressTotalSizeCurrent) / ConverterTool.Unzeroed(_progress.ProgressTotalSpeed));

                UpdateAll();
            }
        }

        private long GetLastSize(long input)
        {
            if (_totalLastSizeCurrent > input)
                _totalLastSizeCurrent = input;

            long a = input - _totalLastSizeCurrent;
            _totalLastSizeCurrent = input;
            return a;
        }

        private async void HttpClientDownloadProgressAdapter(object sender, DownloadEvent e)
        {
            // Set the progress bar not indetermined
            _status.IsProgressPerFileIndetermined = false;
            _status.IsProgressTotalIndetermined = false;

            if (e.State != DownloadState.Merging)
            {
                // Increment the total current size if status is not merging
                _progressTotalSizeCurrent += e.Read;
                // Increment the total last read
                _progressTotalReadCurrent += e.Read;
            }

            if (await base.CheckIfNeedRefreshStopwatch())
            {
                _progress.DownloadEvent = e;

                if (e.State != DownloadState.Merging)
                {
                    // Assign local sizes to progress
                    _progress.ProgressPerFileDownload = _progressPerFileSizeCurrent;
                    _progress.ProgressPerFileSizeToDownload = _progressPerFileSize;
                    _progress.ProgressTotalDownload = _progressTotalSizeCurrent;
                    _progress.ProgressTotalSizeToDownload = _progressTotalSize;

                    // Calculate the speed
                    _progress.ProgressTotalSpeed = _progressTotalReadCurrent / _stopwatch.Elapsed.TotalSeconds;

                    // Calculate percentage
                    _progress.ProgressPerFilePercentage = Math.Round(_progressPerFileSizeCurrent / (double)_progressPerFileSize * 100, 2);
                    _progress.ProgressTotalPercentage = Math.Round(_progressTotalSizeCurrent / (double)_progressTotalSize * 100, 2);

                    // Calculate the timelapse
                    _progress.ProgressTotalTimeLeft = TimeSpan.FromSeconds((_progressTotalSize - _progressTotalSizeCurrent) / ConverterTool.Unzeroed(_progress.ProgressTotalSpeed));
                }
                else
                {
                    // If merging, show per file indicator explicitly
                    // and then update the normal progress
                    _status.IsIncludePerFileIndicator = true;

                    // If status is merging, then use progress for speed and timelapse from Http client
                    // and set the rest from the base class
                    _progress.ProgressTotalTimeLeft = e.TimeLeft;
                    _progress.ProgressTotalSpeed = e.Speed;
                    _progress.ProgressPerFileDownload = _progressPerFileSizeCurrent;
                    _progress.ProgressPerFileSizeToDownload = _progressPerFileSize;
                    _progress.ProgressTotalDownload = _progressTotalSizeCurrent;
                    _progress.ProgressTotalSizeToDownload = _progressTotalSize;
                    _progress.ProgressTotalPercentage = Math.Round(_progressTotalSizeCurrent / (double)_progressTotalSize * 100, 2);
                }

                // Update the status of per file size and current progress from Http client
                _progressPerFileSizeCurrent = e.SizeDownloaded;
                _progressPerFileSize = e.SizeToBeDownloaded;
                _progress.ProgressPerFilePercentage = e.ProgressPercentage;

                // Update the status
                UpdateAll();
            }
        }

        protected override void RestartStopwatch()
        {
            // Reset read count to 0
            _progressTotalReadCurrent = 0;

            // Restart the stopwatch from base
            base.RestartStopwatch();
        }
        #endregion
    }
}
