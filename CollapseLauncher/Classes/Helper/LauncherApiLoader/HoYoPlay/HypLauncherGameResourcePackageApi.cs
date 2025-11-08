using CollapseLauncher.Helper.JsonConverter;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility.Json.Converters;
using System.Collections.Generic;
using System.Text.Json.Serialization;
// ReSharper disable StringLiteralTypo
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable UnusedMember.Global
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;

public class HypLauncherGameResourcePackageApi : HypApiResponse<HypLauncherGameResourcePackageData>;
public class HypLauncherGameResourcePluginApi : HypApiResponse<HypLauncherGameResourcePluginData>;
public class HypLauncherGameResourceSdkApi : HypApiResponse<HypLauncherGameResourceSdkData>;

public class HypLauncherGameResourcePackageData : HypApiDataLookupable<HypResourcesData>
{
    [JsonPropertyName("game_packages")]
    public override List<HypResourcesData> List
    {
        get;
        init;
    } = [];
}

public class HypLauncherGameResourcePluginData : HypApiDataLookupable<HypResourcesData>
{
    [JsonPropertyName("plugin_releases")]
    public override List<HypResourcesData>? List
    {
        get;
        init;
    } = [];
}

public class HypLauncherGameResourceSdkData : HypApiDataLookupable<HypChannelSdkData>
{
    [JsonPropertyName("game_channel_sdks")]
    public override List<HypChannelSdkData> List
    {
        get;
        init;
    } = [];
}

public class HypChannelSdkData : HypApiIdentifiable
{
    [JsonPropertyName("channel_sdk_pkg")]
    public HypPackageData? SdkPackageDetail { get; set; }

    [JsonPropertyName("pkg_version_file_name")]
    public string? PkgVersionFileName { get; set; }

    [JsonPropertyName("version")]
    [JsonConverter(typeof(Utf8SpanParsableJsonConverter<GameVersion>))]
    public GameVersion Version { get; set; }
}

public class HypResourcesData : HypApiIdentifiable
{
    [JsonPropertyName("main")]
    public HypResourcePackageData? MainPackage { get; set; }

    [JsonPropertyName("plugins")]
    public List<HypPluginPackageInfo> PluginPackageSections { get; set; } = [];

    [JsonPropertyName("pre_download")]
    public HypResourcePackageData? PreDownload { get; set; }
}

public class HypResourcePackageData
{
    [JsonPropertyName("major")]
    public HypPackageInfo? CurrentVersion { get; set; }

    [JsonPropertyName("patches")]
    public List<HypPackageInfo> Patches { get; set; } = [];
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

public class HypPackageInfo
{
    [JsonPropertyName("audio_pkgs")]
    public List<HypPackageData> AudioPackages { get; set; } = [];

    [JsonPropertyName("game_pkgs")]
    public List<HypPackageData> GamePackages { get; set; } = [];

    [JsonPropertyName("res_list_url")]
    public string? ResourceListUrl { get; set; }

    [JsonPropertyName("version")]
    [JsonConverter(typeof(Utf8SpanParsableJsonConverter<GameVersion>))]
    public GameVersion Version { get; set; }
}

public class HypPackageData
{
    [JsonPropertyName("decompressed_size")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long PackageDecompressSize { get; init; }

    [JsonPropertyName("md5")]
    public string? PackageMD5Hash { get; init; }

    [JsonPropertyName("size")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? PackageSize { get; init; }

    [JsonPropertyName("url")]
    public string? PackageUrl { get; init; }

    [JsonPropertyName("pkg_version_file_name")]
    public string? UnpackedPkgVersionFileName { get; init; }

    [JsonPropertyName("path")]
    public string? UnpackedBaseUrl { get; init; }

    [JsonPropertyName("channel_sdk_pkg")]
    public string? ChannelSdkPkg { get; init; }

    [JsonPropertyName("command")]
    public string? PackageRunCommand { get; init; }

    [JsonPropertyName("validation")]
    [JsonConverter(typeof(HypPackageFileValidationInfoConverter))]
    public List<HypPackageFileValidationInfo> PackageAssetValidationList { get; init; } = [];

    [JsonPropertyName("language")]
    public string? Language { get; init; }
}
