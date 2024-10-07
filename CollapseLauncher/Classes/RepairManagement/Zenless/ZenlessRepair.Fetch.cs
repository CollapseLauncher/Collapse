using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.EncTool.Parser.Sleepy;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using System;
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

            try
            {
                // Get the primary manifest
                await GetPrimaryManifest(downloadClient, token, assetIndex);

                // Get the in-game res manifest
                await GetResManifest(downloadClient, token, assetIndex);

                // Force-Fetch the Bilibili SDK (if exist :pepehands:)
                await FetchBilibiliSDK(token);

                // Remove plugin from assetIndex
                // Skip the removal for Delta-Patch
                if (!IsOnlyRecoverMain)
                {
                    EliminatePluginAssetIndex(assetIndex);
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
        private async Task GetPrimaryManifest(DownloadClient downloadClient, CancellationToken token, List<FilePropertiesRemote> assetIndex)
        {
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
                pkgVersion = new List<PkgVersionProperties>(parserTool.Deserialize(stream, out DateTime timestamp));
                Logger.LogWriteLine($"Asset index timestamp: {timestamp}", LogType.Default, true);
            }

            // Convert the pkg version list to asset index
            ConvertPkgVersionToAssetIndex(pkgVersion, assetIndex);

            // Clear the pkg version list
            pkgVersion.Clear();
        }

        private async Task<Dictionary<string, string>> FetchMetadata(CancellationToken token)
        {
            // Set metadata URL
            string urlMetadata = string.Format(LauncherConfig.AppGameRepoIndexURLPrefix, GameVersionManagerCast.GamePreset.ProfileName);

            // Start downloading metadata using FallbackCDNUtil
            await using BridgedNetworkStream stream = await FallbackCDNUtil.TryGetCDNFallbackStream(urlMetadata, token);
            return await stream.DeserializeAsync(CoreLibraryJSONContext.Default.DictionaryStringString, token);
        }
        #endregion

        #region ResManifest
        private async Task GetResManifest(DownloadClient downloadClient, CancellationToken token, List<FilePropertiesRemote> assetIndex)
        {
            PresetConfig gamePreset = GameVersionManagerCast.GamePreset;
            SleepyProperty sleepyProperty = SleepyProperty.Create(
                GameVersionManagerCast.GetGameExistingVersion()?.VersionString,
                gamePreset.GameDispatchChannelName,
                gamePreset.GameDispatchURL,
                gamePreset.GameDispatchURLTemplate,
                GameSettings?.GeneralData?.SelectedServerName ?? gamePreset.GameDispatchDefaultName,
                gamePreset.GameGatewayURLTemplate,
                gamePreset.ProtoDispatchKey,
                SleepyBuildProperty.Create(GameVersionManagerCast.SleepyIdentity, GameVersionManagerCast.SleepyArea)
                );
        }
        #endregion

        #region Utilities
        private void ConvertPkgVersionToAssetIndex(List<PkgVersionProperties> pkgVersion, List<FilePropertiesRemote> assetIndex)
        {
            foreach (PkgVersionProperties entry in pkgVersion)
            {
                // Add the pkgVersion entry to asset index
                FilePropertiesRemote normalizedProperty = GetNormalizedFilePropertyTypeBased(
                    _gameRepoURL,
                    entry.remoteName,
                    entry.fileSize,
                    entry.md5,
                    FileType.Generic,
                    true);
                assetIndex.AddSanitize(normalizedProperty);
            }
        }

        private void EliminatePluginAssetIndex(List<FilePropertiesRemote> assetIndex)
        {
            _gameVersionManager.GameAPIProp.data.plugins?.ForEach(plugin =>
            {
                assetIndex.RemoveAll(asset =>
                {
                    return plugin.package.validate?.Exists(validate => validate.path == asset.N) ?? false;
                });
            });
        }

        private FilePropertiesRemote GetNormalizedFilePropertyTypeBased(string remoteParentURL,
                                                                        string remoteRelativePath,
                                                                        long fileSize,
                                                                        string hash,
                                                                        FileType type = FileType.Generic,
                                                                        bool isPatchApplicable = false,
                                                                        bool isHasHashMark = false)
        {
            string remoteAbsolutePath = type switch
            {
                FileType.Generic => ConverterTool.CombineURLFromString(remoteParentURL, remoteRelativePath),
                _ => remoteParentURL
            };
            var localAbsolutePath = Path.Combine(_gamePath, ConverterTool.NormalizePath(remoteRelativePath));

            return new FilePropertiesRemote
            {
                FT = type,
                CRC = hash,
                S = fileSize,
                N = localAbsolutePath,
                RN = remoteAbsolutePath,
                IsPatchApplicable = isPatchApplicable,
                IsHasHashMark = isHasHashMark,
            };
        }

        private string[] GetCurrentAudioLangList(string fallbackCurrentLangname)
        {
            // Initialize the variable.
            string audioLangListPath = _gameAudioLangListPath;
            string audioLangListPathStatic = _gameAudioLangListPathStatic;
            string[] returnValue;

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

            // Return the value
            return returnValue;
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
