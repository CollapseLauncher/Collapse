using CollapseLauncher.Helper.JsonConverter;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.RepairManagement.StarRail.Struct;
using CollapseLauncher.RepairManagement.StarRail.Struct.Assets;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Hashes;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.EncTool.Proto.StarRail;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable CommentTypo
// ReSharper disable UnusedMember.Global

#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher;

internal partial class StarRailPersistentRefResult
{
    public required AssetBaseUrls BaseUrls    { get; set; }
    public required AssetBaseDirs BaseDirs    { get; set; }
    public required AssetMetadata Metadata    { get; set; }
    public          bool          IsCacheMode { get; set; }

    public static async Task<StarRailPersistentRefResult> GetCacheReferenceAsync(
        StarRailRepairV2  instance,
        SRDispatcherInfo  dispatcherInfo,
        HttpClient        client,
        string            gameBaseDir,
        string            persistentDir,
        CancellationToken token)
    {
        StarRailGatewayStatic      gateway    = dispatcherInfo.RegionGateway;
        Dictionary<string, string> gatewayKvp = gateway.ValuePairs;

        // -- Assign main URLs
        string mainUrlLua  = gatewayKvp["LuaBundleVersionUpdateUrl"].CombineURLFromString("client/Windows");
        string mainUrlIFix = gatewayKvp["IFixPatchVersionUpdateUrl"].CombineURLFromString("client/Windows");
        AssetBaseUrls baseUrls = new()
        {
            GatewayKvp         = gatewayKvp,
            Archive            = "",
            AsbBlock           = "",
            AsbBlockPersistent = "",
            Audio              = "",
            DesignData         = "",
            NativeData         = "",
            Video              = "",
            CacheLua           = mainUrlLua,
            CacheIFix          = mainUrlIFix
        };

        string refLuaUrl  = mainUrlLua.CombineURLFromString("M_LuaV.bytes");
        string refIFixUrl = mainUrlIFix.CombineURLFromString("M_IFixV.bytes");

        // -- Initialize persistent dirs
        string        lDirLua  = Path.Combine(persistentDir, @"Lua\Windows");
        string        lDirIFix = Path.Combine(persistentDir, @"IFix\Windows");
        string        aDirLua  = Path.Combine(gameBaseDir,   lDirLua);
        string        aDirIFix = Path.Combine(gameBaseDir,   lDirIFix);
        AssetBaseDirs baseDirs = new()
        {
            CacheLua  = lDirLua,
            CacheIFix = lDirIFix
        };

        // -- Fetch and parse the index references
        StarRailAssetMetadataIndex metadataLua = new(useHeaderSizeOfForAssert: true);
        Dictionary<string, StarRailRefMainInfo> handleLua = await StarRailRefMainInfo
           .ParseMetadataFromUrlAsync(instance,
                                      client,
                                      refLuaUrl,
                                      metadataLua,
                                      x => x.DataList[0].MD5Checksum,
                                      x => x.DataList[0].MetadataIndexFileSize,
                                      x => x.DataList[0].Timestamp,
                                      x => new Version(x.DataList[0].MajorVersion, x.DataList[0].MinorVersion, x.DataList[0].PatchVersion),
                                      aDirLua,
                                      token);

        StarRailAssetMetadataIndex metadataIFix = new(use6BytesPadding: true, useHeaderSizeOfForAssert: true);
        Dictionary<string, StarRailRefMainInfo> handleIFix = await StarRailRefMainInfo
           .ParseMetadataFromUrlAsync(instance,
                                      client,
                                      refIFixUrl,
                                      metadataIFix,
                                      x => x.DataList[0].MD5Checksum,
                                      x => x.DataList[0].MetadataIndexFileSize,
                                      x => x.DataList[0].Timestamp,
                                      x => new Version(x.DataList[0].MajorVersion, x.DataList[0].MinorVersion, x.DataList[0].PatchVersion),
                                      aDirIFix,
                                      token);

        // -- Save local index files
        //    Notes to Dev: HoYo no longer provides a proper raw bytes data anymore and the client creates it based
        //                  on data provided by "handleArchive", so we need to emulate how the game generates these data.
        await SaveLocalIndexFiles(instance, handleLua,  aDirLua,  "LuaV",  token);
        await SaveLocalIndexFiles(instance, handleIFix, aDirIFix, "IFixV", token);

        // -- Load metadata files
        //   -- LuaV
        StarRailAssetSignaturelessMetadata? metadataLuaV = new(".bytes");
        metadataLuaV = await LoadMetadataFile(instance,
                                              handleLua,
                                              client,
                                              baseUrls.CacheLua,
                                              "LuaV",
                                              metadataLuaV,
                                              aDirLua,
                                              token);

        //   -- IFixV
        StarRailAssetCsvMetadata? metadataIFixV =
            await LoadMetadataFile<StarRailAssetCsvMetadata>(instance,
                                                             handleIFix,
                                                             client,
                                                             baseUrls.CacheIFix,
                                                             "IFixV",
                                                             aDirIFix,
                                                             token);

        // -- Generate ChangeLuaPathInfo.bytes

        return new StarRailPersistentRefResult
        {
            BaseDirs = baseDirs,
            BaseUrls = baseUrls,
            Metadata = new AssetMetadata
            {
                CacheLua  = metadataLuaV,
                CacheIFix = metadataIFixV
            },
            IsCacheMode = true
        };
    }

    public static async Task<StarRailPersistentRefResult> GetRepairReferenceAsync(
        StarRailRepairV2  instance,
        SRDispatcherInfo  dispatcherInfo,
        HttpClient        client,
        string            gameBaseDir,
        string            persistentDir,
        CancellationToken token)
    {
        StarRailGatewayStatic      gateway    = dispatcherInfo.RegionGateway;
        Dictionary<string, string> gatewayKvp = gateway.ValuePairs;

        string mainUrlAsb        = gatewayKvp["AssetBundleVersionUpdateUrl"].CombineURLFromString("client/Windows");
        string mainUrlAsbAlt     = gatewayKvp["AssetBundleVersionUpdateUrlAlt"].CombineURLFromString("client/Windows");
        string mainUrlDesignData = gatewayKvp["DesignDataBundleVersionUpdateUrl"].CombineURLFromString("client/Windows");
        string mainUrlArchive    = mainUrlAsb.CombineURLFromString("Archive");

        string refDesignArchiveUrl = mainUrlDesignData.CombineURLFromString("M_Design_ArchiveV.bytes");
        string refArchiveUrl       = mainUrlArchive.CombineURLFromString("M_ArchiveV.bytes");

        // -- Test ArchiveV endpoint
        //    Notes to Dev: This is intentional. We need to find which endpoint is actually represents the ArchiveV file URL.
        bool isSecondArchiveVEndpointRetry = false;
    TestArchiveVEndpoint:
        if (!await IsEndpointAlive(client, refArchiveUrl, token))
        {
            if (isSecondArchiveVEndpointRetry)
            {
                throw new HttpRequestException("Seems like the URL for ArchiveV is missing. Please report this issue to our devs!");
            }

            Logger.LogWriteLine($"[StarRailPersistentRefResult::GetRepairReferenceAsync] Given ArchiveV Url is invalid! (previously: {refArchiveUrl}). Try swapping...",
                                LogType.Warning,
                                true);

            // Also swap the Asset bundle URL so we know that the URL assigned inside the gateway is flipped.
            (mainUrlAsb, mainUrlAsbAlt) = (mainUrlAsbAlt, mainUrlAsb);

            isSecondArchiveVEndpointRetry = true;
            mainUrlArchive = mainUrlAsb.CombineURLFromString("Archive");
            refArchiveUrl  = mainUrlArchive.CombineURLFromString("M_ArchiveV.bytes");
            goto TestArchiveVEndpoint;
        }
        Logger.LogWriteLine($"[StarRailPersistentRefResult::GetRepairReferenceAsync] ArchiveV Url is found! at: {refArchiveUrl}",
                            LogType.Debug,
                            true);

        // -- Assign other URLs after checks
        //    Notes to Dev:
        //    We are now only assigning URL after above check because the game sometimes being a dick for swapping these
        //    distinct Asset bundle URLs. We don't want to assign these other URLs below unless the Asb URL is already correct.
        //    We also made the second check for the actual block URLs below so HoYo wouldn't be able to fuck around with our code
        //    anymore.
        string mainUrlAudio       = mainUrlAsb.CombineURLFromString("AudioBlock");
        string mainUrlAsbBlock    = mainUrlAsb.CombineURLFromString("Block");
        string mainUrlAsbBlockAlt = mainUrlAsbAlt.CombineURLFromString("Block");
        string mainUrlNativeData  = mainUrlDesignData.CombineURLFromString("NativeData");
        string mainUrlVideo       = mainUrlAsb.CombineURLFromString("Video");

        AssetBaseUrls baseUrl = new()
        {
            GatewayKvp         = gatewayKvp,
            DesignData         = mainUrlDesignData,
            Archive            = mainUrlArchive,
            Audio              = mainUrlAudio,
            AsbBlock           = mainUrlAsbBlock,
            AsbBlockPersistent = mainUrlAsbBlockAlt,
            NativeData         = mainUrlNativeData,
            Video              = mainUrlVideo
        };

        // -- Initialize persistent dirs
        string        lDirArchive    = Path.Combine(persistentDir, @"Archive\Windows");
        string        lDirAsbBlock   = Path.Combine(persistentDir, @"Asb\Windows");
        string        lDirAudio      = Path.Combine(persistentDir, @"Audio\AudioPackage\Windows");
        string        lDirDesignData = Path.Combine(persistentDir, @"DesignData\Windows");
        string        lDirNativeData = Path.Combine(persistentDir, @"NativeData\Windows");
        string        lDirVideo      = Path.Combine(persistentDir, @"Video\Windows");
        string        aDirArchive    = Path.Combine(gameBaseDir, lDirArchive);
        string        aDirAsbBlock   = Path.Combine(gameBaseDir, lDirAsbBlock);
        string        aDirAudio      = Path.Combine(gameBaseDir, lDirAudio);
        string        aDirDesignData = Path.Combine(gameBaseDir, lDirDesignData);
        string        aDirNativeData = Path.Combine(gameBaseDir, lDirNativeData);
        string        aDirVideo      = Path.Combine(gameBaseDir, lDirVideo);
        AssetBaseDirs baseDirs = new(lDirArchive, lDirAsbBlock, lDirAudio, lDirDesignData, lDirNativeData, lDirVideo);

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
                                  aDirArchive,
                                  token);

        // -- Test Asset bundle endpoint
        //    Notes to Dev: This is intentional. We need to find which endpoint is actually represents the persistent file URL.
        bool isSecondAsbEndpointRetry = false;
    TestAsbPersistentEndpoint:
        if (!await IsEndpointAlive(handleArchive, client, baseUrl.AsbBlockPersistent, "BlockV", token))
        {
            if (isSecondAsbEndpointRetry)
            {
                throw new HttpRequestException("Seems like the URL for persistent asset bundle is missing. Please report this issue to our devs!");
            }

            Logger.LogWriteLine($"[StarRailPersistentRefResult::GetRepairReferenceAsync] Given persistent asset bundle URL is invalid! (previously: {baseUrl.AsbBlockPersistent}). Try swapping...",
                                LogType.Warning,
                                true);
            isSecondAsbEndpointRetry = true;
            baseUrl.SwapAsbPersistentUrl();
            goto TestAsbPersistentEndpoint;
        }
        Logger.LogWriteLine($"[StarRailPersistentRefResult::GetRepairReferenceAsync] Persistent asset bundle URL is found! at: {baseUrl.AsbBlockPersistent}",
                            LogType.Debug,
                            true);

        // -- Save local index files
        //    Notes to Dev: HoYo no longer provides a proper raw bytes data anymore and the client creates it based
        //                  on data provided by "handleArchive", so we need to emulate how the game generates these data.
        await SaveLocalIndexFiles(instance, handleDesignArchive, aDirDesignData, "DesignV",      token);
        await SaveLocalIndexFiles(instance, handleArchive,       aDirAsbBlock,   "AsbV",         token);
        await SaveLocalIndexFiles(instance, handleArchive,       aDirAsbBlock,   "BlockV",       token);
        await SaveLocalIndexFiles(instance, handleArchive,       aDirAsbBlock,   "Start_AsbV",   token);
        await SaveLocalIndexFiles(instance, handleArchive,       aDirAsbBlock,   "Start_BlockV", token);
        await SaveLocalIndexFiles(instance, handleArchive,       aDirAudio,      "AudioV",       token);
        await SaveLocalIndexFiles(instance, handleArchive,       aDirVideo,      "VideoV",       token);

        // -- Load metadata files
        //   -- DesignV
        StarRailAssetSignaturelessMetadata? metadataDesignV =
            await LoadMetadataFile<StarRailAssetSignaturelessMetadata>(instance,
                                                                       handleDesignArchive,
                                                                       client,
                                                                       baseUrl.DesignData,
                                                                       "DesignV",
                                                                       aDirDesignData,
                                                                       token);

        //   -- NativeDataV
        StarRailAssetNativeDataMetadata? metadataNativeDataV =
            await LoadMetadataFile<StarRailAssetNativeDataMetadata>(instance,
                                                                    handleDesignArchive,
                                                                    client,
                                                                    baseUrl.NativeData,
                                                                    "NativeDataV",
                                                                    aDirNativeData,
                                                                    token);

        //   -- Start_AsbV
        StarRailAssetBundleMetadata? metadataStartAsbV =
            await LoadMetadataFile<StarRailAssetBundleMetadata>(instance,
                                                                handleArchive,
                                                                client,
                                                                baseUrl.AsbBlockPersistent,
                                                                "Start_AsbV",
                                                                aDirAsbBlock,
                                                                token);

        //   -- Start_BlockV
        StarRailAssetBlockMetadata? metadataStartBlockV =
            await LoadMetadataFile<StarRailAssetBlockMetadata>(instance,
                                                               handleArchive,
                                                               client,
                                                               baseUrl.AsbBlockPersistent,
                                                               "Start_BlockV",
                                                               aDirAsbBlock,
                                                               token);

        //   -- AsbV
        StarRailAssetBundleMetadata? metadataAsbV =
            await LoadMetadataFile<StarRailAssetBundleMetadata>(instance,
                                                                handleArchive,
                                                                client,
                                                                baseUrl.AsbBlockPersistent,
                                                                "AsbV",
                                                                null,
                                                                token);

        //   -- BlockV
        StarRailAssetBlockMetadata? metadataBlockV =
            await LoadMetadataFile<StarRailAssetBlockMetadata>(instance,
                                                               handleArchive,
                                                               client,
                                                               baseUrl.AsbBlockPersistent,
                                                               "BlockV",
                                                               null,
                                                               token);

        //   -- AudioV
        StarRailAssetJsonMetadata? metadataAudioV =
            await LoadMetadataFile<StarRailAssetJsonMetadata>(instance,
                                                              handleArchive,
                                                              client,
                                                              baseUrl.Audio,
                                                              "AudioV",
                                                              aDirAudio,
                                                              token);

        //   -- VideoV
        StarRailAssetJsonMetadata? metadataVideoV =
            await LoadMetadataFile<StarRailAssetJsonMetadata>(instance,
                                                              handleArchive,
                                                              client,
                                                              baseUrl.Video,
                                                              "VideoV",
                                                              aDirVideo,
                                                              token);

        return new StarRailPersistentRefResult
        {
            BaseDirs = baseDirs,
            BaseUrls = baseUrl,
            Metadata = new AssetMetadata
            {
                DesignV     = metadataDesignV,
                NativeDataV = metadataNativeDataV,
                StartAsbV   = metadataStartAsbV,
                StartBlockV = metadataStartBlockV,
                AsbV        = metadataAsbV,
                BlockV      = metadataBlockV,
                AudioV      = metadataAudioV,
                VideoV      = metadataVideoV
            }
        };
    }

    private static async ValueTask SaveLocalIndexFiles(
        StarRailRepairV2                          instance,
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

    private static ValueTask<T?> LoadMetadataFile<T>(
        StarRailRepairV2                        instance,
        Dictionary<string, StarRailRefMainInfo> handleArchiveSource,
        HttpClient                              client,
        string                                  baseUrl,
        string                                  indexKey,
        string?                                 saveToLocalDir = null,
        CancellationToken                       token          = default)
        where T : StarRailBinaryData, new()
    {
        T parser = StarRailBinaryData.CreateDefault<T>();
        return LoadMetadataFile(instance,
                                handleArchiveSource,
                                client,
                                baseUrl,
                                indexKey,
                                parser,
                                saveToLocalDir,
                                token);
    }

    private static async ValueTask<T?> LoadMetadataFile<T>(
        StarRailRepairV2                        instance,
        Dictionary<string, StarRailRefMainInfo> handleArchiveSource,
        HttpClient                              client,
        string                                  baseUrl,
        string                                  indexKey,
        T                                       parser,
        string?                                 saveToLocalDir = null,
        CancellationToken                       token          = default)
        where T : StarRailBinaryData
    {
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

        if (!string.IsNullOrEmpty(saveToLocalDir) &&
            Directory.Exists(saveToLocalDir))
        {
            DirectoryInfo dirInfo = new(saveToLocalDir);
            foreach (FileInfo oldFilePath in dirInfo.EnumerateFiles($"{index.UnaliasedFileName}_*.bytes", SearchOption.TopDirectoryOnly))
            {
                ReadOnlySpan<char> fileNameOnly = oldFilePath.Name;
                ReadOnlySpan<char> fileHash = ConverterTool.GetSplit(fileNameOnly, ^2, "_.");
                if (HexTool.IsHexString(fileHash) &&
                    !fileHash.Equals(index.ContentHash, StringComparison.OrdinalIgnoreCase))
                {
                    oldFilePath
                       .EnsureNoReadOnly()
                       .StripAlternateDataStream()
                       .TryDeleteFile();
                }
            }
        }

        string filename = index.RemoteFileName;

        // Check if the stream has been downloaded
        if (!string.IsNullOrEmpty(saveToLocalDir) &&
            Path.Combine(saveToLocalDir, filename) is { } localFilePath &&
            File.Exists(localFilePath))
        {
            await using FileStream existingFileStream = File.OpenRead(localFilePath);
            byte[] hash = await CryptoHashUtility<MD5>
                               .ThreadSafe
                               .GetHashFromStreamAsync(existingFileStream, token: token);
            byte[] hashRemote = HexTool.HexToBytesUnsafe(index.ContentHash);

            if (!hash.SequenceEqual(hashRemote))
            {
                goto GetReadFromRemote;
            }

            existingFileStream.Position = 0;
            await parser.ParseAsync(existingFileStream, true, token);

            return parser;
        }

    GetReadFromRemote:
        string fileUrl = baseUrl.CombineURLFromString(filename);
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

    private static async ValueTask<bool> IsEndpointAlive(
        Dictionary<string, StarRailRefMainInfo> handleArchiveSource,
        HttpClient                              client,
        string                                  baseUrl,
        string                                  indexKey,
        CancellationToken                       token)
    {
        if (!handleArchiveSource.TryGetValue(indexKey, out StarRailRefMainInfo? index))
        {
            Logger.LogWriteLine($"Game server doesn't serve index file: {indexKey}. Please contact our developer to get this fixed!", LogType.Warning, true);
            return false;
        }

        string filename = index.RemoteFileName;
        string url      = baseUrl.CombineURLFromString(filename);

        return await IsEndpointAlive(client, url, token);
    }

    private static async ValueTask<bool> IsEndpointAlive(
        HttpClient        client,
        string            url,
        CancellationToken token)
    {
        UrlStatus status = await client.GetCachedUrlStatus(url, token);
        if (!status.IsSuccessStatusCode)
        {
            Logger.LogWriteLine($"[StarRailPersistentRefResult::IsEndpointAlive] Url: {url} returns unsuccessful status code: {status.StatusCode} ({(int)status.StatusCode})",
                                LogType.Warning,
                                true);
        }

        return status.IsSuccessStatusCode;
    }

    public class AssetBaseDirs(
        string nArchive,
        string nAsbBlock,
        string nAudio,
        string nDesignData,
        string nNativeData,
        string nVideo)
    {
        public AssetBaseDirs() : this("", "", "", "", "", "")
        {

        }

        public string PersistentArchive    { get; set; } = nArchive;
        public string PersistentAsbBlock   { get; set; } = nAsbBlock;
        public string PersistentAudio      { get; set; } = nAudio;
        public string PersistentDesignData { get; set; } = nDesignData;
        public string PersistentNativeData { get; set; } = nNativeData;
        public string PersistentVideo      { get; set; } = nVideo;
        public string StreamingArchive     { get; set; } = GetStreamingAssetsDir(nArchive);
        public string StreamingAsbBlock    { get; set; } = GetStreamingAssetsDir(nAsbBlock);
        public string StreamingAudio       { get; set; } = GetStreamingAssetsDir(nAudio);
        public string StreamingDesignData  { get; set; } = GetStreamingAssetsDir(nDesignData);
        public string StreamingNativeData  { get; set; } = GetStreamingAssetsDir(nNativeData);
        public string StreamingVideo       { get; set; } = GetStreamingAssetsDir(nVideo);

        public string? CacheIFix { get; set; }
        public string? CacheLua  { get; set; }

        private static string GetStreamingAssetsDir(string dir) => dir.Replace("Persistent", "StreamingAssets");
    }

    public class AssetBaseUrls
    {
        public required Dictionary<string, string> GatewayKvp         { get; set; }
        public required string                     DesignData         { get; set; }
        public required string                     Archive            { get; set; }
        public required string                     Audio              { get; set; }
        public required string                     AsbBlock           { get; set; }
        public required string                     AsbBlockPersistent { get; set; }
        public required string                     NativeData         { get; set; }
        public required string                     Video              { get; set; }

        public string? CacheLua  { get; set; }
        public string? CacheIFix { get; set; }

        public void SwapAsbPersistentUrl() => (AsbBlock, AsbBlockPersistent) = (AsbBlockPersistent, AsbBlock);
    }

    public class AssetMetadata
    {
        public StarRailAssetSignaturelessMetadata? DesignV     { get; set; }
        public StarRailAssetNativeDataMetadata?    NativeDataV { get; set; }
        public StarRailAssetBundleMetadata?        StartAsbV   { get; set; }
        public StarRailAssetBlockMetadata?         StartBlockV { get; set; }
        public StarRailAssetBundleMetadata?        AsbV        { get; set; }
        public StarRailAssetBlockMetadata?         BlockV      { get; set; }
        public StarRailAssetJsonMetadata?          AudioV      { get; set; }
        public StarRailAssetJsonMetadata?          VideoV      { get; set; }

        public StarRailAssetSignaturelessMetadata? CacheLua  { get; set; }
        public StarRailAssetCsvMetadata?           CacheIFix { get; set; }
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
            Timestamp             = TimeStamp,
            Reserved              = new byte[10]
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
        StarRailRepairV2  instance,
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
    }

    public static async Task<Dictionary<string, StarRailRefMainInfo>>
        ParseMetadataFromUrlAsync<TParser>(
        StarRailRepairV2              instance,
        HttpClient                    client,
        string                        url,
        TParser                       parser,
        Func<TParser, byte[]>         md5Selector,
        Func<TParser, long>           sizeSelector,
        Func<TParser, DateTimeOffset> timestampSelector,
        Func<TParser, Version>        versionSelector,
        string?                       saveToLocalDir = null,
        CancellationToken             token          = default)
        where TParser : StarRailBinaryData
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

        string filenameNoExt = Path.GetFileNameWithoutExtension(url);

        // Start parsing
        await parser.ParseAsync(sourceStream, true, token);
        byte[]         md5Checksum = md5Selector(parser);
        long           fileSize    = sizeSelector(parser);
        DateTimeOffset timestamp   = timestampSelector(parser);
        Version        version     = versionSelector(parser);

        StarRailRefMainInfo relInfo = new()
        {
            ContentHash  = HexTool.BytesToHexUnsafe(md5Checksum)!,
            FileName     = filenameNoExt,
            FileSize     = fileSize,
            TimeStamp    = timestamp,
            MajorVersion = version.Major,
            MinorVersion = version.Minor,
            PatchVersion = version.Build
        };

        Dictionary<string, StarRailRefMainInfo> dict = [];
        dict.Add(relInfo.UnaliasedFileName, relInfo);
        return dict;
    }

    private static CopyToStream CreateLocalStream(Stream thisSourceStream, string filePath)
    {
        FileInfo fileInfo = new FileInfo(filePath)
                           .EnsureCreationOfDirectory()
                           .EnsureNoReadOnly()
                           .StripAlternateDataStream();
        return new CopyToStream(thisSourceStream, fileInfo.Create(), null, true);
    }
}