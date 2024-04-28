using CollapseLauncher.Extension;
using CollapseLauncher.Helper.LauncherApiLoader.Sophon;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader
{
    internal interface ILauncherApi
    {
        bool IsLoadingCompleted { get; }
        string? GameBackgroundImg { get; }
        string? GameBackgroundImgLocal { get; set; }
        string? GameName { get; }
        string? GameRegion { get; }
        string? GameNameTranslation { get; }
        string? GameRegionTranslation { get; }
        RegionResourceProp? LauncherGameResource { get; }
        LauncherGameNews? LauncherGameNews { get; }
        Task LoadAsync(OnLoadAction? beforeLoadRoutine = null, OnLoadAction? afterLoadRoutine = null,
            ActionOnTimeOutRetry? onTimeoutRoutine = null, ErrorLoadRoutineDelegate? errorLoadRoutine = null,
            CancellationToken token = default);
    }
}
