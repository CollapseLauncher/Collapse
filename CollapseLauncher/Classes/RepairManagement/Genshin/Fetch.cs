using CollapseLauncher.GameVersioning;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.InstallManager.Genshin;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.EncTool.Parser.YSDispatchHelper;
using Hi3Helper.Http;
using Hi3Helper.Sophon;
using Hi3Helper.Sophon.Structs;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
// ReSharper disable IdentifierTypo
// ReSharper disable CheckNamespace
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable GrammarMistakeInComment

namespace CollapseLauncher
{
    internal partial class GenshinRepair
    {
        private async Task<List<PkgVersionProperties>> Fetch(List<PkgVersionProperties> assetIndex, CancellationToken token)
        {
            // Set total activity string as "Loading Indexes..."
            Status.ActivityStatus = Lang._GameRepairPage.Status2;
            Status.IsProgressAllIndetermined = true;

            UpdateStatus();

            // Initialize hashtable for duplicate keys checking
            Dictionary<string, PkgVersionProperties> hashtableManifest = new();

            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder()
                .UseLauncherConfig(DownloadThreadWithReservedCount)
                .SetUserAgent(UserAgent)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Initialize the new DownloadClient instance
            DownloadClient downloadClient = DownloadClient.CreateInstance(client);

            // Region: PrimaryManifest
            // Build primary manifest
            await BuildPrimaryManifest(downloadClient, assetIndex, hashtableManifest, token);

            // Region: PersistentManifest
            // Build persistent manifest
            IsParsePersistentManifestSuccess = await BuildPersistentManifest(downloadClient, _httpClient_FetchManifestAssetProgress, assetIndex, hashtableManifest, token);

            // Force-Fetch the Bilibili SDK (if exist :pepehands:)
            await FetchBilibiliSdk(token);

            // Remove plugin from assetIndex
            EliminatePluginAssetIndex(assetIndex);

            // Clear hashtableManifest
            hashtableManifest.Clear();

            // Eliminate unnecessary asset indexes if persistent manifest is successfully parsed
            if (!IsParsePersistentManifestSuccess)
            {
                return assetIndex;
            }

            // Section: Eliminate unused audio files
            List<string> audioLangList     = (GameVersionManager as GameTypeGenshinVersion)!.AudioVoiceLanguageList;
            string       audioLangListPath = Path.Combine(GamePath, $"{ExecPrefix}_Data", "Persistent", "audio_lang_14");
            EliminateUnnecessaryAssetIndex(audioLangListPath, audioLangList, assetIndex, '/', x => x.remoteName);

            return assetIndex;
        }

        private void EliminatePluginAssetIndex(List<PkgVersionProperties> assetIndex)
        {
            GameVersionManager.GameApiProp.data!.plugins?.ForEach(plugin =>
               {
                   if (plugin.package?.validate == null) return;

                   assetIndex.RemoveAll(asset =>
                    {
                        var r = plugin.package.validate.Any(validate => validate.path != null &&
                                                    (asset.localName.Contains(validate.path) ||
                                                    asset.remoteName.Contains(validate.path) ||
                                                    asset.remoteNamePersistent.Contains(validate.path)));
                        if (r)
                        {
                            LogWriteLine($"[EliminatePluginAssetIndex] Removed: {asset.localName}", LogType.Warning,
                                true);
                        }
                        return r;
                    });
               });
        }

        internal static void EliminateUnnecessaryAssetIndex<T>(string          audioLangListPath,
                                                               List<string>    audioLangList,
                                                               List<T>         assetIndex,
                                                               char            separatorChar,
                                                               Func<T, string> predicate)
        {
            // Get the list of audio lang list
            string[] currentAudioLangList = File.Exists(audioLangListPath) ? File.ReadAllLines(audioLangListPath) : [];

            // Set the ignored audio lang
            string[] ignoredAudioLangList = audioLangList
                .Where(x => !currentAudioLangList.Contains(x))
                .Select(x => $"{separatorChar}{x}{separatorChar}")
                .ToArray();
            SearchValues<string> ignoredAudioLangListSearch = SearchValues.Create(ignoredAudioLangList, StringComparison.OrdinalIgnoreCase);

            // Return only for asset index that doesn't have language included in ignoredAudioLangList
            List<T> tempFiltered = assetIndex.Where(x => !predicate(x)
                                                        .AsSpan()
                                                        .ContainsAny(ignoredAudioLangListSearch))
                                             .ToList();
            assetIndex.Clear();
            assetIndex.AddRange(tempFiltered);
        }

        #region PrimaryManifest
#nullable enable
        private async Task BuildPrimaryManifest(DownloadClient                           downloadClient,
                                                List<PkgVersionProperties>               assetIndex,
                                                Dictionary<string, PkgVersionProperties> hashtableManifest,
                                                CancellationToken                        token)
        {
            // Try Cleanup Download Profile file
            TryDeleteDownloadPref();

            // Get Sophon Properties
            IGameVersion     gameVersionManager = GameVersionManager;
            string           gameAudioListPath  = Path.Combine(GamePath, $"{ExecPrefix}_Data", "Persistent", "audio_lang_14");
            SophonChunkUrls? sophonManifestUrls = gameVersionManager.GamePreset.LauncherResourceChunksURL;
            HttpClient       httpClient         = downloadClient.GetHttpClient();

            if (sophonManifestUrls == null)
            {
                LogWriteLine("Cannot get SophonChunkUrls property as it returns null from the Metadata. Reference of the Game Repair might not be complete. Ignoring!", LogType.Warning, true);
                return;
            }

#if DEBUG
            LogWriteLine($"Fetching data reference from Sophon using Main Url: {sophonManifestUrls.MainUrl}", LogType.Debug, true);
#endif

            // Get Sophon main manifest info pair
            SophonChunkManifestInfoPair manifestMainInfoPair = await SophonManifest
                .CreateSophonChunkManifestInfoPair(httpClient,
                                                   sophonManifestUrls.MainUrl,
                                                   sophonManifestUrls.MainBranchMatchingField,
                                                   token);

            // Create fake pkg_version(s) from Sophon and get the list of SphonAsset(s)
            List<SophonAsset> sophonAssetList = [];
            await GenshinInstall.DownloadPkgVersionStatic(httpClient,
                                                          gameVersionManager,
                                                          GamePath,
                                                          gameAudioListPath,
                                                          manifestMainInfoPair,
                                                          sophonAssetList,
                                                          token);

            foreach (SophonAsset asset in sophonAssetList)
            {
                PkgVersionProperties assetAsPkgVersionProp = new()
                {
                    remoteName               = asset.AssetName,
                    fileSize                 = asset.AssetSize,
                    md5                      = asset.AssetHash,
                    isPatch                  = false,
                    isForceStoreInStreaming  = true,
                    isForceStoreInPersistent = false,
                    type                     = "SophonGeneric"
                };

                _ = SophonAssetDictRef.TryAdd(asset.AssetName.NormalizePath(), asset);
                assetIndex.Add(assetAsPkgVersionProp);
                hashtableManifest.TryAdd(asset.AssetName, assetAsPkgVersionProp);
            }
            LogWriteLine($"Main asset list fetched with count: {SophonAssetDictRef.Count} from Sophon manifest", LogType.Default, true);
        }
#nullable restore

/*
        private async Task BuildPrimaryManifestOld(DownloadClient                           downloadClient,
                                                   DownloadProgressDelegate                 downloadProgress,
                                                   List<PkgVersionProperties>               assetIndex,
                                                   Dictionary<string, PkgVersionProperties> hashtableManifest,
                                                   CancellationToken                        token)
        {
            try
            {
                // Try Cleanup Download Profile file
                TryDeleteDownloadPref();

                // Build basic file entry.
                string manifestPath = Path.Combine(GamePath, "pkg_version");

                // Download basic package version list
                var basicVerURL = CombineURLFromString(GameRepoURL, "pkg_version");
#if DEBUG
                LogWriteLine($"Downloading pkg_version...\r\n\t{basicVerURL}", LogType.Debug, true);
#endif
                await downloadClient.DownloadAsync(
                    basicVerURL,
                    EnsureCreationOfDirectory(manifestPath),
                    true,
                    progressDelegateAsync: downloadProgress,
                    maxConnectionSessions: DownloadThreadCount,
                    cancelToken: token
                    );

                // Download additional package lists
                var dataVerPath = $@"{ExecPrefix}_Data\StreamingAssets\data_versions_streaming";
                var dataVerURL = CombineURLFromString(GameRepoURL, dataVerPath);
#if DEBUG
                LogWriteLine($"Downloading data_versions_streaming...\r\n\t{dataVerURL}", LogType.Debug, true);
#endif
                await downloadClient.DownloadAsync(
                    dataVerURL,
                    EnsureCreationOfDirectory(Path.Combine(GamePath, dataVerPath)),
                    true,
                    progressDelegateAsync: downloadProgress,
                    maxConnectionSessions: DownloadThreadCount,
                    cancelToken: token
                    );

                var silenceVerPath = $@"{ExecPrefix}_Data\StreamingAssets\silence_versions_streaming";
                var silenceVerURL = CombineURLFromString(GameRepoURL, silenceVerPath);
#if DEBUG
                LogWriteLine($"Downloading silence_versions_streaming...\r\n\t{silenceVerURL}", LogType.Debug, true);
#endif
                await downloadClient.DownloadAsync(
                    silenceVerURL,
                    EnsureCreationOfDirectory(Path.Combine(GamePath, silenceVerPath)),
                    true,
                    progressDelegateAsync: downloadProgress,
                    maxConnectionSessions: DownloadThreadCount,
                    cancelToken: token
                    );

                var resVerPath = $@"{ExecPrefix}_Data\StreamingAssets\res_versions_streaming";
                var resVerURL = CombineURLFromString(GameRepoURL, resVerPath);
#if DEBUG
                LogWriteLine($"Downloading res_versions_streaming...\r\n\t{resVerURL}", LogType.Debug, true);
#endif
                await downloadClient.DownloadAsync(
                    resVerURL,
                    EnsureCreationOfDirectory(Path.Combine(GamePath, resVerPath)),
                    true,
                    progressDelegateAsync: downloadProgress,
                    maxConnectionSessions: DownloadThreadCount,
                    cancelToken: token
                    );

                var videoVerPath = $@"{ExecPrefix}_Data\StreamingAssets\VideoAssets\video_versions_streaming";
                var videoVerURL = CombineURLFromString(GameRepoURL, videoVerPath);
#if DEBUG
                LogWriteLine($"Downloading video_versions_streaming...\r\n\t{videoVerURL}", LogType.Debug, true);
#endif
                await downloadClient.DownloadAsync(
                    videoVerURL,
                    EnsureCreationOfDirectory(Path.Combine(GamePath, videoVerPath)),
                    true,
                    progressDelegateAsync: downloadProgress,
                    maxConnectionSessions: DownloadThreadCount,
                    cancelToken: token
                    );

                // Parse basic package version.
                var streamingAssetsPath = $@"{ExecPrefix}_Data\StreamingAssets";
                ParseManifestToAssetIndex(manifestPath, "", assetIndex, hashtableManifest, GameRepoURL, true);

                // Build additional blks entry.
                EnumerateManifestToAssetIndex(streamingAssetsPath, streamingAssetsPath, "data_versions_*", assetIndex, hashtableManifest,
                    GameRepoURL, true);
                EnumerateManifestToAssetIndex(streamingAssetsPath, streamingAssetsPath, "silence_versions_*", assetIndex, hashtableManifest,
                    GameRepoURL, true);
                EnumerateManifestToAssetIndex(streamingAssetsPath, streamingAssetsPath, "res_versions_*", assetIndex, hashtableManifest,
                    GameRepoURL, true);

                // Build cutscenes entry.
                EnumerateManifestToAssetIndex(streamingAssetsPath, streamingAssetsPath, "VideoAssets\\*_versions_*", assetIndex, hashtableManifest,
                    GameRepoURL, true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Parsing primary manifest has failed!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }
*/

        private void TryDeleteDownloadPref()
        {
            // Get the paths
            // string downloadPrefPath = Path.Combine(_gamePath, $"{_execPrefix}_Data\\Persistent\\DownloadPref");
            string ctablePersistPath = Path.Combine(GamePath, $@"{ExecPrefix}_Data\Persistent\ctable.dat");

            // Check the file existence and delete it
            // if (File.Exists(downloadPrefPath)) TryDeleteReadOnlyFile(downloadPrefPath);
            if (File.Exists(ctablePersistPath)) TryDeleteReadOnlyFile(ctablePersistPath);
        }
        #endregion

        #region PersistentManifest
        internal async Task<bool> BuildPersistentManifest(DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, List<PkgVersionProperties> assetIndex,
            Dictionary<string, PkgVersionProperties> hashtableManifest, CancellationToken token)
        {
            try
            {
                // Get the Dispatcher Query
                QueryProperty queryProperty = await GetDispatcherQuery(downloadClient.GetHttpClient(), token);

                // Initialize persistent folder path and check for the folder existence
                string basePersistentPath = $"{ExecPrefix}_Data\\Persistent";
                string persistentFolder = Path.Combine(GamePath, basePersistentPath);

                string baseStreamingAssetsPath = $"{ExecPrefix}_Data\\StreamingAssets";
                string streamingAssetsFolder = Path.Combine(GamePath, baseStreamingAssetsPath);

                if (!Directory.Exists(persistentFolder))
                {
                    Directory.CreateDirectory(persistentFolder);
                }

                if (!Directory.Exists(streamingAssetsFolder))
                {
                    Directory.CreateDirectory(streamingAssetsFolder);
                }

                // Parse res_versions_external
                var primaryParentURL =
                    CombineURLFromString(queryProperty.ClientGameResURL, "StandaloneWindows64");
                var secondaryParentURL = CombineURLFromString(queryProperty.ClientAudioAssetsURL, "StandaloneWindows64");
#if DEBUG
                LogWriteLine($"Downloading res_versions_external...\r\n\t" +
                             $"pri: {primaryParentURL}\r\n\t" +
                             $"sec: {secondaryParentURL}", LogType.Debug, true);
#endif
                await ParseManifestToAssetIndex(downloadClient, downloadProgress, primaryParentURL, secondaryParentURL, "res_versions_external",
                    "res_versions_persist", basePersistentPath, baseStreamingAssetsPath, assetIndex, hashtableManifest, token: token);

                // Parse data_versions
                var dataVerURL = queryProperty.ClientDesignDataURL;
#if DEBUG
                LogWriteLine($"Downloading data_versions_persist...\r\n\t" +
                             $"{dataVerURL}", LogType.Debug, true);
#endif
                await ParseManifestToAssetIndex(downloadClient, downloadProgress, dataVerURL, "",
                    CombineURLFromString("AssetBundles", "data_versions"), "data_versions_persist", basePersistentPath,
                    baseStreamingAssetsPath, assetIndex, hashtableManifest, token: token);

                // Parse data_versions (silence)
                var dataSilURL = queryProperty.ClientDesignDataSilURL;
                SearchValues<string> dataSilIgnoreContainsParams = SearchValues.Create([
                    // Containing InjectFix (aka IFix) files since the game only loads
                    // it without storing it locally.
                    "blocks/00/29342328.blk"
                    // ,"blocks/00/32070509.blk" <- this one is stored locally
                ], StringComparison.OrdinalIgnoreCase);
#if DEBUG
                LogWriteLine($"Downloading silence_data_versions_persist...\r\n\t" +
                             $"{dataSilURL}", LogType.Debug, true);
#endif
                await ParseManifestToAssetIndex(downloadClient, downloadProgress, dataSilURL, "",
                    CombineURLFromString("AssetBundles", "data_versions"), "silence_data_versions_persist",
                    basePersistentPath, baseStreamingAssetsPath, assetIndex, hashtableManifest, true, dataSilIgnoreContainsParams, token);

                // Save persistent manifest numbers
                await SavePersistentRevision(queryProperty, token);
                return true;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Parsing persistent manifest has failed. Ignoring!\r\n{ex}", LogType.Error, true);
                return false;
            }
        }

        private async Task ParseManifestToAssetIndex(DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, string primaryParentURL, string secondaryParentURL,
            string manifestRemoteName, string manifestLocalName,
            string persistentPath, string streamingAssetsPath,
            List<PkgVersionProperties> assetIndex, Dictionary<string, PkgVersionProperties> hashtable,
            bool forceOverwrite = false, SearchValues<string> ignoreContainsParams = null,
            CancellationToken token = default)
        {
            try
            {
                // Get the manifest URL and Path
                string manifestURL = CombineURLFromString(primaryParentURL, manifestRemoteName);
                string manifestPath = Path.Combine(GamePath, persistentPath, manifestLocalName);

                // Make sure the file has been deleted (if exist) before redownloading it
                if (File.Exists(manifestPath))
                {
                    TryDeleteReadOnlyFile(manifestPath);
                }

                // Download the manifest
                await downloadClient.DownloadAsync(manifestURL, manifestPath, true, progressDelegateAsync: downloadProgress, cancelToken: token);
                LogWriteLine($"Manifest: {manifestRemoteName} (localName: {manifestLocalName}) has been fetched", LogType.Default, true);

                // Parse the manifest
                await ParsePkgVersionManifestAsync(manifestPath,
                                                   persistentPath,
                                                   streamingAssetsPath,
                                                   primaryParentURL,
                                                   secondaryParentURL,
                                                   assetIndex,
                                                   hashtable,
                                                   forceOverwrite,
                                                   ignoreContainsParams,
                                                   token);
            }
            catch (TaskCanceledException) { throw; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LogWriteLine($"Failed parsing persistent manifest: {manifestRemoteName} (localName: {manifestLocalName}). Skipped!\r\n{ex}", LogType.Warning, true);
            }
        }

        internal static string GetParentFromAssetRelativePath(ReadOnlySpan<char> relativePath, out bool isPersistentAsStreaming)
        {
            isPersistentAsStreaming = false;

            const string lookupEndsWithAudio = ".pck";
            const string returnAudio = "AudioAssets";

            if (relativePath.EndsWith(lookupEndsWithAudio, StringComparison.OrdinalIgnoreCase))
            {
                return returnAudio;
            }

            const string lookupStartsWithBlocks = "blocks";
            const string lookupEndsWithBlocks = ".blk";
            const string returnBlocks = "AssetBundles";

            if (relativePath.StartsWith(lookupStartsWithBlocks, StringComparison.OrdinalIgnoreCase) ||
                relativePath.EndsWith(lookupEndsWithBlocks, StringComparison.OrdinalIgnoreCase))
            {
                return returnBlocks;
            }

            const string lookupEndsWithVideoA = ".usm";
            const string lookupEndsWithVideoB = ".cuepoint";
            const string returnVideo          = "VideoAssets";

            if (relativePath.EndsWith(lookupEndsWithVideoA, StringComparison.OrdinalIgnoreCase) ||
                relativePath.EndsWith(lookupEndsWithVideoB, StringComparison.OrdinalIgnoreCase))
            {
                return returnVideo;
            }

            isPersistentAsStreaming = true;
            return "";
        }

        private static async Task ParsePkgVersionManifestAsync(string localManifestPath,
                                                               string persistentPath,
                                                               string streamingAssetPath,
                                                               string primaryParentURL,
                                                               string secondaryParentURL,
                                                               List<PkgVersionProperties> assetIndex,
                                                               Dictionary<string, PkgVersionProperties> assetIndexHashtable,
                                                               bool forceOverwrite,
                                                               SearchValues<string> ignoreContainsParams = null,
                                                               CancellationToken token = default)
        {
            // Reverse-normalize path from using '\\' to '/' separator
            NormalizePathInplaceNoTrim(persistentPath, '\\', '/');
            NormalizePathInplaceNoTrim(streamingAssetPath, '\\', '/');

            // Read the manifest file
            FileInfo           manifestFileInfo = new FileInfo(localManifestPath).EnsureNoReadOnly();
            using StreamReader reader           = manifestFileInfo.OpenText();

            // Iterate the asset line
            while (await reader.ReadLineAsync(token) is { } currentLine)
            {
                // Sanity check: Ignore non-json entry
                if (IsCurrentLineJson(currentLine))
                {
                    continue;
                }

                // Deserialize manifest entry
                PkgVersionProperties manifestEntry = currentLine.Deserialize(CoreLibraryJsonContext.Default.PkgVersionProperties);

                // If the ignoreContainsParams is not null and the remoteName contains
                // ignore list, then move to another entry
                if (ignoreContainsParams != null &&
                    manifestEntry != null &&
                    manifestEntry.remoteName
                                 .AsSpan()
                                 .ContainsAny(ignoreContainsParams))
                {
                    continue;
                }

                // Resolve the svc_catalog name where the localName contains "../"
                if (manifestEntry != null && manifestEntry.remoteName.EndsWith("svc_catalog"))
                {
                    manifestEntry.localName = Path.GetFileName(manifestEntry.remoteName);
                }

                // Get relative path based on extension
                bool   isUseRemoteName        = string.IsNullOrEmpty(manifestEntry!.localName);
                string actualRelativeFilePath = isUseRemoteName ? manifestEntry.remoteName : manifestEntry.localName;

                // Get relative path based on extension
                string relativePath = GetParentFromAssetRelativePath(manifestEntry.remoteName, out bool isForceStreaming);
                string assetPersistentPath = CombineURLFromString(persistentPath, relativePath, actualRelativeFilePath);
                string assetStreamingAssetPath = CombineURLFromString(streamingAssetPath, relativePath, manifestEntry.remoteName);

                // If the manifest is using a persistent manifest but
                // has uncategorized type, then assume it as a streaming file.
                if (isForceStreaming && !forceOverwrite)
                {
                    manifestEntry.isPatch = false;
                    manifestEntry.isForceStoreInStreaming = true;
                }

                // If forceOverwrite is toggled, use persistent path anyway
                if (forceOverwrite)
                {
                    manifestEntry.isPatch = true;
                }

                // Set the remote URL
                string remoteUrl;
                if (!string.IsNullOrEmpty(secondaryParentURL) && manifestEntry.isPatch)
                {
                    remoteUrl = CombineURLFromString(secondaryParentURL, relativePath, manifestEntry.remoteName);
                }
                else
                {
                    remoteUrl = CombineURLFromString(primaryParentURL, relativePath, manifestEntry.remoteName);
                }

                // Get the remoteName (StreamingAssets) and remoteNamePersistent (Persistent)
                manifestEntry.remoteURL            = remoteUrl;
                manifestEntry.remoteName           = assetStreamingAssetPath;
                manifestEntry.remoteNamePersistent = assetPersistentPath;

                // Fill the null persistent property
                manifestEntry.remoteURLPersistent  ??= manifestEntry.remoteURL;
                manifestEntry.remoteNamePersistent ??= manifestEntry.remoteName;
                manifestEntry.md5Persistent        ??= manifestEntry.md5;
                manifestEntry.xxh64hashPersistent  ??= manifestEntry.xxh64hash;
                if (manifestEntry.fileSizePersistent == 0 && manifestEntry.fileSize != 0)
                {
                    manifestEntry.fileSizePersistent = manifestEntry.fileSize;
                }

                // Try to add or replace the existing entry
                AddOrReplaceEntry(manifestEntry, assetStreamingAssetPath, assetIndex, assetIndexHashtable);
            }
        }

        private static bool IsCurrentLineJson(ReadOnlySpan<char> chars) => chars[0] != '{' && chars[^1] != '}';

        private static void AddOrReplaceEntry(PkgVersionProperties                     manifestEntry,
                                              string                                   fileStreamingAssetPath,
                                              List<PkgVersionProperties>               assetIndex,
                                              Dictionary<string, PkgVersionProperties> assetIndexHashtable)
        {
            // Try to add the current entry to the hashtable
            if (assetIndexHashtable.TryAdd(fileStreamingAssetPath, manifestEntry))
            {
                // If successful, then add it to the list as well
                assetIndex.Add(manifestEntry);
                return;
            }

            // Find the existing table. If not found, then return
            PkgVersionProperties existingAsset = assetIndexHashtable[fileStreamingAssetPath];
            int existingIndex = assetIndex.IndexOf(existingAsset);
            if (existingIndex == -1)
            {
                return;
            }

            // Resolve the ctable.dat to use persistent URL
            if (existingAsset.remoteName.EndsWith("ctable.dat"))
            {
                existingAsset.remoteURL = manifestEntry.remoteURLPersistent ?? manifestEntry.remoteURL;
            }

            // Otherwise, add the existing one with the persistent properties
            existingAsset.fileSizePersistent   = manifestEntry.fileSizePersistent == 0 ? manifestEntry.fileSize : manifestEntry.fileSizePersistent;
            existingAsset.md5Persistent        = manifestEntry.md5Persistent ?? manifestEntry.md5;
            existingAsset.xxh64hashPersistent  = manifestEntry.xxh64hashPersistent ?? manifestEntry.xxh64hash;
            existingAsset.remoteNamePersistent = manifestEntry.remoteNamePersistent ?? manifestEntry.remoteName;
            existingAsset.remoteURLPersistent  = manifestEntry.remoteURLPersistent ?? manifestEntry.remoteURL;
            existingAsset.isPatch              = manifestEntry.isPatch;
        }

        private async Task SavePersistentRevision(QueryProperty dispatchQuery, CancellationToken token)
        {
            string persistentPath = Path.Combine(GamePath, $"{ExecPrefix}_Data\\Persistent");

            // Get base_res_version_hash content
            string filePath = Path.Combine(GamePath, $@"{ExecPrefix}_Data\StreamingAssets\res_versions_streaming");
            await using FileStream resVersionStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            byte[] hashBytes = Hash.GetCryptoHash<MD5>(resVersionStream);
            string hash = Convert.ToHexStringLower(hashBytes);

#nullable enable
            // Write DownloadPref template
            byte[]? prefTemplateBytes = (GameVersionManager as GameTypeGenshinVersion)?.GamePreset
                .GetGameDataTemplate("DownloadPref", GameVersion.VersionArrayManifest.Select(x => (byte)x).ToArray());
            if (prefTemplateBytes != null) await File.WriteAllBytesAsync(persistentPath + "\\DownloadPref", prefTemplateBytes, token);
#nullable disable

            // Get base_res_version_hash content
            await File.WriteAllTextAsync(persistentPath + "\\base_res_version_hash", hash, token);
            // Get data_revision content
            await File.WriteAllTextAsync(persistentPath + "\\data_revision", $"{dispatchQuery.DataRevisionNum}", token);
            // Get res_revision content
            await File.WriteAllTextAsync(persistentPath + "\\res_revision", $"{dispatchQuery.ResRevisionNum}", token);
            // Get res_revision_eternal content (Yes, you hear it right. It's called "eternal", not "external")...
            // or HoYo just probably typoed it (as usual).
            await File.WriteAllTextAsync(persistentPath + "\\res_revision_eternal", $"{dispatchQuery.ResRevisionNum}", token);
            // Get silence_revision content
            await File.WriteAllTextAsync(persistentPath + "\\silence_revision", $"{dispatchQuery.SilenceRevisionNum}", token);
            // Get audio_revision content
            await File.WriteAllTextAsync(persistentPath + "\\audio_revision", $"{dispatchQuery.AudioRevisionNum}", token);
            // Get ChannelName content
            await File.WriteAllTextAsync(persistentPath + "\\ChannelName", $"{dispatchQuery.ChannelName}", token);
            // Get ScriptVersion content
            await File.WriteAllTextAsync(persistentPath + "\\ScriptVersion", $"{dispatchQuery.GameVersion}", token);
            // Get PatchDone content (the same one as audio_revision)
            await File.WriteAllTextAsync(persistentPath + "\\PatchDone", $"{dispatchQuery.AudioRevisionNum}", token);
        }
        #endregion

        #region DispatcherParser
        // ReSharper disable once UnusedParameter.Local
        private async Task<QueryProperty> GetDispatcherQuery(HttpClient client, CancellationToken token)
        {
            // Initialize dispatch helper
            DispatchHelper dispatchHelper = new DispatchHelper(
                client,
                DispatcherRegionID,
                GameVersionManager.GamePreset.ProtoDispatchKey!,
                DispatcherURL,
                GameVersion.VersionString,
                ILoggerHelper.GetILogger(),
                token);
            {
                // Get the dispatcher info
                DispatchInfo dispatchInfo = await dispatchHelper.LoadDispatchInfo();

                // DEBUG ONLY: Show encrypted Proto as JSON+Base64 format
                string dFormat = $"Query Response (RAW Encrypted form):\r\n{dispatchInfo?.Content}";
#if DEBUG
                LogWriteLine(dFormat);
#endif
                // Write the decrypted query response in the log (for diagnostic)
                WriteLog(dFormat);

                // Try decrypt the dispatcher, parse it and return it
                return await TryDecryptAndParseDispatcher(dispatchInfo, dispatchHelper);
            }
        }

        private async Task<QueryProperty> TryDecryptAndParseDispatcher(DispatchInfo dispatchInfo, DispatchHelper dispatchHelper)
        {
            // Decrypt the dispatcher data from the dispatcher info content
            byte[] decryptedData = YSDispatchDec.DecryptYSDispatch(dispatchInfo.Content, GameVersionManager.GamePreset.DispatcherKeyBitLength ?? 0, GameVersionManager.GamePreset.DispatcherKey);

            // DEBUG ONLY: Show the decrypted Proto as Base64 format
            string dFormat = $"Proto Response (RAW Decrypted form):\r\n{Convert.ToBase64String(decryptedData)}";
#if DEBUG
            LogWriteLine(dFormat);
#endif
            WriteLog(dFormat);

            // Parse the dispatcher data in protobuf format and return it as QueryProperty
            await dispatchHelper.LoadDispatch(decryptedData);
            return dispatchHelper.GetResult();
        }
        #endregion

        #region Tools
        private void CountAssetIndex(List<PkgVersionProperties> assetIndex)
        {
            ProgressAllSizeTotal = assetIndex.Sum(x => x.fileSize);
            ProgressAllCountTotal = assetIndex.Count;
        }

        private void EnumerateManifestToAssetIndex(string path, string parentPath, string filter, List<PkgVersionProperties> assetIndex, Dictionary<string, PkgVersionProperties> hashtable, string parentURL = "", bool forceStoreInStreaming = false)
        {
            // Iterate files inside the desired path based on filter.
            foreach (string entry in Directory.EnumerateFiles(Path.Combine(GamePath, path), filter))
            {
                ParseManifestToAssetIndex(entry, parentPath, assetIndex, hashtable, parentURL, forceStoreInStreaming);
            }
        }

        /// <summary>
        /// This one is used to parse Generic Manifest
        /// </summary>
        private static void ParseManifestToAssetIndex(string manifestPath,
                                                      string parentPath,
                                                      List<PkgVersionProperties> assetIndex,
                                                      Dictionary<string, PkgVersionProperties> hashtable,
                                                      string parentURL,
                                                      bool forceStoreInStreaming = false)
        {
            // Iterate JSON that only contains defined extensions.
            using StreamReader reader = File.OpenText(manifestPath);
            while (reader.ReadLine() is { } data)
            {
                // If it's not a valid JSON data, move to the next line
                if (IsCurrentLineJson(data))
                {
                    continue;
                }

                // Deserialize JSON line into local entry.
                PkgVersionProperties entry = data.Deserialize(CoreLibraryJsonContext.Default.PkgVersionProperties);

                // If the parent path is not defined, then use already-defined parent path from JSON and append it as remote name.
                string relativeParentPath = string.IsNullOrEmpty(parentPath) ? "" : GetParentFromAssetRelativePath(entry.remoteName, out _);
                entry.remoteName = Path.Combine(parentPath ?? "", relativeParentPath, entry.remoteName);

                // Reverse-normalize the path separator
                NormalizePathInplaceNoTrim(entry.remoteName, '\\', '/');

                // Append remote URL to download later
                entry.remoteURL               = CombineURLFromString(parentURL, entry.remoteName);
                entry.isForceStoreInStreaming = forceStoreInStreaming;

                // Always ensure that primary manifest doesn't get recognized as patch.
                entry.isPatch = false;

                // Check if the entry is duplicated. If not, then add to asset index.
                if (hashtable.TryAdd(entry.remoteName, entry))
                {
                    assetIndex.Add(entry);
                }
            }
        }
        #endregion

        private void _httpClient_FetchManifestAssetProgress(int read, DownloadProgress downloadProgress)
        {
            // Update fetch status
            double speed = CalculateSpeed(read);
            
            Status.IsProgressPerFileIndetermined = false;
            Status.ActivityPerFile =
                string.Format(Lang._GameRepairPage.PerProgressSubtitle3, SummarizeSizeSimple(speed));

            // Update fetch progress
            lock (Progress)
            {
                Progress.ProgressPerFilePercentage =
                    ToPercentage(downloadProgress.BytesTotal, downloadProgress.BytesDownloaded);
            }

            // Push status and progress update
            UpdateStatus();
            UpdateProgress();
        }
    }
}
