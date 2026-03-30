using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.FileDialogCOM;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.InstallManager;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.ManagedTools;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher.Plugins;

#nullable enable
internal partial class PluginGameInstallWrapper : ProgressBase<PkgVersionProperties>, IGameInstallManager
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private unsafe delegate void PerFileProgressCallbackNative(InstallPerFileProgress* progress);

    private struct InstallProgressProperty
    {
        public int  StateCount;
        public int  StateCountTotal;
        public int  AssetCount;
        public int  AssetCountTotal;
        public long LastDownloaded;
        public bool IsUpdateMode;
    }

    public event EventHandler? FlushingTrigger;

    public bool IsRunning
    {
        get;
        private set;
    }

    public override string GamePath
    {
        get => GameVersionManager.GameDirPath;
        set => GameVersionManager.GameDirPath = value;
    }

    private readonly IPlugin                   _plugin;
    private readonly PluginPresetConfigWrapper _pluginPresetConfig;
    private readonly IGameInstaller            _gameInstaller;

    private readonly Lock                         _updateStatusLock;
    private readonly InstallProgressDelegate      _updateProgressDelegate;
    private readonly InstallProgressStateDelegate _updateProgressStatusDelegate;
    private          InstallProgressProperty      _updateProgressProperty;

    private PerFileProgressCallbackNative? _perFileProgressDelegate;
    private GCHandle                       _perFileProgressGcHandle;
    private bool                           _hasPerFileProgress;

    private PluginGameVersionWrapper GameManager =>
        GameVersionManager as PluginGameVersionWrapper ?? throw new InvalidCastException("GameVersionManager is not PluginGameVersionWrapper");

    internal PluginGameInstallWrapper(
        UIElement                 parentUi,
        PluginPresetConfigWrapper pluginPresetConfig,
        PluginGameVersionWrapper  pluginVersionManager)
        : base(parentUi,
               pluginVersionManager,
               null,
               null)
    {
        IsRunning       = false;

        _pluginPresetConfig = pluginPresetConfig ?? throw new ArgumentNullException(nameof(pluginPresetConfig));
        _plugin             = pluginPresetConfig.Plugin ?? throw new ArgumentNullException(nameof(pluginPresetConfig));
        _gameInstaller      = pluginPresetConfig.PluginGameInstaller;

        IsSophonInUpdateMode = false;

        _updateStatusLock             = new Lock();
        _updateProgressDelegate       = UpdateProgressCallback;
        _updateProgressStatusDelegate = UpdateStatusCallback;
        _updateProgressProperty       = new InstallProgressProperty();
    }

    public void CancelRoutine()
    {
        Token?.Cancel();
        ResetAndCancelTokenSource();
    }

    private void ResetAndCancelTokenSource()
    {
        Token?.Dispose();
        ResetStatusAndProgressProperty();
    }

    public void Dispose()
    {
        UnregisterPerFileProgressCallback();
        _gameInstaller.Free();
        GC.SuppressFinalize(this);
    }

    private unsafe void TryRegisterPerFileProgressCallback()
    {
        if (_perFileProgressGcHandle.IsAllocated)
            return;

        _perFileProgressDelegate = OnPerFileProgressCallback;
        _perFileProgressGcHandle = GCHandle.Alloc(_perFileProgressDelegate);
        nint callbackPtr = Marshal.GetFunctionPointerForDelegate(_perFileProgressDelegate);

        _hasPerFileProgress = _pluginPresetConfig.PluginInfo.EnablePerFileProgressCallback(callbackPtr);

        if (!_hasPerFileProgress)
        {
            _perFileProgressGcHandle.Free();
            _perFileProgressDelegate = null;
        }
    }

    private void UnregisterPerFileProgressCallback()
    {
        if (!_hasPerFileProgress)
            return;

        _pluginPresetConfig.PluginInfo.DisablePerFileProgressCallback();
        _hasPerFileProgress = false;

        if (_perFileProgressGcHandle.IsAllocated)
            _perFileProgressGcHandle.Free();
        _perFileProgressDelegate = null;
    }

    private unsafe void OnPerFileProgressCallback(InstallPerFileProgress* progress)
    {
        // IMPORTANT: Called from NativeAOT plugin across reverse P/Invoke. Must never throw.
        try
        {
            long downloaded = progress->PerFileDownloadedBytes;
            long total = progress->PerFileTotalBytes;

            Progress.ProgressPerFileSizeCurrent = downloaded;
            Progress.ProgressPerFileSizeTotal   = total;
            Progress.ProgressPerFilePercentage  = total > 0
                ? ConverterTool.ToPercentage(total, downloaded)
                : 0;
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[PluginGameInstallWrapper::OnPerFileProgressCallback] Exception (swallowed):\r\n{ex}",
                                LogType.Error, true);
        }
    }

    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async ValueTask<int> GetInstallationPath(bool isHasOnlyMigrateOption = false)
    {
        // Try get existing path
        string? existingPath = await GameManager.FindGameInstallationPath(string.Empty);

        // If the path is existed, 
        if (!string.IsNullOrEmpty(existingPath) && Directory.Exists(existingPath))
        {
            ContentDialogResult dialogResult = await SimpleDialogs.Dialog_MigrationChoiceDialog(existingPath,
                MetadataHelper.GetTranslatedTitle(_pluginPresetConfig.GameName),
                MetadataHelper.GetTranslatedRegion(_pluginPresetConfig.ZoneName),
                nameof(MigrateFromLauncherType.Plugin),
                MigrateFromLauncherType.Plugin,
                true);

            // Return as success (0) and update the game path.
            if (dialogResult == ContentDialogResult.Primary)
            {
                GameManager.GameDirPath = existingPath;
                GameManager.Reinitialize();
                return 0;
            }
        }

        if (isHasOnlyMigrateOption)
        {
            return -1;
        }

        // Otherwise, ask for new game path
        string? installPath = await AskGameFolderDialog();
        if (string.IsNullOrEmpty(installPath))
        {
            // Return as cancelled if it's empty
            return -1;
        }

        // Register the game path without saving it first.
        // This to ensure that the plugin is aware of the installation path to be used.
        // Then tell the plugin to reinitialize the game manager.
        GameManager.GameDirPath = installPath;
        GameManager.Reinitialize();

        // Check if the game is installed after the game manager is reinitialized.
        return GameManager.IsGameInstalled() ?
            // Return as completed and apply the config.
            0 :
            // Return as to continue to the next routine if the game isn't detected.
            1;
    }

    // Copied from InstallManagerBase
    private async Task<string?> AskGameFolderDialog()
    {
        // Set initial folder variable as empty
        string folder = "";

        // Do loop and break if choice is already been done
        bool isChosen = false;
        while (!isChosen)
        {
            // Show dialog
            switch (await SimpleDialogs.Dialog_InstallationLocation())
            {
                // If primary button is clicked, then set folder with the default path
                case ContentDialogResult.Primary:
                    if (_pluginPresetConfig is not null)
                    {
                        folder = Path.Combine(LauncherConfig.AppGameFolder,
                                              _pluginPresetConfig.ProfileName,
                                              _pluginPresetConfig.GameDirectoryName);
                    }

                    isChosen = true;
                    break;
                // If secondary, then show folder picker dialog to choose the folder
                case ContentDialogResult.Secondary:
                    folder = await FileDialogHelper.GetRestrictedFolderPathDialog(Locale.Current.Lang?._Dialogs?.FolderDialogTitle1);
                    isChosen = !string.IsNullOrEmpty(folder);
                    break;
                case ContentDialogResult.None:
                    return null;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return folder;
    }

    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task StartPackageDownload(bool skipDialog = false)
    {
        ResetStatusAndProgressProperty();
        bool isSuccess = false;

        try
        {
            IsRunning = true;

            Status.IsProgressAllIndetermined     = true;
            Status.IsProgressPerFileIndetermined = true;
            Status.IsRunning                     = true;
            Status.IsIncludePerFileIndicator     = false;
            UpdateStatus();

            Logger.LogWriteLine("[PluginGameInstallWrapper::StartPackageDownload] Step 1/5: InitPluginComAsync...", LogType.Debug, true);
            await _gameInstaller.InitPluginComAsync(_plugin, Token!.Token);

            Logger.LogWriteLine("[PluginGameInstallWrapper::StartPackageDownload] Step 2/5: GetGameSizeAsync...", LogType.Debug, true);
            Guid cancelGuid = _plugin.RegisterCancelToken(Token.Token);

            _gameInstaller.GetGameSizeAsync(GameInstallerKind.Preload, in cancelGuid, out nint asyncSize);
            long sizeToDownload = await asyncSize.AsTask<long>();
            Logger.LogWriteLine($"[PluginGameInstallWrapper::StartPackageDownload] Size to download: {sizeToDownload}", LogType.Debug, true);

            Logger.LogWriteLine("[PluginGameInstallWrapper::StartPackageDownload] Step 3/5: GetGameDownloadedSizeAsync...", LogType.Debug, true);
            _gameInstaller.GetGameDownloadedSizeAsync(GameInstallerKind.Preload, in cancelGuid, out nint asyncDownloaded);
            long sizeAlreadyDownloaded = await asyncDownloaded.AsTask<long>();
            Logger.LogWriteLine($"[PluginGameInstallWrapper::StartPackageDownload] Already downloaded: {sizeAlreadyDownloaded}", LogType.Debug, true);

            Logger.LogWriteLine("[PluginGameInstallWrapper::StartPackageDownload] Step 4/5: EnsureDiskSpaceAvailability...", LogType.Debug, true);
            await EnsureDiskSpaceAvailability(GameManager.GameDirPath, sizeToDownload, sizeAlreadyDownloaded);

            Logger.LogWriteLine("[PluginGameInstallWrapper::StartPackageDownload] Step 5/5: StartPreloadAsync...", LogType.Debug, true);
            TryRegisterPerFileProgressCallback();
            Status.IsIncludePerFileIndicator = _hasPerFileProgress;
            UpdateStatus();

            _gameInstaller.StartPreloadAsync(
                _updateProgressDelegate,
                _updateProgressStatusDelegate,
                _plugin.RegisterCancelToken(Token.Token),
                out nint asyncPreload);

            Logger.LogWriteLine("[PluginGameInstallWrapper::StartPackageDownload] Awaiting preload task...", LogType.Debug, true);
            await asyncPreload.AsTask().ConfigureAwait(false);
            Logger.LogWriteLine("[PluginGameInstallWrapper::StartPackageDownload] Preload task completed.", LogType.Debug, true);
            isSuccess = true;
        }
        catch (OperationCanceledException)
        {
            Logger.LogWriteLine("[PluginGameInstallWrapper::StartPackageDownload] Cancelled by user.", LogType.Warning, true);
            Status.IsCanceled = true;
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[PluginGameInstallWrapper::StartPackageDownload] Preload failed:\r\n{ex}", LogType.Error, true);
            SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
        }
        finally
        {
            Logger.LogWriteLine("[PluginGameInstallWrapper::StartPackageDownload] Entering finally block.", LogType.Debug, true);
            UnregisterPerFileProgressCallback();
            Status.IsCompleted = isSuccess;
            IsRunning          = false;
            UpdateStatus();
        }
    }

    public ValueTask<int> StartPackageVerification(List<GameInstallPackage>? gamePackage = null)
    {
        // NOP — preload download includes verification internally
        return new ValueTask<int>(1);
    }

    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task StartPackageInstallation()
    {
        _updateProgressProperty.IsUpdateMode = await GameManager.GetGameState() == GameInstallStateEnum.NeedsUpdate;

        ResetStatusAndProgressProperty();
        bool isSuccess = false;

        try
        {
            IsRunning = true;

            Status.IsProgressAllIndetermined     = true;
            Status.IsProgressPerFileIndetermined = true;
            Status.IsRunning                     = true;
            Status.IsIncludePerFileIndicator     = false;
            UpdateStatus();

            Guid cancelGuid = _plugin.RegisterCancelToken(Token!.Token);
            await _gameInstaller.InitPluginComAsync(_plugin, Token.Token);

            GameInstallerKind sizeInstallerKind = _updateProgressProperty.IsUpdateMode ? GameInstallerKind.Update : GameInstallerKind.Install;

            _gameInstaller.GetGameSizeAsync(sizeInstallerKind, in cancelGuid, out nint asyncGetGameSizeResult);
            long sizeToDownload = await asyncGetGameSizeResult.AsTask<long>();

            _gameInstaller.GetGameDownloadedSizeAsync(sizeInstallerKind, in cancelGuid, out nint asyncGetDownloadedSizeResult);
            long sizeAlreadyDownloaded = await asyncGetDownloadedSizeResult.AsTask<long>();

            await EnsureDiskSpaceAvailability(GameManager.GameDirPath, sizeToDownload, sizeAlreadyDownloaded);

            TryRegisterPerFileProgressCallback();
            Status.IsIncludePerFileIndicator = _hasPerFileProgress;
            UpdateStatus();

            Task routineTask;
            if (_updateProgressProperty.IsUpdateMode)
            {
                _gameInstaller
                   .StartUpdateAsync(_updateProgressDelegate,
                                     _updateProgressStatusDelegate,
                                     _plugin.RegisterCancelToken(Token.Token),
                                     out nint asyncStartUpdateResult);

                routineTask = asyncStartUpdateResult.AsTask();
            }
            else
            {
                _gameInstaller
                   .StartInstallAsync(_updateProgressDelegate,
                                      _updateProgressStatusDelegate,
                                      _plugin.RegisterCancelToken(Token.Token),
                                      out nint asyncStartInstallResult);

                routineTask = asyncStartInstallResult.AsTask();
            }

            await routineTask.ConfigureAwait(false);
            isSuccess = true;
        }
        catch (OperationCanceledException)
        {
            Status.IsCanceled = true;
            throw;
        }
        finally
        {
            UnregisterPerFileProgressCallback();
            Status.IsCompleted = isSuccess;
            IsRunning = false;
            UpdateStatus();
        }
    }

    private void UpdateProgressCallback(in InstallProgress delegateProgress)
    {
        // IMPORTANT: This callback is invoked via a function pointer from the NativeAOT plugin.
        // Any unhandled exception here crosses the reverse P/Invoke boundary and causes a
        // FailFast (STATUS_FAIL_FAST_EXCEPTION / exit code -1073741189). Must never throw.
        try
        {
            using (_updateStatusLock.EnterScope())
            {
                _updateProgressProperty.StateCount      = delegateProgress.StateCount;
                _updateProgressProperty.StateCountTotal = delegateProgress.TotalStateToComplete;

                _updateProgressProperty.AssetCount      = delegateProgress.DownloadedCount;
                _updateProgressProperty.AssetCountTotal = delegateProgress.TotalCountToDownload;

                long downloadedBytes = delegateProgress.DownloadedBytes;
                long downloadedBytesTotal = delegateProgress.TotalBytesToDownload;

                long   readDownload = delegateProgress.DownloadedBytes - _updateProgressProperty.LastDownloaded;
                double currentSpeed = CalculateSpeed(readDownload);

                Progress.ProgressAllSizeCurrent = downloadedBytes;
                Progress.ProgressAllSizeTotal = downloadedBytesTotal;
                Progress.ProgressAllSpeed = currentSpeed;

                if (_hasPerFileProgress)
                {
                    // V1Ext_Update5: per-file bytes/percentage come from OnPerFileProgressCallback.
                    // We only set the speed here (overall throughput is the meaningful metric).
                    Progress.ProgressPerFileSpeed = currentSpeed;
                }
                else
                {
                    // Fallback: mirror aggregate values into per-file fields for older plugins.
                    Progress.ProgressPerFileSizeCurrent = downloadedBytes;
                    Progress.ProgressPerFileSizeTotal   = downloadedBytesTotal;
                    Progress.ProgressPerFileSpeed       = currentSpeed;
                    Progress.ProgressPerFilePercentage  = downloadedBytesTotal > 0
                        ? ConverterTool.ToPercentage(downloadedBytesTotal, downloadedBytes)
                        : 0;
                }

                Progress.ProgressAllTimeLeft = downloadedBytesTotal > 0 && currentSpeed > 0
                    ? ConverterTool.ToTimeSpanRemain(downloadedBytesTotal, downloadedBytes, currentSpeed)
                    : TimeSpan.Zero;

                Progress.ProgressAllPercentage = downloadedBytesTotal > 0
                    ? ConverterTool.ToPercentage(downloadedBytesTotal, downloadedBytes)
                    : 0;

                _updateProgressProperty.LastDownloaded = downloadedBytes;

                if (!CheckIfNeedRefreshStopwatch())
                {
                    return;
                }

                if (Status.IsProgressAllIndetermined)
                {
                    Status.IsProgressAllIndetermined = false;
                    Status.IsProgressPerFileIndetermined = false;
                    UpdateStatus();
                }

                UpdateProgress();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[PluginGameInstallWrapper::UpdateProgressCallback] Exception (swallowed to prevent FailFast):\r\n{ex}", LogType.Error, true);
        }
    }

    private void UpdateStatusCallback(InstallProgressState delegateState)
    {
        // IMPORTANT: Same reverse P/Invoke boundary guard as UpdateProgressCallback above.
        try
        {
            using (_updateStatusLock.EnterScope())
            {
                string stateString = delegateState switch
                {
                    InstallProgressState.Removing => string.Format("Deleting" + ": " + Locale.Current.Lang._Misc.PerFromTo, _updateProgressProperty.StateCount, _updateProgressProperty.StateCountTotal),
                    InstallProgressState.Idle => Locale.Current.Lang._Misc.Idle,
                    InstallProgressState.Install => string.Format(Locale.Current.Lang._Misc.Extracting + ": " + Locale.Current.Lang._Misc.PerFromTo, _updateProgressProperty.StateCount, _updateProgressProperty.StateCountTotal),
                    InstallProgressState.Verify or InstallProgressState.Preparing => string.Format(Locale.Current.Lang._Misc.Verifying + ": " + Locale.Current.Lang._Misc.PerFromTo, _updateProgressProperty.StateCount, _updateProgressProperty.StateCountTotal),
                    _ => string.Format((!_updateProgressProperty.IsUpdateMode ? Locale.Current.Lang._Misc.Downloading : Locale.Current.Lang._Misc.Updating) + ": " + Locale.Current.Lang._Misc.PerFromTo, _updateProgressProperty.StateCount, _updateProgressProperty.StateCountTotal)
                };

                Status.ActivityStatus = stateString;
                Status.ActivityAll = string.Format(Locale.Current.Lang._Misc.PerFromTo, _updateProgressProperty.AssetCount, _updateProgressProperty.AssetCountTotal);

                UpdateStatus();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[PluginGameInstallWrapper::UpdateStatusCallback] Exception (swallowed to prevent FailFast):\r\n{ex}", LogType.Error, true);
        }
    }

    private static async Task EnsureDiskSpaceAvailability(string gamePath, long sizeToDownload, long sizeAlreadyDownloaded)
    {
        long sizeRemainedToDownload = sizeToDownload - sizeAlreadyDownloaded;

        // Push log regarding how much size remained to download
        Logger.LogWriteLine($"Size remained to download: {ConverterTool.SummarizeSizeSimple(sizeRemainedToDownload)}.",
                            LogType.Default, true);

        // Get the information about the disk
        DriveInfo driveInfo = new DriveInfo(gamePath);

        // Push log regarding disk space
        Logger.LogWriteLine($"Total free space remained on disk: {driveInfo.Name}: {ConverterTool.SummarizeSizeSimple(driveInfo.TotalFreeSpace)}.",
                            LogType.Default, true);

        // If the space is insufficient, then show the dialog and throw
        if (sizeRemainedToDownload > driveInfo.TotalFreeSpace)
        {
            string errStr = $"Free Space on {driveInfo.Name} is not sufficient! " +
                            $"(Free space: {ConverterTool.SummarizeSizeSimple(driveInfo.TotalFreeSpace)}, Req. Space: {ConverterTool.SummarizeSizeSimple(sizeRemainedToDownload)} (Total: {ConverterTool.SummarizeSizeSimple(sizeToDownload)}), " +
                            $"Drive: {driveInfo.Name})";
            await SimpleDialogs.Dialog_InsufficientDriveSpace(driveInfo.TotalFreeSpace,
                                                              sizeRemainedToDownload, driveInfo.Name);

            // Push log for the disk space error
            Logger.LogWriteLine(errStr, LogType.Error, true);
            throw new TaskCanceledException(errStr);
        }
    }

    public void ApplyGameConfig(bool forceUpdateToLatest = false)
    {
        GameManager.UpdateGamePath();

        if (forceUpdateToLatest)
        {
            GameManager.UpdateGameVersionToLatest();
        }
    }

    public ValueTask<bool> MoveGameLocation()
    {
        // NOP
        return new ValueTask<bool>(false);
    }

    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async ValueTask<bool> UninstallGame()
    {
        if (!ComMarshal<IGameInstaller>.TryCastComObjectAs(_gameInstaller,
                                                           out IGameUninstaller? asUninstaller,
                                                           out Exception? castEx))
        {
            Logger.LogWriteLine($"The current plugin interface doesn't implement IGameUninstaller. Function will not be called!\r\n{castEx}", LogType.Error, true);
            return false;
        }

        try
        {
            await asUninstaller.InitPluginComAsync(_plugin, CancellationToken.None);
            asUninstaller.UninstallAsync(_plugin.RegisterCancelToken(CancellationToken.None), out nint asyncUninstallResult);
            await asyncUninstallResult.AsTask();
            return true;
        }
        catch (Exception ex)
        {
            SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
            Logger.LogWriteLine($"An error has occurred while performing game uninstaller from the plugin.\r\n{ex}", LogType.Error, true);
            return false;
        }
    }

    public void Flush() => FlushingTrigger?.Invoke(this, EventArgs.Empty);

    public async ValueTask<bool> IsPreloadCompleted(CancellationToken token = default)
    {
        try
        {
            await _gameInstaller.InitPluginComAsync(_plugin, token);

            Guid cancelGuid = _plugin.RegisterCancelToken(token);

            _gameInstaller.GetGameSizeAsync(GameInstallerKind.Preload, in cancelGuid, out nint asyncTotal);
            long totalSize = await asyncTotal.AsTask<long>();

            if (totalSize <= 0)
                return false;

            _gameInstaller.GetGameDownloadedSizeAsync(GameInstallerKind.Preload, in cancelGuid, out nint asyncDownloaded);
            long downloadedSize = await asyncDownloaded.AsTask<long>();

            return downloadedSize >= totalSize;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[PluginGameInstallWrapper::IsPreloadCompleted] Error checking preload status:\r\n{ex}", LogType.Error, true);
            return false;
        }
    }

    // TODO:
    // Might consider delta-patch to be supported via Plugin. But that's for future.
    public ValueTask<bool> TryShowFailedDeltaPatchState()
    {
        // NOP
        return new ValueTask<bool>(false);
    }

    // Intended to be NOP. The plugin wouldn't have feature to show game conversion failure status.
    public ValueTask<bool> TryShowFailedGameConversionState()
    {
        // NOP
        return new ValueTask<bool>(false);
    }

    // TODO:
    // Implement this after WuWa Plugin implementation is completed
    public ValueTask CleanUpGameFiles(bool withDialog = true)
    {
        // NOP
        return new ValueTask();
    }

    // TODO:
    // Implement this after WuWa Plugin implementation is completed
    public void UpdateCompletenessStatus(CompletenessStatus status)
    {
        // NOP
    }

    public PostInstallBehaviour PostInstallBehaviour { get; set; } = PostInstallBehaviour.Nothing;
    public bool                 StartAfterInstall    { get; set; }
    public bool                 IsUseSophon          => false;
}
