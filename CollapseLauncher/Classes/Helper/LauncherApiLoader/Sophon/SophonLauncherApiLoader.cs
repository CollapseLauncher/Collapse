using CollapseLauncher.Helper.Metadata;

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.Sophon
{
    internal class SophonLauncherApiLoader : LauncherApiBase, ILauncherApi
    {
        private SophonLauncherApiLoader(PresetConfig presetConfig, string gameName, string gameRegion)
            : base(presetConfig, gameName, gameRegion) { }

        public static ILauncherApi CreateApiInstance(PresetConfig presetConfig, string gameName, string gameRegion)
            => new SophonLauncherApiLoader(presetConfig, gameName, gameRegion);
    }
}
