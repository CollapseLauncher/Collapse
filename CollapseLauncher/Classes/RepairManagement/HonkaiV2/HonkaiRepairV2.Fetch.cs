using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.RepairManagement;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.EncTool.Parser.CacheParser;
using Hi3Helper.EncTool.Parser.KianaDispatch;
using Hi3Helper.EncTool.Parser.Senadina;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Sophon;
using Microsoft.Win32;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
        public SenadinaFileIdentifier? Audio          { get; init; }
        public SenadinaFileIdentifier? XmfMeta        { get; init; }
        public SenadinaFileIdentifier? XmfInfoCurrent { get; init; }
        public SenadinaFileIdentifier? XmfPatch       { get; init; }
    }

    #region Fetch by Sophon
    private async Task FetchAssetFromSophon(List<FilePropertiesRemote> assetIndex, CancellationToken token)
    {
        await this.FetchAssetsFromSophonAsync(HttpClientGeneric,
                                              assetIndex,
                                              DetermineFileTypeFromExtension,
                                              GameVersion,
                                              ["en-us", "zh-cn", "ja-jp", "ko-kr"],
                                              token);
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
        List<FilePropertiesRemote> assetListFromVideoOnlyDownloadable = [];
        Task assetListFromVideoTask =
            HttpClientAssetBundle
               .GetVideoAssetListAsync(gamePresetConfig,
                                       gameServerInfo,
                                       this,
                                       ignoredAssets.IgnoredVideoCgSubCategory,
                                       token)
               .GetResultFromAction(result =>
                                    {
                                        assetListFromVideo.AddRange(result);
                                        assetListFromVideoOnlyDownloadable.AddRange(result.Where(x => ((KianaCgMetadata)x.AssociatedObject).DownloadMode == CGDownloadMode.DownloadTipOnce));
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
                                       this,
                                       ignoredAssets.IgnoredAudioPckType,
                                       token)
               .GetResultFromAction(async result =>
                                    {
                                        assetListFromAudio.AddRange(result);
                                        await FinalizeAudioAssetsPath(assetIndex,
                                                                      assetListFromAudio,
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

        #region Remove Video Assets from base

        if (!IsMainAssetOnlyMode && !IsCacheMode)
        {
            assetIndex.RemoveUnlistedVideoAssetFromList(assetListFromVideo);
        }

        #endregion

        // Finalize the asset index list by overriding it from above additional sources.
        FinalizeBaseAssetIndex(assetIndex,
                               assetListFromVideoOnlyDownloadable,
                               assetListFromAudio,
                               assetListFromBlock);
    }
    #endregion

    #region Fetch by Game Cache Files
    private static Task FetchAssetFromGameCacheFiles(List<FilePropertiesRemote> assetIndex, CancellationToken token)
    {
        // TODO: Use it for altering assets for Cache Update mode
        return Task.CompletedTask;
    }
    #endregion

    #region Fetch Utils
    private static void RemoveBlockAssetFromList(List<FilePropertiesRemote> assetIndex)
    {
        List<FilePropertiesRemote> filtered = [];
        filtered.AddRange(assetIndex.Where(x => x.FT != FileType.Block));
        assetIndex.Clear();
        assetIndex.AddRange(filtered);
    }

    private static FileType DetermineFileTypeFromExtension(string fileName)
    {
        if (fileName.EndsWith(".wmv", StringComparison.OrdinalIgnoreCase))
        {
            return FileType.Block;
        }

        if (fileName.EndsWith(".usm", StringComparison.OrdinalIgnoreCase))
        {
            return FileType.Video;
        }

        if (fileName.EndsWith(".pck", StringComparison.OrdinalIgnoreCase))
        {
            return FileType.Audio;
        }

        return FileType.Generic;
    }

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
            IgnoredAudioPckType       = ignoredAudioPckTypes,
            IgnoredVideoCgSubCategory = ignoredVideoCgSubCategory
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
            if (asset.AssociatedObject is KianaCgMetadata { DownloadMode: CGDownloadMode.DownloadTipOnce })
            {
                versionStreamWriter.WriteLine($"Video/{asset.N}\t1");
            }
            asset.N = relativePath;
        }
    }

    private async Task FinalizeAudioAssetsPath(List<FilePropertiesRemote> originAssetList,
                                               List<FilePropertiesRemote> assetList,
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
        FilePropertiesRemote? audioDefaultManifestAsset = originAssetList.FirstOrDefault(x => x.N.EndsWith("AUDIO_Default_manifest.m"));

        // Edit: 2026-05-07
        // Starting from 8.8, AUDIO_Default_manifest.m must expect to be existed. So, if the file is missing or hash doesn't match,
        // perform download in the background. Then write AUDIO_Default_Version.txt after.
        string audioDefaultVersionPath = Path.Combine(audioBasePath, "AUDIO_Default_Version.txt");
        string audioDefaultManifestPath = Path.Combine(audioBasePath, "AUDIO_Default_manifest.m");
        if (await EnsureAudioDefaultManifestExisted(HttpClientGeneric, audioDefaultManifestAsset, audioDefaultManifestPath, token))
        {
            byte[] manifestContent = File.ReadAllBytes(audioDefaultManifestPath);
            ulong audioDefaultAssetHash = BinaryPrimitives.ReadUInt64BigEndian(manifestContent.AsSpan(0, 8));
            await File.WriteAllTextAsync(audioDefaultVersionPath, $"{gameVersion}\t{audioDefaultAssetHash}", token);
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
                    AssociatedObject = audioManifestIdentifier,
                    CRC              = HexTool.BytesToHexUnsafe(audioManifestIdentifier.lastOriginHash),
                    FT               = FileType.Generic,
                    RN               = audioManifestIdentifier.GetOriginalFileUrl(),
                    N                = Path.Combine(AssetBundleExtension.RelativePathAudio, "manifest.m"),
                    S = (await HttpClientAssetBundle.GetURLStatusCode(audioManifestIdentifier.GetOriginalFileUrl(),
                                                                      token)).FileSize
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


                CDNCacheResult originManifestResponse =
                    await audioManifestIdentifier
                       .GetOriginalFileHttpResponse(HttpClientAssetBundle, token: token);
                if (originManifestResponse.IsSuccessStatusCode)
                {
                    await using Stream     originManifestStreamRemote = originManifestResponse.Stream;
                    await using FileStream originManifestStreamLocal  = manifestFileInfo.Create();
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

    private async Task<bool> EnsureAudioDefaultManifestExisted(
        HttpClient client,
        FilePropertiesRemote? audioDefaultManifestAsset,
        string audioDefaultManifestPath,
        CancellationToken token)
    {
        if (audioDefaultManifestAsset == null)
        {
            return false;
        }

        // Check current hash first. If not match, delete and redownload.
        if (File.Exists(audioDefaultManifestPath))
        {
            byte[] hashRemote = audioDefaultManifestAsset.CRCArray;
            byte[] hashLocal = hashRemote.Length == 8 ?
                await GetHashAsync<XxHash64>(audioDefaultManifestPath, false, false, token) :
                await GetCryptoHashAsync<MD5>(audioDefaultManifestPath, null, false, false, token);

            if (!IsBytesEqualReversible(hashRemote, hashLocal))
            {
                new FileInfo(audioDefaultManifestPath).TryDeleteFile();
            }
        }

        if (!File.Exists(audioDefaultManifestPath))
        {
            if (audioDefaultManifestAsset.AssociatedObject is SophonAsset)
            {
                await RepairAssetGenericSophonType(audioDefaultManifestAsset, token);
            }
            else
            {
                await RepairAssetGenericType(client, audioDefaultManifestAsset, token);
            }
        }

        return true;
    }

    private async Task FinalizeBlockAssetsPath(
        List<FilePropertiesRemote> sourceAssetList,
        List<FilePropertiesRemote> targetAssetList,
        SenadinaFileResult         senadinaResults,
        CancellationToken          token)
    {
        // Block assets replacement and add
        HashSet<string> oldBlockNames = new(StringComparer.OrdinalIgnoreCase);
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
                    AssociatedObject = identifier,
                    CRC              = HexTool.BytesToHexUnsafe(identifier.lastOriginHash),
                    FT               = FileType.Generic,
                    RN               = identifier.GetOriginalFileUrl(),
                    N                = Path.Combine(AssetBundleExtension.RelativePathBlock, targetFilename),
                    S = (await HttpClientAssetBundle.GetURLStatusCode(identifier.GetOriginalFileUrl(),
                                                                      innerToken)).FileSize
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

                CDNCacheResult originResponse =
                    await identifier
                       .GetOriginalFileHttpResponse(HttpClientAssetBundle, token: innerToken);
                if (originResponse.IsSuccessStatusCode)
                {
                    await using Stream     originStreamRemote = originResponse.Stream;
                    await using FileStream originStreamLocal  = fileInfo.Create();
                    await DoCopyStreamProgress(originStreamRemote, originStreamLocal, token: innerToken);
                }
            }
        }
    }
    #endregion
}
