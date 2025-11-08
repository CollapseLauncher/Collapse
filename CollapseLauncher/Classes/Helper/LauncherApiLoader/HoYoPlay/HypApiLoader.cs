using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.Plugin.Core.Management;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable IdentifierTypo
// ReSharper disable once CheckNamespace
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay
{
    internal sealed partial class HypApiLoader : LauncherApiBase
    {
        #region Constructor
        private HypApiLoader(PresetConfig presetConfig, string gameName, string gameRegion)
            : base(presetConfig, gameName, gameRegion, GeneralHttpClientFactory, ResourceHttpClientFactory)
        {
            ArgumentNullException.ThrowIfNull(presetConfig);
        }

        private static HttpClient GeneralHttpClientFactory(PresetConfig presetConfig)
        {
            // Set the HttpClientBuilder for HoYoPlay's own General API.
            HttpClientBuilder apiGeneralHttpBuilder = new HttpClientBuilder()
                                                     .UseLauncherConfig()
                                                     .AllowUntrustedCert()
                                                     .SetHttpVersion(HttpVersion.Version30)
                                                     .SetAllowedDecompression()
                                                     .AddHeader("x-rpc-device_id", GetDeviceId(presetConfig));

            // If the metadata has user-agent defined, set the resource's HttpClient user-agent
            if (!string.IsNullOrEmpty(presetConfig.ApiGeneralUserAgent))
            {
                apiGeneralHttpBuilder.SetUserAgent(presetConfig.ApiGeneralUserAgent);
            }

            // Add other API general and resource headers from the metadata configuration
            presetConfig.AddApiGeneralAdditionalHeaders((key, value) => apiGeneralHttpBuilder.AddHeader(key, value));
            
            // Create HttpClient instances for both General and Resource APIs.
            return apiGeneralHttpBuilder.Create();
        }

        private static HttpClient ResourceHttpClientFactory(PresetConfig presetConfig)
        {
            // Set the HttpClientBuilder for HoYoPlay's own Resource API.
            HttpClientBuilder apiResourceHttpBuilder = new HttpClientBuilder()
                                                      .UseLauncherConfig()
                                                      .AllowUntrustedCert()
                                                      .SetHttpVersion(HttpVersion.Version30)
                                                      .SetAllowedDecompression(DecompressionMethods.None)
                                                      .AddHeader("x-rpc-device_id", GetDeviceId(presetConfig));

            // If the metadata has user-agent defined, set the resource's HttpClient user-agent
            if (!string.IsNullOrEmpty(presetConfig.ApiResourceUserAgent))
            {
                apiResourceHttpBuilder.SetUserAgent(string.Format(presetConfig.ApiResourceUserAgent, InnerLauncherConfig.m_isWindows11 ? "11" : "10"));
            }

            // Add other API general and resource headers from the metadata configuration
            presetConfig.AddApiResourceAdditionalHeaders((key, value) => apiResourceHttpBuilder.AddHeader(key, value));

            // Create HttpClient instances for both General and Resource APIs.
            return apiResourceHttpBuilder.Create();
        }
        #endregion

        public static HypApiLoader CreateApiInstance(PresetConfig presetConfig, string gameName, string gameRegion)
            => new(presetConfig, gameName, gameRegion);

        #region Loaders
        protected override async Task
            LoadAsyncInner(ActionOnTimeOutRetry? onTimeoutRoutine,
                           CancellationToken     token)
        {
            // 2025-05-05: As per now, the Sophon resource information requires to be fetched first.
            //             This is mandatory due to latest Genshin Impact changes which removes zip
            //             packages and also version infos.
            await Task.WhenAll(LoadLauncherResourceSophon(onTimeoutRoutine, token),
                               LoadLauncherResource(onTimeoutRoutine, token),
                               LoadLauncherNews(onTimeoutRoutine, token),
                               LoadLauncherGameInfo(onTimeoutRoutine, token));

            InitializeFakeVersionInfo();
            PerformDebugRoutines();
        }
        #endregion

        #region 1. Load Sophon Packages

        private async Task LoadLauncherResourceSophon(ActionOnTimeOutRetry? onTimeoutRoutine,
                                                      CancellationToken     token)
        {
            SophonChunkUrls? sophonUrls = PresetConfig?.LauncherResourceChunksURL;
            if (sophonUrls == null)
            {
                return;
            }

            string? sophonBranchUrl = sophonUrls.BranchUrl;
            if (string.IsNullOrEmpty(PresetConfig!.LauncherBizName) || string.IsNullOrEmpty(sophonBranchUrl))
            {
                Logger.LogWriteLine("This game/region doesn't have Sophon->BranchUrl or PresetConfig->LauncherBizName property defined! This might cause the launcher inaccurately check the version if Zip download is unavailable", LogType.Warning, true);
            }

            // Ensure associated
            await sophonUrls.EnsureReassociated(ApiGeneralHttpClient,
                                                sophonBranchUrl,
                                                PresetConfig.LauncherBizName!,
                                                false,
                                                token);

            sophonUrls.ResetAssociation(); // Reset association so it won't conflict with preload/update/install activity

            ActionTimeoutTaskAwaitableCallback<HypLauncherGameInfoApi?> launcherSophonBranchCallback =
                innerToken =>
                    ApiGeneralHttpClient
                       .GetFromCachedJsonAsync(PresetConfig.LauncherResourceChunksURL?.BranchUrl,
                                               HoYoPlayLauncherGameInfoJsonContext.Default.HypLauncherGameInfoApi,
                                               null,
                                               innerToken)
                       .ConfigureAwait(false);

            await launcherSophonBranchCallback
               .WaitForRetryAsync(ExecutionTimeout,
                                  ExecutionTimeoutStep,
                                  ExecutionTimeoutAttempt,
                                  onTimeoutRoutine,
                                  result => LauncherGameResourceSophon = result,
                                  token);
        }

        private void InitializeFakeVersionInfo()
        {
            if (LauncherGameResource?.data == null)
            {
                return;
            }

            HypGameInfoBranchesKind? gameBranch = LauncherGameResourceSophon?.Data?.GameBranchesInfo?
               .FirstOrDefault(x => x.GameInfo?.GameBiz?.Equals(PresetConfig?.LauncherBizName) ?? false);

            if (gameBranch == null)
            {
                return;
            }

            if (GameVersion.TryParse(gameBranch.GameMainField?.Tag, out GameVersion latestSophonVersion) &&
                GameVersion.TryParse(LauncherGameResource.data.game?.latest?.version, out GameVersion latestZipVersion) &&
                latestSophonVersion == latestZipVersion)
            {
                return;
            }

            HypGameInfoBranchData? branchPreloadField = gameBranch.GamePreloadField;
            HypGameInfoBranchData? branchBaseField    = gameBranch.GameMainField;

            if (branchPreloadField != null)
            {
                LauncherGameResource.data.pre_download_game ??= new RegionResourceLatest();
                AddFakeVersionInfo(branchPreloadField, LauncherGameResource.data.pre_download_game);
            }

            if (branchBaseField == null)
            {
                return;
            }

            LauncherGameResource.data.game ??= new RegionResourceLatest();
            AddFakeVersionInfo(branchBaseField, LauncherGameResource.data.game);
            IsForceRedirectToSophon = true;
        }

        private static void AddFakeVersionInfo(HypGameInfoBranchData branchData, RegionResourceLatest region)
        {
            region.latest         ??= new RegionResourceVersion();
            region.latest.version =   branchData.Tag;

            region.diffs ??= [];

            HashSet<string> existingDiffsVer = new(region.diffs.Select(x => x.version)!);
            foreach (string versionTags in (branchData.DiffTags ?? [])
                    .Where(x => !existingDiffsVer.Contains(x)))
            {
                region.diffs.Add(new RegionResourceVersion
                {
                    version = versionTags
                });
            }
        }
        #endregion

        #region 2. Load Zip-Based Packages
        private Task LoadLauncherResource(ActionOnTimeOutRetry? onTimeoutRoutine,
                                            CancellationToken     token)
        {
            ActionTimeoutTaskAwaitableCallback<HypLauncherResourceApi?> hypResourceResponseCallback =
                innerToken => ApiGeneralHttpClient
                             .GetFromCachedJsonAsync(PresetConfig?.LauncherResourceURL,
                                                     HypLauncherResourceApiJsonContext.Default.HypLauncherResourceApi,
                                                     token: innerToken)
                             .ConfigureAwait(false);

            // Assign as 3 Task array
            Task[] tasks = [
                Task.CompletedTask,
                Task.CompletedTask,
                Task.CompletedTask
                ];

            // Init as null first before being assigned when the backing task is called
            HypLauncherResourceApi? hypResourceResponse = null;
            HypLauncherResourceApi? hypPluginResource   = null;
            HypLauncherResourceApi? hypSdkResource      = null;

            tasks[0] = hypResourceResponseCallback
                .WaitForRetryAsync(ExecutionTimeout,
                                   ExecutionTimeoutStep,
                                   ExecutionTimeoutAttempt,
                                   onTimeoutRoutine,
                                   result => hypResourceResponse = result,
                                   token);

            if (!string.IsNullOrEmpty(PresetConfig?.LauncherPluginURL) && (PresetConfig.IsPluginUpdateEnabled ?? false))
            {
                ActionTimeoutTaskAwaitableCallback<HypLauncherResourceApi?> hypPluginResourceCallback =
                    innerToken => ApiGeneralHttpClient
                                 .GetFromCachedJsonAsync(PresetConfig?.LauncherPluginURL,
                                                         HypLauncherResourceApiJsonContext.Default.HypLauncherResourceApi,
                                                         token: innerToken)
                                 .ConfigureAwait(false);

                tasks[1] = hypPluginResourceCallback
                    .WaitForRetryAsync(ExecutionTimeout,
                                       ExecutionTimeoutStep,
                                       ExecutionTimeoutAttempt,
                                       onTimeoutRoutine,
                                       result => hypPluginResource = result,
                                       token);
            }

            if (!string.IsNullOrEmpty(PresetConfig?.LauncherGameChannelSDKURL))
            {
                ActionTimeoutTaskAwaitableCallback<HypLauncherResourceApi?> hypSdkResourceCallback =
                    innerToken => ApiGeneralHttpClient
                                 .GetFromCachedJsonAsync(PresetConfig?.LauncherGameChannelSDKURL,
                                                         HypLauncherResourceApiJsonContext.Default.HypLauncherResourceApi,
                                                         token: innerToken)
                                 .ConfigureAwait(false);

                tasks[2] = hypSdkResourceCallback
                   .WaitForRetryAsync(ExecutionTimeout,
                                      ExecutionTimeoutStep,
                                      ExecutionTimeoutAttempt,
                                      onTimeoutRoutine,
                                      result => hypSdkResource = result,
                                      token);
            }

            RegionResourceLatest sophonResourceCurrentPackage = new();
            RegionResourceGame sophonResourceData = new()
            {
                game = sophonResourceCurrentPackage
            };

            RegionResourceProp sophonResourcePropRoot = new()
            {
                data = sophonResourceData
            };

            // Await all callbacks
            Task waitAllTask = Task.WhenAll(tasks);
            waitAllTask.GetAwaiter().OnCompleted(AfterExecute);

            return waitAllTask;

            void AfterExecute()
            {
                ConvertPluginResources(ref sophonResourceData, hypPluginResource);
                ConvertSdkResources(ref sophonResourceData, hypSdkResource);
                ConvertPackageResources(sophonResourceData, hypResourceResponse?.Data?.LauncherPackages);

                LauncherGameResource = sophonResourcePropRoot;
            }
        }
        #endregion

        #region 3. Load News

        private Task LoadLauncherNews(ActionOnTimeOutRetry? onTimeoutRoutine,
                                      CancellationToken     token)
        {
            bool isUseMultiLang = PresetConfig?.LauncherSpriteURLMultiLang ?? false;

            string localeCode = isUseMultiLang ? Locale.Lang.LanguageID.ToLower() : PresetConfig?.LauncherSpriteURLMultiLangFallback!;
            string launcherSpriteUrl = string.Format(PresetConfig?.LauncherSpriteURL!, localeCode);
            string launcherNewsUrl = string.Format(PresetConfig?.LauncherNewsURL!, localeCode);

            ActionTimeoutTaskAwaitableCallback<HypLauncherBackgroundApi?> hypLauncherBackgroundCallback =
                innerToken =>
                    ApiResourceHttpClient
                       .GetFromCachedJsonAsync(launcherSpriteUrl,
                                               HypLauncherBackgroundApiJsonContext.Default.HypLauncherBackgroundApi,
                                               null,
                                               innerToken)
                       .ConfigureAwait(false);

            ActionTimeoutTaskAwaitableCallback<HypLauncherBackgroundApi?> hypLauncherNewsCallback =
                innerToken =>
                    ApiResourceHttpClient
                       .GetFromCachedJsonAsync(launcherNewsUrl,
                                               HypLauncherBackgroundApiJsonContext.Default.HypLauncherBackgroundApi,
                                               null,
                                               innerToken)
                       .ConfigureAwait(false);

            HypLauncherBackgroundApi? hypLauncherBackground = null;
            HypLauncherBackgroundApi? hypLauncherNews = null;

            // Load both in parallel
            Task[] tasks = [
                hypLauncherBackgroundCallback
                    .WaitForRetryAsync(ExecutionTimeout,
                                       ExecutionTimeoutStep,
                                       ExecutionTimeoutAttempt,
                                       onTimeoutRoutine,
                                       result => hypLauncherBackground = result,
                                       token),
                hypLauncherNewsCallback
                    .WaitForRetryAsync(ExecutionTimeout,
                                       ExecutionTimeoutStep,
                                       ExecutionTimeoutAttempt,
                                       onTimeoutRoutine,
                                       result => hypLauncherNews = result,
                                       token)
                ];

            // Await the result
            Task waitAllTask = Task.WhenAll(tasks);
            waitAllTask.GetAwaiter().OnCompleted(AfterExecute);
            return waitAllTask;

            void AfterExecute()
            {
                // Merge background image
                if (hypLauncherBackground?.Data?.Backgrounds != null && hypLauncherNews?.Data != null)
                    hypLauncherNews.Data.Backgrounds = hypLauncherBackground.Data.Backgrounds;
            }
        }
        #endregion

        #region 4. Load Game Info

        private Task LoadLauncherGameInfo(ActionOnTimeOutRetry? onTimeoutRoutine,
                                          CancellationToken     token)
        {
            if (PresetConfig?.LauncherGameInfoDisplayURL == null)
            {
                LauncherGameInfoField = new HypGameInfoData();
                return Task.CompletedTask;
            }

            bool isUseMultiLang = PresetConfig?.LauncherSpriteURLMultiLang ?? false;

            string localeCode = isUseMultiLang ? Locale.Lang.LanguageID.ToLower() : PresetConfig?.LauncherSpriteURLMultiLangFallback!;
            string launcherGameInfoUrl = string.Format(PresetConfig?.LauncherGameInfoDisplayURL!, localeCode);

            ActionTimeoutTaskAwaitableCallback<HypLauncherGameInfoApi?> hypLauncherGameInfoCallback =
                innerToken =>
                    ApiResourceHttpClient
                       .GetFromCachedJsonAsync(launcherGameInfoUrl,
                                               HoYoPlayLauncherGameInfoJsonContext.Default.HypLauncherGameInfoApi,
                                               null,
                                               innerToken)
                       .ConfigureAwait(false);

            return hypLauncherGameInfoCallback
                  .WaitForRetryAsync(ExecutionTimeout,
                                     ExecutionTimeoutStep,
                                     ExecutionTimeoutAttempt,
                                     onTimeoutRoutine,
                                     action =>
                                     {
                                         HypGameInfoData? sophonLauncherGameInfoRoot = new();
                                         if (action == null)
                                         {
                                             return;
                                         }

                                         ConvertGameInfoResources(ref sophonLauncherGameInfoRoot, action.Data);
                                         LauncherGameInfoField = sophonLauncherGameInfoRoot ?? new HypGameInfoData();
                                     },
                                     token);
        }
        #endregion

        #region Convert Sdk Resources
        private void ConvertSdkResources(ref RegionResourceGame sophonResourceData, HypLauncherResourceApi? hypSdkResources)
        {
            HypChannelSdkData? sdkPackages = hypSdkResources?.Data?.ChannelSdks?
                .FirstOrDefault(x => x.GameInfo?
                                   .GameBiz?
                                   .Equals(PresetConfig?.LauncherBizName,
                                           StringComparison.OrdinalIgnoreCase) ?? false);

            if (sdkPackages?.SdkPackageDetail == null) return;

            sophonResourceData.sdk = new RegionResourceVersion
            {
                md5 = sdkPackages.SdkPackageDetail.PackageMD5Hash,
                size = sdkPackages.SdkPackageDetail.PackageDecompressSize,
                package_size = sdkPackages.SdkPackageDetail.PackageSize ?? 0,
                path = sdkPackages.SdkPackageDetail.PackageUrl,
                url = sdkPackages.SdkPackageDetail.PackageUrl,
                version = sdkPackages.Version,
                pkg_version = sdkPackages.PkgVersionFileName
            };
        }
        #endregion

        #region Convert Plugin Resources
        private void ConvertPluginResources(ref RegionResourceGame sophonResourceData, HypLauncherResourceApi? hypPluginResources)
        {
            HypResourcesData? hypPluginPackage = hypPluginResources?.Data?
                .PluginPackages?
                .FirstOrDefault(x => x.GameInfo?
                    .GameBiz?
                    .Equals(PresetConfig?.LauncherBizName, StringComparison.OrdinalIgnoreCase) ?? false);

            if (hypPluginPackage == null) return;

            List<RegionResourcePlugin> pluginCurrentPackageList = [];
            GuessAssignPluginConversion(pluginCurrentPackageList, hypPluginPackage);
            sophonResourceData.plugins = pluginCurrentPackageList;
        }

        private static void GuessAssignPluginConversion(List<RegionResourcePlugin> sophonPluginList, HypResourcesData hypPlugin)
        {
            List<HypPluginPackageInfo>? pluginSectionsList = hypPlugin.PluginPackageSections;
            if ((pluginSectionsList?.Count ?? 0) == 0) return;
            if (pluginSectionsList == null) return;

            sophonPluginList.AddRange(pluginSectionsList.Select(firstPluginSection => new RegionResourcePlugin
            {
                version    = firstPluginSection.Version,
                plugin_id  = firstPluginSection.PluginId,
                release_id = firstPluginSection.ReleaseId,
                package = new RegionResourceVersion
                {
                    validate     = firstPluginSection.PluginPackage?.PackageAssetValidationList,
                    md5          = firstPluginSection.PluginPackage?.PackageMD5Hash,
                    url          = firstPluginSection.PluginPackage?.PackageUrl,
                    path         = firstPluginSection.PluginPackage?.PackageUrl,
                    size         = firstPluginSection.PluginPackage?.PackageDecompressSize ?? 0,
                    package_size = firstPluginSection.PluginPackage?.PackageSize ?? 0,
                    run_command  = firstPluginSection.PluginPackage?.PackageRunCommand,
                    version      = firstPluginSection.Version
                }
            }));
        }
        #endregion

        #region Convert Package Resources
        private void ConvertPackageResources(RegionResourceGame sophonPackageResources, List<HypResourcesData>? hypLauncherPackagesList)
        {
            if (hypLauncherPackagesList == null) throw new NullReferenceException("HoYoPlay package list is null!");

            foreach (HypResourcesData hypRootPackage in hypLauncherPackagesList
                        .Where(x => x.GameInfo?.GameBiz?
                                  .Equals(PresetConfig?.LauncherBizName,
                                          StringComparison.OrdinalIgnoreCase) ?? false))
            {
                // Assign and convert main game package (latest)
                HypPluginPackageData? hypMainPackageSection    = hypRootPackage.MainPackage?.CurrentVersion;
                RegionResourceVersion    sophonMainPackageSection = new();
                if (hypMainPackageSection != null)
                    ConvertHYPSectionToResourceVersion(ref hypMainPackageSection, ref sophonMainPackageSection);

                // If the main game package reference is null, skip it
                if (sophonPackageResources.game == null)
                    continue;

                sophonPackageResources.game.latest = sophonMainPackageSection;

                // Assign and convert main game package (diff)
                if (hypRootPackage.MainPackage?.Patches != null)
                {
                    sophonPackageResources.game.diffs = [];
                    foreach (HypPluginPackageData hypMainDiffPackageSection in hypRootPackage.MainPackage.Patches)
                    {
                        HypPluginPackageData hypMainDiffHypPluginPackageSectionRef = hypMainDiffPackageSection;
                        RegionResourceVersion   sophonResourceVersion        = new();
                        ConvertHYPSectionToResourceVersion(ref hypMainDiffHypPluginPackageSectionRef, ref sophonResourceVersion);
                        sophonPackageResources.game.diffs.Add(sophonResourceVersion);
                    }
                }
                sophonPackageResources.pre_download_game = new RegionResourceLatest();

                // Convert if preload entry is not empty or null
                if (hypRootPackage.PreDownload?.CurrentVersion == null &&
                    (hypRootPackage.PreDownload?.Patches?.Count ?? 0) == 0)
                {
                    continue;
                }

                // Assign and convert preload game package (latest)
                HypPluginPackageData? hypPreloadPackageSection = hypRootPackage.PreDownload?.CurrentVersion;
                if (hypPreloadPackageSection != null)
                {
                    RegionResourceVersion sophonPreloadPackageSection = new();
                    ConvertHYPSectionToResourceVersion(ref hypPreloadPackageSection, ref sophonPreloadPackageSection);
                    sophonPackageResources.pre_download_game.latest = sophonPreloadPackageSection;
                }

                // Assign and convert preload game package (diff)
                if (hypRootPackage.PreDownload?.Patches == null || hypRootPackage.PreDownload.Patches.Count == 0)
                    continue;

                sophonPackageResources.pre_download_game.diffs = [];
                foreach (HypPluginPackageData hypPreloadDiffPackageSection in hypRootPackage.PreDownload
                            .Patches)
                {
                    HypPluginPackageData hypPreloadDiffHypPluginPackageSectionRef = hypPreloadDiffPackageSection;
                    RegionResourceVersion   sophonResourceVersion           = new();
                    ConvertHYPSectionToResourceVersion(ref hypPreloadDiffHypPluginPackageSectionRef,
                                                       ref sophonResourceVersion);
                    sophonPackageResources.pre_download_game.diffs.Add(sophonResourceVersion);
                }
            }
        }

        private void ConvertHYPSectionToResourceVersion(ref HypPluginPackageData hypHypPluginPackageData, ref RegionResourceVersion sophonResourceVersion)
        {
            ArgumentNullException.ThrowIfNull(hypHypPluginPackageData);
            ArgumentNullException.ThrowIfNull(sophonResourceVersion);

            // Convert game packages
            RegionResourceVersion packagesVersion = new();
            DelegatePackageResConversionMode(ref packagesVersion, hypHypPluginPackageData.GamePackages, hypHypPluginPackageData.AudioPackages, hypHypPluginPackageData.Version, hypHypPluginPackageData.ResourceListUrl);
            sophonResourceVersion = packagesVersion;
        }

        private delegate void PackageResConversionModeDelegate(ref RegionResourceVersion sophonPackageVersion, List<HypPackageData> hypPackageList,
            string? version, string? resourceListUrl);

        private void DelegatePackageResConversionMode(ref RegionResourceVersion sophonPackageVersion, List<HypPackageData>? hypGamePackageList, List<HypPackageData>? hypAudioPackageList,
            string? version, string? resourceListUrl)
        {
            // If the main package list is not null or empty, then process
            if (hypGamePackageList != null && hypGamePackageList.Count != 0)
            {
                // Delegate the conversion mode for the resource, then process it
                PackageResConversionModeDelegate conversionDelegate = hypGamePackageList.Count > 1 ? ConvertMultiPackageResource : ConvertSinglePackageResource;
                conversionDelegate(ref sophonPackageVersion, hypGamePackageList, version, resourceListUrl);
            }

            // If the audio package list is not null or empty, then process
            if (hypAudioPackageList == null || hypAudioPackageList.Count == 0)
            {
                return;
            }

            sophonPackageVersion.voice_packs = [];
            foreach (HypPackageData hypAudioPackage in hypAudioPackageList)
            {
                sophonPackageVersion.voice_packs.Add(new RegionResourceVersion
                {
                    url          = hypAudioPackage.PackageUrl,
                    path         = hypAudioPackage.PackageUrl, // As fallback for PackageUrl
                    size         = hypAudioPackage.PackageDecompressSize,
                    package_size = hypAudioPackage.PackageSize ?? 0,
                    md5          = hypAudioPackage.PackageMD5Hash,
                    language     = hypAudioPackage.Language
                });
            }
        }

        private void ConvertSinglePackageResource(ref RegionResourceVersion sophonPackageVersion, List<HypPackageData> hypPackageList,
            string? version, string? resourceListUrl)
        {
            HypPackageData hypPackageData = hypPackageList[0];
            sophonPackageVersion.url = hypPackageData.PackageUrl;
            sophonPackageVersion.path = hypPackageData.PackageUrl; // As fallback for PackageUrl
            sophonPackageVersion.size = hypPackageData.PackageDecompressSize;
            sophonPackageVersion.package_size = hypPackageData.PackageSize ?? 0;
            sophonPackageVersion.md5 = hypPackageData.PackageMD5Hash;
            sophonPackageVersion.entry = PresetConfig?.GameExecutableName;
            sophonPackageVersion.version = version;
            sophonPackageVersion.decompressed_path = resourceListUrl;
        }

        private void ConvertMultiPackageResource(ref RegionResourceVersion sophonPackageVersion, List<HypPackageData> hypPackageList,
            string? version, string? resourceListUrl)
        {
            long totalSize = hypPackageList.Sum(x => x.PackageDecompressSize);
            long totalPackageSize = hypPackageList.Sum(x => x.PackageSize ?? 0);

            sophonPackageVersion.url = string.Empty;
            sophonPackageVersion.path = string.Empty; // As fallback for PackageUrl
            sophonPackageVersion.md5 = string.Empty;
            sophonPackageVersion.size = totalSize;
            sophonPackageVersion.package_size = totalPackageSize;
            sophonPackageVersion.entry = PresetConfig?.GameExecutableName;
            sophonPackageVersion.version = version;
            sophonPackageVersion.decompressed_path = resourceListUrl;

            sophonPackageVersion.segments = [];

            foreach (HypPackageData packageDetail in hypPackageList)
            {
                sophonPackageVersion.segments.Add(new RegionResourceVersion
                {
                    url = packageDetail.PackageUrl,
                    path = packageDetail.PackageUrl, // As fallback for PackageUrl
                    size = packageDetail.PackageDecompressSize,
                    package_size = packageDetail.PackageSize ?? 0,
                    md5 = packageDetail.PackageMD5Hash
                });
            }
        }
        #endregion

        #region Convert Game Info Resources
        private void ConvertGameInfoResources([DisallowNull] ref HypGameInfoData? sophonGameInfo, HypGameInfo? hypLauncherGameInfoList)
        {
            if (hypLauncherGameInfoList != null)
            {
                sophonGameInfo =
                    hypLauncherGameInfoList.Data?.FirstOrDefault(x =>
                                                                     x.GameBiz?.Equals(PresetConfig
                                                                                ?.LauncherBizName) ??
                                                                     false);
            }
            else
            {
                throw new ArgumentNullException(nameof(hypLauncherGameInfoList));
            }
        }
        #endregion

        #region GetDeviceId override
        private static string GetDeviceId(PresetConfig preset)
        {
            // Determine if the client is a mainland client based on the zone name
            bool isMainlandClient = (preset.ZoneName?.Equals("Mainland China") ?? false) || (preset.ZoneName?.Equals("Bilibili") ?? false);

            // Set the publisher name based on the client type
            string publisherName = isMainlandClient ? "miHoYo" : "Cognosphere";
            // Define the registry root path for the publisher
            string registryRootPath = $@"Software\{publisherName}\HYP";

            // Open the registry key for the root path
            RegistryKey? rootRegistryKey = Registry.CurrentUser.OpenSubKey(registryRootPath, true);
            // Find or create the HYP device ID
            string hypDeviceId = FindOrCreateHYPDeviceId(rootRegistryKey, isMainlandClient, registryRootPath);
            return hypDeviceId;
        }

        private static string FindOrCreateHYPDeviceId(RegistryKey? rootRegistryKey, bool isMainlandClient, string registryRootPath)
        {
            // Define default version keys for mainland and global clients
            const string HYPVerDefaultCN = "1_1";
            const string HYPVerDefaultGlb = "1_0";

            // Use the root registry key or create it if it doesn't exist
            using (rootRegistryKey ??= Registry.CurrentUser.CreateSubKey(registryRootPath, true))
            {
                // Get the subkey names under the root registry key
                string[] subKeyNames = rootRegistryKey.GetSubKeyNames();
                foreach (string subKeyNameString in subKeyNames)
                {
                    // Open each subkey and check for the HYPDeviceId value
                    using RegistryKey? subKeyNameKey = rootRegistryKey.OpenSubKey(subKeyNameString, true);
                    if (subKeyNameKey == null)
                    {
                        continue;
                    }

                    // Get the current HYP device ID from the subkey
                    string? currentHypDeviceId = (string?)subKeyNameKey.GetValue("HYPDeviceId", null);
                    if (string.IsNullOrEmpty(currentHypDeviceId))
                    {
                        continue;
                    }

                    // Return the current HYP device ID if found
                    return currentHypDeviceId;
                }

                // Open or create the subkey for the default version based on the client type
                using RegistryKey subRegistryKey = rootRegistryKey.OpenSubKey(isMainlandClient ? HYPVerDefaultCN : HYPVerDefaultGlb, true)
                    ?? rootRegistryKey.CreateSubKey(isMainlandClient ? HYPVerDefaultCN : HYPVerDefaultGlb, true);

                // Generate a new HYP device ID
                string newHypDeviceId = CreateNewDeviceId();
                // Set the new HYP device ID in the subkey
                subRegistryKey.SetValue("HYPDeviceId", newHypDeviceId, RegistryValueKind.String);

                return newHypDeviceId;
            }
        }

        private static string CreateNewDeviceId()
        {
            string guid;
            try
            {
                // Define the registry key path for cryptography settings
                const string regKeyCryptography = @"SOFTWARE\Microsoft\Cryptography";

                // Open the registry key for reading
                using var rootRegistryKey = Registry.LocalMachine.OpenSubKey(regKeyCryptography, true);
                // Retrieve the MachineGuid value from the registry, or generate a new GUID if it doesn't exist
                guid = ((string?)rootRegistryKey?.GetValue("MachineGuid", null) ??
                               Guid.NewGuid().ToString()).Replace("-", string.Empty);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[HypApiLoader::CreateNewDeviceId] Failed to retrieve MachineGuid from registry, using a dummy GUID instead" +
                                    $"\r\n{ex}", LogType.Error, true);
                guid = Guid.NewGuid().ToString().Replace("-", string.Empty);
            }

            // Append the current Unix timestamp in milliseconds to the GUID
            return guid + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        #endregion

        #region Debug-Specific Methods
        protected void PerformDebugRoutines()
        {
#if DEBUG
            if (LauncherGameResource?.data?.game?.latest?.decompressed_path != null)
            {
                Logger.LogWriteLine($"Decompressed Path: {LauncherGameResource.data.game.latest.decompressed_path}",
                                    LogType.Default, true);
            }

            if (LauncherGameResource?.data?.game?.latest?.path != null)
            {
                Logger.LogWriteLine($"ZIP Path: {LauncherGameResource.data.game.latest.path}", LogType.Default, true);
            }

            if (LauncherGameResource?.data?.pre_download_game?.latest?.decompressed_path != null)
            {
                Logger.LogWriteLine($"Decompressed Path Pre-load: {LauncherGameResource.data.pre_download_game?.latest?.decompressed_path}",
                                    LogType.Default, true);
            }

            if (LauncherGameResource?.data?.sdk?.path != null)
            {
                Logger.LogWriteLine($"SDK found! Path: {LauncherGameResource.data.sdk.path}", LogType.Default, true);
            }

            if (LauncherGameResource?.data?.pre_download_game?.latest?.path != null)
            {
                Logger.LogWriteLine($"ZIP Path Pre-load: {LauncherGameResource.data.pre_download_game?.latest?.path}",
                                    LogType.Default, true);
            }
#endif

#if SIMULATEPRELOAD && !SIMULATEAPPLYPRELOAD
            if (LauncherGameResource != null && LauncherGameResource?.data?.pre_download_game == null)
            {
                Logger.LogWriteLine("[FetchLauncherDownloadInformation] SIMULATEPRELOAD: Simulating Pre-load!");
                RegionResourceVersion? simDataLatest = LauncherGameResource?.data?.game?.latest?.Copy();
                List<RegionResourceVersion>? simDataDiff = LauncherGameResource?.data?.game?.diffs?.Copy();

                if (simDataLatest == null)
                    return;

                simDataLatest.version = new GameVersion(simDataLatest.version).GetIncrementedVersion().ToString();

                LauncherGameResource!.data ??= new RegionResourceGame();
                LauncherGameResource.data.pre_download_game = new RegionResourceLatest() { latest = simDataLatest };

                if (simDataDiff == null || simDataDiff.Count == 0) return;
                foreach (RegionResourceVersion diff in simDataDiff)
                {
                    diff.version = new GameVersion(diff.version)
                        .GetIncrementedVersion()
                        .ToString();
                }
                LauncherGameResource.data.pre_download_game.diffs = simDataDiff;
            }
#endif
#if !SIMULATEPRELOAD && SIMULATEAPPLYPRELOAD
            if (LauncherGameResource?.data?.pre_download_game != null)
            {
                LauncherGameResource.data.game = LauncherGameResource.data.pre_download_game;
            }
#endif

#if DEBUG
            Logger.LogWriteLine("[LauncherApiBase::LoadLauncherResource] Loading game resource has been completed!",
                                LogType.Debug, true);
#endif
        }
        #endregion
    }
}
