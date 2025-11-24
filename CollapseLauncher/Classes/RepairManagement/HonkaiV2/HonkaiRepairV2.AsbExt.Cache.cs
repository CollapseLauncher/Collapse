using CollapseLauncher.GameSettings.Honkai;
using CollapseLauncher.GameSettings.Honkai.Context;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.EncTool.Parser.KianaDispatch;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Preset;
using Hi3Helper.UABT;
using Microsoft.Win32;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable CheckNamespace
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.RepairManagement;

internal class CacheAssetInfo
{
    public          byte[]     HmacSha1Salt           { get; set; } = [];
    public          int        MhyMersenneTwisterSeed { get; set; }
    public required string     AssetUrl               { get; init; }
    public required CacheAsset Asset                  { get; init; }
}

internal static partial class AssetBundleExtension
{
    private const CacheAssetType AcceptableCacheAssetTypeFlags = CacheAssetType.Data
                                                                 | CacheAssetType.Event
                                                                 | CacheAssetType.AI;

    private static async Task<List<CacheAssetInfo>>
        GetCacheAssetBundleListAsync<T>(
            this HttpClient   assetBundleHttpClient,
            PresetConfig      presetConfig,
            KianaDispatch     gameServerInfo,
            CacheAssetType?   cacheAssetType,
            ProgressBase<T>   progressibleInstance,
            CancellationToken token                = default)
        where T : IAssetIndexSummary
    {
        // ReSharper disable once RedundantAssignment
        cacheAssetType ??= AcceptableCacheAssetTypeFlags;

        string gameLanguage = presetConfig.GetGameLanguage() ?? "en";

        Exception? lastException = null;
        foreach (string assetBundleBaseUrl in gameServerInfo.AssetBundleUrls)
        {
            try
            {
                string assetBundleUrlTemplate = assetBundleBaseUrl.CombineURLFromString("/{0}/editor_compressed/");
                foreach (CacheAssetType cacheType in Enum.GetValues<CacheAssetType>()
                                                         .Where(x => cacheAssetType?.HasFlag(x) ?? false))
                {
                    string typeAsString = cacheType.ToString().ToLowerInvariant();
                    string baseAssetUrl = string.Format(assetBundleUrlTemplate, typeAsString);

                    string indexFileUrl = baseAssetUrl.CombineURLFromString("{0}Version.unity3d");
                    indexFileUrl = string.Format(indexFileUrl, cacheType == CacheAssetType.Data ? "Data" : "Resource");

                    // Update Progress
                    progressibleInstance.Status.ActivityStatus =
                        string.Format(Locale.Lang._CachesPage.CachesStatusFetchingType, cacheType);
                    progressibleInstance.Status.IsProgressAllIndetermined = true;
                    progressibleInstance.Status.IsIncludePerFileIndicator = false;

                    progressibleInstance.UpdateStatus();

                    Logger.LogWriteLine($"[AssetBundleExtension::GetCacheAssetBundleListAsync] Fetching ASB Cache Asset Type: {cacheType} from: {indexFileUrl}",
                                        LogType.Default,
                                        true);

                    // Get cached stream and copy over to MemoryStream
                    await using Stream indexFileStream =
                        (await assetBundleHttpClient.TryGetCachedStreamFrom(indexFileUrl, token: token))
                       .Stream;
                    await using MemoryStream indexFileMemoryStream = new();
                    await indexFileStream.CopyToAsync(indexFileMemoryStream, token);
                    indexFileMemoryStream.Position = 0;

                    await using XORStream indexFileDeXorStream = new(indexFileMemoryStream);

                    List<CacheAssetInfo> assetInfoList =
                        await DeserializeCacheAssetListAsync(assetBundleHttpClient,
                                                             indexFileDeXorStream,
                                                             baseAssetUrl,
                                                             gameLanguage,
                                                             cacheType,
                                                             progressibleInstance,
                                                             token);

                    Logger.LogWriteLine($"""
                                         [AssetBundleExtension::GetCacheAssetBundleListAsync] ASB Cache Asset Type {cacheType}!
                                                                                                   Total Size: {assetInfoList.Sum(x => x.Asset.CS)}
                                                                                                   Count: {assetInfoList.Count}
                                         """,
                                        LogType.Default,
                                        true);

                    return assetInfoList;
                }
            }
            catch when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e) when (!token.IsCancellationRequested)
            {
                lastException = e;
            }
        }

        throw lastException ?? new InvalidOperationException("No game server was reachable");
    }

    private static async Task<List<CacheAssetInfo>> DeserializeCacheAssetListAsync<T>(
        this HttpClient   checkAsbHttpClient,
        Stream            seekableStream,
        string            asbBaseUrl,
        string            gameLanguage,
        CacheAssetType    cacheType,
        ProgressBase<T>   progressibleInstance,
        CancellationToken token = default)
        where T : IAssetIndexSummary
    {
        int parallelThread = progressibleInstance.ThreadForIONormalized;

        if (LauncherMetadataHelper.CurrentMasterKey?.Key == null)
        {
            throw new
                InvalidOperationException("Cannot call this method while CurrentMasterKey.Key config is not initialized!");
        }

        List<CacheAssetInfo> tempAssetList = [];

        BundleFile     bundleFile     = new(seekableStream);
        SerializedFile serializedFile = new(bundleFile.fileList.First().Stream);

        // Read packageversion.txt
        byte[]    packageVersionBytes = serializedFile.GetDataFirstOrDefaultByName("packageversion.txt");
        TextAsset packageVersionText  = new(packageVersionBytes);

        int    mersenneTwisterSeed = 0;
        byte[] hmacHashSalt        = [];

        // Enumerate each of JSON object entry
        foreach (ReadOnlySpan<char> currentJsonEntry in packageVersionText.GetStringEnumeration())
        {
            if (currentJsonEntry.IsEmpty)
            {
                continue;
            }

            // Message from @neon-nyan to whoever fuck responsible for Hi3 security team at miHoYo:
            // I'm not going to try to hide our internal calling names if miHoYo keeps changing something AGAIN.
            // I'm already sick of this. One more changes happen, I'm not going to bother to type everything in its real name anymore.
            
            // Try parse entry if it's not a JSON object
            if (currentJsonEntry[0] != '{' && currentJsonEntry[^1] != '}')
            {
                // Try check if it's a seed for miHoYo's 64-bit Mersenne Twister Encryption RNG (MT19937_64)
                if (int.TryParse(currentJsonEntry, out mersenneTwisterSeed))
                {
#if DEBUG
                    Logger.LogWriteLine($"[AssetBundleExtension::DeserializeCacheAssetListAsync] Got MT19937_64 seed: {mersenneTwisterSeed}",
                                        LogType.Debug,
                                        true);
#endif
                    continue;
                }

                // Try check if the current entry is a signature bytes in Hex string.
                if (HexTool.IsHexString(currentJsonEntry))
                {
                    byte[] masterKey;
                    // Initialize Master key to decrypt the signature bytes (only if it's in ServeV3 format)
                    if (DataCooker.IsServeV3Data(LauncherMetadataHelper.CurrentMasterKey.Key))
                    {
                        DataCooker.GetServeV3DataSize(LauncherMetadataHelper.CurrentMasterKey.Key,
                                                      out long keyCompSize,
                                                      out long keyDecompressedSize);

                        masterKey = new byte[keyCompSize];
                        DataCooker.ServeV3Data(LauncherMetadataHelper.CurrentMasterKey.Key,
                                               masterKey,
                                               (int)keyCompSize,
                                               (int)keyDecompressedSize,
                                               out _);

                    }
                    else // Use existed served bytes
                    {
                        masterKey = LauncherMetadataHelper.CurrentMasterKey.Key;
                    }

                    // Use Collapse's MhyEncTool to get the salt from the last 8 bytes of the signature bytes.
                    MhyEncTool saltTool = new(currentJsonEntry.ToString(), masterKey);
                    hmacHashSalt = saltTool.GetSalt();
#if DEBUG
                    Logger.LogWriteLine($"[AssetBundleExtension::DeserializeCacheAssetListAsync] Got HMACSHA1 salt: 0x{BinaryPrimitives.ReadInt64LittleEndian(hmacHashSalt):x8}",
                                        LogType.Debug,
                                        true);
#endif
                }

                continue;
            }

            CacheAsset cacheAsset = currentJsonEntry.Deserialize(CacheAssetJsonContext.Default.CacheAsset)
                                    ?? throw new InvalidOperationException("Cannot deserialize CacheAsset object");
            tempAssetList.Add(new CacheAssetInfo
            {
                Asset    = cacheAsset,
                AssetUrl = $"{asbBaseUrl.CombineURLFromString(cacheAsset.N)}_{cacheAsset.CRC}"
            });
#if DEBUG
            Logger.LogWriteLine($"[AssetBundleExtension::DeserializeCacheAssetListAsync] Entry: {currentJsonEntry}",
                                LogType.Debug,
                                true);
#endif
        }

        List<CacheAssetInfo> cacheAssetInfoRet = [];
        await Parallel.ForEachAsync(tempAssetList.Where(x => IsCurrentRegionalFile(x.Asset.N, gameLanguage)),
                                    new ParallelOptions
                                    {
                                        MaxDegreeOfParallelism = parallelThread,
                                        CancellationToken      = token
                                    },
                                    ImplCheck);

        return cacheAssetInfoRet;

        async ValueTask ImplCheck(CacheAssetInfo assetInfo, CancellationToken innerToken)
        {
            if (assetInfo.Asset.DLM == 2)
            {
                // Update progress
                progressibleInstance.Status.ActivityStatus = string.Format(Locale.Lang._CachesPage.Status2, cacheType, assetInfo.Asset.N);
                progressibleInstance.UpdateStatus();

                UrlStatus urlStatus = await checkAsbHttpClient.GetURLStatusCode(assetInfo.AssetUrl, innerToken);
                Logger.LogWriteLine($"[AssetBundleExtension::DeserializeCacheAssetListAsync] Cache Asset: {assetInfo.Asset.N} " + (urlStatus.IsSuccessStatusCode ? "is" : "is not") + $" available (Status code: {urlStatus.StatusCode})",
                                    urlStatus.IsSuccessStatusCode ? LogType.Warning : LogType.Default,
                                    true);

                if (!urlStatus.IsSuccessStatusCode)
                {
                    return;
                }
            }

            lock (cacheAssetInfoRet)
            {
                // Assign Salt and Seed
                assetInfo.HmacSha1Salt           = hmacHashSalt;
                assetInfo.MhyMersenneTwisterSeed = mersenneTwisterSeed;
                cacheAssetInfoRet.Add(assetInfo);
            }
        }

        static bool IsCurrentRegionalFile(ReadOnlySpan<char> filename, string lang)
        {
            const string regionSpecificPrefix = "sprite_";

            int indexOfPrefix = filename.IndexOf(regionSpecificPrefix, StringComparison.OrdinalIgnoreCase);
            if (indexOfPrefix < 0)
            {
                return true;
            }

            ReadOnlySpan<char> slicePrefix      = filename[indexOfPrefix..];
            int                indexOfSeparator = slicePrefix.IndexOf('/');
            if (indexOfSeparator < 0)
            {
                return true;
            }

            slicePrefix = slicePrefix[..indexOfSeparator];
            slicePrefix = slicePrefix[regionSpecificPrefix.Length..];

            if (slicePrefix.IsEmpty)
            {
                return true;
            }

            // If the game is language specific but not in current language, false. Otherwise, true.
            return slicePrefix.Equals(lang, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static async Task<KianaDispatch> GetGameServerInfoAsync(
        this HttpClient   assetBundleHttpClient,
        PresetConfig      presetConfig,
        GameVersion       gameVersion,
        CancellationToken token)
    {
        if (presetConfig.GameDispatchArrayURL?.Count == 0)
        {
            throw new NullReferenceException("GameDispatchArrayURL cannot be empty on the game preset");
        }

        if (string.IsNullOrEmpty(presetConfig.DispatcherKey))
        {
            throw new NullReferenceException("DispatcherKey cannot be empty on the game preset");
        }

        if (string.IsNullOrEmpty(presetConfig.GameDispatchURLTemplate))
        {
            throw new NullReferenceException("GameDispatchURLTemplate cannot be empty on the game preset");
        }

        if (string.IsNullOrEmpty(presetConfig.GameDispatchChannelName))
        {
            throw new NullReferenceException("GameDispatchChannelName cannot be empty on the game preset");
        }

        if (string.IsNullOrEmpty(presetConfig.GameGatewayDefault))
        {
            throw new NullReferenceException("GameGatewayDefault cannot be empty on the game preset");
        }

        Exception? lastException = null;
        for (int i = 0; i < presetConfig.GameDispatchArrayURL?.Count; i++)
        {
            string currentBaseUrl = presetConfig.GameDispatchArrayURL[i];
            try
            {
                KianaDispatch dispatch = await KianaDispatch
                   .GetDispatch(assetBundleHttpClient,
                                currentBaseUrl,
                                presetConfig.GameDispatchURLTemplate,
                                presetConfig.GameDispatchChannelName,
                                presetConfig.DispatcherKey,
                                gameVersion.VersionArray,
                                token);

                return await KianaDispatch.GetGameserver(assetBundleHttpClient,
                                                         dispatch,
                                                         presetConfig.GameGatewayDefault,
                                                         token);
            }
            catch when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e) when (!token.IsCancellationRequested)
            {
                lastException = e;
            }
        }

        throw lastException ?? new InvalidOperationException("No dispatcher URLs was reachable");
    }

    private static AudioLanguageType GetCurrentGameAudioLanguage(PresetConfig presetConfig)
    {
        using RegistryKey? rootRegistryKey = Registry.CurrentUser.OpenSubKey(presetConfig.ConfigRegistryLocation);
        if (rootRegistryKey?.GetValue(PersonalAudioSetting.ValueName) is not byte[] jsonValue)
        {
            return presetConfig.GameDefaultCVLanguage;
        }

        PersonalAudioSetting? audioSetting =
            jsonValue.Deserialize(HonkaiSettingsJsonContext.Default.PersonalAudioSetting);
        if (audioSetting == null)
        {
            return presetConfig.GameDefaultCVLanguage;
        }

        if (audioSetting
           ._userCVLanguage?
           .StartsWith("Chinese", StringComparison.OrdinalIgnoreCase) ?? false)
        {
            return AudioLanguageType.Chinese;
        }

        // Use default value based on preset.
        return presetConfig.GameDefaultCVLanguage;
    }
}