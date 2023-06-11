using CollapseLauncher.Interfaces;

namespace CollapseLauncher.Statics
{
    internal partial class PageStatics
    {
        internal static IGameSettings _GameSettings { get; set; }
        internal static IRepair _GameRepair { get; set; }
        internal static ICache _GameCache { get; set; }
        internal static IGameVersionCheck _GameVersion { get; set; }
        internal static IGameInstallManager _GameInstall { get; set; }
        internal static CommunityToolsProperty _CommunityToolsProperty { get; set; }
    }
}
