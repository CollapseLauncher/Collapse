using CollapseLauncher.Extension;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.Metadata;
using Google.Protobuf.WellKnownTypes;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using System;
#if SIMULATEPRELOAD
using System.Collections.Generic;
#endif
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable VirtualMemberCallInConstructor
// ReSharper disable CommentTypo

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader
{
    public delegate void ErrorLoadRoutineDelegate(Exception ex);

    internal abstract partial class LauncherApiBase(
        PresetConfig                    presetConfig,
        string                          gameName,
        string                          gameRegion,
        Func<PresetConfig, HttpClient> apiGeneralHttpClientFactory,
        Func<PresetConfig, HttpClient> apiResourceHttpClientFactory)
        : ILauncherApi
    {
        public const int           ExecutionTimeout        = 10;
        public const int           ExecutionTimeoutStep    = 5;
        public const int           ExecutionTimeoutAttempt = 5;
        public       bool          IsPlugin                => false;
        public       bool          IsLoadingCompleted      { get; private set; }
        public       bool          IsForceRedirectToSophon { get; protected set; }
        public       string?       GameBackgroundImg       { get => LauncherGameBackground?.Data?.BackgroundImageUrl; }
        public       string?       GameBackgroundImgLocal  { get; set; }
        public       string?       GameName                { get; } = gameName;
        public       string?       GameRegion              { get; } = gameRegion;
        protected    PresetConfig? PresetConfig            { get; } = presetConfig;

        public string? GameNameTranslation =>
            InnerLauncherConfig.GetGameTitleRegionTranslationString(GameName, Locale.Lang._GameClientTitles);

        public string? GameRegionTranslation =>
            InnerLauncherConfig.GetGameTitleRegionTranslationString(GameRegion, Locale.Lang._GameClientRegions);

        public RegionResourceProp?        LauncherGameResource       { get; protected set; }
        public HypLauncherGameInfoApi?    LauncherGameResourceSophon { get; protected set; }
        public HypLauncherResourceWpfApi? LauncherGameResourceWpf    { get; protected set; }
        public HypLauncherBackgroundApi?  LauncherGameBackground     { get; protected set; }
        public HypLauncherContentApi?     LauncherGameContent        { get; protected set; }
        public HypGameInfoData?           LauncherGameInfoField      { get; protected set; }

        public HttpClient ApiGeneralHttpClient  { get; } = apiGeneralHttpClientFactory(presetConfig);
        public HttpClient ApiResourceHttpClient { get; } = apiResourceHttpClientFactory(presetConfig);

        public void Dispose()
        {
            ApiGeneralHttpClient.Dispose();
            ApiResourceHttpClient.Dispose();
            GC.SuppressFinalize(this);
        }

        public async ValueTask<bool> LoadAsync(Func<CancellationToken, ValueTask>? beforeLoadRoutineAsync,
                                               Func<CancellationToken, ValueTask>? afterLoadRoutineAsync,
                                               ActionOnTimeOutRetry?               onTimeoutRoutine,
                                               Action<Exception>?                  errorLoadRoutine,
                                               CancellationToken                   token)
        {
            try
            {
                IsLoadingCompleted = false;
                await (beforeLoadRoutineAsync?.Invoke(token) ?? ValueTask.CompletedTask);

                await LoadAsyncInner(onTimeoutRoutine, token);
                await (afterLoadRoutineAsync?.Invoke(token) ?? ValueTask.CompletedTask);

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

        protected abstract Task LoadAsyncInner(
            ActionOnTimeOutRetry? onTimeoutRoutine,
            CancellationToken     token);
    }
}
