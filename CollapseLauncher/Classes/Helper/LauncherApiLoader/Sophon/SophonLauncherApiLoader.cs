using CollapseLauncher.Helper.Metadata;
// ReSharper disable IdentifierTypo
// ReSharper disable PartialTypeWithSinglePart

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.Sophon
{
    internal sealed partial class SophonLauncherApiLoader : LauncherApiBase
    {
        private SophonLauncherApiLoader(PresetConfig presetConfig, string gameName, string gameRegion)
            : base(presetConfig, gameName, gameRegion) { }

        public static ILauncherApi CreateApiInstance(PresetConfig presetConfig, string gameName, string gameRegion)
            => new SophonLauncherApiLoader(presetConfig, gameName, gameRegion);
    }
}
