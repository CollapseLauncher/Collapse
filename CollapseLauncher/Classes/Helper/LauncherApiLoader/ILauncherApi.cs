using CollapseLauncher.Extension;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.LauncherApiLoader.Sophon;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader
{
    public interface ILauncherApi : IDisposable
    {
        bool IsLoadingCompleted { get; }
        string? GameBackgroundImg { get; }
        string? GameBackgroundImgLocal { get; set; }
        string? GameName { get; }
        string? GameRegion { get; }
        string? GameNameTranslation { get; }
        string? GameRegionTranslation { get; }
        HoYoPlayGameInfoField? LauncherGameInfoField { get; }
        RegionResourceProp? LauncherGameResource { get; }
        LauncherGameNews? LauncherGameNews { get; }
        HttpClient? ApiGeneralHttpClient { get; }
        HttpClient? ApiResourceHttpClient { get; }
        Task<bool> LoadAsync(OnLoadAction? beforeLoadRoutine = null, OnLoadAction? afterLoadRoutine = null,
            ActionOnTimeOutRetry? onTimeoutRoutine = null, ErrorLoadRoutineDelegate? errorLoadRoutine = null,
            CancellationToken token = default);
    }
}
