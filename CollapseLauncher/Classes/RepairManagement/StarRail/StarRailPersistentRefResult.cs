using CollapseLauncher.Helper.JsonConverter;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.RepairManagement.StarRail.Struct;
using CollapseLauncher.RepairManagement.StarRail.Struct.Assets;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.EncTool.Proto.StarRail;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable CommentTypo

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

        string mainUrlAsb        = gatewayKvp["AssetBundleVersionUpdateUrl"].CombineURLFromString("client/Windows");
        string mainUrlDesignData = gatewayKvp["DesignDataBundleVersionUpdateUrl"].CombineURLFromString("client/Windows");
        string mainUrlArchive    = mainUrlAsb.CombineURLFromString("Archive");
        string mainUrlAudio      = mainUrlAsb.CombineURLFromString("AudioBlock");
        string mainUrlAsbBlock   = mainUrlAsb.CombineURLFromString("Block");
        string mainUrlNativeData = mainUrlDesignData.CombineURLFromString("NativeData");
        string mainUrlVideo      = mainUrlAsb.CombineURLFromString("Video");

        string lDirArchive    = Path.Combine(persistentDir, @"Archive\Windows");
        string lDirAsbBlock   = Path.Combine(persistentDir, @"Asb\Windows");
        string lDirAudio      = Path.Combine(persistentDir, @"Audio\AudioPackage\Windows");
        string lDirDesignData = Path.Combine(persistentDir, @"DesignData\Windows");
        string lDirNativeData = Path.Combine(persistentDir, @"NativeData\Windows");
        string lDirVideo      = Path.Combine(persistentDir, @"Video\Windows");

        string refDesignArchiveUrl = mainUrlDesignData.CombineURLFromString("M_Design_ArchiveV.bytes");
        string refArchiveUrl       = mainUrlArchive.CombineURLFromString("M_ArchiveV.bytes");

        // -- Fetch and parse the index references
        Dictionary<string, StarRailRefMainInfo> handleDesignArchive = await StarRailRefMainInfo
           .ParseListFromUrlAsync(instance,
                                  client,
                                  refDesignArchiveUrl,
                                  null,
                                  token);

        Dictionary<string, StarRailRefMainInfo> handleArchive = await StarRailRefMainInfo
           .ParseListFromUrlAsync(instance,
                                  client,
                                  refArchiveUrl,
                                  lDirArchive,
                                  token);

        // -- Save local index files
        //    Notes to Dev: HoYo no longer provides a proper raw bytes data anymore and the client creates it based
        //                  on data provided by "handleArchive", so we need to emulate how the game generates these data.
        await SaveLocalIndexFiles(instance, handleDesignArchive, lDirDesignData, "DesignV",      token);
        await SaveLocalIndexFiles(instance, handleArchive,       lDirAsbBlock,   "AsbV",         token);
        await SaveLocalIndexFiles(instance, handleArchive,       lDirAsbBlock,   "BlockV",       token);
        await SaveLocalIndexFiles(instance, handleArchive,       lDirAsbBlock,   "Start_AsbV",   token);
        await SaveLocalIndexFiles(instance, handleArchive,       lDirAsbBlock,   "Start_BlockV", token);
        await SaveLocalIndexFiles(instance, handleArchive,       lDirAudio,      "AudioV",       token);
        await SaveLocalIndexFiles(instance, handleArchive,       lDirVideo,      "VideoV",       token);

        // -- Load metadata files
        //   -- DesignV
        StarRailAssetSignaturelessMetadata? metadataDesignV =
            await LoadMetadataFile<StarRailAssetSignaturelessMetadata>(instance,
                                                                       handleDesignArchive,
                                                                       client,
                                                                       mainUrlDesignData,
                                                                       "DesignV",
                                                                       lDirDesignData,
                                                                       token);

        //   -- NativeDataV
        StarRailAssetNativeDataMetadata? metadataNativeDataV =
            await LoadMetadataFile<StarRailAssetNativeDataMetadata>(instance,
                                                                    handleDesignArchive,
                                                                    client,
                                                                    mainUrlNativeData,
                                                                    "NativeDataV",
                                                                    lDirNativeData,
                                                                    token);

        //   -- Start_AsbV
        StarRailAssetBundleMetadata? metadataStartAsbV =
            await LoadMetadataFile<StarRailAssetBundleMetadata>(instance,
                                                                handleArchive,
                                                                client,
                                                                mainUrlAsbBlock,
                                                                "Start_AsbV",
                                                                lDirAsbBlock,
                                                                token);

        //   -- Start_BlockV
        StarRailAssetBlockMetadata? metadataStartBlockV =
            await LoadMetadataFile<StarRailAssetBlockMetadata>(instance,
                                                               handleArchive,
                                                               client,
                                                               mainUrlAsbBlock,
                                                               "Start_BlockV",
                                                               lDirAsbBlock,
                                                               token);

        //   -- AsbV
        StarRailAssetBundleMetadata? metadataAsbV =
            await LoadMetadataFile<StarRailAssetBundleMetadata>(instance,
                                                                handleArchive,
                                                                client,
                                                                mainUrlAsbBlock,
                                                                "AsbV",
                                                                null,
                                                                token);

        //   -- BlockV
        StarRailAssetBlockMetadata? metadataBlockV =
            await LoadMetadataFile<StarRailAssetBlockMetadata>(instance,
                                                               handleArchive,
                                                               client,
                                                               mainUrlAsbBlock,
                                                               "BlockV",
                                                               null,
                                                               token);

        //   -- AudioV
        StarRailAssetJsonMetadata? metadataAudioV =
            await LoadMetadataFile<StarRailAssetJsonMetadata>(instance,
                                                              handleArchive,
                                                              client,
                                                              mainUrlAudio,
                                                              "AudioV",
                                                              lDirAudio,
                                                              token);

        //   -- VideoV
        StarRailAssetJsonMetadata? metadataVideoV =
            await LoadMetadataFile<StarRailAssetJsonMetadata>(instance,
                                                              handleArchive,
                                                              client,
                                                              mainUrlVideo,
                                                              "VideoV",
                                                              lDirVideo,
                                                              token);

        return default;
    }

    private static async ValueTask SaveLocalIndexFiles(
        StarRailRepair                          instance,
        Dictionary<string, StarRailRefMainInfo> handleArchiveSource,
        string                                  outputDir,
        string                                  indexKey,
        CancellationToken                       token)
    {
        if (!handleArchiveSource.TryGetValue(indexKey, out StarRailRefMainInfo? index))
        {
            Logger.LogWriteLine($"Game server doesn't serve index file: {indexKey}. Please contact our developer to get this fixed!", LogType.Warning, true);
            return;
        }

        // Set total activity string as "Fetching Caches Type: <type>"
        instance.Status.ActivityStatus = string.Format(Locale.Lang._CachesPage.CachesStatusFetchingType, $"Game Index: {index.FileName}");
        instance.Status.IsProgressAllIndetermined = true;
        instance.Status.IsIncludePerFileIndicator = false;
        instance.UpdateStatus();

        StarRailAssetMetadataIndex indexMetadata = index;
        string filePath = Path.Combine(outputDir, index.FileName + ".bytes");
        await indexMetadata.WriteAsync(filePath, token);
    }

    private static async ValueTask<T?> LoadMetadataFile<T>(
        StarRailRepair                          instance,
        Dictionary<string, StarRailRefMainInfo> handleArchiveSource,
        HttpClient                              client,
        string                                  baseUrl,
        string                                  indexKey,
        string?                                 saveToLocalDir = null,
        CancellationToken                       token          = default)
        where T : StarRailBinaryData, new()
    {
        T parser = StarRailBinaryData.CreateDefault<T>();

        if (!handleArchiveSource.TryGetValue(indexKey, out StarRailRefMainInfo? index))
        {
            Logger.LogWriteLine($"Game server doesn't serve index file: {indexKey}. Please contact our developer to get this fixed!", LogType.Warning, true);
            return null;
        }

        // Set total activity string as "Fetching Caches Type: <type>"
        instance.Status.ActivityStatus = string.Format(Locale.Lang._CachesPage.CachesStatusFetchingType, $"Game Metadata: {index.FileName}");
        instance.Status.IsProgressAllIndetermined = true;
        instance.Status.IsIncludePerFileIndicator = false;
        instance.UpdateStatus();

        string             filename      = index.RemoteFileName;
        string             fileUrl       = baseUrl.CombineURLFromString(filename);
        await using Stream networkStream = (await client.TryGetCachedStreamFrom(fileUrl, token: token)).Stream;
        await using Stream sourceStream = !string.IsNullOrEmpty(saveToLocalDir)
            ? CreateLocalStream(networkStream, Path.Combine(saveToLocalDir, filename))
            : networkStream;

        await parser.ParseAsync(sourceStream, true, token);
        return parser;

        static Stream CreateLocalStream(Stream thisSourceStream, string filePath)
        {
            FileInfo fileInfo = new FileInfo(filePath)
                               .EnsureCreationOfDirectory()
                               .EnsureNoReadOnly()
                               .StripAlternateDataStream();
            return new CopyToStream(thisSourceStream, fileInfo.Create(), null, true);
        }
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

    /// <summary>
    /// Converts this instance to <see cref="StarRailAssetMetadataIndex"/>.<br/>
    /// This is necessary as SRMI (Star Rail Metadata Index) file is now being generated on Client-side, so
    /// we wanted to save the files locally to save the information about the reference file.
    /// </summary>
    /// <returns>An instance of <see cref="StarRailAssetMetadataIndex"/>.</returns>
    public StarRailAssetMetadataIndex ToMetadataIndex()
    {
        StarRailAssetMetadataIndex metadataIndex =
            StarRailBinaryData.CreateDefault<StarRailAssetMetadataIndex>();

        StarRailAssetMetadataIndex.MetadataIndex indexData = new()
        {
            MajorVersion          = MajorVersion,
            MinorVersion          = MinorVersion,
            PatchVersion          = PatchVersion,
            MD5Checksum           = HexTool.HexToBytesUnsafe(ContentHash),
            MetadataIndexFileSize = (int)FileSize,
            PrevPatch             = 0, // Leave PrevPatch to be 0
            Timestamp             = TimeStamp
        };

        metadataIndex.DataList.Add(indexData);
        return metadataIndex;
    }

    /// <summary>
    /// Converts this instance to <see cref="StarRailAssetMetadataIndex"/>.<br/>
    /// This is necessary as SRMI (Star Rail Metadata Index) file is now being generated on Client-side, so
    /// we wanted to save the files locally to save the information about the reference file.
    /// </summary>
    /// <returns>An instance of <see cref="StarRailAssetMetadataIndex"/>.</returns>
    public static implicit operator StarRailAssetMetadataIndex(StarRailRefMainInfo instance) => instance.ToMetadataIndex();

    public static async Task<Dictionary<string, StarRailRefMainInfo>> ParseListFromUrlAsync(
        StarRailRepair    instance,
        HttpClient        client,
        string            url,
        string?           saveToLocalDir = null,
        CancellationToken token          = default)
    {
        // Set total activity string as "Fetching Caches Type: <type>"
        instance.Status.ActivityStatus = string.Format(Locale.Lang._CachesPage.CachesStatusFetchingType, $"Game Ref: {Path.GetFileNameWithoutExtension(url)}");
        instance.Status.IsProgressAllIndetermined = true;
        instance.Status.IsIncludePerFileIndicator = false;
        instance.UpdateStatus();

        await using Stream networkStream = (await client.TryGetCachedStreamFrom(url, token: token)).Stream;
        await using Stream sourceStream = !string.IsNullOrEmpty(saveToLocalDir)
            ? CreateLocalStream(networkStream, Path.Combine(saveToLocalDir, Path.GetFileName(url)))
            : networkStream;

        Dictionary<string, StarRailRefMainInfo> returnList = [];
        using StreamReader                      reader     = new(sourceStream);
        while (await reader.ReadLineAsync(token) is { } line)
        {
            StarRailRefMainInfo refInfo = line.Deserialize(StarRailRepairJsonContext.Default.StarRailRefMainInfo)
                                          ?? throw new NullReferenceException();

            returnList.Add(refInfo.UnaliasedFileName, refInfo);
        }

        return returnList;

        static Stream CreateLocalStream(Stream thisSourceStream, string filePath)
        {
            FileInfo fileInfo = new FileInfo(filePath)
                                .EnsureCreationOfDirectory()
                                .EnsureNoReadOnly()
                                .StripAlternateDataStream();
            return new CopyToStream(thisSourceStream, fileInfo.Create(), null, true);
        }
    }
}