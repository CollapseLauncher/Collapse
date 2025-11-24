using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.EncTool.Parser.YSDispatchHelper;
using Hi3Helper.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

#nullable enable
namespace CollapseLauncher
{
    internal partial class GenshinRepair
    {
        internal class PersistentAssetProperty
        {
            public required List<PkgVersionProperties>                                                   AssetIndex         { get; init; }
            public required Dictionary<string, PkgVersionProperties>                                     Hashtable          { get; init; }
            public required Dictionary<string, PkgVersionProperties>.AlternateLookup<ReadOnlySpan<char>> HashtableAltLookup { get; init; }
            public required DownloadProgressDelegate                                                     ReadDelegate       { get; init; }
        }

        internal class PersistentDispatchUrls
        {
            public required string  RefUrl                  { get; init; }
            public required string  RefPath                 { get; init; }
            public required string  FileUrl                 { get; init; }
            public required string  FileStreamingAssetsPath { get; init; }
            public required string  FilePersistentPath      { get; init; }
            public          string? PatchUrl                { get; init; }
        }

        internal delegate (string RelativePath, string PrimaryUrl, string? AltUrl, RepairAssetType AssetType)
            GetRelativePathSelector(PkgVersionProperties asset, string mainUrl, string? patchUrl);

        internal async Task<bool> BuildPersistentManifest(DownloadClient                           downloadClient,
                                                          DownloadProgressDelegate                 downloadProgress,
                                                          List<PkgVersionProperties>               assetIndex,
                                                          Dictionary<string, PkgVersionProperties> hashtableManifest,
                                                          CancellationToken                        token)
        {
            const string platform  = "StandaloneWindows64";

            // Get HttpClient from DownloadClient
            HttpClient client = downloadClient.GetHttpClient();

            // Initialize persistent folder path and check for the folder existence
            string persistentRelativeDir      = $"{ExecPrefix}_Data/Persistent";
            string streamingAssetsRelativeDir = $"{ExecPrefix}_Data/StreamingAssets";
            string persistentDir              = Path.Combine(GamePath, persistentRelativeDir);
            string streamingAssetsDir         = Path.Combine(GamePath, streamingAssetsRelativeDir);

            // Ensure persistent and streamingassets directory
            Directory.CreateDirectory(persistentDir);
            Directory.CreateDirectory(streamingAssetsDir);

            // Create Property
            PersistentAssetProperty assetProperty = new PersistentAssetProperty
            {
                AssetIndex         = assetIndex,
                Hashtable          = hashtableManifest,
                HashtableAltLookup = hashtableManifest.GetAlternateLookup<ReadOnlySpan<char>>(),
                ReadDelegate       = downloadProgress
            };

            try
            {
                // Get the Dispatcher Query
                QueryProperty queryProperty = await GetCachedDispatcherQuery(client, token);

                // Manifest: Get External Resource Assets
                string resVerExtBaseUrl = queryProperty.ClientGameResURL.CombineURLFromString(platform);
                string resVerExtRefUrl  = resVerExtBaseUrl.CombineURLFromString("res_versions_external");
                string resVerExtRefPath = Path.Combine(persistentDir, "res_versions_persist");
                string resVerExtFileUrl = queryProperty.ClientAudioAssetsURL.CombineURLFromString(platform);
                PersistentDispatchUrls resVerUrls = new()
                {
                    RefUrl                  = resVerExtRefUrl,
                    RefPath                 = resVerExtRefPath,
                    PatchUrl                = resVerExtBaseUrl,
                    FileUrl                 = resVerExtFileUrl,
                    FileStreamingAssetsPath = streamingAssetsRelativeDir,
                    FilePersistentPath      = persistentRelativeDir
                };
                await ParsePersistentManifest(client, assetProperty, resVerUrls, RelativePathSelector, token);

                // Manifest: Get Design Assets
                string designBaseUrl = queryProperty.ClientDesignDataURL ?? "";
                string designRefUrl  = designBaseUrl.CombineURLFromString("AssetBundles", "data_versions");
                string designRefPath = Path.Combine(persistentDir, "data_versions_persist");
                PersistentDispatchUrls designUrls = new()
                {
                    RefUrl                  = designRefUrl,
                    RefPath                 = designRefPath,
                    PatchUrl                = designBaseUrl,
                    FileUrl                 = designBaseUrl,
                    FileStreamingAssetsPath = streamingAssetsRelativeDir,
                    FilePersistentPath      = persistentRelativeDir
                };
                await ParsePersistentManifest(client, assetProperty, designUrls, RelativePathSelector, token);

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"An error has occurred while parsing Persistent Manifests! {ex}", LogType.Warning, true);
                return false;
            }
        }

        private static (string RelativePath, string PrimaryUrl, string? AltUrl, RepairAssetType AssetType)
            RelativePathSelector(PkgVersionProperties asset, string mainUrl, string? patchUrl)
        {
            string relativePath = GetRelativePathByRemoteName(asset.remoteName, out RepairAssetType assetType);

            if (asset.isPatch && !string.IsNullOrEmpty(patchUrl))
            {
                return (relativePath, patchUrl, mainUrl, assetType);
            }

            return (relativePath, mainUrl, patchUrl, assetType);
        }

        internal static async Task ParsePersistentManifest(HttpClient              client,
                                                           PersistentAssetProperty persistentProperty,
                                                           PersistentDispatchUrls  persistentUrls,
                                                           GetRelativePathSelector assetRelativePathSelector,
                                                           CancellationToken       token)
        {
            // Get reference stream and reader. Use CopyToStream so the reference data from NetworkStream to FileStream
            // can be copied while being read by the StreamReader.
            await using Stream networkStream = (await client.TryGetCachedStreamFrom(persistentUrls.RefUrl, token: token)).Stream;
            await using FileStream resStream = File.Create(persistentUrls.RefPath);
            await using CopyToStream bridgeStream = new CopyToStream(networkStream, resStream, persistentProperty.ReadDelegate, true);
            using StreamReader streamReader = new StreamReader(bridgeStream);

            // Start enumerate the line and deserialize the entry
            while (await streamReader.ReadLineAsync(token) is { } line)
            {
                // If the current line is not a valid JSON or the deserializer returns
                // a null PkgVersionProperties, then go to the next line.
                if (!IsCurrentLineJson(line) ||
                    line.Deserialize(CoreLibraryJsonContext.Default.PkgVersionProperties) is not { } asset)
                {
                    continue;
                }

                // Get asset relative path by the selector.
                var assetPaths = assetRelativePathSelector(asset, persistentUrls.FileUrl, persistentUrls.PatchUrl);

                // Assign asset local path
                ReadOnlySpan<char> localBasePath = asset.isPatch ?
                    persistentUrls.FilePersistentPath :
                    persistentUrls.FileStreamingAssetsPath;
                string assetLocalPath = ConverterTool.CombineURLFromString(localBasePath, assetPaths.RelativePath, asset.localName ?? asset.remoteName);

                // If the hash is different or forcefully required to store on persistent, then flag as isPatch = true
                if (IsRequireForcePersistent(asset, assetLocalPath, persistentProperty))
                {
                    asset.isPatch = true;
                    assetPaths = assetRelativePathSelector(asset, persistentUrls.FileUrl, persistentUrls.PatchUrl);
                    localBasePath = persistentUrls.FilePersistentPath;
                    assetLocalPath = ConverterTool.CombineURLFromString(localBasePath, assetPaths.RelativePath, asset.localName ?? asset.remoteName);
                }

                // Determine file placement
                switch (asset)
                {
                    case { isPatch: true }:
                        asset.isForceStoreInPersistent = true;
                        asset.isForceStoreInStreaming  = false;
                        break;
                    default:
                        asset.isForceStoreInPersistent = false;
                        asset.isForceStoreInStreaming  = true;
                        break;
                }

                // Assign asset URLs
                asset.remoteURL            = assetPaths.PrimaryUrl.CombineURLFromString(assetPaths.RelativePath, asset.remoteName);
                asset.remoteURLAlternative = (assetPaths.AltUrl ?? assetPaths.PrimaryUrl).CombineURLFromString(assetPaths.RelativePath, asset.remoteName);

                // Assign remoteName with local path
                asset.remoteName = assetLocalPath;

                // Try override existed assets
                TryReplaceAssetToPersistent(asset, assetPaths.RelativePath, persistentUrls, persistentProperty);
            }
        }

        internal static bool IsRequireForcePersistent(PkgVersionProperties asset, string assetKey, PersistentAssetProperty property)
        {
            // Check if the key exists on hashtable. If not, return false as marked as "non-forced"
            if (!property.HashtableAltLookup.TryGetValue(assetKey, out PkgVersionProperties? existedAsset))
            {
                return false;
            }

            // Otherwise, compare the hash. If it's not the same, mark as "forced"
            bool isHashEqual = existedAsset.md5.Equals(asset.md5, StringComparison.OrdinalIgnoreCase);
            return !isHashEqual;
        }

        internal static void TryReplaceAssetToPersistent(PkgVersionProperties    asset,
                                                         string                  assetRelativePath,
                                                         PersistentDispatchUrls  persistentUrls,
                                                         PersistentAssetProperty property)
        {
            // Get base path
            // Get the asset key and try adding the asset.
            if (property.HashtableAltLookup.TryAdd(asset.remoteName, asset))
            {
                property.AssetIndex.Add(asset);
                return;
            }

            // If it already existed, then try to overwrite with the new one
            ref PkgVersionProperties existedAsset = ref CollectionsMarshal.GetValueRefOrNullRef(property.HashtableAltLookup, asset.remoteName);
            int existedAssetIndex = property.AssetIndex.IndexOf(existedAsset);
            if (existedAssetIndex < 0)
            {
                return;
            }

            // Replace the reference of the existed one with new asset
            property.AssetIndex[existedAssetIndex] = asset;
            existedAsset                           = asset;
        }

        internal static string GetRelativePathByRemoteName(ReadOnlySpan<char> remoteName, out RepairAssetType assetType)
        {
            assetType = RepairAssetType.Generic;

            const string lookupEndsWithAudio = ".pck";
            const string returnAudio         = "AudioAssets";

            if (remoteName.EndsWith(lookupEndsWithAudio, StringComparison.OrdinalIgnoreCase))
            {
                assetType = RepairAssetType.Audio;
                return returnAudio;
            }

            const string lookupStartsWithBlocks = "blocks";
            const string lookupEndsWithBlocksA  = ".blk";
            const string lookupEndsWithBlocksB  = "svc_catalog";
            const string returnBlocks           = "AssetBundles";

            if ((remoteName.Contains(lookupStartsWithBlocks, StringComparison.OrdinalIgnoreCase) &&
                remoteName.EndsWith(lookupEndsWithBlocksA, StringComparison.OrdinalIgnoreCase)) ||
                remoteName.EndsWith(lookupEndsWithBlocksB, StringComparison.OrdinalIgnoreCase))
            {
                assetType = RepairAssetType.Block;
                return returnBlocks;
            }

            const string lookupEndsWithVideoA = ".usm";
            const string lookupEndsWithVideoB = ".cuepoint";
            const string returnVideo          = "VideoAssets";

            // ReSharper disable once InvertIf
            if (remoteName.EndsWith(lookupEndsWithVideoA, StringComparison.OrdinalIgnoreCase) ||
                remoteName.EndsWith(lookupEndsWithVideoB, StringComparison.OrdinalIgnoreCase))
            {
                assetType = RepairAssetType.Video;
                return returnVideo;
            }

            return "";
        }
    }
}
