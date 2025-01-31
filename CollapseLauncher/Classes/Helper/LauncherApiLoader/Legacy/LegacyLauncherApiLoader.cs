using CollapseLauncher.Helper.Metadata;
// ReSharper disable IdentifierTypo
// ReSharper disable PartialTypeWithSinglePart

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.Legacy
{
    internal sealed partial class LegacyLauncherApiLoader : LauncherApiBase
    {
        private LegacyLauncherApiLoader(PresetConfig presetConfig, string gameName, string gameRegion)
            : base(presetConfig, gameName, gameRegion) { }

        public static ILauncherApi CreateApiInstance(PresetConfig presetConfig, string gameName, string gameRegion)
            => new LegacyLauncherApiLoader(presetConfig, gameName, gameRegion);
    }
}
