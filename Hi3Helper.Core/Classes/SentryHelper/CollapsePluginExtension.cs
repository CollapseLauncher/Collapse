using Sentry;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace Hi3Helper.SentryHelper;

public static class PluginListBreadcrumb
{
    private static readonly ObservableCollection<(string Name, string Version, string StdVersion)> List = [];
    

    public static void Add(string name, string version, string stdVersion)
    {
        List.Add((name, version, stdVersion));
        
        List.CollectionChanged += ListOnCollectionChanged;
    }

    private static void ListOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        SentryHelper.PluginInfo = new Breadcrumb("Plugin Info", "app.plugins",
                                                 List.ToDictionary(
                                                                   item => item.Name,
                                                                   item => $"{item.Version}-{item.StdVersion}"
                                                                   ),
                                                 "PluginInfo");
    }
}