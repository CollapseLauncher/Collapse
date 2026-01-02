using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.RepairManagement;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault

#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher
{
    internal partial class StarRailRepairV2
    {
        private async Task Fetch(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            // Set total activity string as "Loading Indexes..."
            Status.ActivityStatus            = Lang._GameRepairPage.Status2;
            Status.IsProgressAllIndetermined = true;

            UpdateStatus();

            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder()
                                     .UseLauncherConfig(DownloadThreadWithReservedCount)
                                     .SetUserAgent(UserAgent)
                                     .SetAllowedDecompression(DecompressionMethods.None)
                                     .Create();

            // Get shared HttpClient
            HttpClient sharedClient       = FallbackCDNUtil.GetGlobalHttpClient(true);
            string     regionId           = GetExistingGameRegionID();
            string[]   installedVoiceLang = await GetInstalledVoiceLanguageOrDefault(token);

            // Redirect to fetch cache if Cache Update Mode is used.
            if (IsCacheUpdateMode)
            {
                await FetchForCacheUpdateMode(client, regionId, assetIndex, token);
                return;
            }

            // Get the primary manifest
            await GetPrimaryManifest(assetIndex, installedVoiceLang, token);

            // If the this._isOnlyRecoverMain && base._isVersionOverride is true, copy the asset index into the _originAssetIndex
            if (IsOnlyRecoverMain && IsVersionOverride)
            {
                OriginAssetIndex = [];
                foreach (FilePropertiesRemote asset in assetIndex)
                {
                    FilePropertiesRemote newAsset          = asset.Copy();
                    ReadOnlyMemory<char> assetRelativePath = newAsset.N.AsMemory(GamePath.Length).TrimStart('\\');
                    newAsset.N = assetRelativePath.ToString();
                    OriginAssetIndex.Add(newAsset);
                }
            }

            // Fetch assets from game server
            if (!IsVersionOverride &&
                !IsOnlyRecoverMain)
            {
                PresetConfig gamePreset = GameVersionManager.GamePreset;
                SRDispatcherInfo dispatcherInfo = new(gamePreset.GameDispatchArrayURL,
                                                      gamePreset.ProtoDispatchKey,
                                                      gamePreset.GameDispatchURLTemplate,
                                                      gamePreset.GameGatewayURLTemplate,
                                                      gamePreset.GameDispatchChannelName,
                                                      GameVersionManager.GetGameVersionApi().ToString());
                await dispatcherInfo.Initialize(client, regionId, token);

                StarRailPersistentRefResult persistentRefResult = await StarRailPersistentRefResult
                   .GetRepairReferenceAsync(this,
                                      dispatcherInfo,
                                      client,
                                      GamePath,
                                      GameDataPersistentPathRelative,
                                      token);

                assetIndex.AddRange(persistentRefResult.GetPersistentFiles(assetIndex, GamePath, installedVoiceLang));
                await persistentRefResult.FinalizeRepairFetchAsync(this, sharedClient, assetIndex,
                                                                   GameDataPersistentPath, token);
            }

            // Force-Fetch the Bilibili SDK (if exist :pepehands:)
            await FetchBilibiliSdk(token);

            // Remove plugin from assetIndex
            // Skip the removal for Delta-Patch
            if (!IsOnlyRecoverMain)
            {
                EliminatePluginAssetIndex(assetIndex, x => x.N, x => x.RN);
            }
        }

        #region PrimaryManifest

        private async Task<string[]> GetInstalledVoiceLanguageOrDefault(CancellationToken token)
        {
            if (!File.Exists(GameAudioLangListPathStatic))
            {
                return []; // Return empty. For now, let's not mind about what VOs the user actually have and let the game decide.
            }

            string[] installedAudioLang = (await File.ReadAllLinesAsync(GameAudioLangListPathStatic, token))
                                   .Where(x => !string.IsNullOrEmpty(x))
                                   .ToArray();
            return installedAudioLang;
        }

        private async Task GetPrimaryManifest(List<FilePropertiesRemote> assetIndex, string[] voiceLang, CancellationToken token)
        {
            // 2025/12/28:
            // Starting from this, we use Sophon as primary manifest source instead of relying on our Game Repair Index
            // as miHoYo might remove uncompressed files from their CDN and fully moving to Sophon.

            HttpClient client = FallbackCDNUtil.GetGlobalHttpClient(true);

            string[] excludedMatchingField = ["en-us", "zh-cn", "ja-jp", "ko-kr"];
            if (File.Exists(GameAudioLangListPathStatic))
            {
                string[] installedAudioLang = voiceLang
                                       .Select(x => x switch
                                                    {
                                                        "English" => "en-us",
                                                        "English(US)" => "en-us",
                                                        "Japanese" => "ja-jp",
                                                        "Chinese(PRC)" => "zh-cn",
                                                        "Korean" => "ko-kr",
                                                        _ => ""
                                                    })
                                       .Where(x => !string.IsNullOrEmpty(x))
                                       .ToArray();

                excludedMatchingField = excludedMatchingField.Where(x => !installedAudioLang.Contains(x))
                                                             .ToArray();
            }

            await this.FetchAssetsFromSophonAsync(client,
                                                  assetIndex,
                                                  DetermineFileTypeFromExtension,
                                                  GameVersion,
                                                  excludedMatchingField,
                                                  token);
        }

        #endregion

        #region Utilities

        internal static FileType DetermineFileTypeFromExtension(string fileName)
        {
            if (fileName.EndsWith(".block", StringComparison.OrdinalIgnoreCase))
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

        private unsafe string GetExistingGameRegionID()
        {
            // Delegate the default return value
            string GetDefaultValue() => InnerGameVersionManager.GamePreset.GameDispatchDefaultName ?? throw new KeyNotFoundException("Default dispatcher name in metadata is not exist!");

            // Try to get the value as nullable object
            object? value = GameSettings?.RegistryRoot?.GetValue("App_LastServerName_h2577443795", null);
            // Check if the value is null, then return the default name
            // Return the dispatch default name. If none, then throw
            if (value == null) return GetDefaultValue();

            // Cast the value as byte array
            byte[] valueBytes = (byte[])value;
            int count = valueBytes.Length;

            // If the registry is empty, then return the default value;
            if (valueBytes.Length == 0)
                return GetDefaultValue();

            // Get the pointer of the byte array
            fixed (byte* valuePtr = &valueBytes[0])
            {
                // Try check the byte value. If it's null, then continue the loop while
                // also decreasing the count as its index
                while (*(valuePtr + (count - 1)) == 0) { --count; }

                // Get the name from the span and trim the \0 character at the end
                string name = Encoding.UTF8.GetString(valuePtr, count);
                return name;
            }
        }

        private void CountAssetIndex(List<FilePropertiesRemote> assetIndex)
        {
            // Sum the assetIndex size and assign to _progressAllSize
            ProgressAllSizeTotal = assetIndex.Sum(x => x.S);

            // Assign the assetIndex count to _progressAllCount
            ProgressAllCountTotal = assetIndex.Count;
        }

        private static RepairAssetType ConvertRepairAssetTypeEnum(FileType assetType) => assetType switch
        {
            FileType.Unused => RepairAssetType.Unused,
            FileType.Block => RepairAssetType.Block,
            FileType.Audio => RepairAssetType.Audio,
            FileType.Video => RepairAssetType.Video,
            _ => RepairAssetType.Generic
        };
        #endregion
    }
}
