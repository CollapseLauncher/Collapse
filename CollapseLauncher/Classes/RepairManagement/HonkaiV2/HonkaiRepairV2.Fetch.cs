using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.RepairManagement;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
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
using System.Net.Http;
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

        SenadinaFileResult senadinaResult = null!;
        Task senadinaTask = HttpClientGeneric
                           .GetSenadinaPropertyAsync(senadinaPrimaryUrl,
                                                     senadinaSecondaryUrl,
                                                     GameVersion,
                                                     true,
                                                     this,
                                                     token)
                           .GetResultFromAction(result => senadinaResult = result);

        KianaDispatch gameServerInfo = null!;
        Task gameServerInfoTask = HttpClientAssetBundle
                                 .GetGameServerInfoAsync(gamePresetConfig, GameVersion, token)
                                 .GetResultFromAction(result => gameServerInfo = result);

        await Task.WhenAll(senadinaTask, gameServerInfoTask);
        #endregion

        #region Fetch Video Assets from AssetBundle
        List<FilePropertiesRemote> assetListFromVideo = [];
        Task assetListFromVideoTask =
            HttpClientAssetBundle
               .GetVideoAssetListAsync(gamePresetConfig,
                                       GameVersion,
                                       gameServerInfo,
                                       ignoredAssets.IgnoredVideoCGSubCategory,
                                       this,
                                       token)
               .GetResultFromAction(result =>
                                    {
                                        assetListFromVideo.AddRange(result);
                                        FinalizeVideoAssetsPath(assetListFromVideo);
                                    });
        #endregion

        #region Fetch Audio Assets from AssetBundle
        List<FilePropertiesRemote> assetListFromAudio = [];
        Task assetListFromAudioTask =
            HttpClientAssetBundle
               .GetAudioAssetListAsync(gamePresetConfig,
                                       GameVersion,
                                       gameServerInfo,
                                       senadinaResult.Audio,
                                       ignoredAssets.IgnoredAudioPCKType,
                                       this,
                                       token)
               .GetResultFromAction(async result =>
                                    {
                                        assetListFromAudio.AddRange(result);
                                        await FinalizeAudioAssetsPath(assetListFromAudio,
                                                                      senadinaResult.Audio,
                                                                      token);
                                    });
        #endregion

        #region Fetch Block Assets from AssetBundle
        List<FilePropertiesRemote> assetListFromBlock = [];
        Task assetListFromBlockTask =
            HttpClientAssetBundle
               .GetBlockAssetListAsync(gamePresetConfig,
                                       GameVersion,
                                       GamePath,
                                       gameServerInfo,
                                       senadinaResult,
                                       this,
                                       token)
               .GetResultFromAction(async result =>
                                    {
                                        assetListFromBlock.AddRange(result);
                                        await FinalizeBlockAssetsPath(assetIndex,
                                                                      assetListFromBlock,
                                                                      senadinaResult,
                                                                      token);
                                    });
        #endregion

        #region Run Task Continuation in Parallel
        await Task.WhenAll(assetListFromVideoTask,
                           assetListFromAudioTask,
                           assetListFromBlockTask);
        #endregion

        // Finalize the asset index list by overriding it from above additional sources.
        FinalizeBaseAssetIndex(assetIndex,
                               assetListFromVideo,
                               assetListFromAudio,
                               assetListFromBlock);
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

    #region Fetch Finalizers
    private static void FinalizeBasicAssetsPath(List<FilePropertiesRemote> assetList)
    {
        foreach (FilePropertiesRemote asset in assetList)
        {
            ConverterTool.NormalizePathInplaceNoTrim(asset.N);
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
            string relativePath = Path.Combine(AssetBundleExtension.RelativePathVideo, asset.N);
            ConverterTool.NormalizePathInplaceNoTrim(relativePath);
            if (asset.AssociatedObject is CGMetadata { InStreamingAssets: false })
            {
                versionStreamWriter.WriteLine($"Video/{asset.N}\t1");
            }
            asset.N = relativePath;
        }
    }

    private async Task FinalizeAudioAssetsPath(List<FilePropertiesRemote> assetList,
                                               SenadinaFileIdentifier?    audioManifestIdentifier,
                                               CancellationToken          token)
    {
        // Edit: 2025-05-01
        // Starting from 8.2, the hash for the audio asset will be a sequence of MhyMurmurHash2 in UInt64-BE format.
        // Also, this version added another file as its "state manifest" for the default audio, "AUDIO_Default_manifest.m"
        string gameVersion = $"{GameVersion.Major}.{GameVersion.Minor}";
        ManifestAssetInfo? audioDefaultAsset = assetList
                                              .Select(x => x.AssociatedObject)
                                              .OfType<ManifestAssetInfo>()
                                              .FirstOrDefault(x => x.Name.StartsWith("AUDIO_Default"));

        string audioBasePath = Path.Combine(GamePath, AssetBundleExtension.RelativePathAudio);

        if (audioDefaultAsset != null)
        {
            ulong  audioDefaultAssetHash      = GetLongFromHashStr(audioDefaultAsset.HashString);
            byte[] audioDefaultManifestBuffer = new byte[16];
            audioDefaultAsset.Hash.CopyTo(audioDefaultManifestBuffer);
            await File.WriteAllTextAsync(Path.Combine(audioBasePath, "AUDIO_Default_Version.txt"), $"{gameVersion}\t{audioDefaultAssetHash}", token);
            await File.WriteAllBytesAsync(Path.Combine(audioBasePath, "AUDIO_Default_manifest.m"), audioDefaultManifestBuffer, token);
        }

        // Create Versioning file.
        FileInfo fileInfo = new FileInfo(Path.Combine(audioBasePath, "Version.txt"))
                           .EnsureCreationOfDirectory()
                           .EnsureNoReadOnly()
                           .StripAlternateDataStream();

        if (audioManifestIdentifier != null)
        {
            if (audioManifestIdentifier.lastOriginHash?.Length != 0)
            {
                FilePropertiesRemote manifestAsset = new()
                {
                    CRC = HexTool.BytesToHexUnsafe(audioManifestIdentifier.lastOriginHash),
                    FT  = FileType.Generic,
                    RN  = audioManifestIdentifier.GetOriginalFileUrl(),
                    N   = Path.Combine(AssetBundleExtension.RelativePathAudio, "manifest.m"),
                    S = (await HttpClientAssetBundle.GetURLStatusCode(audioManifestIdentifier.GetOriginalFileUrl(),
                                                                      token)).FileSize ??
                        0
                };
                assetList.Add(manifestAsset);
            }
            else
            {
                // Copy manifest.m if hash info is not available on senadina
                FileInfo manifestFileInfo = new FileInfo(Path.Combine(audioBasePath, "manifest.m"))
                                           .EnsureCreationOfDirectory()
                                           .EnsureNoReadOnly()
                                           .StripAlternateDataStream();

                using HttpResponseMessage originManifestResponse =
                    await audioManifestIdentifier
                       .GetOriginalFileHttpResponse(HttpClientAssetBundle, token: token);
                if (originManifestResponse.IsSuccessStatusCode)
                {
                    await using BridgedNetworkStream originManifestStreamRemote =
                        await BridgedNetworkStream.CreateStream(originManifestResponse, token);
                    await using FileStream originManifestStreamLocal = manifestFileInfo.Create();
                    await DoCopyStreamProgress(originManifestStreamRemote, originManifestStreamLocal, token: token);
                }
            }
        }

        // Build audio versioning file
        await using StreamWriter versionStreamWriter = fileInfo.CreateText();

        foreach (FilePropertiesRemote asset in assetList.Where(x => x.N.EndsWith(".pck", StringComparison.OrdinalIgnoreCase)))
        {
            string relativePath = Path.Combine(AssetBundleExtension.RelativePathAudio, asset.N);
            ConverterTool.NormalizePathInplaceNoTrim(relativePath);
            if (asset.AssociatedObject is ManifestAssetInfo asManifestInfo)
            {
                ulong  audioAssetHash = GetLongFromHashStr(asManifestInfo.HashString);
                string line           = $"{asManifestInfo.Name}.pck\t{audioAssetHash}";
                await versionStreamWriter.WriteLineAsync(line.AsMemory(), token);
            }
            asset.N = relativePath;
        }
    }

    private async Task FinalizeBlockAssetsPath(
        List<FilePropertiesRemote> sourceAssetList,
        List<FilePropertiesRemote> targetAssetList,
        SenadinaFileResult         senadinaResults,
        CancellationToken          token)
    {
        // Block assets replacement and add
        HashSet<string> oldBlockNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (FilePropertiesRemote asset in targetAssetList)
        {
            string relativePath = Path.Combine(AssetBundleExtension.RelativePathBlock, asset.N);
            asset.N = relativePath;

            if (asset.BlockPatchInfo is {} patchInfo &&
                patchInfo.PatchPairs.FirstOrDefault() is {} patchPair)
            {
                oldBlockNames.Add(Path.Combine(AssetBundleExtension.RelativePathBlock, patchPair.OldName));
            }
        }

        Dictionary<string, FilePropertiesRemote> sourceNonBlock =
            sourceAssetList
               .Where(x => !x.N.EndsWith(".wmv", StringComparison.OrdinalIgnoreCase))
               .ToDictionary(x => x.N.NormalizePath());
        Dictionary<string, FilePropertiesRemote> sourceBlock =
            sourceAssetList
               .Where(x => x.N.EndsWith(".wmv", StringComparison.OrdinalIgnoreCase) && !oldBlockNames.Contains(x.N))
               .ToDictionary(x => x.N.NormalizePath());

        sourceAssetList.Clear();
        sourceAssetList.AddRange(sourceNonBlock.Values);

        foreach (FilePropertiesRemote asset in targetAssetList)
        {
            ref FilePropertiesRemote oldAsset =
                ref CollectionsMarshal.GetValueRefOrNullRef(sourceBlock, asset.N);

            if (Unsafe.IsNullRef(ref oldAsset))
            {
                sourceBlock.Add(asset.N, asset);
                continue;
            }

            oldAsset = asset;
        }

        sourceAssetList.AddRange(sourceBlock.Values);

        // Add current version's XMF file into the
        await AddBlockAndMetaToAssetList($"Blocks_{GameVersion.Major}_{GameVersion.Minor}.xmf",
                                         senadinaResults.XmfInfoCurrent,
                                         token);
        await AddBlockAndMetaToAssetList("BlockMeta.xmf",
                                         senadinaResults.XmfInfoCurrent,
                                         token);

        return;

        async Task AddBlockAndMetaToAssetList(string                  targetFilename,
                                              SenadinaFileIdentifier? identifier,
                                              CancellationToken       innerToken)
        {
            if (identifier == null)
            {
                return;
            }

            if (identifier.lastOriginHash?.Length != 0)
            {
                FilePropertiesRemote manifestAsset = new()
                {
                    CRC = HexTool.BytesToHexUnsafe(identifier.lastOriginHash),
                    FT  = FileType.Generic,
                    RN  = identifier.GetOriginalFileUrl(),
                    N   = Path.Combine(AssetBundleExtension.RelativePathBlock, targetFilename),
                    S = (await HttpClientAssetBundle.GetURLStatusCode(identifier.GetOriginalFileUrl(),
                                                                      innerToken)).FileSize ??
                        0
                };
                targetAssetList.Add(manifestAsset);
            }
            else
            {
                string filePath = Path.Combine(GamePath, AssetBundleExtension.RelativePathBlock, targetFilename);
                FileInfo fileInfo = new FileInfo(filePath)
                                   .EnsureCreationOfDirectory()
                                   .EnsureNoReadOnly()
                                   .StripAlternateDataStream();

                using HttpResponseMessage originResponse =
                    await identifier
                       .GetOriginalFileHttpResponse(HttpClientAssetBundle, token: innerToken);
                if (originResponse.IsSuccessStatusCode)
                {
                    await using BridgedNetworkStream originStreamRemote =
                        await BridgedNetworkStream.CreateStream(originResponse, innerToken);
                    await using FileStream originStreamLocal = fileInfo.Create();
                    await DoCopyStreamProgress(originStreamRemote, originStreamLocal, token: innerToken);
                }
            }
        }
    }
    #endregion
}
