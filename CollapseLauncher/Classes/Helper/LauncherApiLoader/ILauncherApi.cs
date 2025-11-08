using CollapseLauncher.Extension;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable UnusedMemberInSuper.Global
// ReSharper disable IdentifierTypo

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader
{
    public interface ILauncherApi : IDisposable
    {
        bool                       IsLoadingCompleted         { get; }
        bool                       IsForceRedirectToSophon    { get; }
        string?                    GameBackgroundImg          { get; }
        string?                    GameBackgroundImgLocal     { get; set; }
        string?                    GameName                   { get; }
        string?                    GameRegion                 { get; }
        string?                    GameNameTranslation        { get; }
        string?                    GameRegionTranslation      { get; }
        RegionResourceProp?        LauncherGameResource       { get; }
        HypLauncherGameInfoApi?    LauncherGameResourceSophon { get; }
        HypLauncherBackgroundApi?  LauncherGameBackground     { get; }
        HypLauncherContentApi?     LauncherGameContent        { get; }
        HypLauncherResourceWpfApi? LauncherGameResourceWpf    { get; }
        HypGameInfoData?           LauncherGameInfoField      { get; }
        HttpClient?                ApiGeneralHttpClient       { get; }
        HttpClient?                ApiResourceHttpClient      { get; }
        bool                       IsPlugin                   { get; }
        ValueTask<bool> LoadAsync(Func<CancellationToken, ValueTask>? beforeLoadRoutineAsync = null,
                                  Func<CancellationToken, ValueTask>? afterLoadRoutineAsync  = null,
                                  ActionOnTimeOutRetry?               onTimeoutRoutine       = null,
                                  Action<Exception>?                  errorLoadRoutine       = null,
                                  CancellationToken                   token                  = default);
    }
}
