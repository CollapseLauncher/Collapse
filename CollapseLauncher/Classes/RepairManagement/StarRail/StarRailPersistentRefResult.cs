using CollapseLauncher.Helper.JsonConverter;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.RepairManagement.StarRail.Struct.Assets;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.EncTool.Proto.StarRail;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Path = System.IO.Path;

#nullable enable
#pragma warning disable IDE0130
namespace CollapseLauncher;

internal class StarRailPersistentRefResult
{
    public static async Task<StarRailPersistentRefResult> GetReferenceAsync(
        StarRailRepair    instance,
        SRDispatcherInfo  dispatcherInfo,
        HttpClient        client,
        string            persistentDir,
        CancellationToken token)
    {
        StarRailGatewayStatic      gateway    = dispatcherInfo.RegionGateway;
        Dictionary<string, string> gatewayKvp = gateway.ValuePairs;

        string mainUrlAsb        = gatewayKvp["AssetBundleVersionUpdateUrl"];
        string mainUrlDesignData = gatewayKvp["DesignDataBundleVersionUpdateUrl"];

        string mInfoDesignArchiveUrl = mainUrlDesignData.CombineURLFromString("client/Windows/M_Design_ArchiveV.bytes");
        Dictionary<string, StarRailRefMainInfo> mInfoDesignArchive = await StarRailRefMainInfo
           .ParseListFromUrlAsync(instance,
                                  client,
                                  mInfoDesignArchiveUrl,
                                  token);

        string mInfoArchiveUrl = mainUrlAsb.CombineURLFromString("client/Windows/Archive/M_ArchiveV.bytes");
        Dictionary<string, StarRailRefMainInfo> mInfoArchive = await StarRailRefMainInfo
           .ParseListFromUrlAsync(instance,
                                  client,
                                  mInfoArchiveUrl,
                                  token);

        await using FileStream stream =
            File.OpenRead(@"C:\Users\neon-nyan\AppData\LocalLow\CollapseLauncher\GameFolder\SRGlb\Games\StarRail_Data\StreamingAssets\NativeData\Windows\NativeDataV_2ebcf9e27323a0561ef4825a14819ed5.bytes");

        StarRailBinaryDataNative binaryRefNativeData = new();
        await binaryRefNativeData.ParseAsync(stream, token);

        return default;
    }
}

[JsonSerializable(typeof(StarRailRefMainInfo))]
internal partial class StarRailRepairJsonContext : JsonSerializerContext;

internal class StarRailRefMainInfo
{
    [JsonPropertyOrder(0)] public int    MajorVersion          { get; init; }
    [JsonPropertyOrder(1)] public int    MinorVersion          { get; init; }
    [JsonPropertyOrder(2)] public int    PatchVersion          { get; init; }
    [JsonPropertyOrder(3)] public int    PrevPatch             { get; init; }
    [JsonPropertyOrder(4)] public string ContentHash           { get; init; } = "";
    [JsonPropertyOrder(5)] public long   FileSize              { get; init; }
    [JsonPropertyOrder(7)] public string FileName              { get; init; } = "";
    [JsonPropertyOrder(8)] public string BaseAssetsDownloadUrl { get; init; } = "";

    [JsonPropertyOrder(6)]
    [JsonConverter(typeof(UtcUnixStampToDateTimeOffsetJsonConverter))]
    public DateTimeOffset TimeStamp { get; init; }

    [JsonIgnore]
    public string UnaliasedFileName =>
        FileName.StartsWith("M_", StringComparison.OrdinalIgnoreCase) ? FileName[2..] : FileName;

    [JsonIgnore]
    public string RemoteFileName => field ??= $"{UnaliasedFileName}_{ContentHash}.bytes";

    public override string ToString() => RemoteFileName;

    public static async Task<Dictionary<string, StarRailRefMainInfo>> ParseListFromUrlAsync(
        StarRailRepair    instance,
        HttpClient        client,
        string            url,
        CancellationToken token)
    {
        // Set total activity string as "Fetching Caches Type: <type>"
        instance.Status.ActivityStatus = string.Format(Locale.Lang._CachesPage.CachesStatusFetchingType, $"Game Ref: {Path.GetFileNameWithoutExtension(url)}");
        instance.Status.IsProgressAllIndetermined = true;
        instance.Status.IsIncludePerFileIndicator = false;
        instance.UpdateStatus();

        await using Stream networkStream = (await client.TryGetCachedStreamFrom(url, token: token)).Stream;

        Dictionary<string, StarRailRefMainInfo> returnList = [];
        using StreamReader                      reader     = new(networkStream);
        while (await reader.ReadLineAsync(token) is { } line)
        {
            StarRailRefMainInfo refInfo = line.Deserialize(StarRailRepairJsonContext.Default.StarRailRefMainInfo)
                                          ?? throw new NullReferenceException();

            returnList.Add(refInfo.UnaliasedFileName, refInfo);
        }

        return returnList;
    }
}