using CollapseLauncher.Interfaces;
using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher.InstallManager.Base
{
    internal class InstallManagerBase<T> : ProgressBase<GameInstallPackageType, GameInstallPackage> where T : IGameVersionCheck
    {
        #region Properties
        protected T _gameVersionManager { get => (T)PageStatics._GameVersion; }
        protected RegionResourceGame _gameRegion { get => _gameVersionManager.GameAPIProp.data; }
        protected GameVersion _gameLatestVersion { get => _gameVersionManager.GetGameVersionAPI(); }
        protected GameVersion? _gameLatestPreloadVersion { get => _gameVersionManager.GetGameVersionAPIPreload(); }
        protected GameVersion? _gameInstalledVersion { get => _gameVersionManager.GetGameExistingVersion(); }
        protected GameInstallStateEnum _gameInstallationStatus { get => _gameVersionManager.GetGameState(); }
        // TODO: Override if the game was supposed to have voice packs (For example: Genshin)
        protected virtual int _gameVoiceLanguageID { get => int.MinValue; }
        protected IRepair _gameRepairTool { get; set; }
        protected Http _httpClient { get; private set; }
        protected bool _canDeleteZip { get => !File.Exists(Path.Combine(_gamePath, "@NoDeleteZip")); }
        protected bool _canSkipVerif { get => File.Exists(Path.Combine(_gamePath, "@NoVerification")); }
        protected bool _canSkipExtract { get => File.Exists(Path.Combine(_gamePath, "@NoExtraction")); }
        protected virtual bool _canDeltaPatch { get => false; }
        protected virtual DeltaPatchProperty _gameDeltaPatchProperty { get => null; }

        private long _totalLastSizeCurrent = 0;
        #endregion

        #region Public Properties
        #endregion

        public InstallManagerBase(UIElement parentUI)
            : base(parentUI, "", "", null)
        {
            _httpClient = new Http(true);
        }

        ~InstallManagerBase() => Dispose();

        protected void ResetToken() => _token = new CancellationTokenSource();

        public void Dispose()
        {
            _httpClient?.Dispose();
            _gameRepairTool?.Dispose();
            _token?.Cancel();
            Flush();
        }

        public virtual void Flush()
        {
            ResetToken();
            _gameRepairTool?.Dispose();
            _assetIndex.Clear();
        }

        #region Public Methods
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
            // Get the game state and run the action for each of them
            GameInstallStateEnum gameState = _gameVersionManager.GetGameState();
            LogWriteLine($"Gathering packages information for installation (State: {gameState})...", LogType.Default, true);

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
        }

        // Bool:  0      -> Indicates that one of the package is failing and need to redownload
        //                  or the verification can't be started because the download never being performed first
        //        1      -> Continue to the next step (all passes)
        //       -1      -> Cancel the operation
        public virtual async ValueTask<int> StartPackageVerification()
        {
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

            return 1;
        }

        private async ValueTask<int> RunPackageVerificationRoutine(GameInstallPackage asset, CancellationToken token)
        {
            // Reset per size counter
            _progressPerFileSizeCurrent = 0;

            byte[] hashLocal;
            using (FileStream fs = new FileStream(asset.PathOutput, FileMode.Open, FileAccess.Read, FileShare.Read, _bufferBigLength))
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

        public virtual async Task StartPackageInstallation()
        {
            // Sanity Check: Check if the package list is empty or not
            if (_assetIndex.Count == 0) throw new InvalidOperationException("Package list is empty. Make sure you have ran StartPackageDownload() first.");

            // If _canSkipExtract flag is true, then return (skip) the extraction
            if (_canSkipExtract) return;

            // Start Async Thread
            // Since the SevenZipTool (especially with the callbacks) can't run under
            // different thread, so the async call will be called at the start
            await Task.Run(() =>
            {
                // Initialize the zip tool
                using (SevenZipTool zip = new SevenZipTool())
                {
                    // Get the sum of uncompressed size and
                    // Set progress count to beginning
                    _progressTotalSize = _assetIndex.Sum(x => x.Segments != null ?
                        zip.GetUncompressedSize(x.Segments.Select(x => x.PathOutput).ToArray()) :
                        zip.GetUncompressedSize(x.PathOutput));

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

                        try
                        {
                            // Load the zip
                            zip.LoadArchive(asset.Segments != null ? asset.Segments.Select(x => x.PathOutput).ToArray() : new string[] { asset.PathOutput });

                            // Start extraction
                            zip.ExtractProgressChanged += ZipProgressAdapter;
                            zip.ExtractToDirectory(_gamePath, _threadCount, _token.Token);

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

                            // Make sure that the ZipTool is getting disposed first
                            zip.Dispose();

                            // If the _canDeleteZip flag is true, then delete the zip
                            if (_canDeleteZip)
                            {

                                if (asset.Segments != null)
                                {
                                    foreach (GameInstallPackage segment in asset.Segments)
                                    {
                                        FileInfo fileInfo = new FileInfo(asset.PathOutput);
                                        fileInfo.IsReadOnly = false;
                                        fileInfo.Delete();
                                    }
                                }
                                else
                                {
                                    FileInfo fileInfo = new FileInfo(asset.PathOutput);
                                    fileInfo.IsReadOnly = false;
                                    fileInfo.Delete();
                                }
                            }
                        }
                        finally
                        {
                            zip.ExtractProgressChanged -= ZipProgressAdapter;
                            zip.Dispose();
                        }

                        _progressTotalCountCurrent++;
                    }
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

        public virtual bool IsPreloadCompleted()
        {
            return _gameVersionManager.GetGamePreloadZip().All(x =>
            {
                string name = Path.GetFileName(x.path);
                string path = Path.Combine(_gamePath, name);

                return File.Exists(path);
            });
        }

        public async Task MoveGameLocation()
        {

        }

        public async Task<bool> UninstallGame()
        {
            string GameFolder = ConverterTool.NormalizePath(_gamePath);

            switch (await Dialog_UninstallGame(_parentUI, GameFolder, _gamePreset.ZoneFullname))
            {
                case ContentDialogResult.Primary:
                    try
                    {
                        Directory.Delete(GameFolder, true);
                    }
                    catch (Exception ex)
                    {
                        LogWriteLine($"Failed while deleting the game folder: {GameFolder}\r\n{ex}", LogType.Error, true);
                    }
                    PageStatics._GameVersion.Reinitialize();
                    return true;
                default:
                    return false;
            }
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
            if (_gamePreset.CheckExistingGame())
            {
                switch (await Dialog_ExistingInstallation(_parentUI))
                {
                    // If action to migrate was taken, then update the game path (but don't save it to the config file)
                    case ContentDialogResult.Primary:
                        _gameVersionManager.UpdateGamePath(_gamePreset.ActualGameDataLocation.Replace('\\', '/'), false);
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
            if (_gamePreset.SteamGameID == null) return false;
            // Assign Steam ID
            int steamID = _gamePreset.SteamGameID ?? 0;

            // Try get the list of Steam Libs and Apps
            List<string> steamLibsList = SteamTool.GetSteamLibs();
            if (steamLibsList == null) return false;

            List<SteamTool.AppInfo> steamAppList = SteamTool.GetSteamApps(steamLibsList);
#nullable enable
            SteamTool.AppInfo? steamAppInfo = steamAppList.Where(x => x.Id == steamID).FirstOrDefault();

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
            if (_gamePreset.BetterHi3LauncherVerInfoReg == null) return false;

            // Try open BHI3L registry key
            // If the key doesn't exist, then return false
            RegistryKey? Key = Registry.CurrentUser.OpenSubKey("Software\\Bp\\Better HI3 Launcher");
            if (Key == null) return false;

            // Try get the key value
            // If the key also doesn't exist, then return false
            byte[]? keyValue = (byte[]?)Key?.GetValue(_gamePreset.BetterHi3LauncherVerInfoReg);
            if (keyValue == null) return false;

            BHI3LInfo? config;
            string value = "";
            try
            {
                // Try parsing the config
                value = Encoding.UTF8.GetString(keyValue);
                config = (BHI3LInfo?)JsonSerializer.Deserialize(value, typeof(BHI3LInfo), BHI3LInfoContext.Default);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Registry Value {_gamePreset.BetterHi3LauncherVerInfoReg}:\r\n{value}\r\n\r\nException:\r\n{ex}", LogType.Error, true);
                return false;
            }

            // Assign OutputPath to the path provided by the config
            if (config != null && config.game_info.installed
             && !string.IsNullOrEmpty(config.game_info.install_path))
            {
                FileInfo execPath = new FileInfo(Path.Combine(config.game_info.install_path, _gamePreset.GameExecutableName));
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
                        folder = Path.Combine(LauncherConfig.AppGameFolder, _gamePreset.ProfileName, _gamePreset.GameDirectoryName);
                        isChoosen = true;
                        break;
                    // If secondary, then show folder picker dialog to choose the folder
                    case ContentDialogResult.Secondary:
                        folder = await FileDialogNative.GetFolderPicker();

                        if (folder != null)
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
        protected virtual async Task TryAddResourceVersionList(RegionResourceVersion asset, List<GameInstallPackage> packageList)
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
                return;
            }
            LogWriteLine($"Adding general package: {package.Name} to the list (Hash: {package.HashString})", LogType.Default, true);
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        #endregion

        #region Private Methods - StartPackageInstallation
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
        private async Task InvokePackageDownloadRoutine(List<GameInstallPackage> packageList, CancellationToken token)
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

        private async Task RunPackageDownloadRoutine(GameInstallPackage package, CancellationToken token, int packageCount)
        {
            // Set the activity status
            _status.IsIncludePerFileIndicator = packageCount > 1;
            _status.ActivityStatus = string.Format("{0}: {1}", Lang._Misc.Downloading, string.Format(Lang._Misc.PerFromTo, _progressTotalCountCurrent, _progressTotalCount));
            LogWriteLine($"Downloading package URL {_progressTotalCountCurrent}/{_progressTotalCount} ({ConverterTool.SummarizeSizeSimple(package.Size)}): {package.URL}");

            // If the file exist or package size is unmatched,
            // then start downloading
            FileInfo packageOutInfo = new FileInfo(package.PathOutput);
            if (!packageOutInfo.Exists
              || packageOutInfo.Length != package.SizeDownloaded)
            {
                await _httpClient.Download(package.URL, package.PathOutput, _downloadThreadCount, false, token);
                _status.ActivityStatus = string.Format("{0}: {1}", Lang._Misc.Merging, string.Format(Lang._Misc.PerFromTo, _progressTotalCountCurrent, _progressTotalCount));
                UpdateStatus();
                _stopwatch.Stop();
                await _httpClient.Merge();
                _stopwatch.Start();
            }

            // Increment the total count
            _progressTotalCountCurrent++;
        }

        private async Task CheckExistingDownloadAsync(UIElement Content, List<GameInstallPackage> packageList)
        {
            // If the _progressTotalSizeCurrent has the size, then
            // display the reset or continue download dialog
            if (_progressTotalSizeCurrent > 0)
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

        private async Task CheckDriveFreeSpace(UIElement Content, List<GameInstallPackage> packageList)
        {
            // Get the information about the disk
            DriveInfo _DriveInfo = new DriveInfo(_gamePath);

            // Get the total of required size on every package
            long RequiredSpace = packageList.Sum(x => x.SizeRequired);
            long ExistingPackageSize = packageList.Sum(x => x.Segments != null ? x.Segments.Sum(y => GetExistingPartialDownloadLength(y.PathOutput, y.Size)) : GetExistingPartialDownloadLength(x.PathOutput, x.Size));

            // Get the total free space of the disk
            long DiskSpace = _DriveInfo.TotalFreeSpace;
            LogWriteLine($"Total free space required: {ConverterTool.SummarizeSizeSimple(RequiredSpace)} with {_DriveInfo.Name} remained free space: {ConverterTool.SummarizeSizeSimple(DiskSpace)}", LogType.Default, true);

            // Check if the disk space is insufficient, then show the dialog.
            if (DiskSpace < (RequiredSpace - ExistingPackageSize))
            {
                string errStr = $"Free Space on {_DriveInfo.Name} is sufficient! (Free space: {DiskSpace}, Req. Space: {RequiredSpace - ExistingPackageSize}, Drive: {_DriveInfo.Name})";
                LogWriteLine(errStr, LogType.Error, true);
                await Dialog_InsufficientDriveSpace(Content, DiskSpace, RequiredSpace - ExistingPackageSize, _DriveInfo.Name);
                throw new TaskCanceledException(errStr);
            }
        }

        private long GetExistingPartialDownloadLength(string fileOutput, long remoteSize)
        {
            // Try get the status of the full downloaded package output
            FileInfo fileInfo = new FileInfo(fileOutput);

            // If the full downloaded package output is already exist and the size is the same as remote,
            // then return the actual size
            if (fileInfo.Exists && fileInfo.Length != remoteSize)
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
        protected async Task TryGetPackageRemoteSize(GameInstallPackage asset, CancellationToken token)
        {
            asset.Size = await _httpClient.TryGetContentLengthAsync(asset.URL, token) ?? 0;

            LogWriteLine($"Package: [T: {asset.PackageType}] {asset.Name} has {ConverterTool.SummarizeSizeSimple(asset.Size)} in size with {ConverterTool.SummarizeSizeSimple(asset.SizeRequired)} of free space required", LogType.Default, true);
        }
        protected async Task TryGetSegmentedPackageRemoteSize(GameInstallPackage asset, CancellationToken token)
        {
            long totalSize = 0;
            for (int i = 0; i < asset.Segments.Count; i++)
            {
                long segmentSize = await _httpClient.TryGetContentLengthAsync(asset.Segments[i].URL, token) ?? 0;
                totalSize += segmentSize;
                asset.Segments[i].Size = segmentSize;
                LogWriteLine($"Package Segment: [T: {asset.PackageType}] {asset.Segments[i].Name} has {ConverterTool.SummarizeSizeSimple(segmentSize)} in size", LogType.Default, true);
            }

            asset.Size = totalSize;
            LogWriteLine($"Package Segment (count: {asset.Segments.Count}) has {ConverterTool.SummarizeSizeSimple(asset.Size)} in total size with {ConverterTool.SummarizeSizeSimple(asset.SizeRequired)} of free space required", LogType.Default, true);
        }
        #endregion

        #region Event Methods
        protected void UpdateProgressBase() => base.UpdateProgress();

        private async void ZipProgressAdapter(object sender, ExtractProgress e)
        {
            if (await base.CheckIfNeedRefreshStopwatch())
            {
                // Incrment current total size
                long lastSize = GetLastSize(e.totalExtractedSize);
                _progressTotalSizeCurrent += lastSize;

                // Assign per file size
                _progressPerFileSizeCurrent = e.totalExtractedSize;
                _progressPerFileSize = e.totalUncompressedSize;

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

                UpdateProgress();
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
