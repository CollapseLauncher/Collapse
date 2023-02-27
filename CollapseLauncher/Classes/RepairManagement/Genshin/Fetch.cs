using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.KianaManifest;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region.Honkai;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Preset.ConfigV2Store;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    internal partial class GenshinRepair
    {
        private async Task Fetch(List<PkgVersionProperties> assetIndex, CancellationToken token)
        {
            // Set total activity string as "Loading Indexes..."
            _status.ActivityStatus = Lang._GameRepairPage.Status2;
            _status.IsProgressTotalIndetermined = true;
            UpdateStatus();

            // Initialize hashtable for duplicate keys checking
            Dictionary<string, PkgVersionProperties> hashtableManifest = new Dictionary<string, PkgVersionProperties>();

            // Use HttpClient instance on fetching
            Http _httpClient = new Http(true, 5, 1000, _userAgent);
            try
            {
                // Subscribe the progress update
                _httpClient.DownloadProgress += _httpClient_FetchManifestAssetProgress;

                // Region: PrimaryManifest
                // Build primary manifest
                await BuildPrimaryManifest(_httpClient, assetIndex, hashtableManifest, token);

                // Region: PersistentManifest
                // Build persistent manifest
                await BuildPersistentManifest(_httpClient, assetIndex, hashtableManifest, token);
            }
            catch
            {
                // Rethrow the throw if this happen
                throw;
            }
            finally
            {
                // Unsubscribe and dispose the _httpClient
                _httpClient.DownloadProgress -= _httpClient_FetchManifestAssetProgress;
                _httpClient?.Dispose();
            }
        }

        #region PrimaryManifest
        private async Task BuildPrimaryManifest(Http _httpClient, List<PkgVersionProperties> assetIndex, Dictionary<string, PkgVersionProperties> hashtableManifest, CancellationToken token)
        {
            // Build basic file entry.
            string ManifestPath = Path.Combine(_gamePath, "pkg_version");

            // If the basic package version doesn't exist, then download it.
            if (!File.Exists(ManifestPath))
            {
                await _httpClient.Download(_gameRepoURL + "/pkg_version", ManifestPath, true, null, null, token);
            }

            // Parse basic package version.
            ParseManifestToAssetIndex(ManifestPath, assetIndex, hashtableManifest, "", "", _gameRepoURL);

            // Build local audio entry.
            EnumerateManifestToAssetIndex("", "Audio_*_pkg_version", assetIndex, hashtableManifest, "", "", _gameRepoURL);

            // Build additional blks entry.
            EnumerateManifestToAssetIndex($"{_execPrefix}_Data\\StreamingAssets", "data_versions_*", assetIndex, hashtableManifest, $"{_execPrefix}_Data\\StreamingAssets\\AssetBundles", "", _gameRepoURL);
            EnumerateManifestToAssetIndex($"{_execPrefix}_Data\\StreamingAssets", "silence_versions_*", assetIndex, hashtableManifest, $"{_execPrefix}_Data\\StreamingAssets\\AssetBundles", "", _gameRepoURL);
            EnumerateManifestToAssetIndex($"{_execPrefix}_Data\\StreamingAssets", "res_versions_*", assetIndex, hashtableManifest, $"{_execPrefix}_Data\\StreamingAssets\\AssetBundles", ".blk", _gameRepoURL);

            // Build cutscenes entry.
            EnumerateManifestToAssetIndex($"{_execPrefix}_Data\\StreamingAssets\\VideoAssets", "*_versions_*", assetIndex, hashtableManifest, $"{_execPrefix}_Data\\StreamingAssets\\VideoAssets", "", _gameRepoURL);
        }
        #endregion

        #region PersistentManifest
        private async Task BuildPersistentManifest(Http _httpClient, List<PkgVersionProperties> assetIndex, Dictionary<string, PkgVersionProperties> hashtableManifest, CancellationToken token)
        {
            // Get the Dispatcher Query
            QueryProperty queryProperty = await GetDispatcherQuery(_httpClient, token).ConfigureAwait(false);

            // Initialize persistent folder path and check for the folder existence
            string basePersistentPath = $"{_execPrefix}_Data\\Persistent";
            string persistentFolder = Path.Combine(_gamePath, basePersistentPath);
            if (!Directory.Exists(persistentFolder))
            {
                Directory.CreateDirectory(persistentFolder);
            }

            // Parse data_versions (silence)
            await ParseAndDownloadPersistentManifest(_httpClient, token, assetIndex, hashtableManifest, basePersistentPath, persistentFolder,
                "data_versions", queryProperty.ClientDesignDataSilURL, "", true);

            // Parse data_versions
            await ParseAndDownloadPersistentManifest(_httpClient, token, assetIndex, hashtableManifest, basePersistentPath, persistentFolder,
                "data_versions", queryProperty.ClientDesignDataURL);

            // Parse release_res_versions_external
            await ParseAndDownloadPersistentManifest(_httpClient, token, assetIndex, hashtableManifest, basePersistentPath, persistentFolder,
                "release_res_versions_external", queryProperty.ClientGameResURL,
                queryProperty.ClientAudioAssetsURL, false, true);
        }

        private async Task ParseAndDownloadPersistentManifest(Http _httpClient, CancellationToken token, List<PkgVersionProperties> assetIndex,
            Dictionary<string, PkgVersionProperties> hashtable, string basePersistentPath, string persistentPath, string manifestName, string parentURL,
            string parentAudioURL = "", bool isSilence = false, bool isResVersion = false)
        {
            // Assign manifest path and append the parent URL based on the isResVersion boolean
            string manifestPath = Path.Combine(persistentPath, (isSilence ? "silence_" : "") + manifestName + "_persist");
            string appendURLPath = isResVersion ? "/StandaloneWindows64" : "/AssetBundles";
            parentURL += appendURLPath;

            // Check if the parent audio URL isn't empty, then append based on the isResVersion boolean
            if (!string.IsNullOrEmpty(parentAudioURL))
            {
                parentAudioURL += appendURLPath;
            }

            // Make sure the file has been deleted (if exist) before redownloading it
            if (File.Exists(manifestPath))
            {
                TryDeleteReadOnlyFile(manifestPath);
            }

            try
            {
                // Download the manifest
                await _httpClient.Download(parentURL + '/' + manifestName, manifestPath, true, null, null, token);
                LogWriteLine($"{manifestName} (isSilence: {isSilence}) URL: {parentURL}", LogType.Default, true);

                // Parse the manifest
                string parentPath = Path.Combine(basePersistentPath, isResVersion ? "" : "AssetBundles");
                ParseManifestToAssetIndex(manifestPath, assetIndex, hashtable, parentPath, "", parentURL, isResVersion, parentAudioURL);
            }
            catch (TaskCanceledException) { throw; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LogWriteLine($"Failed parsing persistent manifest: {manifestName} (isSilence: {isSilence} | isResVersion: {isResVersion}). Skipped!\r\n{ex}", LogType.Warning, true);
            }
        }
        #endregion

        #region DispatcherParser
        private async Task<QueryProperty> GetDispatcherQuery(Http _httpClient, CancellationToken token)
        {
            // Initialize dispatch helper
            using (GenshinDispatchHelper dispatchHelper = new GenshinDispatchHelper(_dispatcherRegionID, _gamePreset.ProtoDispatchKey, _dispatcherURL, _gameVersion.VersionString, token))
            {
                // Get the master decryptor
                YSDispatchDec dispatchDecryptor = InitializeMasterDecryptor();

                // Do decryption for Genshin key
                DecryptDispatchKey(dispatchDecryptor);

                // Get the dispatcher info
                YSDispatchInfo dispatchInfo = await dispatchHelper.LoadDispatchInfo();

                // DEBUG ONLY: Show encrypted Proto as JSON+Base64 format
                string dFormat = string.Format("Query Response (RAW Encrypted form):\r\n{0}", dispatchInfo.content);
#if DEBUG
                LogWriteLine(dFormat);
#endif
                // Write the decrypted query response in the log (for diagnostic)
                WriteLog(dFormat, LogType.Default);

                // Try decrypt the dispatcher, parse it and return it
                return await TryDecryptAndParseDispatcher(dispatchInfo, dispatchDecryptor, dispatchHelper);
            }
        }

        private YSDispatchDec InitializeMasterDecryptor()
        {
            // Initialize master decryptor
            YSDispatchDec decryptor = new YSDispatchDec();

            // Initialize the master key
            decryptor.InitMasterKey(ConfigV2.MasterKey, ConfigV2.MasterKeyBitLength, RSAEncryptionPadding.Pkcs1);

            // Return the decryptor
            return decryptor;
        }

        private void DecryptDispatchKey(in YSDispatchDec decryptor)
        {
            // Initialize local variable for key decryption
            string key = _dispatcherKey;
            int keyLength = _dispatcherKeyLength;

            // Decrypt the key and initialize the dispatcher decoder
            decryptor.DecryptStringWithMasterKey(ref key);
            decryptor.InitYSDecoder(key, RSAEncryptionPadding.Pkcs1, keyLength);

            // Then, initialize the RSA key
            decryptor.InitRSA();
        }

        private async Task<QueryProperty> TryDecryptAndParseDispatcher(YSDispatchInfo dispatchInfo, YSDispatchDec dispatchDecryptor, GenshinDispatchHelper dispatchHelper)
        {
            // Decrypt the dispatcher data from the dispatcher info content
            byte[] decryptedData = dispatchDecryptor.DecryptYSDispatch(dispatchInfo.content);

            // DEBUG ONLY: Show the decrypted Proto as Base64 format
            string dFormat = string.Format("Proto Response (RAW Decrypted form):\r\n{0}", Convert.ToBase64String(decryptedData));
#if DEBUG
            LogWriteLine(dFormat);
#endif
            WriteLog(dFormat, LogType.Default);

            // Parse the dispatcher data in protobuf format and return it as QueryProperty
            await dispatchHelper.LoadDispatch(decryptedData);
            return dispatchHelper.GetResult();
        }
        #endregion

        #region Tools
        private void TryDeleteReadOnlyFile(string path)
        {
            try
            {
                FileInfo file = new FileInfo(path);
                file.IsReadOnly = false;
                file.Delete();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to delete file: {path}\r\n{ex}", LogType.Error, true);
            }
        }

        private void EnumerateManifestToAssetIndex(string path, string filter, List<PkgVersionProperties> assetIndex, Dictionary<string, PkgVersionProperties> hashtable, string parentPath = "", string acceptedExtension = "", string parentURL = "")
        {
            // Iterate files inside the desired path based on filter.
            foreach (string entry in Directory.EnumerateFiles(Path.Combine(_gamePath, path), filter))
            {
                ParseManifestToAssetIndex(entry, assetIndex, hashtable, parentPath, acceptedExtension, parentURL);
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
        private void ParseManifestToAssetIndex(string manifestPath, List<PkgVersionProperties> assetIndex, Dictionary<string, PkgVersionProperties> hashtable, string parentPath, string acceptedExtension, string parentURL)
        {
            // Initialize local entry.
            PkgVersionProperties entry;
            bool isHashHasValue = false;

            // Iterate JSON that only contains defined extensions.
            foreach (string data in File.ReadAllLines(manifestPath).Where(x => x.EndsWith(acceptedExtension, StringComparison.OrdinalIgnoreCase)))
            {
                // Deserialize JSON line into local entry.
                entry = (PkgVersionProperties)JsonSerializer.Deserialize(data, typeof(PkgVersionProperties), PkgVersionPropertiesContext.Default);

                // If the parent path is not defined, then use already-defined parent path from JSON and append it as remote name.
                if (parentPath != "")
                {
                    entry.remoteName = $"{parentPath.Replace('\\', '/')}/{entry.remoteName}";
                }

                // Append remote URL for download later.
                entry.remoteURL = $"{parentURL}/{entry.remoteName}";

                // Check if the entry is duplicated. If not, then add to asset index.
                isHashHasValue = hashtable.ContainsKey(entry.remoteName);
                if (!isHashHasValue)
                {
                    hashtable.Add(entry.remoteName, entry);
                    assetIndex.Add(entry);
                }
            }
        }

        /// <summary>
        /// This one is used to parse Persistent Manifest
        /// </summary>
        /// <param name="manifestPath"></param>
        /// <param name="assetIndex"></param>
        /// <param name="hashtable"></param>
        /// <param name="parentPath"></param>
        /// <param name="onlyAcceptExt"></param>
        /// <param name="parentURL"></param>
        /// <param name="IsResVersion"></param>
        /// <param name="parentAudioURL"></param>
        private void ParseManifestToAssetIndex(string manifestPath, in List<PkgVersionProperties> assetIndex,
            Dictionary<string, PkgVersionProperties> hashtable, string parentPath = "",
            string onlyAcceptExt = "", string parentURL = "", bool IsResVersion = false, string parentAudioURL = "")
        {
            PkgVersionProperties Entry;
            bool IsHashHasValue = false;
            int GameVoiceLanguageID = (int)_audioLanguage;

            foreach (string data in File.ReadAllLines(manifestPath)
                .Where(x => x.EndsWith(onlyAcceptExt, StringComparison.OrdinalIgnoreCase)))
            {
                Entry = (PkgVersionProperties)JsonSerializer.Deserialize(data, typeof(PkgVersionProperties), PkgVersionPropertiesContext.Default);

                IsHashHasValue = hashtable.ContainsKey(Entry.remoteName);
                if (!IsHashHasValue)
                {
                    if (IsResVersion)
                    {
                        switch (Path.GetExtension(Entry.remoteName).ToLower())
                        {
                            case ".pck":
                                // Only add if GameVoiceLanguageID == 1 (en-us)
                                if (Entry.remoteName.Contains("English(US)") && GameVoiceLanguageID == 1)
                                {
                                    if (Entry.isPatch)
                                        Entry.remoteURL = $"{parentURL}/AudioAssets/{Entry.remoteName}";
                                    else
                                        Entry.remoteURL = $"{parentAudioURL}/AudioAssets/{Entry.remoteName}";

                                    if (parentPath != "")
                                        Entry.remoteName = $"{parentPath.Replace('\\', '/')}/AudioAssets/{Entry.remoteName}";

                                    hashtable.Add(Entry.remoteName, Entry);
                                    assetIndex.Add(Entry);
                                }
                                break;
                            case ".blk":
                                if (Entry.isPatch)
                                {
                                    Entry.remoteURL = $"{parentURL}/AssetBundles/{Entry.remoteName}";
                                    if (parentPath != "")
                                        Entry.remoteName = $"{parentPath.Replace('\\', '/')}/AssetBundles/{Entry.remoteName}";

                                    Entry.remoteName = Entry.localName != null ? $"{parentPath.Replace('\\', '/')}/AssetBundles/{Entry.localName}" : Entry.remoteName;
                                    hashtable.Add(Entry.remoteName, Entry);
                                    assetIndex.Add(Entry);
                                }
                                break;
                            case ".usm":
                            case ".cuepoint":
                            case ".json":
                                break;
                            default:
                                switch (Path.GetFileName(Entry.remoteName))
                                {
                                    case "svc_catalog":
                                        break;
                                    case "ctable.dat":
                                        Entry.remoteURL = $"{parentAudioURL}/{Entry.remoteName}";
                                        if (parentPath != "")
                                            Entry.remoteName = $"{parentPath.Replace('\\', '/')}/{Entry.remoteName}";

                                        hashtable.Add(Entry.remoteName, Entry);
                                        assetIndex.Add(Entry);
                                        break;
                                    default:
                                        Entry.remoteURL = $"{parentURL}/{Entry.remoteName}";
                                        if (parentPath != "")
                                            Entry.remoteName = $"{parentPath.Replace('\\', '/')}/{Entry.remoteName}";

                                        hashtable.Add(Entry.remoteName, Entry);
                                        assetIndex.Add(Entry);
                                        break;
                                }
                                break;
                        }
                    }
                    else
                    {
                        Entry.remoteURL = $"{parentURL}/{Entry.remoteName}";
                        if (parentPath != "")
                            Entry.remoteName = $"{parentPath.Replace('\\', '/')}/{Entry.remoteName}";
                        hashtable.Add(Entry.remoteName, Entry);
                        assetIndex.Add(Entry);
                    }
                }
            }
        }
        #endregion

        private void _httpClient_FetchManifestAssetProgress(object sender, DownloadEvent e)
        {
            // Update fetch status
            _status.IsProgressPerFileIndetermined = false;
            _status.ActivityPerFile = string.Format(Lang._GameRepairPage.PerProgressSubtitle3, SummarizeSizeSimple(e.Speed));

            // Update fetch progress
            _progress.ProgressPerFilePercentage = e.ProgressPercentage;

            // Push status and progress update
            UpdateStatus();
            UpdateProgress();
        }
    }
}
