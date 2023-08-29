using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Universal;
using CollapseLauncher.Interfaces;
using Microsoft.Win32;
using System.IO;

namespace CollapseLauncher.GameSettings.Genshin
{
    internal class GenshinSettings : SettingsBase, IGameSettings, IGameSettingsUniversal
    {
        public CustomArgs SettingsCustomArgument { get; set; }
        public BaseScreenSettingData SettingsScreen { get; set; }
        public CollapseScreenSetting SettingsCollapseScreen { get; set; }
        public GeneralData SettingsGeneralData { get; set; }

        public GenshinSettings(IGameVersionCheck GameVersionManager)
            : base(GameVersionManager)
        {
            // Init Root Registry Key
            RegistryPath = Path.Combine($"Software\\{_gameVersionManager.VendorTypeProp.VendorType}", _gameVersionManager.GamePreset.InternalGameNameInConfig);
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
            SettingsScreen = ScreenManager.Load();
            
        }

        public void ReloadSettings()
        {
            SettingsGeneralData = GeneralData.Load();
            InitializeSettings();
        }

        public void SaveSettings()
        {
            // Save Settings
            SettingsCustomArgument.Save();
            SettingsCollapseScreen.Save();
            SettingsScreen.Save();
            SettingsGeneralData.Save();
        }

        public IGameSettingsUniversal AsIGameSettingsUniversal() => this;
    }
}
