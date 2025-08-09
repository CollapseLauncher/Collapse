using CollapseLauncher.Pages;
using CommunityToolkit.Mvvm.Input;
using Hi3Helper.Shared.Region;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

#nullable enable
namespace CollapseLauncher.Plugins;

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

    internal bool IsUpdateAllShow
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public PluginManagerPageContext()
    {
        PluginCollection.CollectionChanged += OnChangeUpdatePluginCollection;
    }

    private static ObservableCollection<PluginInfo> CreateNewCollectionFromPluginManager()
    {
        List<PluginInfo> backedList = PluginManager.PluginInstances.Values.ToList();
        foreach (PluginInfo pluginInfo in backedList)
        {
            pluginInfo.PropertyChanged += OnPluginInfoPropertyChanged;
        }

        ObservableCollection<PluginInfo> newCollection = new(backedList);

        return newCollection;
    }

    private static void OnPluginInfoPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => PluginManagerPage.Context.IsChanged = true;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        // Raise the PropertyChanged event, passing the name of the property whose value has changed.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
}
