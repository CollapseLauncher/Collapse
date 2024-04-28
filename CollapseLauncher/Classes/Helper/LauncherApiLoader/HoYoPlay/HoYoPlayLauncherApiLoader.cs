using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Metadata;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.Sophon
{
    internal class HoYoPlayLauncherApiLoader : LauncherApiBase, ILauncherApi
    {
        private HoYoPlayLauncherApiLoader(PresetConfig presetConfig, string gameName, string gameRegion)
            : base(presetConfig, gameName, gameRegion) { }

        public static ILauncherApi CreateApiInstance(PresetConfig presetConfig, string gameName, string gameRegion)
            => new HoYoPlayLauncherApiLoader(presetConfig, gameName, gameRegion);

        protected override async Task LoadLauncherGameResource(ActionOnTimeOutRetry? onTimeoutRoutine, CancellationToken token)
        {
            // TODO: HoYoPlay API reading and conversion into Sophon format
            await base.LoadLauncherGameResource(onTimeoutRoutine, token);
        }

        protected override async ValueTask LoadLauncherNews(ActionOnTimeOutRetry? onTimeoutRoutine, CancellationToken token)
        {
            // TODO: HoYoPlay API reading and conversion into Sophon format
            await base.LoadLauncherNews(onTimeoutRoutine, token);
        }
    }
}
