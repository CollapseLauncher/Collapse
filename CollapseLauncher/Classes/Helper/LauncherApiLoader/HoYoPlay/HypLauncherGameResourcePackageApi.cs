using CollapseLauncher.Helper.JsonConverter;
using Hi3Helper.Data;
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

public class HypLauncherGameResourcePackageData : HypApiDataLookupable<HypResourcesData>
{
    [JsonPropertyName("game_packages")]
    public override List<HypResourcesData> List
    {
        get;
        set => field = Init(value);
    } = [];
}

public class HypResourcesData : HypApiIdentifiable
{
    [JsonPropertyName("main")]
    public HypResourcePackageData? MainPackage { get; set; }

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
    [JsonConverter(typeof(HexStringToArrayJsonConverter<byte>))]
    public byte[]? PackageMD5Hash { get; init; }

    [JsonIgnore]
    public string? PackageMD5HashString
    {
        get => field ??= HexTool.BytesToHexUnsafe(PackageMD5Hash);
    }

    [JsonPropertyName("size")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long PackageSize { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("pkg_version_file_name")]
    public string? UnpackedPkgVersionFileName { get; init; }

    [JsonPropertyName("path")]
    public string? FilePath { get; init; }

    [JsonPropertyName("channel_sdk_pkg")]
    public string? ChannelSdkPkg { get; init; }

    [JsonPropertyName("command")]
    public string? PackageRunCommand { get; init; }

    [JsonPropertyName("validation")]
    [JsonConverter(typeof(HypPackageDataConverter))]
    public List<HypPackageData> PackageAssetValidationList { get; init; } = [];

    [JsonPropertyName("language")]
    public string? Language { get; init; }
}
