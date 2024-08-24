using CollapseLauncher.GameVersioning;
using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    internal partial class GenshinRepair
    {
        private async ValueTask<List<PkgVersionProperties>> Fetch(List<PkgVersionProperties> assetIndex, CancellationToken token)
        {
            // Set total activity string as "Loading Indexes..."
            _status.ActivityStatus = Lang._GameRepairPage.Status2;
            _status.IsProgressAllIndetermined = true;
            UpdateStatus();

            // Initialize hashtable for duplicate keys checking
            Dictionary<string, PkgVersionProperties> hashtableManifest = new Dictionary<string, PkgVersionProperties>();

            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder()
                .UseLauncherConfig(_downloadThreadCount + 16)
                .SetUserAgent(_userAgent)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Initialize the new DownloadClient instance
            DownloadClient downloadClient = DownloadClient.CreateInstance(client);

            // Region: PrimaryManifest
            // Build primary manifest
            await BuildPrimaryManifest(downloadClient, _httpClient_FetchManifestAssetProgress, assetIndex, hashtableManifest, token);

            // Region: PersistentManifest
            // Build persistent manifest
            _isParsePersistentManifestSuccess = await BuildPersistentManifest(downloadClient, _httpClient_FetchManifestAssetProgress, assetIndex, hashtableManifest, token);

            // Force-Fetch the Bilibili SDK (if exist :pepehands:)
            await FetchBilibiliSDK(token);

            // Remove plugin from assetIndex
            EliminatePluginAssetIndex(assetIndex);

            // Clear hashtableManifest
            hashtableManifest.Clear();

            // Eliminate unnecessary asset indexes
            return _isParsePersistentManifestSuccess ? EliminateUnnecessaryAssetIndex(assetIndex) : assetIndex;
        }

        private void EliminatePluginAssetIndex(List<PkgVersionProperties> assetIndex)
        {
            _gameVersionManager.GameAPIProp.data.plugins?.ForEach(plugin =>
            {
                assetIndex.RemoveAll(asset =>
                {
                    return plugin.package.validate?.Exists(validate => validate.path == asset.remoteName) ?? false;
                });
            });
        }

        private List<PkgVersionProperties> EliminateUnnecessaryAssetIndex(IEnumerable<PkgVersionProperties> assetIndex)
        {
            // Section: Eliminate unused audio files
            List<string> audioLangList = (_gameVersionManager as GameTypeGenshinVersion)!._audioVoiceLanguageList;
            string audioLangListPath = Path.Combine(_gamePath, $"{_execPrefix}_Data", "Persistent", "audio_lang_14");

            // Get the list of audio lang list
            string[] currentAudioLangList = File.Exists(audioLangListPath) ? File.ReadAllLines(audioLangListPath) : new string[] { };

            // Set the ignored audio lang
            List<string> ignoredAudioLangList = audioLangList.Where(x => !currentAudioLangList.Contains(x)).ToList();

            // Return only for asset index that doesn't have language included in ignoredAudioLangList
            return assetIndex.Where(x => !ignoredAudioLangList.Any(y => x.remoteName.Contains(y))).ToList();
        }

        #region PrimaryManifest
        private async Task BuildPrimaryManifest(DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, List<PkgVersionProperties> assetIndex, Dictionary<string, PkgVersionProperties> hashtableManifest, CancellationToken token)
        {
            try
            {
                // Try Cleanup Download Profile file
                TryDeleteDownloadPref();

                // Build basic file entry.
                string ManifestPath = Path.Combine(_gamePath, "pkg_version");

                // Download basic package version list
                var basicVerURL = CombineURLFromString(_gameRepoURL, "pkg_version");
                #if DEBUG
                LogWriteLine($"Downloading pkg_version...\r\n\t{basicVerURL}", LogType.Debug, true);
                #endif
                await downloadClient.DownloadAsync(
                    basicVerURL,
                    EnsureCreationOfDirectory(ManifestPath),
                    true,
                    progressDelegateAsync: downloadProgress,
                    maxConnectionSessions: _downloadThreadCount,
                    cancelToken: token
                    );
                
                // Download additional package lists
                var dataVerPath = $@"{_execPrefix}_Data\StreamingAssets\data_versions_streaming";
                var dataVerURL = CombineURLFromString(_gameRepoURL, dataVerPath);
                #if DEBUG
                LogWriteLine($"Downloading data_versions_streaming...\r\n\t{dataVerURL}", LogType.Debug, true);
                #endif
                await downloadClient.DownloadAsync(
                    dataVerURL,
                    EnsureCreationOfDirectory(Path.Combine(_gamePath, dataVerPath)),
                    true,
                    progressDelegateAsync: downloadProgress,
                    maxConnectionSessions: _downloadThreadCount,
                    cancelToken: token
                    );

                var silenceVerPath = $@"{_execPrefix}_Data\StreamingAssets\silence_versions_streaming";
                var silenceVerURL = CombineURLFromString(_gameRepoURL, silenceVerPath);
#if DEBUG
                LogWriteLine($"Downloading silence_versions_streaming...\r\n\t{silenceVerURL}", LogType.Debug, true);
#endif
                await downloadClient.DownloadAsync(
                    silenceVerURL,
                    EnsureCreationOfDirectory(Path.Combine(_gamePath, silenceVerPath)),
                    true,
                    progressDelegateAsync: downloadProgress,
                    maxConnectionSessions: _downloadThreadCount,
                    cancelToken: token
                    );

                var resVerPath = $@"{_execPrefix}_Data\StreamingAssets\res_versions_streaming";
                var resVerURL = CombineURLFromString(_gameRepoURL, resVerPath);
                #if DEBUG
                LogWriteLine($"Downloading res_versions_streaming...\r\n\t{resVerURL}", LogType.Debug, true);
                #endif
                await downloadClient.DownloadAsync(
                    resVerURL,
                    EnsureCreationOfDirectory(Path.Combine(_gamePath, resVerPath)),
                    true,
                    progressDelegateAsync: downloadProgress,
                    maxConnectionSessions: _downloadThreadCount,
                    cancelToken: token
                    );

                var videoVerPath = $@"{_execPrefix}_Data\StreamingAssets\VideoAssets\video_versions_streaming";
                var videoVerURL = CombineURLFromString(_gameRepoURL, videoVerPath);
#if DEBUG
                LogWriteLine($"Downloading video_versions_streaming...\r\n\t{videoVerURL}", LogType.Debug, true);
#endif
                await downloadClient.DownloadAsync(
                    videoVerURL,
                    EnsureCreationOfDirectory(Path.Combine(_gamePath, videoVerPath)),
                    true,
                    progressDelegateAsync: downloadProgress,
                    maxConnectionSessions: _downloadThreadCount,
                    cancelToken: token
                    );

                // Parse basic package version.
                ParseManifestToAssetIndex(ManifestPath, assetIndex, hashtableManifest, "", "", _gameRepoURL, true);

                // Build additional blks entry.
                var streamingAssetsPath = $"{_execPrefix}_Data\\StreamingAssets";
                var assetBundlesPath = $@"{_execPrefix}_Data\StreamingAssets\AssetBundles";
                
                EnumerateManifestToAssetIndex(streamingAssetsPath, "data_versions_*", assetIndex, hashtableManifest,
                    assetBundlesPath, "", _gameRepoURL, true);
                EnumerateManifestToAssetIndex(streamingAssetsPath, "silence_versions_*", assetIndex, hashtableManifest,
                    assetBundlesPath, "", _gameRepoURL, true);
                EnumerateManifestToAssetIndex(streamingAssetsPath, "res_versions_*", assetIndex, hashtableManifest,
                    assetBundlesPath, ".blk", _gameRepoURL, true);

                // Build cutscenes entry.
                var videoAssetsPath = $@"{_execPrefix}_Data\StreamingAssets\VideoAssets";
                EnumerateManifestToAssetIndex(videoAssetsPath, "*_versions_*", assetIndex, hashtableManifest,
                    videoAssetsPath, "", _gameRepoURL, true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Parsing primary manifest has failed!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void TryDeleteDownloadPref()
        {
            // Get the paths
            // string downloadPrefPath = Path.Combine(_gamePath, $"{_execPrefix}_Data\\Persistent\\DownloadPref");
            string ctablePersistPath = Path.Combine(_gamePath, $"{_execPrefix}_Data\\Persistent\\ctable.dat");

            // Check the file existence and delete it
            // if (File.Exists(downloadPrefPath)) TryDeleteReadOnlyFile(downloadPrefPath);
            if (File.Exists(ctablePersistPath)) TryDeleteReadOnlyFile(ctablePersistPath);
        }
        #endregion

        #region PersistentManifest
        private async Task<bool> BuildPersistentManifest(DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, List<PkgVersionProperties> assetIndex,
            Dictionary<string, PkgVersionProperties> hashtableManifest, CancellationToken token)
        {
            try
            {
                // Get the Dispatcher Query
                QueryProperty queryProperty = await GetDispatcherQuery(token);

                // Initialize persistent folder path and check for the folder existence
                string basePersistentPath = $"{_execPrefix}_Data\\Persistent";
                string persistentFolder = Path.Combine(_gamePath, basePersistentPath);

                string baseStreamingAssetsPath = $"{_execPrefix}_Data\\StreamingAssets";
                string streamingAssetsFolder = Path.Combine(_gamePath, baseStreamingAssetsPath);

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
                             $"pri: {primaryParentURL}\r\n\t"+
                             $"sec: {secondaryParentURL}", LogType.Debug, true);
                #endif
                await ParseManifestToAssetIndex(downloadClient, downloadProgress, primaryParentURL, secondaryParentURL, "res_versions_external",
                    "res_versions_external_persist", basePersistentPath, baseStreamingAssetsPath, assetIndex, hashtableManifest, token);

                // Parse data_versions
                var dataVerURL = queryProperty.ClientDesignDataURL;
                #if DEBUG
                LogWriteLine($"Downloading data_versions_persist...\r\n\t" +
                             $"{dataVerURL}", LogType.Debug, true);
                #endif
                await ParseManifestToAssetIndex(downloadClient, downloadProgress, dataVerURL, "",
                    CombineURLFromString("AssetBundles", "data_versions"), "data_versions_persist", basePersistentPath,
                    baseStreamingAssetsPath, assetIndex, hashtableManifest, token, true, true);

                // Parse data_versions (silence)
                var dataSilURL = queryProperty.ClientDesignDataSilURL;
                #if DEBUG
                LogWriteLine($"Downloading silence_data_versions_persist...\r\n\t" +
                             $"{dataSilURL}", LogType.Debug, true);
                #endif
                await ParseManifestToAssetIndex(downloadClient, downloadProgress, dataSilURL, "",
                    CombineURLFromString("AssetBundles", "data_versions"), "silence_data_versions_persist",
                    basePersistentPath, baseStreamingAssetsPath, assetIndex, hashtableManifest, token, true, true);

                // Save persistent manifest numbers
                SavePersistentRevision(queryProperty);
                return true;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Parsing persistent manifest has failed. Ignoring!\r\n{ex}", LogType.Error, true);
                return false;
            }
        }

        private async ValueTask ParseManifestToAssetIndex(DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, string primaryParentURL, string secondaryParentURL,
            string manifestRemoteName, string manifestLocalName,
            string persistentPath, string streamingAssetsPath,
            List<PkgVersionProperties> assetIndex, Dictionary<string, PkgVersionProperties> hashtable,
            CancellationToken token, bool forceStoreInPersistent = false, bool forceOverwrite = false)
        {
            try
            {
                // Get the manifest URL and Path
                string manifestURL = CombineURLFromString(primaryParentURL, manifestRemoteName);
                string manifestPath = Path.Combine(_gamePath, persistentPath, manifestLocalName);

                // Make sure the file has been deleted (if exist) before redownloading it
                if (File.Exists(manifestPath))
                {
                    TryDeleteReadOnlyFile(manifestPath);
                }

                // Download the manifest
                await downloadClient.DownloadAsync(manifestURL, manifestPath, true, progressDelegateAsync: downloadProgress, cancelToken: token);
                LogWriteLine($"Manifest: {manifestRemoteName} (localName: {manifestLocalName}) has been fetched", LogType.Default, true);

                // Parse the manifest
                ParsePersistentManifest(manifestPath,
                    persistentPath, streamingAssetsPath,
                    primaryParentURL, secondaryParentURL,
                    assetIndex, hashtable, forceStoreInPersistent, forceOverwrite);
            }
            catch (TaskCanceledException) { throw; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LogWriteLine($"Failed parsing persistent manifest: {manifestRemoteName} (localName: {manifestLocalName}). Skipped!\r\n{ex}", LogType.Warning, true);
            }
        }

        private void ParsePersistentManifest(string localManifestPath,
            string persistentPath, string streamingAssetPath,
            string primaryParentURL, string secondaryParentURL,
            List<PkgVersionProperties> assetIndex, Dictionary<string, PkgVersionProperties> hashtable,
            bool forceStoreInPersistent, bool forceOverwrite)
        {
            persistentPath = persistentPath.Replace('\\', '/');
            streamingAssetPath = streamingAssetPath.Replace('\\', '/');

            // Start reading the manifest
            using (StreamReader reader = new StreamReader(localManifestPath, new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.Read }))
            {
                while (!reader.EndOfStream)
                {
                    string manifestLine = reader.ReadLine();
                    PkgVersionProperties manifestEntry = manifestLine.Deserialize<PkgVersionProperties>(CoreLibraryJSONContext.Default);

                    // Ignore if the remote name is "svc_catalog" or "ctable.dat"
                    if (Path.GetFileName(manifestEntry.remoteName).Equals("svc_catalog", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetFileName(manifestEntry.remoteName).Equals("ctable.dat", StringComparison.OrdinalIgnoreCase)) continue;

                    // Get relative path based on extension
                    string relativePath = Path.GetExtension(manifestEntry.remoteName).ToLower() switch
                    {
                        ".pck" => "AudioAssets",
                        ".blk" => "AssetBundles",
                        ".usm" => "VideoAssets",
                        ".cuepoint" => "VideoAssets",
                        _ => ""
                    };

                    string actualName = string.IsNullOrEmpty(manifestEntry.localName) ? manifestEntry.remoteName : manifestEntry.localName;
                    string assetPersistentPath = relativePath == "" ? null : CombineURLFromString(persistentPath, relativePath, actualName);
                    string assetStreamingAssetPath = CombineURLFromString(streamingAssetPath, relativePath, manifestEntry.remoteName);

                    // Set the remote URL
                    string remoteURL;
                    if (!string.IsNullOrEmpty(secondaryParentURL) && !manifestEntry.isPatch)
                    {
                        remoteURL = CombineURLFromString(secondaryParentURL, relativePath, manifestEntry.remoteName);
                    }
                    else
                    {
                        remoteURL = CombineURLFromString(primaryParentURL, relativePath, manifestEntry.remoteName);
                    }

                    // Get the remoteName (StreamingAssets) and remoteNamePersistent (Persistent)
                    manifestEntry.remoteURL = remoteURL;
                    manifestEntry.remoteName = assetStreamingAssetPath;
                    manifestEntry.remoteNamePersistent = assetPersistentPath;
                    // Decide if the file is forced to be in persistent or not
                    manifestEntry.isForceStoreInPersistent = forceStoreInPersistent || manifestEntry.isPatch;

                    // If forceOverwrite and forceStoreInPwrsistent is true, then
                    // make it as a patch file and store it to persistent
                    if (forceOverwrite && forceStoreInPersistent)
                    {
                        manifestEntry.isForceStoreInStreaming = false;
                        manifestEntry.isForceStoreInPersistent = true;
                        manifestEntry.isPatch = true;
                    }

                    // If the manifest has isPatch set to true, then set force store in streaming to false
                    if (manifestEntry.isPatch) manifestEntry.isForceStoreInStreaming = false;

                    // Check if the hashtable has the value
                    bool IsHashHasValue = hashtable.ContainsKey(assetStreamingAssetPath);
                    if (IsHashHasValue)
                    {
                        // If yes, then get the reference and index ID
                        PkgVersionProperties reference = hashtable[assetStreamingAssetPath];
                        int indexID = assetIndex.IndexOf(reference);

                        // If the index is not found (== -1), then skip it.
                        // Otherwise, continue overriding its value
                        if (indexID == -1) continue;

                        // Override the force state if isPatch is true
                        manifestEntry.isForceStoreInStreaming = !manifestEntry.isPatch;

                        // If it has isForceStoreStreamingAssets flag and isPatch is false, then continue.
                        if (hashtable[assetStreamingAssetPath].isForceStoreInStreaming
                            && !manifestEntry.isPatch) continue;

                        // Start overriding the value
                        hashtable[assetStreamingAssetPath] = manifestEntry;
                        assetIndex[indexID] = manifestEntry;
                    }
                    else
                    {
                        manifestEntry.isForceStoreInStreaming = !manifestEntry.isPatch;

                        hashtable.Add(manifestEntry.remoteName, manifestEntry);
                        assetIndex.Add(manifestEntry);
                    }
                }
            }
        }

        private void SavePersistentRevision(QueryProperty dispatchQuery)
        {
            string PersistentPath = Path.Combine(_gamePath, $"{_execPrefix}_Data\\Persistent");

            // Get base_res_version_hash content
            string FilePath = Path.Combine(_gamePath, $"{_execPrefix}_Data\\StreamingAssets\\res_versions_streaming");
            string Hash = CreateMD5Shared(new FileStream(FilePath, FileMode.Open, FileAccess.Read));

#nullable enable
            // Write DownloadPref template
            byte[]? PrefTemplateBytes = (_gameVersionManager as GameTypeGenshinVersion)?.GamePreset
                .GetGameDataTemplate("DownloadPref", _gameVersion.VersionArrayManifest.Select(x => (byte)x).ToArray());
            if (PrefTemplateBytes != null) File.WriteAllBytes(PersistentPath + "\\DownloadPref", PrefTemplateBytes);
#nullable disable

            // Get base_res_version_hash content
            File.WriteAllText(PersistentPath + "\\base_res_version_hash", Hash);
            // Get data_revision content
            File.WriteAllText(PersistentPath + "\\data_revision", $"{dispatchQuery.DataRevisionNum}");
            // Get res_revision content
            File.WriteAllText(PersistentPath + "\\res_revision", $"{dispatchQuery.ResRevisionNum}");
            // Get silence_revision content
            File.WriteAllText(PersistentPath + "\\silence_revision", $"{dispatchQuery.SilenceRevisionNum}");
            // Get audio_revision content
            File.WriteAllText(PersistentPath + "\\audio_revision", $"{dispatchQuery.AudioRevisionNum}");
            // Get ChannelName content
            File.WriteAllText(PersistentPath + "\\ChannelName", $"{dispatchQuery.ChannelName}");
            // Get ScriptVersion content
            File.WriteAllText(PersistentPath + "\\ScriptVersion", $"{dispatchQuery.GameVersion}");
        }
        #endregion

        #region DispatcherParser
        // ReSharper disable once UnusedParameter.Local
        private async Task<QueryProperty> GetDispatcherQuery(CancellationToken token)
        {
            // Initialize dispatch helper
            using (GenshinDispatchHelper dispatchHelper = new GenshinDispatchHelper(_dispatcherRegionID, _gameVersionManager.GamePreset.ProtoDispatchKey, _dispatcherURL, _gameVersion.VersionString, token))
            {
                // Get the dispatcher info
                YSDispatchInfo dispatchInfo = await dispatchHelper.LoadDispatchInfo();

                // DEBUG ONLY: Show encrypted Proto as JSON+Base64 format
                string dFormat = string.Format("Query Response (RAW Encrypted form):\r\n{0}", dispatchInfo.content);
#if DEBUG
                LogWriteLine(dFormat);
#endif
                // Write the decrypted query response in the log (for diagnostic)
                WriteLog(dFormat);

                // Try decrypt the dispatcher, parse it and return it
                return await TryDecryptAndParseDispatcher(dispatchInfo, dispatchHelper);
            }
        }

        private async Task<QueryProperty> TryDecryptAndParseDispatcher(YSDispatchInfo dispatchInfo, GenshinDispatchHelper dispatchHelper)
        {
            YSDispatchDec dispatchDecryptor = new YSDispatchDec();

            // Decrypt the dispatcher data from the dispatcher info content
            byte[] decryptedData = dispatchDecryptor.DecryptYSDispatch(dispatchInfo.content, _gameVersionManager.GamePreset.DispatcherKeyBitLength ?? 0, _gameVersionManager.GamePreset.DispatcherKey);

            // DEBUG ONLY: Show the decrypted Proto as Base64 format
            string dFormat = string.Format("Proto Response (RAW Decrypted form):\r\n{0}", Convert.ToBase64String(decryptedData));
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
            _progressAllSizeTotal = assetIndex.Sum(x => x.fileSize);
            _progressAllCountTotal = assetIndex.Count;
        }

        private void EnumerateManifestToAssetIndex(string path, string filter, List<PkgVersionProperties> assetIndex, Dictionary<string, PkgVersionProperties> hashtable, string parentPath = "", string acceptedExtension = "", string parentURL = "", bool forceStoreInStreaming = false)
        {
            // Iterate files inside the desired path based on filter.
            foreach (string entry in Directory.EnumerateFiles(Path.Combine(_gamePath, path), filter))
            {
                ParseManifestToAssetIndex(entry, assetIndex, hashtable, parentPath, acceptedExtension, parentURL, forceStoreInStreaming);
            }
        }

        /// <summary>
        /// This one is used to parse Generic Manifest
        /// </summary>
        /// <param name="manifestPath"></param>
        /// <param name="assetIndex"></param>
        /// <param name="hashtable"></param>
        /// <param name="parentPath"></param>
        /// <param name="acceptedExtension"></param>
        /// <param name="parentURL"></param>
        /// <param name="forceStoreInStreaming"></param>
        private void ParseManifestToAssetIndex(string manifestPath, List<PkgVersionProperties> assetIndex, Dictionary<string, PkgVersionProperties> hashtable, string parentPath, string acceptedExtension, string parentURL, bool forceStoreInStreaming = false)
        {
            // Initialize local entry.
            // ReSharper disable TooWideLocalVariableScope
            PkgVersionProperties entry;
            bool isHashHasValue;
            // ReSharper restore TooWideLocalVariableScope

            // Iterate JSON that only contains defined extensions.
            foreach (string data in File.ReadAllLines(manifestPath).Where(x => x.EndsWith(acceptedExtension, StringComparison.OrdinalIgnoreCase)))
            {
                // Deserialize JSON line into local entry.
                entry = data.Deserialize<PkgVersionProperties>(CoreLibraryJSONContext.Default);

                // If the parent path is not defined, then use already-defined parent path from JSON and append it as remote name.
                if (!string.IsNullOrEmpty(parentPath))
                {
                    entry!.remoteName = $"{parentPath.Replace('\\', '/')}/{entry.remoteName}";
                }

                // Append remote URL for download later.
                entry!.remoteURL = CombineURLFromString(parentURL, entry.remoteName);
                entry!.isForceStoreInStreaming = forceStoreInStreaming;

                // Check if the entry is duplicated. If not, then add to asset index.
                isHashHasValue = hashtable.ContainsKey(entry.remoteName);
                if (!isHashHasValue)
                {
                    hashtable.Add(entry.remoteName, entry);
                    assetIndex.Add(entry);
                }
            }
        }
        #endregion

        private void _httpClient_FetchManifestAssetProgress(int read, DownloadProgress downloadProgress)
        {
            // Update fetch status
            double speed = downloadProgress.BytesDownloaded / _stopwatch.Elapsed.TotalSeconds;
            _status.IsProgressPerFileIndetermined = false;
            _status.ActivityPerFile = string.Format(Lang._GameRepairPage.PerProgressSubtitle3, SummarizeSizeSimple(speed));

            // Update fetch progress
            _progress.ProgressPerFilePercentage = GetPercentageNumber(downloadProgress.BytesDownloaded, downloadProgress.BytesTotal);

            // Push status and progress update
            UpdateStatus();
            UpdateProgress();
        }
    }
}
