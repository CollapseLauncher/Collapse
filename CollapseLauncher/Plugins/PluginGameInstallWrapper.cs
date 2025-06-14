using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.FileDialogCOM;
using CollapseLauncher.InstallManager;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility;
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
internal class PluginGameInstallWrapper : IGameInstallManager
{
    public event EventHandler<TotalPerFileProgress>? ProgressChanged;
    public event EventHandler<TotalPerFileStatus>?   StatusChanged;
    public event EventHandler?                       FlushingTrigger;

    public bool                    IsRunning         { get; }
    public UIElement               ParentUI          { get; }
    public DispatcherQueue         DispatcherQueue   { get; }
    public CancellationTokenSource CancelTokenSource { get; set; }

    private readonly IPlugin                   _plugin;
    private readonly PluginPresetConfigWrapper _pluginPresetConfig;
    private readonly PluginGameVersionWrapper  _gameManager;
    private readonly IGameInstaller            _gameInstaller;

    internal PluginGameInstallWrapper(UIElement parentUi, PluginPresetConfigWrapper pluginPresetConfig, PluginGameVersionWrapper pluginVersionManager)
    {
        ParentUI        = parentUi ?? throw new ArgumentNullException(nameof(parentUi));
        IsRunning       = false;
        DispatcherQueue = ParentUI.DispatcherQueue;

        _plugin             = pluginPresetConfig.Plugin ?? throw new ArgumentNullException(nameof(pluginPresetConfig));
        _pluginPresetConfig = pluginPresetConfig ?? throw new ArgumentNullException(nameof(pluginPresetConfig));

        _gameManager   = pluginVersionManager;
        _gameInstaller = pluginPresetConfig.PluginGameInstaller;

        CancelTokenSource = new CancellationTokenSource();
    }

    public void CancelRoutine() => ResetAndCancelTokenSource();

    private void ResetAndCancelTokenSource()
    {
        CancelTokenSource.Dispose();
        CancelTokenSource = new CancellationTokenSource();
    }

    public void Dispatch(DispatcherQueueHandler handler, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            handler();
            return;
        }

        DispatcherQueue.TryEnqueue(priority, handler);
    }

    public Task DispatchAsync(Action handler, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        => Task.Factory.StartNew(() => Dispatch(() => handler(), priority));

    public void Dispose()
    {
        _gameInstaller.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask<int> GetInstallationPath(bool isHasOnlyMigrateOption = false)
    {
        // Try get existing path
        string? existingPath = await _gameManager.FindGameInstallationPath(string.Empty);

        // If the path is existed, 
        if (!string.IsNullOrEmpty(existingPath) && Directory.Exists(existingPath))
        {
            ContentDialogResult dialogResult = await SimpleDialogs.Dialog_MigrationChoiceDialog(existingPath,
                InnerLauncherConfig.GetGameTitleRegionTranslationString(_pluginPresetConfig.GameName, Locale.Lang._GameClientTitles) ?? _pluginPresetConfig.GameName,
                InnerLauncherConfig.GetGameTitleRegionTranslationString(_pluginPresetConfig.ZoneName, Locale.Lang._GameClientRegions) ?? _pluginPresetConfig.ZoneName,
                nameof(MigrateFromLauncherType.Unknown),
                MigrateFromLauncherType.Unknown,
                false);

            // Return as success (0) and update the game path.
            if (dialogResult == ContentDialogResult.Primary)
            {
                _gameManager.UpdateGamePath(existingPath);
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
        _gameManager.GameDirPath = installPath;
        _gameManager.Reinitialize();

        // Check if the game is installed after the game manager is reinitialized.
        if (_gameManager.IsGameInstalled())
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
        bool isUpdateMode = !_gameManager.IsGameVersionMatch();

        Guid cancelGuid = _plugin.RegisterCancelToken(CancelTokenSource.Token);

        await _gameInstaller.InitPluginComAsync(_plugin, CancelTokenSource.Token);

        GameInstallerKind sizeInstallerKind = isUpdateMode ? GameInstallerKind.Update : GameInstallerKind.Install;
        long sizeToDownload = await _gameInstaller.GetGameSizeAsync(sizeInstallerKind, in cancelGuid).WaitFromHandle<long>();

        // TODO: Check remained size
        Task routineTask = isUpdateMode ?
            _gameInstaller
               .StartUpdateAsync(UpdateProgress,
                                 UpdateStatus,
                                 _plugin.RegisterCancelToken(CancelTokenSource.Token))
               .WaitFromHandle() :
            _gameInstaller
               .StartInstallAsync(UpdateProgress,
                                  UpdateStatus,
                                  _plugin.RegisterCancelToken(CancelTokenSource.Token))
               .WaitFromHandle();

        await routineTask.ConfigureAwait(false);
    }

    void UpdateProgress(in InstallProgress progress)
    {
    }

    void UpdateStatus(in InstallProgressState state)
    {
    }

    public void ApplyGameConfig(bool forceUpdateToLatest = false)
    {
        _gameManager.UpdateGamePath();

        if (forceUpdateToLatest)
        {
            _gameManager.UpdateGameVersionToLatest();
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
        // NOP
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
    public bool IsSophonInUpdateMode => false;
}
