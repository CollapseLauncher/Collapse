using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.EncTool.Parser.Sleepy;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    internal partial class ZenlessRepair
    {
        #region Main Fetch Routine
        private async Task Fetch(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            // Set total activity string as "Loading Indexes..."
            _status.ActivityStatus = Locale.Lang._GameRepairPage.Status2;
            _status.IsProgressAllIndetermined = true;
            UpdateStatus();
            StarRailRepairExtension.ClearHashtable();

            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder()
                .UseLauncherConfig(_downloadThreadCount + _downloadThreadCountReserved)
                .SetUserAgent(_userAgent)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Initialize the new DownloadClient
            DownloadClient downloadClient = DownloadClient.CreateInstance(client);

            // Create a hash set to overwrite local files
            Dictionary<string, FilePropertiesRemote> hashSet = new Dictionary<string, FilePropertiesRemote>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // If not in cache mode, then fetch main package
                if (!IsCacheUpdateMode)
                {
                    // Get the primary manifest
                    await GetPrimaryManifest(downloadClient, hashSet, token, assetIndex);
                }

                // Execute on non-recover main mode
                if (!IsOnlyRecoverMain)
                {
                    // Get the in-game res manifest
                    await GetResManifest(downloadClient, hashSet, token, assetIndex);

                    // Execute plugin things if not in cache mode only
                    if (!IsCacheUpdateMode)
                    {
                        // Force-Fetch the Bilibili SDK (if exist :pepehands:)
                        await FetchBilibiliSDK(token);

                        // Remove plugin from assetIndex
                        // Skip the removal for Delta-Patch
                        EliminatePluginAssetIndex(assetIndex);
                    }
                }
            }
            finally
            {
                // Clear the hashtable
                StarRailRepairExtension.ClearHashtable();
                // Unsubscribe the fetching progress and dispose it and unsubscribe cacheUtil progress to adapter
                // _innerGameVersionManager.StarRailMetadataTool.HttpEvent -= _httpClient_FetchAssetProgress;
            }
        }
        #endregion

        #region PrimaryManifest
        private async Task GetPrimaryManifest(DownloadClient downloadClient, Dictionary<string, FilePropertiesRemote> hashSet, CancellationToken token, List<FilePropertiesRemote> assetIndex)
        {
            // If it's using cache update mode, then return since we don't need to add manifest
            // from pkg_version on cache update mode.
            if (IsCacheUpdateMode)
                return;

            // Initialize pkgVersion list
            List<PkgVersionProperties> pkgVersion = new List<PkgVersionProperties>();

            // Initialize repo metadata
            try
            {
                // Get the metadata
                Dictionary<string, string> repoMetadata = await FetchMetadata(token);

                // Check for manifest. If it doesn't exist, then throw and warn the user
                if (!(repoMetadata.TryGetValue(_gameVersion.VersionString, out var value)))
                {
                    throw new VersionNotFoundException($"Manifest for {_gameVersionManager.GamePreset.ZoneName} (version: {_gameVersion.VersionString}) doesn't exist! Please contact @neon-nyan or open an issue for this!");
                }

                // Assign the URL based on the version
                _gameRepoURL = value;
            }
            // If the base._isVersionOverride is true, then throw. This sanity check is required if the delta patch is being performed.
            catch when (base._isVersionOverride) { throw; }

            // Fetch the asset index from CDN
            // Set asset index URL
            string urlIndex = string.Format(LauncherConfig.AppGameRepairIndexURLPrefix, _gameVersionManager.GamePreset.ProfileName, _gameVersion.VersionString) + ".binv2";

            // Start downloading asset index using FallbackCDNUtil and return its stream
            await using BridgedNetworkStream stream = await FallbackCDNUtil.TryGetCDNFallbackStream(urlIndex, token);
            if (stream != null)
            {
                // Deserialize asset index and set it to list
                AssetIndexV2 parserTool = new AssetIndexV2();
                pkgVersion = await Task.Run(() => parserTool.Deserialize(stream, out DateTime timestamp)).ConfigureAwait(false);
            }

            // Convert the pkg version list to asset index
            foreach (FilePropertiesRemote entry in pkgVersion.RegisterMainCategorizedAssetsToHashSet(assetIndex, hashSet, _gamePath, _gameRepoURL))
            {
                // If entry is null (means, an existing entry has been overwritten), then next
                if (entry == null)
                    continue;

                assetIndex.Add(entry);
            }

            // Clear the pkg version list
            pkgVersion.Clear();
        }

        private async Task<Dictionary<string, string>> FetchMetadata(CancellationToken token)
        {
            // Set metadata URL
            string urlMetadata = string.Format(LauncherConfig.AppGameRepoIndexURLPrefix, GameVersionManagerCast!.GamePreset.ProfileName);

            // Start downloading metadata using FallbackCDNUtil
            await using BridgedNetworkStream stream = await FallbackCDNUtil.TryGetCDNFallbackStream(urlMetadata, token);
            return await stream.DeserializeAsync(CoreLibraryJSONContext.Default.DictionaryStringString, token);
        }
        #endregion

        #region ResManifest
        private async Task GetResManifest(DownloadClient downloadClient, Dictionary<string, FilePropertiesRemote> hashSet, CancellationToken token, List<FilePropertiesRemote> assetIndex)
        {
            // Create sleepy property
            PresetConfig gamePreset = GameVersionManagerCast!.GamePreset;
            HttpClient client = downloadClient.GetHttpClient();

            // Get sleepy info
            SleepyInfo sleepyInfo = await TryGetSleepyInfo(
                client,
                gamePreset,
                GameSettings!.GeneralData.SelectedServerName,
                gamePreset.GameDispatchDefaultName,
                token);

            // Get persistent path
            string persistentPath = GameDataPersistentPath;

            // Fetch cache files
            if (IsCacheUpdateMode)
            {
                SleepyFileInfoResult infoKindSilence = sleepyInfo.GetFileInfoResult(FileInfoKind.Silence);
                SleepyFileInfoResult infoKindData = sleepyInfo.GetFileInfoResult(FileInfoKind.Data);

                IAsyncEnumerable<FilePropertiesRemote> infoSilenceEnumerable = EnumerateResManifestToAssetIndexAsync(
                    infoKindSilence.RegisterSleepyFileInfoToManifest(client, assetIndex, true, persistentPath, token),
                    assetIndex,
                    hashSet,
                    Path.Combine(_gamePath, string.Format(@"{0}_Data\", ExecutableName)),
                    infoKindSilence.BaseUrl);

                IAsyncEnumerable<FilePropertiesRemote> infoDataEnumerable = EnumerateResManifestToAssetIndexAsync(
                    infoKindData.RegisterSleepyFileInfoToManifest(client, assetIndex, true, persistentPath, token),
                    assetIndex,
                    hashSet,
                    Path.Combine(_gamePath, string.Format(@"{0}_Data\", ExecutableName)),
                    infoKindData.BaseUrl);

                await foreach (FilePropertiesRemote asset in ZenlessRepairExtensions
                    .MergeAsyncEnumerable(infoSilenceEnumerable, infoDataEnumerable))
                {
                    assetIndex.Add(asset);
                }
            }
            // Fetch repair files
            else
            {
                SleepyFileInfoResult infoKindRes = sleepyInfo.GetFileInfoResult(FileInfoKind.Res);
                SleepyFileInfoResult infoKindAudio = sleepyInfo.GetFileInfoResult(FileInfoKind.Audio);

                IAsyncEnumerable<FilePropertiesRemote> infoResEnumerable = EnumerateResManifestToAssetIndexAsync(
                    infoKindRes.RegisterSleepyFileInfoToManifest(client, assetIndex, true, persistentPath, token),
                    assetIndex,
                    hashSet,
                    Path.Combine(_gamePath, string.Format(@"{0}_Data\", ExecutableName)),
                    infoKindRes.BaseUrl);

                IAsyncEnumerable<FilePropertiesRemote> infoAudioEnumerable = GetOnlyInstalledAudioPack(
                        EnumerateResManifestToAssetIndexAsync(
                        infoKindAudio.RegisterSleepyFileInfoToManifest(client, assetIndex, true, persistentPath, token),
                        assetIndex,
                        hashSet,
                        Path.Combine(_gamePath, string.Format(@"{0}_Data\", ExecutableName)),
                        infoKindAudio.BaseUrl)
                    );

                await foreach (FilePropertiesRemote asset in ZenlessRepairExtensions
                    .MergeAsyncEnumerable(infoResEnumerable, infoAudioEnumerable))
                {
                    assetIndex.Add(asset);
                }
            }
        }

        private async Task<SleepyInfo> TryGetSleepyInfo(HttpClient client, PresetConfig gamePreset, string targetServerName, string fallbackServerName = null, CancellationToken token = default)
        {
            try
            {
                // Initialize property
                SleepyProperty sleepyProperty = SleepyProperty.Create(
                    GameVersionManagerCast!.GetGameExistingVersion()?.VersionString,
                    gamePreset.GameDispatchChannelName,
                    gamePreset.GameDispatchArrayURL![Random.Shared.Next(0, gamePreset.GameDispatchArrayURL.Count - 1)],
                    gamePreset.GameDispatchURLTemplate,
                    targetServerName ?? fallbackServerName,
                    gamePreset.GameGatewayURLTemplate,
                    gamePreset.ProtoDispatchKey,
                    SleepyBuildProperty.Create(GameVersionManagerCast.SleepyIdentity, GameVersionManagerCast.SleepyArea)
                    );

                // Create sleepy instance
                SleepyInfo sleepyInfoReturn = SleepyInfo.CreateSleepyInfo(
                    client,
                    GameVersionManagerCast.SleepyInstance,
                    sleepyProperty
                    );

                // Initialize sleepy instance before using
                await sleepyInfoReturn.Initialize(token);

                // Return SleepyInfo
                return sleepyInfoReturn;
            }
            catch (TaskCanceledException) { throw; }
            catch (OperationCanceledException) { throw; }
            catch (Exception) when (!string.IsNullOrEmpty(fallbackServerName))
            {
                // If it fails, try to get the SleepyInfo from fallback server name
                return await TryGetSleepyInfo(client, gamePreset, fallbackServerName, null, token);
            }
        }
        
        private async IAsyncEnumerable<FilePropertiesRemote> GetOnlyInstalledAudioPack(IAsyncEnumerable<FilePropertiesRemote> enumerable)
        {
            const string WindowsFullPath = "Audio\\Windows\\Full";
            string[] audioList = GetCurrentAudioLangList("Jp");
            SearchValues<string> searchExclude = SearchValues.Create(audioList, StringComparison.OrdinalIgnoreCase);

            await foreach (FilePropertiesRemote asset in enumerable)
            {
                if (IsAudioFileIncluded(WindowsFullPath, searchExclude, asset))
                    yield return asset;
            }
        }

        private static bool IsAudioFileIncluded(string WindowsFullPath, SearchValues<string> searchInclude, FilePropertiesRemote asset)
        {
            // If it's not audio file, then return
            ReadOnlySpan<char> relPath = asset.GetAssetRelativePath(out RepairAssetType assetType);
            if (relPath.IsEmpty || assetType != RepairAssetType.Audio)
                return true;

            ReadOnlySpan<char> dirRelPath = Path.GetDirectoryName(relPath);
            // If non language audio file, then return
            if (dirRelPath.EndsWith(WindowsFullPath))
                return true;

            // Check if non full path, then return
            int indexOf = dirRelPath.LastIndexOf(WindowsFullPath);
            if (indexOf < 0)
                return true;

            // If the index is more than WindowsFullPath.Length (included), then return
            ReadOnlySpan<char> lastSequence = Path.GetFileName(dirRelPath);
            int indexOfAny = lastSequence.IndexOfAny(searchInclude);
            if (indexOfAny == 0)
                return true;

            // Otherwise, return false
            return false;
        }
        #endregion

        #region Utilities
#nullable enable
        private async IAsyncEnumerable<FilePropertiesRemote> EnumerateResManifestToAssetIndexAsync(
            IAsyncEnumerable<PkgVersionProperties> pkgVersion,
            List<FilePropertiesRemote> assetIndex,
            Dictionary<string, FilePropertiesRemote> hashSet,
            string baseLocalPath,
            string baseUrl)
        {
            await foreach (FilePropertiesRemote? entry in pkgVersion.RegisterResCategorizedAssetsToHashSetAsync(assetIndex, hashSet, baseLocalPath, baseUrl))
            {
                // If entry is null (means, an existing entry has been overwritten), then next
                if (entry == null)
                    continue;

                yield return entry;
            }
        }
#nullable restore

        private void EliminatePluginAssetIndex(List<FilePropertiesRemote> assetIndex)
        {
            _gameVersionManager.GameAPIProp.data!.plugins?.ForEach(plugin =>
            {
                assetIndex.RemoveAll(asset =>
                {
                    return plugin.package!.validate?.Exists(validate => validate.path == asset.N) ?? false;
                });
            });
        }

        private string[] GetCurrentAudioLangList(string fallbackCurrentLangname)
        {
            // Initialize the variable.
            string audioLangListPath = _gameAudioLangListPath;
            string audioLangListPathStatic = _gameAudioLangListPathStatic;
            string audioLangListPathAlternative = _gameAudioLangListPathAlternate;
            string audioLangListPathAlternativeStatic = _gameAudioLangListPathAlternateStatic;

            string[] returnValue;
            string fallbackCurrentLangnameNative = fallbackCurrentLangname switch
            {
                "Jp" => "Japanese",
                "En" => "English(US)",
                "Cn" => "Chinese",
                "Kr" => "Korean",
                _ => throw new NotSupportedException()
            };

            // Check if the audioLangListPath is null or the file is not exist,
            // then create a new one from the fallback value
            if (audioLangListPath == null || !File.Exists(audioLangListPathStatic))
            {
                // Try check if the folder is exist. If not, create one.
                string audioLangPathDir = Path.GetDirectoryName(audioLangListPathStatic);
                if (Directory.Exists(audioLangPathDir))
                    Directory.CreateDirectory(audioLangPathDir);

                // Assign the default value and write to the file, then return.
                returnValue = new string[] { fallbackCurrentLangname };
                File.WriteAllLines(audioLangListPathStatic, returnValue);
                return returnValue;
            }

            // Read all the lines. If empty, then assign the default value and rewrite it
            returnValue = File.ReadAllLines(audioLangListPathStatic);
            if (returnValue.Length == 0)
            {
                returnValue = new string[] { fallbackCurrentLangname };
                File.WriteAllLines(audioLangListPathStatic, returnValue);
            }

            string[] returnValueAlternate;

            // Check if the audioLangListPathAlternative is null or the file is not exist,
            // then create a new one from the fallback value
            if (audioLangListPathAlternative == null || !File.Exists(audioLangListPathAlternativeStatic))
            {
                // Try check if the folder is exist. If not, create one.
                string audioLangPathDir = Path.GetDirectoryName(audioLangListPathAlternativeStatic);
                if (Directory.Exists(audioLangPathDir))
                    Directory.CreateDirectory(audioLangPathDir);

                // Assign the default value and write to the file, then return.
                returnValueAlternate = new string[] { fallbackCurrentLangnameNative };
                File.WriteAllLines(audioLangListPathAlternativeStatic, returnValueAlternate);
                return returnValueAlternate;
            }

            // Read all the lines. If empty, then assign the default value and rewrite it
            returnValueAlternate = File.ReadAllLines(audioLangListPathAlternativeStatic);
            if (returnValueAlternate.Length == 0)
            {
                returnValueAlternate = new string[] { fallbackCurrentLangnameNative };
                File.WriteAllLines(audioLangListPathAlternativeStatic, returnValueAlternate);
            }

            // Return the value
            return returnValueAlternate;
        }

        private void CountAssetIndex(List<FilePropertiesRemote> assetIndex)
        {
            // Sum the assetIndex size and assign to _progressAllSize
            _progressAllSizeTotal = assetIndex.Sum(x => x.S);

            // Assign the assetIndex count to _progressAllCount
            _progressAllCountTotal = assetIndex.Count;
        }
        #endregion
    }
}
