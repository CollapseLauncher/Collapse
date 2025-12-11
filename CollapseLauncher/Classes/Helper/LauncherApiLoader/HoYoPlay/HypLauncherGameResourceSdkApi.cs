using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility.Json.Converters;
using System.Collections.Generic;
using System.Text.Json.Serialization;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;

public class HypLauncherGameResourceSdkApi : HypApiResponse<HypLauncherGameResourceSdkData>;

public class HypLauncherGameResourceSdkData : HypApiDataLookupable<HypChannelSdkData>
{
    [JsonPropertyName("game_channel_sdks")]
    public override List<HypChannelSdkData> List
    {
        get;
        set => field = Init(value);
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
