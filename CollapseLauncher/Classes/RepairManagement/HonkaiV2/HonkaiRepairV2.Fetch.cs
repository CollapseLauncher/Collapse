using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.RepairManagement;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.EncTool.Parser.Cache;
using Hi3Helper.EncTool.Parser.KianaDispatch;
using Hi3Helper.EncTool.Parser.Senadina;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Sophon;
using Hi3Helper.Sophon.Structs;
using Microsoft.Win32;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher;

internal partial class HonkaiRepairV2
{
    internal class SenadinaFileResult
    {
        public SenadinaFileIdentifier? Audio          { get; set; }
        public SenadinaFileIdentifier? XmfMeta        { get; set; }
        public SenadinaFileIdentifier? XmfInfoBase    { get; set; }
        public SenadinaFileIdentifier? XmfInfoCurrent { get; set; }
        public SenadinaFileIdentifier? XmfPatch       { get; set; }
    }

    #region Fetch by Sophon
    private async Task FetchAssetFromSophon(List<FilePropertiesRemote> assetIndex, CancellationToken token)
    {
        // Set total activity string as "Fetching Caches Type: <type>"
        Status.ActivityStatus            = string.Format(Locale.Lang._CachesPage.CachesStatusFetchingType, "Sophon");
        Status.IsProgressAllIndetermined = true;
        Status.IsIncludePerFileIndicator = false;
        UpdateStatus();

        PresetConfig gamePreset    = GameVersionManager.GamePreset;
        string?      sophonApiUrl  = gamePreset.LauncherResourceChunksURL?.MainUrl;
        string?      matchingField = gamePreset.LauncherResourceChunksURL?.MainBranchMatchingField;

        if (sophonApiUrl == null)
        {
            throw new NullReferenceException("Sophon API URL inside of the metadata is null. Try to clear your metadata by going to \"App Settings\" > \"Clear Metadata and Restart\"");
        }

        string versionApiToUse = GameVersion.ToString();
        sophonApiUrl += $"&tag={versionApiToUse}";

        SophonChunkManifestInfoPair infoPair = await SophonManifest
           .CreateSophonChunkManifestInfoPair(HttpClientGeneric,
                                              sophonApiUrl,
                                              matchingField,
                                              false,
                                              token);

        if (!infoPair.IsFound)
        {
            throw new InvalidOperationException($"Sophon cannot find matching field: {matchingField} from API URL: {sophonApiUrl}");
        }

        SearchValues<string> excludedMatchingFields =
            SearchValues.Create(["en-us", "zh-cn", "ja-jp", "ko-kr"], StringComparison.OrdinalIgnoreCase);
        List<SophonChunkManifestInfoPair> infoPairs = [infoPair];
        infoPairs.AddRange(infoPair
                          .OtherSophonBuildData?
                          .ManifestIdentityList
                          .Where(x => !x.MatchingField
                                        .ContainsAny(excludedMatchingFields) && !x.MatchingField.Equals(matchingField))
                          .Select(x => infoPair.GetOtherManifestInfoPair(x.MatchingField)) ?? []);


        foreach (SophonChunkManifestInfoPair pair in infoPairs)
        {
            await foreach (SophonAsset asset in SophonManifest
                              .EnumerateAsync(HttpClientGeneric,
                                              pair,
                                              token: token))
            {
                assetIndex.Add(new FilePropertiesRemote
                {
                    AssociatedObject = asset,
                    S                = asset.AssetSize,
                    N                = asset.AssetName.NormalizePath(),
                    CRC              = asset.AssetHash,
                    FT               = DetermineFileTypeFromExtension(asset.AssetName)
                });
            }
        }
    }
    #endregion

    #region Fetch by Game AssetBundle
    private async Task FetchAssetFromGameAssetBundle(List<FilePropertiesRemote> assetIndex, CancellationToken token)
    {
        PresetConfig gamePresetConfig = GameVersionManager.GamePreset;
        FinalizeBasicAssetsPath(assetIndex);

        // Get ignored assets from registry
        HonkaiRepairAssetIgnore ignoredAssets = GetIgnoredAssetsProperty(gamePresetConfig.ConfigRegistryLocation);

        #region Fetch Senadina Daftar Pustaka
        // Fetch asset list from Senadina files.
        string senadinaPrimaryUrl = $"https://cdn.collapselauncher.com/cl-meta/pustaka/{gamePresetConfig.ProfileName}/{GameVersion}";
        string senadinaSecondaryUrl = $"https://r2.bagelnl.my.id/cl-meta/pustaka/{gamePresetConfig.ProfileName}/{GameVersion}";

        SenadinaFileResult senadinaResult = await HttpClientGeneric
           .GetSenadinaPropertyAsync(senadinaPrimaryUrl,
                                     senadinaSecondaryUrl,
                                     GameVersion,
                                     true,
                                     this,
                                     token);
        #endregion

        #region Fetch Video Assets from AssetBundle
        (List<FilePropertiesRemote> assetListFromCg, KianaDispatch gameServerInfo) =
            await HttpClientAssetBundle
               .GetVideoAssetListAsync(gamePresetConfig,
                                       GameVersion,
                                       ignoredAssets.IgnoredVideoCGSubCategory,
                                       this,
                                       token);
        FinalizeVideoAssetsPath(assetListFromCg);
        #endregion

        #region Fetch Audio Assets from AssetBundle
        List<FilePropertiesRemote> assetListFromAudio =
            await HttpClientAssetBundle
               .GetAudioAssetListAsync(gamePresetConfig,
                                       GameVersion,
                                       gameServerInfo,
                                       senadinaResult.Audio,
                                       ignoredAssets.IgnoredAudioPCKType,
                                       this,
                                       token);
        FinalizeAudioAssetsPath(assetListFromAudio);
        #endregion

        // Finalize the asset index list by overriding it from above additional sources.
        FinalizeBaseAssetIndex(assetIndex,
                               assetListFromCg,
                               assetListFromAudio);
    }

    private void FinalizeBasicAssetsPath(List<FilePropertiesRemote> assetList)
    {
        foreach (FilePropertiesRemote asset in assetList)
        {
            string relativePath = Path.Combine(GamePath, asset.N);
            ConverterTool.NormalizePathInplaceNoTrim(relativePath);
            asset.N = relativePath;
        }
    }

    private void FinalizeVideoAssetsPath(List<FilePropertiesRemote> assetList)
    {
        FileInfo fileInfo = new FileInfo(Path.Combine(GamePath, AssetBundleExtension.RelativePathVideo, "Version.txt"))
                           .EnsureCreationOfDirectory()
                           .EnsureNoReadOnly()
                           .StripAlternateDataStream();

        using StreamWriter versionStreamWriter = fileInfo.CreateText();

        foreach (FilePropertiesRemote asset in assetList)
        {
            string relativePath = Path.Combine(GamePath, AssetBundleExtension.RelativePathVideo, asset.N);
            ConverterTool.NormalizePathInplaceNoTrim(relativePath);
            if (asset.AssociatedObject is CGMetadata { InStreamingAssets: false })
            {
                versionStreamWriter.WriteLine($"Video/{asset.N}\t1");
            }
            asset.N = relativePath;
        }
    }

    private void FinalizeAudioAssetsPath(List<FilePropertiesRemote> assetList)
    {
        // Edit: 2025-05-01
        // Starting from 8.2, the hash for the audio asset will be a sequence of MhyMurmurHash2 in UInt64-BE format.
        // Also, this version added another file as its "state manifest" for the default audio, "AUDIO_Default_manifest.m"
        string gameVersion = $"{GameVersion.Major}.{GameVersion.Minor}";
        ManifestAssetInfo? audioDefaultAsset = assetList
                                              .Select(x => x.AssociatedObject)
                                              .OfType<ManifestAssetInfo>()
                                              .FirstOrDefault(x => x.Name.StartsWith("AUDIO_Default"));

        if (audioDefaultAsset != null)
        {
            ulong      audioDefaultAssetHash      = GetLongFromHashStr(audioDefaultAsset.HashString);
            Span<byte> audioDefaultManifestBuffer = stackalloc byte[16];
            audioDefaultAsset.Hash.CopyTo(audioDefaultManifestBuffer);
            File.WriteAllText(Path.Combine(GamePath, AssetBundleExtension.RelativePathAudio, "AUDIO_Default_Version.txt"), $"{gameVersion}\t{audioDefaultAssetHash}");
            File.WriteAllBytes(Path.Combine(GamePath, AssetBundleExtension.RelativePathAudio, "AUDIO_Default_manifest.m"), audioDefaultManifestBuffer);
        }

        FileInfo fileInfo = new FileInfo(Path.Combine(GamePath, AssetBundleExtension.RelativePathAudio, "Version.txt"))
                           .EnsureCreationOfDirectory()
                           .EnsureNoReadOnly()
                           .StripAlternateDataStream();

        // Build audio versioning file
        using StreamWriter versionStreamWriter = fileInfo.CreateText();

        foreach (FilePropertiesRemote asset in assetList)
        {
            string relativePath = Path.Combine(GamePath, AssetBundleExtension.RelativePathAudio, asset.N);
            ConverterTool.NormalizePathInplaceNoTrim(relativePath);
            if (asset.AssociatedObject is ManifestAssetInfo asManifestInfo)
            {
                ulong audioAssetHash = GetLongFromHashStr(asManifestInfo.HashString);
                versionStreamWriter.WriteLine($"{asManifestInfo.Name}.pck\t{audioAssetHash}");
            }
            asset.N = relativePath;
        }
    }
    #endregion

    #region Fetch by Game Cache Files
    private async Task FetchAssetFromGameCacheFiles(List<FilePropertiesRemote> assetIndex, CancellationToken token)
    {

    }
    #endregion

    #region Fetch Utils
    private static ulong GetLongFromHashStr(ReadOnlySpan<char> span)
    {
        Span<byte> spanChar = stackalloc byte[span.Length];
        _ = HexTool.TryHexToBytesUnsafe(span, spanChar);
        return BinaryPrimitives.ReadUInt64BigEndian(spanChar);
    }

    private static HonkaiRepairAssetIgnore GetIgnoredAssetsProperty(string configRegistryRootLocation)
    {
        // Try to get the parent registry key
        RegistryKey? keys = Registry.CurrentUser.OpenSubKey(configRegistryRootLocation);
        if (keys == null) return HonkaiRepairAssetIgnore.CreateEmpty(); // Return an empty property if the parent key doesn't exist

        // Initialize the property
        AudioPCKType[] ignoredAudioPckTypes      = [];
        int[]          ignoredVideoCgSubCategory = [];

        // Try to get the values of the registry key of the Audio ignored list
        if (keys.GetValue("GENERAL_DATA_V2_DeletedAudioTypes_h214176984") is byte[] audioJson)
        {
            ignoredAudioPckTypes = audioJson.Deserialize(GenericJsonContext.Default.AudioPCKTypeArray) ?? ignoredAudioPckTypes;
        }

        // Try to get the values of the registry key of the Video CG ignored list
        if (keys.GetValue("GENERAL_DATA_V2_DeletedCGPackages_h2282700200") is byte[] videoCgJson)
        {
            ignoredVideoCgSubCategory = videoCgJson.Deserialize(GenericJsonContext.Default.Int32Array) ?? ignoredVideoCgSubCategory;
        }

        // Return the property value
        return new HonkaiRepairAssetIgnore
        {
            IgnoredAudioPCKType       = ignoredAudioPckTypes,
            IgnoredVideoCGSubCategory = ignoredVideoCgSubCategory
        };
    }

    private static void FinalizeBaseAssetIndex(
        List<FilePropertiesRemote>                      baseAssetIndex,
        params ReadOnlySpan<List<FilePropertiesRemote>> overrideAssetIndex)
    {
        Dictionary<string, FilePropertiesRemote> filtered =
            baseAssetIndex.ToDictionary(x => x.N);

        foreach (List<FilePropertiesRemote> assetList in overrideAssetIndex)
        {
            foreach (FilePropertiesRemote asset in assetList)
            {
                ref FilePropertiesRemote oldAsset =
                    ref CollectionsMarshal.GetValueRefOrNullRef(filtered, asset.N);
                if (Unsafe.IsNullRef(ref oldAsset))
                {
                    filtered.Add(asset.N, asset);
                    continue;
                }

                // Override the old asset with the new one
                oldAsset = asset;
            }
        }

        baseAssetIndex.Clear();
        baseAssetIndex.AddRange(filtered.Values);
    }
    #endregion
}
