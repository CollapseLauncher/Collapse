using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;

public class HypLauncherGameResourcePluginApi : HypApiResponse<HypLauncherGameResourcePluginReleasesData>;

public class HypLauncherGameResourcePluginReleasesData : HypApiDataLookupable<HypResourcePluginData>
{
    [JsonPropertyName("plugin_releases")]
    public override List<HypResourcePluginData> List
    {
        get;
        set => field = Init(value);
    } = [];
}

public class HypResourcePluginData : HypApiIdentifiable
{
    [JsonPropertyName("plugins")]
    public List<HypPluginPackageInfo> Plugins
    {
        get;
        init => field = RemoveExcludedPackages(value);
    } = [];

    private static List<HypPluginPackageInfo> RemoveExcludedPackages(List<HypPluginPackageInfo> package)
    {
        List<HypPluginPackageInfo> returnList = [];
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (HypPluginPackageInfo plugin in package)
        {
            string packageUrl           = plugin.PluginPackage?.Url ?? "";
            string packageFileNameNoExt = Path.GetFileNameWithoutExtension(packageUrl);

            if (packageFileNameNoExt.Contains("DXSetup", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            returnList.Add(plugin);
        }

        return returnList;
    }
}

public class HypPluginPackageInfo
{
    [JsonPropertyName("plugin_id")]
    public string? PluginId { get; set; }

    [JsonPropertyName("plugin_pkg")]
    public HypPackageData? PluginPackage { get; set; }

    [JsonPropertyName("release_id")]
    public string? ReleaseId { get; set; }

    [JsonPropertyName("version")]
    [JsonConverter(typeof(Utf8SpanParsableJsonConverter<GameVersion>))]
    public GameVersion Version { get; set; }
}