using CollapseLauncher.InstallManager;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper.Plugin.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher.Plugins;

#nullable enable
internal class PluginGameInstallWrapper : IGameInstallManager
{
    public event EventHandler<TotalPerFileProgress>? ProgressChanged;
    public event EventHandler<TotalPerFileStatus>?   StatusChanged;
    public event EventHandler?                       FlushingTrigger;

    public bool      IsRunning { get; }
    public UIElement ParentUI  { get; }

    private IPlugin                   _plugin;
    private PluginPresetConfigWrapper _pluginPresetConfig;

    internal PluginGameInstallWrapper(UIElement parentUi, PluginPresetConfigWrapper pluginPresetConfig)
    {
        ParentUI  = parentUi ?? throw new ArgumentNullException(nameof(parentUi));
        IsRunning = false;

        _plugin             = pluginPresetConfig.Plugin ?? throw new ArgumentNullException(nameof(pluginPresetConfig));
        _pluginPresetConfig = pluginPresetConfig ?? throw new ArgumentNullException(nameof(pluginPresetConfig));
    }

    public void CancelRoutine()
    {
        // NOP
    }

    public void Dispatch(DispatcherQueueHandler handler, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        // NOP
    }

    public Task DispatchAsync(Action handler, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        // NOP
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // NOP
    }

    public ValueTask<int> GetInstallationPath(bool isHasOnlyMigrateOption = false)
    {
        // NOP
        return new ValueTask<int>(0);
    }

    public Task StartPackageDownload(bool skipDialog = false)
    {
        // NOP
        return Task.CompletedTask;
    }

    public ValueTask<int> StartPackageVerification(List<GameInstallPackage> gamePackage = null)
    {
        // NOP
        return new ValueTask<int>(0);
    }

    public Task StartPackageInstallation()
    {
        // NOP
        return Task.CompletedTask;
    }

    public void ApplyGameConfig(bool forceUpdateToLatest = false)
    {
        // NOP
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
