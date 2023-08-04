using CollapseLauncher.Interfaces;
using Microsoft.Win32;

namespace CollapseLauncher.GameSettings.Base
{
    internal class SettingsBase : ImportExportBase
    {
#nullable enable
        internal static string? RegistryPath;
        internal static RegistryKey? RegistryRoot;

        public SettingsBase(IGameVersionCheck GameVersionManager)
        {
            _gameVersionManager = GameVersionManager;
        }
#nullable disable
        protected static IGameVersionCheck _gameVersionManager { get; set; }
    }
}
