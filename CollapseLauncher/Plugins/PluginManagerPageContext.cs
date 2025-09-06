using CollapseLauncher.Extension;
using CollapseLauncher.Pages;
using CommunityToolkit.Mvvm.Input;
using Hi3Helper;
using Hi3Helper.Plugin.Core.Update;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WinRT;

#nullable enable
namespace CollapseLauncher.Plugins;

[GeneratedBindableCustomProperty]
public partial class PluginManagerPageContext : INotifyPropertyChanged
{
    internal ObservableCollection<PluginInfo> PluginCollection { get; } = CreateNewCollectionFromPluginManager();
    public event PropertyChangedEventHandler? PropertyChanged = delegate { };

    internal bool IsChanged
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    internal int ListViewSelectedItemsCountUninstalled
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    internal int ListViewSelectedItemsCountRestored
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    internal int ListViewSelectedItemsCountEnabled
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    internal int ListViewSelectedItemsCountDisabled
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    internal bool IsUpdateCheckOnProgress
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    internal bool IsUpdateOnProgress
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    internal bool IsUpdateAllShow
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsEnableAutoUpdate
    {
        get => field = LauncherConfig.GetAppConfigValue("IsEnablePluginAutoUpdate");
        set
        {
            if (field == value)
            {
                return;
            }
            LauncherConfig.SetAndSaveConfigValue("IsEnablePluginAutoUpdate", field = value);
            OnPropertyChanged();
        }
    }

    public PluginManagerPageContext()
    {
        PluginCollection.CollectionChanged +=  OnChangeUpdatePluginCollection;
    }

    private static ObservableCollection<PluginInfo> CreateNewCollectionFromPluginManager()
    {
        List<PluginInfo> backedList = PluginManager.PluginInstances.Values.ToList();
        foreach (PluginInfo pluginInfo in backedList)
        {
            pluginInfo.PropertyChanged += OnPluginInfoPropertyChanged;
        }

        ObservableCollection<PluginInfo> newCollection = [];
        ObservableCollectionExtension<PluginInfo>.GetBackedCollectionList(newCollection) = backedList;
        return newCollection;
    }

    private static void OnPluginInfoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Update Property based on all PluginInfo instance
        bool isAnyUpdateCheckOnProgress = PluginManager.PluginInstances.Values.Any(x => x.IsUpdateCheckInProgress);
        bool isAnyUpdateOnProgress      = PluginManager.PluginInstances.Values.Any(x => x.IsUpdateInProgress);
        bool isAnyUpdateCompleted       = PluginManager.PluginInstances.Values.Any(x => x.IsUpdateCompleted);

        PluginManagerPage.Context.IsUpdateCheckOnProgress = isAnyUpdateCheckOnProgress;
        PluginManagerPage.Context.IsUpdateOnProgress      = isAnyUpdateOnProgress;
        PluginManagerPage.Context.IsUpdateAllShow         = !isAnyUpdateCheckOnProgress && !isAnyUpdateOnProgress;

        bool isPluginEnableChanged       = e.PropertyName?.Equals(nameof(PluginInfo.IsEnabled)) ?? false;
        bool isPluginMarkDeletionChanged = e.PropertyName?.Equals(nameof(PluginInfo.IsMarkedForDeletion)) ?? false;

        if (isAnyUpdateCompleted ||
            isPluginEnableChanged ||
            isPluginMarkDeletionChanged)
        {
            PluginManagerPage.Context.IsChanged = true;
        }
    }

    private void OnChangeUpdatePluginCollection(object? sender, NotifyCollectionChangedEventArgs e)
    {
        try
        {
            if (e is not { Action: NotifyCollectionChangedAction.Add, NewItems: not null })
            {
                return;
            }

            foreach (PluginInfo newPluginInfo in e.NewItems)
            {
                PluginManager.PluginInstances.TryAdd(newPluginInfo.PluginDirPath, newPluginInfo);
            }
        }
        finally
        {
            IsChanged = true;
        }
    }

    public static ICommand OnOpenCurrentPluginFolderCommand { get; } = new RelayCommand<PluginInfo>(OnOpenCurrentPluginFolder);
    public static void OnOpenCurrentPluginFolder(PluginInfo? obj)
    {
        if (obj == null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = obj.PluginDirPath,
            UseShellExecute = true
        });
    }

    public static ICommand OnOpenCollapsePluginFolderCommand { get; } = new RelayCommand<PluginInfo>(OnOpenCollapsePluginFolder);
    public static void OnOpenCollapsePluginFolder(PluginInfo? obj)
    {
        if (obj == null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = LauncherConfig.AppPluginFolder,
            UseShellExecute = true
        });
    }

    public static ICommand OnDisableSelectedPluginsCommand { get; } = new RelayCommand<IList<object>>(OnDisableSelectedPlugins);
    public static void OnDisableSelectedPlugins(IList<object>? obj) => ToggleIsEnabledSelectedPlugins(obj, false);

    public static ICommand OnEnableSelectedPluginsCommand { get; } = new RelayCommand<IList<object>>(OnEnableSelectedPlugins);
    public static void OnEnableSelectedPlugins(IList<object>? obj) => ToggleIsEnabledSelectedPlugins(obj, true);

    public static ICommand OnMarkForDeletionSelectedPluginsCommand { get; } = new RelayCommand<IList<object>>(OnMarkForDeletionSelectedPlugins);
    public static void OnMarkForDeletionSelectedPlugins(IList<object>? obj) => ToggleIsMarkedForDeletionSelectedPlugins(obj, true);

    public static ICommand OnRestoreFromDeletionSelectedPluginsCommand { get; } = new RelayCommand<IList<object>>(OnRestoreFromDeletionSelectedPlugins);
    public static void OnRestoreFromDeletionSelectedPlugins(IList<object>? obj) => ToggleIsMarkedForDeletionSelectedPlugins(obj, false);

    public static ICommand OnCheckUpdateCurrentPluginCommand { get; } = new RelayCommand<PluginInfo>(OnCheckUpdateCurrentPlugin);
    public static void OnCheckUpdateCurrentPlugin(PluginInfo? plugin)
    {
        if (plugin == null)
        {
            return;
        }

        CheckUpdateEnumeratePlugins(plugin);
    }

    public static ICommand OnCheckAndDownloadUpdateCurrentPluginCommand { get; } = new RelayCommand<PluginInfo>(OnCheckAndDownloadUpdateCurrentPlugin);
    public static void OnCheckAndDownloadUpdateCurrentPlugin(PluginInfo? plugin)
    {
        if (plugin == null)
        {
            return;
        }

        OnCheckAndDownloadUpdateSelectedPlugins([plugin]);
    }

    public static ICommand OnDownloadUpdateCurrentPluginCommand { get; } = new RelayCommand<PluginInfo>(OnDownloadUpdateCurrentPlugin);
    public static async void OnDownloadUpdateCurrentPlugin(PluginInfo? plugin)
    {
        try
        {
            if (plugin == null)
            {
                return;
            }

            await plugin.RunUpdateTask();
        }
        catch (Exception ex)
        {
            ErrorSender.SendException(new UnknownPluginException(null, ex));
        }
    }

    public static ICommand OnCheckUpdateSelectedPluginsCommand { get; } = new RelayCommand<IList<object>>(OnCheckUpdateSelectedPlugins);
    public static void OnCheckUpdateSelectedPlugins(IList<object>? obj)
    {
        if (obj == null)
        {
            return;
        }

        CheckUpdateEnumeratePlugins(obj.OfType<PluginInfo>().ToList());
    }

    public static ICommand OnCheckAndDownloadUpdateSelectedPluginsCommand { get; } = new RelayCommand<IList<object>>(OnCheckAndDownloadUpdateSelectedPlugins);
    public static void OnCheckAndDownloadUpdateSelectedPlugins(IList<object>? obj)
    {
        if (obj == null)
        {
            return;
        }

        CheckAndDownloadUpdateEnumeratePlugins(obj.OfType<PluginInfo>().ToList());
    }

    internal static async void CheckUpdateEnumeratePlugins(params PluginInfo[] pluginInfos)
    {
        try
        {
            await Parallel.ForEachAsync(pluginInfos, Impl);

            async ValueTask Impl(PluginInfo plugin, CancellationToken token)
            {
                await plugin.RunCheckUpdateTask(token);
            }
        }
        catch (Exception ex)
        {
            ErrorSender.SendException(new UnknownPluginException(null, ex));
        }
    }

    internal static async void CheckUpdateEnumeratePlugins(ICollection<PluginInfo> pluginInfos)
    {
        try
        {
            await Parallel.ForEachAsync(pluginInfos, Impl);

            async ValueTask Impl(PluginInfo plugin, CancellationToken token)
            {
                await plugin.RunCheckUpdateTask(token);
            }
        }
        catch (Exception ex)
        {
            ErrorSender.SendException(new UnknownPluginException(null, ex));
        }
    }

    internal static async void CheckAndDownloadUpdateEnumeratePlugins(IEnumerable<PluginInfo> pluginInfos)
    {
        try
        {
            await Parallel.ForEachAsync(pluginInfos, Impl);

            async ValueTask Impl(PluginInfo plugin, CancellationToken token)
            {
                if (await plugin.RunCheckUpdateTask(token))
                {
                    await plugin.RunUpdateTask(token);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorSender.SendException(new UnknownPluginException(null, ex));
        }
    }

    private static void ToggleIsEnabledSelectedPlugins(IList<object>? obj, bool isEnabled)
    {
        if (obj == null)
        {
            return;
        }

        foreach (PluginInfo pluginInfo in obj.OfType<PluginInfo>())
        {
            pluginInfo.IsEnabled = isEnabled;
        }
    }

    private static void ToggleIsMarkedForDeletionSelectedPlugins(IList<object>? obj, bool isMarkedForDeletion)
    {
        if (obj == null)
        {
            return;
        }

        foreach (PluginInfo pluginInfo in obj.OfType<PluginInfo>())
        {
            pluginInfo.IsMarkedForDeletion = isMarkedForDeletion;
        }
    }

    internal async Task<(List<(string, PluginManifest)>, bool)> StartBackgroundUpdateTask()
    {
        List<(string, PluginManifest)> returnList = [];
        if (!IsEnableAutoUpdate)
        {
            return (returnList, false);
        }

        await Parallel.ForEachAsync(PluginCollection, Impl);

        return (returnList, returnList.Count > 0);

        async ValueTask Impl(PluginInfo pluginInfo, CancellationToken token)
        {
            if (!pluginInfo.IsUpdateSupported)
            {
                return;
            }

            try
            {
                if (!await pluginInfo.RunCheckUpdateTask(token))
                {
                    return;
                }

                if (pluginInfo is { IsUpdateAvailable: true, NextUpdateManifestInfo: not null })
                {
                    await pluginInfo.RunUpdateTask(token);
                }

                if (pluginInfo.NextUpdateManifestInfo != null)
                {
                    lock (returnList)
                    {
                        returnList.Add((pluginInfo.PluginKey, pluginInfo.NextUpdateManifestInfo));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"An error has occurred while performing auto-update for plugin: {pluginInfo.Name}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        InnerLauncherConfig
           .m_mainPage?
           .DispatcherQueue
           .TryEnqueue(() => PropertyChanged?
                          .Invoke(this, new PropertyChangedEventArgs(propertyName)));
    }
}
