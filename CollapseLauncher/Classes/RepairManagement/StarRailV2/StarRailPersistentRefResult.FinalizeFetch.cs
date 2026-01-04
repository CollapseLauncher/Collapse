using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.RepairManagement.StarRail.Struct.Assets;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Sophon;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130
#nullable enable
namespace CollapseLauncher;

internal partial class StarRailPersistentRefResult
{
    public async Task FinalizeCacheFetchAsync(StarRailRepairV2           instance,
                                              HttpClient                 client,
                                              List<FilePropertiesRemote> assetIndex,
                                              string                     gameDir,
                                              string                     aLuaDir,
                                              CancellationToken          token)
    {
        if (!IsCacheMode)
        {
            throw new
                InvalidOperationException("You cannot call this method for finalization as you're using Game Repair mode. Please use FinalizeRepairFetchAsync instead!");
        }

        // Set total activity string as "Fetching Caches Type: <type>"
        instance.Status.ActivityStatus = string.Format(Locale.Lang._CachesPage.CachesStatusFetchingType, "ChangeLuaPathInfo.bytes");
        instance.Status.IsProgressAllIndetermined = true;
        instance.Status.IsIncludePerFileIndicator = false;
        instance.UpdateStatus();

        // -- Get stock LuaV manifest file from sophon
        FilePropertiesRemote? luaStockManifestFile =
            assetIndex.FirstOrDefault(x => x.N.Contains(@"StreamingAssets\Lua\Windows\LuaV_",
                                                        StringComparison.OrdinalIgnoreCase) &&
                                           x.N.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase));

        if (luaStockManifestFile is not { AssociatedObject: SophonAsset sophonAsset })
        {
            Logger.LogWriteLine("[StarRailPersistentRefResult::FinalizeCacheFetchAsync] We cannot finalize fetching process as necessary file is not available. The game might behave incorrectly!",
                                LogType.Warning,
                                true);
            return;
        }

        // -- Load temporarily from sophon
        await using MemoryStream tempStream = new();
        await sophonAsset.WriteToStreamAsync(client, tempStream, token: token);
        tempStream.Position = 0;

        // -- Parse manifest and get the first asset from stock metadata
        StarRailAssetSignaturelessMetadata metadataLuaV = new(".bytes");
        await metadataLuaV.ParseAsync(tempStream, true, token);

        // -- Get stock dictionary asset
        StarRailAssetSignaturelessMetadata.Metadata? stockLuaDictPath = metadataLuaV.DataList.FirstOrDefault();
        FilePropertiesRemote? stockLuaDictAsset =
            assetIndex.FirstOrDefault(x => x.N.EndsWith(stockLuaDictPath?.Filename ?? "",
                                                        StringComparison.OrdinalIgnoreCase));
        if (stockLuaDictAsset is not { AssociatedObject: SophonAsset stockLuaDictSophon })
        {
            Logger.LogWriteLine("[StarRailPersistentRefResult::FinalizeCacheFetchAsync] Stock Lua Dictionary file is not found! Skipping",
                                LogType.Warning,
                                true);
            return;
        }

        using MemoryStream stockLuaDictStream = new();
        await stockLuaDictSophon.WriteToStreamAsync(client, stockLuaDictStream, token: token);
        stockLuaDictStream.Position = 0;

        // -- Get game server's dictionary asset
        StarRailAssetSignaturelessMetadata.Metadata? gameServStockLuaPath = Metadata.CacheLua?.DataList.FirstOrDefault();
        string gameServLuaDictUrl = BaseUrls.CacheLua.CombineURLFromString(gameServStockLuaPath?.Filename);
        if (string.IsNullOrEmpty(gameServLuaDictUrl))
        {
            Logger.LogWriteLine("[StarRailPersistentRefResult::FinalizeCacheFetchAsync] Game Server's Lua Dictionary file is not found! Skipping",
                                LogType.Warning,
                                true);
            return;
        }

        CDNCacheResult gameServLuaDictRemote = await client.TryGetCachedStreamFrom(gameServLuaDictUrl, token: token);
        if (!gameServLuaDictRemote.IsSuccessStatusCode)
        {
            Logger.LogWriteLine("[StarRailPersistentRefResult::FinalizeCacheFetchAsync] Game Server's Lua Dictionary file returns unsuccessful code! Skipping",
                                LogType.Warning,
                                true);
            return;
        }
        await using Stream       gameServLuaDictRemoteStream = gameServLuaDictRemote.Stream;
        await using MemoryStream gameServLuaDictStream       = new();
        await gameServLuaDictRemoteStream.CopyToAsync(gameServLuaDictStream, token);
        gameServLuaDictStream.Position = 0;

        // -- Load Lua Dictionary Stream
        Dictionary<string, StarRailLuaPath> stockLuaDic = await LoadStarRailLuaPathDictAsync(stockLuaDictStream, token);
        Dictionary<string, StarRailLuaPath> gameServLuaDic =
            await LoadStarRailLuaPathDictAsync(gameServLuaDictStream, token);

        // -- Generate ChangeLuaPathInfo.bytes to persistent folder
        List<StarRailLuaPath> newLuaDic = CompareLuaDict(stockLuaDic, gameServLuaDic);
        if (newLuaDic.Count == 0)
        {
            return;
        }

        FileInfo luaPathInfo = new FileInfo(Path.Combine(gameDir, aLuaDir, "ChangeLuaPathInfo.bytes"))
                              .EnsureCreationOfDirectory()
                              .EnsureNoReadOnly()
                              .StripAlternateDataStream();
        await using StreamWriter luaPathInfoWriter = luaPathInfo.CreateText();
        luaPathInfoWriter.NewLine = "\n";
        foreach (string line in newLuaDic.Select(newLuaEntry => newLuaEntry.Serialize(StarRailLuaPathJsonContext.Default.StarRailLuaPath, false)))
        {
            await luaPathInfoWriter.WriteLineAsync(line);
        }
    }

    public async Task FinalizeRepairFetchAsync(StarRailRepairV2           instance,
                                               HttpClient                 client,
                                               List<FilePropertiesRemote> assetIndex,
                                               string                     persistentDir,
                                               CancellationToken          token)
    {
        if (IsCacheMode)
        {
            throw new
                InvalidOperationException("You cannot call this method for finalization as you're using Cache Update mode. Please use FinalizeCacheFetchAsync instead!");
        }

        // Set total activity string as "Fetching Caches Type: <type>"
        instance.Status.ActivityStatus = string.Format(Locale.Lang._CachesPage.CachesStatusFetchingType, "BinaryVersion.bytes");
        instance.Status.IsProgressAllIndetermined = true;
        instance.Status.IsIncludePerFileIndicator = false;
        instance.UpdateStatus();

        FilePropertiesRemote? binaryVersionFile =
            assetIndex.FirstOrDefault(x => x.N.EndsWith("StreamingAssets\\BinaryVersion.bytes",
                                                        StringComparison.OrdinalIgnoreCase));

        if (binaryVersionFile is not { AssociatedObject: SophonAsset asSophonAsset })
        {
            Logger.LogWriteLine("[StarRailPersistentRefResult::FinalizeRepairFetchAsync] We cannot finalize fetching process as necessary file is not available. The game might behave incorrectly!",
                                LogType.Warning,
                                true);
            return;
        }

        await using MemoryStream tempStream = new();
        await asSophonAsset.WriteToStreamAsync(client, tempStream, token: token);
        tempStream.Position = 0;

        byte[]     buffer     = tempStream.ToArray();
        Span<byte> bufferSpan = buffer.AsSpan()[..^3];

        string binAppIdentityPath          = Path.Combine(persistentDir, "AppIdentity.txt");
        string binDownloadedFullAssetsPath = Path.Combine(persistentDir, "DownloadedFullAssets.txt");
        string binInstallVersionPath       = Path.Combine(persistentDir, "InstallVersion.bin");

        Span<byte> hashSpan = bufferSpan[^36..^4];
        string     hashStr  = Encoding.UTF8.GetString(hashSpan);

        GetVersionNumber(bufferSpan, out uint majorVersion, out uint minorVersion, out uint stockPatchVersion);

        await File.WriteAllTextAsync(binAppIdentityPath, hashStr, token);
        await File.WriteAllTextAsync(binDownloadedFullAssetsPath, hashStr, token);
        await File.WriteAllTextAsync(binInstallVersionPath, $"{hashStr},{majorVersion}.{minorVersion}.{stockPatchVersion}", token);

        return;

        static void GetVersionNumber(ReadOnlySpan<byte> span, out uint major, out uint minor, out uint patch)
        {
            ushort strLen = BinaryPrimitives.ReadUInt16BigEndian(span);
            span  = span[(2 + strLen)..]; // Skip
            patch = BinaryPrimitives.ReadUInt32BigEndian(span);
            major = BinaryPrimitives.ReadUInt32BigEndian(span[4..]);
            minor = BinaryPrimitives.ReadUInt32BigEndian(span[8..]);
        }
    }

    private static List<StarRailLuaPath> CompareLuaDict(
        Dictionary<string, StarRailLuaPath> stock,
        Dictionary<string, StarRailLuaPath> gameServ)
    {
        List<StarRailLuaPath> newPaths = [];
        newPaths.AddRange(from kvp in gameServ where !stock.ContainsKey(kvp.Key) select kvp.Value);

        return newPaths;
    }

    private static async Task<Dictionary<string, StarRailLuaPath>>
        LoadStarRailLuaPathDictAsync(
            Stream            stream,
            CancellationToken token)
    {
        Dictionary<string, StarRailLuaPath> dic    = new(StringComparer.OrdinalIgnoreCase);
        using StreamReader                  reader = new(stream, leaveOpen: true);
        while (await reader.ReadLineAsync(token) is { } line)
        {
            // Break if we are already at the end of the JSON part
            if (!string.IsNullOrEmpty(line) &&
                line[0] != '{')
            {
                break;
            }

            if (line.Deserialize(StarRailLuaPathJsonContext.Default.StarRailLuaPath) is { } entry)
            {
                dic.TryAdd(entry.Md5 + entry.Path, entry);
            }
        }

        return dic;
    }

    [JsonSerializable(typeof(StarRailLuaPath))]
    [JsonSourceGenerationOptions(NewLine = "\n")]
    public partial class StarRailLuaPathJsonContext : JsonSerializerContext;

    public class StarRailLuaPath
    {
        [JsonPropertyOrder(0)]
        public required string Path { get; set; }

        [JsonPropertyOrder(1)]
        public required string Md5 { get; set; }
    }
}
