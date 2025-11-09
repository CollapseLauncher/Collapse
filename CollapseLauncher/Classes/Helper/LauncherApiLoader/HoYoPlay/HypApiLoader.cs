using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.Plugin.Core.Management;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
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

            ActionTimeoutTaskAwaitableCallback<HypLauncherSophonBranchesApi?> launcherSophonBranchCallback =
                innerToken =>
                    ApiGeneralHttpClient
                       .GetFromCachedJsonAsync(PresetConfig.LauncherResourceChunksURL?.BranchUrl,
                                               HypApiJsonContext.Default.HypLauncherSophonBranchesApi,
                                               null,
                                               innerToken)
                       .ConfigureAwait(false);

            await launcherSophonBranchCallback
               .WaitForRetryAsync(ExecutionTimeout,
                                  ExecutionTimeoutStep,
                                  ExecutionTimeoutAttempt,
                                  onTimeoutRoutine,
                                  result => LauncherGameSophonBranches = result,
                                  token);
        }

        private void InitializeFakeVersionInfo()
        {
            if (LauncherGameResourcePackage?.Data is not { } gameResourceData ||
                LauncherGameSophonBranches?.Data is not { } gameSophonBranch)
            {
                return;
            }

            if (!gameSophonBranch
                   .TryFindByBizOrId(PresetConfig?.LauncherBizName ?? "",
                                     PresetConfig?.LauncherGameId ?? "",
                                     out HypLauncherSophonBranchesKind? gameBranchSophon))
            {
                return;
            }

            if (!gameResourceData
                   .TryFindByBizOrId(PresetConfig?.LauncherBizName ?? "",
                                     PresetConfig?.LauncherGameId ?? "",
                                     out HypResourcesData? gamePackage))
            {
                return;
            }

            GameVersion currentGameVersion       = gamePackage.MainPackage?.CurrentVersion?.Version ?? default;
            GameVersion currentGameVersionSophon = gameBranchSophon.GameMainField?.Tag;

            if (currentGameVersionSophon == currentGameVersion)
            {
                return;
            }

            HypGameInfoBranchData? branchPreloadField = gameBranchSophon.GamePreloadField;
            HypGameInfoBranchData? branchBaseField    = gameBranchSophon.GameMainField;

            if (branchPreloadField != null)
            {
                gamePackage.PreDownload = new HypResourcePackageData();
                AddFakeVersionInfo(branchPreloadField, gamePackage.PreDownload);
            }

            if (branchBaseField == null)
            {
                return;
            }

            gamePackage.MainPackage = new HypResourcePackageData();
            AddFakeVersionInfo(branchBaseField, gamePackage.MainPackage);
            IsForceRedirectToSophon = true;
        }

        private static void AddFakeVersionInfo(HypGameInfoBranchData branchData, HypResourcePackageData region)
        {
            region.CurrentVersion ??= new HypPackageInfo
            {
                Version = branchData.Tag
            };

            HashSet<string> existingDiffsVer = new(region.Patches.Select(x => x.Version.ToString()));
            foreach (string versionTag in (branchData.DiffTags ?? [])
                    .Where(x => !existingDiffsVer.Contains(x)))
            {
                region.Patches.Add(new HypPackageInfo
                {
                    Version = versionTag
                });
            }
        }
        #endregion

        #region 2. Load Zip-Based Packages
        private Task LoadLauncherResource(ActionOnTimeOutRetry? onTimeoutRoutine,
                                            CancellationToken     token)
        {
            // Assign as 3 Task array
            Task[] tasks = [
                Task.CompletedTask,
                Task.CompletedTask,
                Task.CompletedTask
                ];

            ActionTimeoutTaskAwaitableCallback<HypLauncherGameResourcePackageApi?> hypResourceResponseCallback =
                innerToken => ApiGeneralHttpClient
                             .GetFromCachedJsonAsync(PresetConfig?.LauncherResourceURL,
                                                     HypApiJsonContext.Default.HypLauncherGameResourcePackageApi,
                                                     token: innerToken)
                             .ConfigureAwait(false);


            // Init as null first before being assigned when the backing task is called
            tasks[0] = hypResourceResponseCallback
                .WaitForRetryAsync(ExecutionTimeout,
                                   ExecutionTimeoutStep,
                                   ExecutionTimeoutAttempt,
                                   onTimeoutRoutine,
                                   result => LauncherGameResourcePackage = result,
                                   token);

            if (!string.IsNullOrEmpty(PresetConfig?.LauncherPluginURL) && (PresetConfig.IsPluginUpdateEnabled ?? false))
            {
                ActionTimeoutTaskAwaitableCallback<HypLauncherGameResourcePluginApi?> hypPluginResourceCallback =
                    innerToken => ApiGeneralHttpClient
                                 .GetFromCachedJsonAsync(PresetConfig?.LauncherPluginURL,
                                                         HypApiJsonContext.Default.HypLauncherGameResourcePluginApi,
                                                         token: innerToken)
                                 .ConfigureAwait(false);

                tasks[1] = hypPluginResourceCallback
                    .WaitForRetryAsync(ExecutionTimeout,
                                       ExecutionTimeoutStep,
                                       ExecutionTimeoutAttempt,
                                       onTimeoutRoutine,
                                       result => LauncherGameResourcePlugin = result,
                                       token);
            }

            if (!string.IsNullOrEmpty(PresetConfig?.LauncherGameChannelSDKURL))
            {
                ActionTimeoutTaskAwaitableCallback<HypLauncherGameResourceSdkApi?> hypSdkResourceCallback =
                    innerToken => ApiGeneralHttpClient
                                 .GetFromCachedJsonAsync(PresetConfig?.LauncherGameChannelSDKURL,
                                                         HypApiJsonContext.Default.HypLauncherGameResourceSdkApi,
                                                         token: innerToken)
                                 .ConfigureAwait(false);

                tasks[2] = hypSdkResourceCallback
                   .WaitForRetryAsync(ExecutionTimeout,
                                      ExecutionTimeoutStep,
                                      ExecutionTimeoutAttempt,
                                      onTimeoutRoutine,
                                      result => LauncherGameResourceSdk = result,
                                      token);
            }

            // Await all callbacks
            Task waitAllTask = Task.WhenAll(tasks);
            waitAllTask.GetAwaiter().OnCompleted(AfterExecute);

            return waitAllTask;

            void AfterExecute()
            {
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
                                               HypApiJsonContext.Default.HypLauncherBackgroundApi,
                                               null,
                                               innerToken)
                       .ConfigureAwait(false);

            ActionTimeoutTaskAwaitableCallback<HypLauncherContentApi?> hypLauncherNewsCallback =
                innerToken =>
                    ApiResourceHttpClient
                       .GetFromCachedJsonAsync(launcherNewsUrl,
                                               HypApiJsonContext.Default.HypLauncherContentApi,
                                               null,
                                               innerToken)
                       .ConfigureAwait(false);

            // Load both in parallel
            Task[] tasks = [
                hypLauncherBackgroundCallback
                    .WaitForRetryAsync(ExecutionTimeout,
                                       ExecutionTimeoutStep,
                                       ExecutionTimeoutAttempt,
                                       onTimeoutRoutine,
                                       result => LauncherGameBackground = result,
                                       token),
                hypLauncherNewsCallback
                    .WaitForRetryAsync(ExecutionTimeout,
                                       ExecutionTimeoutStep,
                                       ExecutionTimeoutAttempt,
                                       onTimeoutRoutine,
                                       result => LauncherGameContent = result,
                                       token)
                ];

            // Await the result
            Task waitAllTask = Task.WhenAll(tasks);
            waitAllTask.GetAwaiter().OnCompleted(AfterExecute);
            return waitAllTask;

            void AfterExecute()
            {
                // Merge background image
                /* TODO: Remove these lines after HYP API refactor is complete
                if (hypLauncherBackground?.Data?.Backgrounds != null && hypLauncherNews?.Data != null)
                    hypLauncherNews.Data.Backgrounds = hypLauncherBackground.Data.Backgrounds;
                */
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

            ActionTimeoutTaskAwaitableCallback<HypLauncherSophonBranchesApi?> hypLauncherGameInfoCallback =
                innerToken =>
                    ApiResourceHttpClient
                       .GetFromCachedJsonAsync(launcherGameInfoUrl,
                                               HypApiJsonContext.Default.HypLauncherSophonBranchesApi,
                                               null,
                                               innerToken)
                       .ConfigureAwait(false);

            return hypLauncherGameInfoCallback
                  .WaitForRetryAsync(ExecutionTimeout,
                                     ExecutionTimeoutStep,
                                     ExecutionTimeoutAttempt,
                                     onTimeoutRoutine,
                                     result => LauncherGameSophonBranches = result,
                                     token);
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

        private void PerformDebugRoutines()
        {
            /*
#if DEBUG
            if (LauncherGameResourcePackage?.data?.game?.latest?.decompressed_path != null)
            {
                Logger.LogWriteLine($"Decompressed Path: {LauncherGameResourcePackage.data.game.latest.decompressed_path}",
                                    LogType.Default, true);
            }

            if (LauncherGameResourcePackage?.data?.game?.latest?.path != null)
            {
                Logger.LogWriteLine($"ZIP Path: {LauncherGameResourcePackage.data.game.latest.path}", LogType.Default, true);
            }

            if (LauncherGameResourcePackage?.data?.pre_download_game?.latest?.decompressed_path != null)
            {
                Logger.LogWriteLine($"Decompressed Path Pre-load: {LauncherGameResourcePackage.data.pre_download_game?.latest?.decompressed_path}",
                                    LogType.Default, true);
            }

            if (LauncherGameResourcePackage?.data?.sdk?.path != null)
            {
                Logger.LogWriteLine($"SDK found! Path: {LauncherGameResourcePackage.data.sdk.path}", LogType.Default, true);
            }

            if (LauncherGameResourcePackage?.data?.pre_download_game?.latest?.path != null)
            {
                Logger.LogWriteLine($"ZIP Path Pre-load: {LauncherGameResourcePackage.data.pre_download_game?.latest?.path}",
                                    LogType.Default, true);
            }
#endif

#if SIMULATEPRELOAD && !SIMULATEAPPLYPRELOAD
            if (LauncherGameResourcePackage != null && LauncherGameResourcePackage?.data?.pre_download_game == null)
            {
                Logger.LogWriteLine("[FetchLauncherDownloadInformation] SIMULATEPRELOAD: Simulating Pre-load!");
                RegionResourceVersion? simDataLatest = LauncherGameResourcePackage?.data?.game?.latest?.Copy();
                List<RegionResourceVersion>? simDataDiff = LauncherGameResourcePackage?.data?.game?.diffs?.Copy();

                if (simDataLatest == null)
                    return;

                simDataLatest.version = new GameVersion(simDataLatest.version).GetIncrementedVersion().ToString();

                LauncherGameResourcePackage!.data ??= new RegionResourceGame();
                LauncherGameResourcePackage.data.pre_download_game = new RegionResourceLatest() { latest = simDataLatest };

                if (simDataDiff == null || simDataDiff.Count == 0) return;
                foreach (RegionResourceVersion diff in simDataDiff)
                {
                    diff.version = new GameVersion(diff.version)
                        .GetIncrementedVersion()
                        .ToString();
                }
                LauncherGameResourcePackage.data.pre_download_game.diffs = simDataDiff;
            }
#endif
#if !SIMULATEPRELOAD && SIMULATEAPPLYPRELOAD
            if (LauncherGameResourcePackage?.data?.pre_download_game != null)
            {
                LauncherGameResourcePackage.data.game = LauncherGameResourcePackage.data.pre_download_game;
            }
#endif

#if DEBUG
            Logger.LogWriteLine("[LauncherApiBase::LoadLauncherResource] Loading game resource has been completed!",
                                LogType.Debug, true);
#endif
            */
        }
        #endregion
    }
}
