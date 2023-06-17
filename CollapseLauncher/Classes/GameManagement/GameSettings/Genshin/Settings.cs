using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Genshin;
using CollapseLauncher.GameSettings.Universal;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Statics;
using Microsoft.Win32;
using System.IO;
using static CollapseLauncher.GameSettings.Statics;

namespace CollapseLauncher.GameSettings.Genshin
{
    internal class GenshinSettings : ImportExportBase, IGameSettings, IGameSettingsUniversal
    {
        public CustomArgs SettingsCustomArgument { get; set; }
        public BaseScreenSettingData SettingsScreen { get; set; }
        public CollapseScreenSetting SettingsCollapseScreen { get; set; }
        public ScreenManager SettingsScreenManager { get; set; }

        public GenshinSettings()
        {
            // Init Root Registry Key
            RegistryPath = Path.Combine(RegistryRootPath, PageStatics._GameVersion.GamePreset.InternalGameNameInConfig);
            RegistryRoot = Registry.CurrentUser.OpenSubKey(RegistryPath, true);

            // If the Root Registry Key is null (not exist), then create a new one.
            if (RegistryRoot == null)
            {
                RegistryRoot = Registry.CurrentUser.CreateSubKey(RegistryPath, true, RegistryOptions.None);
            }

            // Initialize and Load Settings
            InitializeSettings();
        }

        private void InitializeSettings()
        {
            // Load Settings
            SettingsCustomArgument = CustomArgs.Load();
            SettingsCollapseScreen = CollapseScreenSetting.Load();
            SettingsScreenManager = ScreenManager.Load();
        }

        public void RevertSettings() => InitializeSettings();

        public void SaveSettings()
        {
            // Save Settings
            SettingsCustomArgument.Save();
            SettingsCollapseScreen.Save();
            SettingsScreenManager.Save();
        }

        public IGameSettingsUniversal AsIGameSettingsUniversal() => this;
    }
}
