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

#nullable enable
            // Explicitly exclude ctable_streaming.dat
            PkgVersionProperties? ctableStreamingContent = assetIndex.FirstOrDefault(x => x.remoteName.EndsWith("ctable_streaming.dat", StringComparison.OrdinalIgnoreCase));
            if (ctableStreamingContent != null)
            {
                // If ctable.dat is found, then remove it from the asset index
                assetIndex.Remove(ctableStreamingContent);
            }

            // Explicitly exclude ctable.dat if no url is assigned
            PkgVersionProperties? ctableContent = assetIndex.FirstOrDefault(x => x.remoteName.EndsWith("ctable.dat", StringComparison.OrdinalIgnoreCase));
            if (ctableContent != null && string.IsNullOrEmpty(ctableContent.remoteURL))
            {
                // If ctable.dat is found, then remove it from the asset index
                assetIndex.Remove(ctableContent);
            }

            return assetIndex;
#nullable restore
        }

        private void EliminatePluginAssetIndex(List<PkgVersionProperties> assetIndex)
        {
            GameVersionManager.GameApiProp?.data!.plugins?.ForEach(plugin =>
               {
                   if (plugin.package?.validate == null) return;

                   assetIndex.RemoveAll(asset =>
                    {
                        var r = plugin.package.validate.Any(validate => validate.path != null &&
                                                    (asset.localName.Contains(validate.path) ||
                                                    asset.remoteName.Contains(validate.path)));
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
        internal async Task BuildPrimaryManifest(DownloadClient                           downloadClient,
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
                    isForceStoreInPersistent = false
                };

                _ = SophonAssetDictRef.TryAdd(asset.AssetName.NormalizePath(), asset);
                assetIndex.Add(assetAsPkgVersionProp);
                hashtableManifest.TryAdd(asset.AssetName, assetAsPkgVersionProp);
            }
            LogWriteLine($"Main asset list fetched with count: {SophonAssetDictRef.Count} from Sophon manifest", LogType.Default, true);
        }
#nullable restore

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

        private static bool IsCurrentLineJson(ReadOnlySpan<char> chars) => chars[0] == '{' && chars[^1] == '}';

        private async Task SavePersistentRevision(QueryProperty dispatchQuery, CancellationToken token)
        {
            string persistentPath = Path.Combine(GamePath, $"{ExecPrefix}_Data\\Persistent");

            // Get base_res_version_hash content
            string filePath = Path.Combine(GamePath, $@"{ExecPrefix}_Data\StreamingAssets\res_versions_streaming");
            FileInfo fileInfo = new FileInfo(filePath).EnsureCreationOfDirectory().StripAlternateDataStream().EnsureNoReadOnly();
            if (fileInfo.Exists)
            {
                await using FileStream resVersionStream = fileInfo.OpenRead();
                byte[] hashBytes = Hash.GetCryptoHash<MD5>(resVersionStream, token: token);
                string hash = Convert.ToHexStringLower(hashBytes);

                // Get base_res_version_hash content
                await File.WriteAllTextAsync(persistentPath + "\\base_res_version_hash", hash, token);
            }

#nullable enable
            // Write DownloadPref template
            byte[]? prefTemplateBytes = (GameVersionManager as GameTypeGenshinVersion)?.GamePreset
                .GetGameDataTemplate("DownloadPref", GameVersion.VersionArrayManifest.Select(x => (byte)x).ToArray());
            if (prefTemplateBytes != null) await File.WriteAllBytesAsync(persistentPath + "\\DownloadPref", prefTemplateBytes, token);
#nullable disable

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
                await CurrentLogger.LogWriter.WriteLineAsync(dFormat);

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
            await CurrentLogger.LogWriter.WriteLineAsync(dFormat);

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
