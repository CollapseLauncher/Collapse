using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.FileDialogCOM;
using CollapseLauncher.InstallManager;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher.Plugins;

#nullable enable
internal class PluginGameInstallWrapper : ProgressBase<PkgVersionProperties>, IGameInstallManager
{
    public event EventHandler? FlushingTrigger;

    public bool            IsRunning       { get; private set; }
    public DispatcherQueue DispatcherQueue { get; }

    private readonly IPlugin                   _plugin;
    private readonly PluginPresetConfigWrapper _pluginPresetConfig;
    private readonly IGameInstaller            _gameInstaller;

    private PluginGameVersionWrapper GameManager =>
        GameVersionManager as PluginGameVersionWrapper ?? throw new InvalidCastException("GameVersionManager is not PluginGameVersionWrapper");

    internal PluginGameInstallWrapper(UIElement parentUi, PluginPresetConfigWrapper pluginPresetConfig, PluginGameVersionWrapper pluginVersionManager)
        : base(parentUi, pluginVersionManager, pluginVersionManager.GameDirPath, null, null)
    {
        ParentUI        = parentUi ?? throw new ArgumentNullException(nameof(parentUi));
        IsRunning       = false;
        DispatcherQueue = ParentUI.DispatcherQueue;

        _plugin             = pluginPresetConfig.Plugin ?? throw new ArgumentNullException(nameof(pluginPresetConfig));
        _pluginPresetConfig = pluginPresetConfig ?? throw new ArgumentNullException(nameof(pluginPresetConfig));
        _gameInstaller      = pluginPresetConfig.PluginGameInstaller;

        IsSophonInUpdateMode = false;
    }

    public void CancelRoutine()
    {
        Token.Cancel();
        ResetAndCancelTokenSource();
    }

    private void ResetAndCancelTokenSource()
    {
        Token.Dispose();
        ResetStatusAndProgressProperty();
    }

    public void Dispose()
    {
        _gameInstaller.Free();
        GC.SuppressFinalize(this);
    }

    public async ValueTask<int> GetInstallationPath(bool isHasOnlyMigrateOption = false)
    {
        // Try get existing path
        string? existingPath = await GameManager.FindGameInstallationPath(string.Empty);

        // If the path is existed, 
        if (!string.IsNullOrEmpty(existingPath) && Directory.Exists(existingPath))
        {
            ContentDialogResult dialogResult = await SimpleDialogs.Dialog_MigrationChoiceDialog(existingPath,
                InnerLauncherConfig.GetGameTitleRegionTranslationString(_pluginPresetConfig.GameName, Locale.Lang._GameClientTitles) ?? _pluginPresetConfig.GameName,
                InnerLauncherConfig.GetGameTitleRegionTranslationString(_pluginPresetConfig.ZoneName, Locale.Lang._GameClientRegions) ?? _pluginPresetConfig.ZoneName,
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
        if (GameManager.IsGameInstalled())
        {
            // Return as completed and apply the config.
            return 0;
        }

        // Return as to continue to the next routine if the game isn't detected.
        return 1;
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
                    folder = await FileDialogHelper.GetRestrictedFolderPathDialog(Locale.Lang._Dialogs.FolderDialogTitle1);
                    isChosen = !string.IsNullOrEmpty(folder);
                    break;
                case ContentDialogResult.None:
                    return null;
            }
        }

        return folder;
    }

    public Task StartPackageDownload(bool skipDialog = false)
    {
        // NOP
        return Task.CompletedTask;
    }

    public ValueTask<int> StartPackageVerification(List<GameInstallPackage>? gamePackage = null)
    {
        // NOP
        return new ValueTask<int>(1);
    }

    public async Task StartPackageInstallation()
    {
        bool isUpdateMode = await GameManager.GetGameState() == GameInstallStateEnum.NeedsUpdate;

        Lock updateStatusLock = new Lock();
        ResetStatusAndProgressProperty();

        try
        {
            IsRunning = true;

            Status.IsProgressAllIndetermined     = true;
            Status.IsProgressPerFileIndetermined = true;
            Status.IsRunning                     = true;
            Status.IsIncludePerFileIndicator     = false;
            UpdateStatus();

            int stateCount = 0;
            int stateCountTotal = 0;

            int assetCount = 0;
            int assetCountTotal = 0;

            long lastDownloaded = 0;

            Guid cancelGuid = _plugin.RegisterCancelToken(Token.Token);
            await _gameInstaller.InitPluginComAsync(_plugin, Token.Token);

            InstallProgressDelegate      progressDelegate       = UpdateProgressCallback;
            InstallProgressStateDelegate progressStatusDelegate = UpdateStatusCallback;

            GameInstallerKind sizeInstallerKind = isUpdateMode ? GameInstallerKind.Update : GameInstallerKind.Install;
            long sizeToDownload = await _gameInstaller.GetGameSizeAsync(sizeInstallerKind, in cancelGuid).WaitFromHandle<long>();
            long sizeAlreadyDownloaded = await _gameInstaller.GetGameDownloadedSizeAsync(sizeInstallerKind, in cancelGuid).WaitFromHandle<long>();

            await EnsureDiskSpaceAvailability(GameManager.GameDirPath, sizeToDownload, sizeAlreadyDownloaded);

            Task routineTask = isUpdateMode ?
                _gameInstaller
                   .StartUpdateAsync(progressDelegate,
                                     progressStatusDelegate,
                                     _plugin.RegisterCancelToken(Token.Token))
                   .WaitFromHandle() :
                _gameInstaller
                   .StartInstallAsync(progressDelegate,
                                      progressStatusDelegate,
                                      _plugin.RegisterCancelToken(Token.Token))
                   .WaitFromHandle();

            await routineTask.ConfigureAwait(false);

            return;

            void UpdateProgressCallback(in InstallProgress delegateProgress)
            {
                using (updateStatusLock.EnterScope())
                {
                    stateCount = delegateProgress.StateCount;
                    stateCountTotal = delegateProgress.TotalStateToComplete;

                    assetCount = delegateProgress.DownloadedCount;
                    assetCountTotal = delegateProgress.TotalCountToDownload;

                    long downloadedBytes = delegateProgress.DownloadedBytes;
                    long downloadedBytesTotal = delegateProgress.TotalBytesToDownload;

                    long readDownload = delegateProgress.DownloadedBytes - lastDownloaded;
                    double currentSpeed = CalculateSpeed(readDownload);

                    Progress.ProgressAllEntryCountCurrent = assetCount;
                    Progress.ProgressAllEntryCountTotal = assetCountTotal;

                    Progress.ProgressAllSizeCurrent = downloadedBytes;
                    Progress.ProgressAllSizeTotal = downloadedBytesTotal;
                    Progress.ProgressAllSpeed = currentSpeed;

                    Progress.ProgressAllTimeLeft = ConverterTool
                       .ToTimeSpanRemain(downloadedBytesTotal, downloadedBytes, currentSpeed);

                    Progress.ProgressAllPercentage = ConverterTool.ToPercentage(downloadedBytesTotal, downloadedBytes);

                    lastDownloaded = downloadedBytes;

                    if (CheckIfNeedRefreshStopwatch())
                    {
                        return;
                    }

                    if (Status.IsProgressAllIndetermined)
                    {
                        Status.IsProgressAllIndetermined     = false;
                        Status.IsProgressPerFileIndetermined = false;
                        UpdateStatus();
                    }

                    UpdateProgress();
                }
            }

            void UpdateStatusCallback(InstallProgressState delegateState)
            {
                using (updateStatusLock.EnterScope())
                {
                    string stateString = delegateState switch
                    {
                        InstallProgressState.Removing => string.Format("Deleting" + ": " + Locale.Lang._Misc.PerFromTo, stateCount, stateCountTotal),
                        InstallProgressState.Idle => Locale.Lang._Misc.Idle,
                        InstallProgressState.Install => string.Format(Locale.Lang._Misc.Extracting + ": " + Locale.Lang._Misc.PerFromTo, stateCount, stateCountTotal),
                        InstallProgressState.Verify or InstallProgressState.Preparing => string.Format(Locale.Lang._Misc.Verifying + ": " + Locale.Lang._Misc.PerFromTo, stateCount, stateCountTotal),
                        _ => string.Format((!isUpdateMode ? Locale.Lang._Misc.Downloading : Locale.Lang._Misc.Updating) + ": " + Locale.Lang._Misc.PerFromTo, stateCount, stateCountTotal)
                    };

                    Status.ActivityStatus = stateString;
                    Status.ActivityAll = string.Format(Locale.Lang._Misc.PerFromTo, assetCount, assetCountTotal);

                    UpdateStatus();
                }
            }
        }
        catch (OperationCanceledException) when (Token.IsCancellationRequested)
        {
            Status.IsCanceled = true;
            throw;
        }
        finally
        {
            Status.IsCompleted = true;
            IsRunning = false;
            UpdateStatus();
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

    public ValueTask<bool> UninstallGame()
    {
        // NOP
        return new ValueTask<bool>(false);
    }

    public void Flush()
    {
        FlushingTrigger?.Invoke(this, EventArgs.Empty);
    }

    public ValueTask<bool> IsPreloadCompleted(CancellationToken token = default)
    {
        // NOP
        return new ValueTask<bool>(true);
    }

    public ValueTask<bool> TryShowFailedDeltaPatchState()
    {
        // NOP
        return new ValueTask<bool>(false);
    }

    public ValueTask<bool> TryShowFailedGameConversionState()
    {
        // NOP
        return new ValueTask<bool>(false);
    }

    public ValueTask CleanUpGameFiles(bool withDialog = true)
    {
        // NOP
        return new ValueTask();
    }

    public void UpdateCompletenessStatus(CompletenessStatus status)
    {
        // NOP
    }

    public bool StartAfterInstall { get; set; }
    public bool IsUseSophon => false;
}
