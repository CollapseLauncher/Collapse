using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Universal;
using CollapseLauncher.Interfaces;
using Microsoft.Win32;
using System.IO;

namespace CollapseLauncher.GameSettings.Genshin
{
    internal class GenshinSettings : SettingsBase, IGameSettings, IGameSettingsUniversal
    {
        public CustomArgs            SettingsCustomArgument   { get; set; }
        public BaseScreenSettingData SettingsScreen           { get; set; }
        public CollapseScreenSetting SettingsCollapseScreen   { get; set; }
        public CollapseMiscSetting   SettingsCollapseMisc     { get; set; }
        public GeneralData           SettingsGeneralData      { get; set; }
        public VisibleBackground     SettingVisibleBackground { get; set; }
        public WindowsHDR            SettingsWindowsHDR       { get; set; }

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
            SettingsCustomArgument   = CustomArgs.Load();
            SettingsCollapseScreen   = CollapseScreenSetting.Load();
            SettingsCollapseMisc     = CollapseMiscSetting.Load();
            SettingsScreen           = ScreenManager.Load();
            SettingVisibleBackground = VisibleBackground.Load();
            SettingsWindowsHDR       = WindowsHDR.Load();
        }

        public void ReloadSettings()
        {
            // To ease up resource and prevent bad JSON locking up launcher
            SettingsGeneralData = GeneralData.Load();
            InitializeSettings();
        } 

        public void SaveSettings()
        {
            // Save Settings
            SettingsCustomArgument.Save();
            SettingsCollapseScreen.Save();
            SettingsCollapseMisc.Save();
            SettingsScreen.Save();
            SettingsGeneralData.Save();
            SettingVisibleBackground.Save();
            SettingsWindowsHDR.Save();
        }

        public IGameSettingsUniversal AsIGameSettingsUniversal() => this;
    }
}
