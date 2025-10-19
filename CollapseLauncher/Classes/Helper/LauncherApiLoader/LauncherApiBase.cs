using CollapseLauncher.Extension;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.LauncherApiLoader.Legacy;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.Plugin.Core.Management;
using Microsoft.Win32;
using System;
#if SIMULATEPRELOAD
using System.Collections.Generic;
#endif
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.SentryHelper;
using System.Net.Http;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Collections.Generic;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable VirtualMemberCallInConstructor
// ReSharper disable CommentTypo

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader
{
    public delegate Task OnLoadTaskAction(CancellationToken token);
    public delegate void ErrorLoadRoutineDelegate(Exception ex);

    internal partial class LauncherApiBase : ILauncherApi
    {
        public const int           ExecutionTimeout        = 10;
        public const int           ExecutionTimeoutStep    = 5;
        public const int           ExecutionTimeoutAttempt = 5;
        public       bool          IsPlugin => false;
        public       bool          IsLoadingCompleted { get; private set; }
        public       bool          IsForceRedirectToSophon { get; private set; }
        public       string?       GameBackgroundImg { get => LauncherGameNews?.Content?.Background?.BackgroundImg; }
        public       string?       GameBackgroundImgLocal { get; set; }
        public       string?       GameName { get; init; }
        public       string?       GameRegion { get; init; }
        protected    PresetConfig? PresetConfig { get; }

        public string? GameNameTranslation =>
            InnerLauncherConfig.GetGameTitleRegionTranslationString(GameName, Locale.Lang._GameClientTitles);

        public string? GameRegionTranslation =>
            InnerLauncherConfig.GetGameTitleRegionTranslationString(GameRegion, Locale.Lang._GameClientRegions);

        public virtual RegionResourceProp?       LauncherGameResource       { get; protected set; }
        public virtual HoYoPlayLauncherGameInfo? LauncherGameResourceSophon { get; protected set; }
        public virtual LauncherGameNews?         LauncherGameNews           { get; protected set; }
        public virtual HoYoPlayGameInfoField?    LauncherGameInfoField      { get; protected set; }
        public virtual HttpClient                ApiGeneralHttpClient       { get; protected set; }
        public virtual HttpClient                ApiResourceHttpClient      { get; protected set; }

        public void Dispose()
        {
            ApiGeneralHttpClient.Dispose();
            ApiResourceHttpClient.Dispose();
            GC.SuppressFinalize(this);
        }

        ~LauncherApiBase()
        {
            Dispose();
        }

        protected LauncherApiBase(PresetConfig presetConfig, string gameName, string gameRegion)
            : this(presetConfig, gameName, gameRegion, false) { }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        protected LauncherApiBase(PresetConfig presetConfig, string gameName, string gameRegion, bool isIgnoreBaseHttpClientInit)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
            PresetConfig = presetConfig;
            GameName     = gameName;
            GameRegion   = gameRegion;

            EnsurePresetConfigNotNull();

            if (isIgnoreBaseHttpClientInit)
            {
                return;
            }

            // Create generic HttpClientBuilder
            HttpClientBuilder apiGeneralHttpBuilder = new HttpClientBuilder()
                                                                         .UseLauncherConfig()
                                                                         .AllowUntrustedCert()
                                                                         .SetAllowedDecompression()
                                                                         .SetHttpVersion(HttpVersion.Version30);

            // Create resource HttpClientBuilder
            HttpClientBuilder apiResourceHttpBuilder = new HttpClientBuilder()
                                                                          .UseLauncherConfig()
                                                                          .AllowUntrustedCert()
                                                                          .SetAllowedDecompression(DecompressionMethods.None)
                                                                          .SetHttpVersion(HttpVersion.Version30);

            // If the metadata has user-agent defined, set the resource's HttpClient user-agent
            if (!string.IsNullOrEmpty(presetConfig.ApiGeneralUserAgent))
            {
                apiGeneralHttpBuilder.SetUserAgent(presetConfig.ApiGeneralUserAgent);
            }
            if (!string.IsNullOrEmpty(presetConfig.ApiResourceUserAgent))
            {
                apiResourceHttpBuilder.SetUserAgent(string.Format(presetConfig.ApiResourceUserAgent, InnerLauncherConfig.m_isWindows11 ? "11" : "10"));
            }

            // Add other API general and resource headers from the metadata configuration
            presetConfig.AddApiGeneralAdditionalHeaders((key,  value) => apiGeneralHttpBuilder.AddHeader(key, value));
            presetConfig.AddApiResourceAdditionalHeaders((key, value) => apiResourceHttpBuilder.AddHeader(key, value));

            // Create HttpClient instances for both General and Resource APIs.
            ApiGeneralHttpClient  = apiGeneralHttpBuilder.Create();
            ApiResourceHttpClient = apiResourceHttpBuilder.Create();
        }

        public async Task<bool> LoadAsync(OnLoadTaskAction?         beforeLoadRoutine,
                                          OnLoadTaskAction?         afterLoadRoutine,
                                          ActionOnTimeOutRetry?     onTimeoutRoutine,
                                          ErrorLoadRoutineDelegate? errorLoadRoutine,
                                          CancellationToken         token)
        {
            _ = beforeLoadRoutine?.Invoke(token) ?? Task.CompletedTask;

            try
            {
                IsLoadingCompleted = false;
                await LoadAsyncInner(onTimeoutRoutine, token);
                await (afterLoadRoutine?.Invoke(token) ?? Task.CompletedTask);

                return true;
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                errorLoadRoutine?.Invoke(ex);
                return false;
            }
            finally
            {
                IsLoadingCompleted = true;
            }
        }

        protected virtual async Task LoadAsyncInner(ActionOnTimeOutRetry? onTimeoutRoutine,
                                                    CancellationToken     token)
        {
            // 2025-05-05: As per now, the Sophon resource information requires to be fetched first.
            //             This is mandatory due to latest Genshin Impact changes which removes zip
            //             packages and also version infos.
            await Task.WhenAll(LoadLauncherGameResourceSophon(onTimeoutRoutine, token),
                               LoadLauncherGameResource(onTimeoutRoutine, token),
                               LoadLauncherNews(onTimeoutRoutine, token),
                               LoadLauncherGameInfo(onTimeoutRoutine, token));

            InitializeFakeVersionInfo();
            PerformDebugRoutines();
        }

        protected virtual void InitializeFakeVersionInfo()
        {
            if (LauncherGameResource?.data == null)
            {
                return;
            }

            HoYoPlayGameInfoBranch? gameBranch = LauncherGameResourceSophon?.GameInfoData?.GameBranchesInfo?
               .FirstOrDefault(x => x.GameInfo?.BizName?.Equals(PresetConfig?.LauncherBizName) ?? false);

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

            var branchPreloadField = gameBranch.GamePreloadField;
            var branchBaseField    = gameBranch.GameMainField;

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

        protected virtual void AddFakeVersionInfo(HoYoPlayGameInfoBranchField branchField, RegionResourceLatest region)
        {
            region.latest ??= new RegionResourceVersion();
            region.latest.version = branchField.Tag;

            region.diffs ??= [];

            HashSet<string> existingDiffsVer = new(region.diffs.Select(x => x.version)!);
            foreach (var versionTags in (branchField.DiffTags ?? []).Where(x => !existingDiffsVer.Contains(x)))
            {
                region.diffs.Add(new RegionResourceVersion
                {
                    version = versionTags
                });
            }
        }

        protected virtual async Task LoadLauncherGameResourceSophon(ActionOnTimeOutRetry? onTimeoutRoutine,
                                                                    CancellationToken     token)
        {
            EnsurePresetConfigNotNull();

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

            ActionTimeoutTaskAwaitableCallback<HoYoPlayLauncherGameInfo?> launcherSophonBranchCallback =
                innerToken =>
                    ApiGeneralHttpClient
                       .GetFromCachedJsonAsync(PresetConfig.LauncherResourceChunksURL?.BranchUrl,
                                               HoYoPlayLauncherGameInfoJsonContext.Default.HoYoPlayLauncherGameInfo,
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

        protected virtual Task LoadLauncherGameResource(ActionOnTimeOutRetry? onTimeoutRoutine,
                                                        CancellationToken token)
        {
            EnsurePresetConfigNotNull();
            EnsureResourceUrlNotNull();

            ActionTimeoutTaskCallback<RegionResourceProp?> launcherGameResourceCallback =
                innerToken =>
                    ApiGeneralHttpClient.GetFromJsonAsync(PresetConfig?.LauncherResourceURL,
                                                          RegionResourcePropJsonContext.Default.RegionResourceProp,
                                                          innerToken);

            Task[] tasks = [
                launcherGameResourceCallback
                    .WaitForRetryAsync(ExecutionTimeout,
                                       ExecutionTimeoutStep,
                                       ExecutionTimeoutAttempt,
                                       onTimeoutRoutine,
                                       token)
                    .ContinueWith(async result => LauncherGameResource = await result, token),
                Task.CompletedTask
                ];

            RegionResourceProp? pluginProp = null;
            if (!string.IsNullOrEmpty(PresetConfig?.LauncherPluginURL))
            {
                return Task.WhenAll(tasks)
                           .ContinueWith(AfterExecute, token);
            }

            ActionTimeoutTaskCallback<RegionResourceProp?> launcherPluginPropCallback =
                innerToken =>
                    ApiGeneralHttpClient.GetFromJsonAsync(string.Format(PresetConfig?.LauncherPluginURL!,
                                                                        GetDeviceId(PresetConfig!)),
                                                          RegionResourcePropJsonContext.Default.RegionResourceProp,
                                                          innerToken);

            tasks[1] = launcherPluginPropCallback
                      .WaitForRetryAsync(ExecutionTimeout,
                                         ExecutionTimeoutStep,
                                         ExecutionTimeoutAttempt,
                                         onTimeoutRoutine,
                                         token)
                      .ContinueWith(async result => pluginProp = await result, token);

            return Task.WhenAll(tasks)
                       .ContinueWith(AfterExecute, token);

            void AfterExecute(Task action)
            {
                if (LauncherGameResource == null)
                {
                    throw new NullReferenceException("Launcher game resource returns a null!");
                }

                if (pluginProp != null && LauncherGameResource.data != null)
                {
                    LauncherGameResource.data.plugins = pluginProp.data?.plugins;
#if DEBUG
                    Logger.LogWriteLine("[LauncherApiBase::LoadLauncherGameResource] Loading plugin handle!",
                                        LogType.Debug, true);
#endif
                }
            }
        }

        protected virtual void PerformDebugRoutines()
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
            Logger.LogWriteLine("[LauncherApiBase::LoadLauncherGameResource] Loading game resource has been completed!",
                                LogType.Debug, true);
#endif
        }

        protected void EnsurePresetConfigNotNull()
        {
            if (PresetConfig == null)
            {
                throw new NullReferenceException("Preset config is null!");
            }
        }

        protected void EnsureResourceUrlNotNull()
        {
            if (string.IsNullOrEmpty(PresetConfig?.LauncherResourceURL))
            {
                throw new NullReferenceException("Launcher resource URL is null or empty!");
            }
        }

        protected virtual async Task LoadLauncherNews(ActionOnTimeOutRetry? onTimeoutRoutine,
                                                      CancellationToken     token)
        {
            EnsurePresetConfigNotNull();

            string localeLang     = Locale.Lang.LanguageID.ToLower();
            bool   isMultilingual = PresetConfig!.LauncherSpriteURLMultiLang ?? false;
            
            // WORKAROUND: Certain language is not supported by the API and will return null/empty response.
            // Use other locale to prevent crashes/empty background image
            localeLang = localeLang switch
                         {
                             "es-419" => "es-es",
                             "pt-br" => "pt-pt",
                             _ => localeLang
                         };

            LauncherGameNews? regionResourceProp =
                await LoadLauncherNewsInner(isMultilingual, localeLang, PresetConfig, onTimeoutRoutine, token);
            int backgroundVersion = regionResourceProp?.Content?.Background?.BackgroundVersion ?? 5;

            // NOTE: Check if background is null (not supported by API), if it is then reload the news with fallback language (en-us).
            if (regionResourceProp?.Content?.Background == null || (backgroundVersion <= 4 && PresetConfig.GameType == GameNameType.Honkai))
            {
                Logger.LogWriteLine("[LauncherApiBase::LoadLauncherNews()] Using en-us fallback for game news!", LogType.Warning, true);
                string localeFallback = PresetConfig.LauncherSpriteURLMultiLangFallback ?? "en-us";
                regionResourceProp = await LoadLauncherNewsInner(true, localeFallback, PresetConfig, onTimeoutRoutine, token);
            }

            regionResourceProp?.Content?.InjectDownloadableItemCancelToken(this, token);
            LauncherGameNews = regionResourceProp;
        }


#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected virtual async Task LoadLauncherGameInfo(ActionOnTimeOutRetry? onTimeoutRoutine,
                                                          CancellationToken     token)
        {
            LauncherGameInfoField = new HoYoPlayGameInfoField();
        }
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

        private ConfiguredTaskAwaitable<LauncherGameNews?>
            LoadLauncherNewsInner(bool                  isMultiLang,
                                  string                lang,
                                  PresetConfig          presetConfig,
                                  ActionOnTimeOutRetry? onTimeoutRoutine,
                                  CancellationToken     token)
        {
            if (string.IsNullOrEmpty(presetConfig.LauncherSpriteURL))
            {
                throw new NullReferenceException("Launcher news URL is null or empty!");
            }

            ActionTimeoutTaskCallback<LauncherGameNews?> taskGameLauncherNewsSophonCallback =
                innerToken =>
                    isMultiLang
                        ? LoadMultiLangLauncherNews(presetConfig.LauncherSpriteURL, lang, innerToken)
                        : LoadSingleLangLauncherNews(presetConfig.LauncherSpriteURL, innerToken);

            return taskGameLauncherNewsSophonCallback.WaitForRetryAsync(ExecutionTimeout,
                                                                        ExecutionTimeoutStep,
                                                                        ExecutionTimeoutAttempt,
                                                                        onTimeoutRoutine,
                                                                        token)
                                                     .ConfigureAwait(false);
        }

        private Task<LauncherGameNews?>
            LoadSingleLangLauncherNews(string            launcherSpriteUrl,
                                       CancellationToken token)
        {
            return ApiResourceHttpClient.GetFromJsonAsync(launcherSpriteUrl,
                                                          LauncherGameNewsJsonContext.Default.LauncherGameNews,
                                                          token);
        }

        private Task<LauncherGameNews?>
            LoadMultiLangLauncherNews(string            launcherSpriteUrl,
                                      string            lang,
                                      CancellationToken token)
        {
            return ApiResourceHttpClient.GetFromJsonAsync(string.Format(launcherSpriteUrl, lang),
                                                          LauncherGameNewsJsonContext.Default.LauncherGameNews,
                                                          token);
        }

        protected virtual string GetDeviceId(PresetConfig preset)
        {
            if (string.IsNullOrEmpty(preset.InstallRegistryLocation))
                throw new NullReferenceException("InstallRegistryLocation inside of the game metadata is null!");

            string? deviceId = (string?)Registry.GetValue(preset.InstallRegistryLocation, "UUID", null);
            if (deviceId != null)
                return deviceId;

            const string regKeyCryptography = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography";
            string guid = (string?)Registry.GetValue(regKeyCryptography, "MachineGuid", null) ??
                           Guid.NewGuid().ToString();
            deviceId = guid.Replace("-", "") + (long)DateTime.Now.Subtract(DateTime.UnixEpoch).TotalMilliseconds;
            Registry.SetValue(preset.InstallRegistryLocation, "UUID", deviceId);
            return deviceId;
        }
    }
}
