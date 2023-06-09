using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Universal;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Statics;
using Microsoft.Win32;
using System.IO;
using static CollapseLauncher.GameSettings.Statics;

namespace CollapseLauncher.GameSettings.StarRail
{
    internal class StarRailSettings : ImportExportBase, IGameSettings, IGameSettingsUniversal
    {
        public CustomArgs SettingsCustomArgument { get; set; }
        public BaseScreenSettingData SettingsScreen { get; set; }
        public CollapseScreenSetting SettingsCollapseScreen { get; set; }
        public Model GraphicsSettings { get; set; }
        public BGMVolume AudioSettings_BGM { get; set; }
        public MasterVolume AudioSettings_Master { get; set; }
        public SFXVolume AudioSettings_SFX { get; set; }
        public VOVolume AudioSettings_VO { get; set; }
        public LocalAudioLanguage AudioLanguage { get; set; } 
        public LocalTextLanguage TextLanguage { get; set; }

        public StarRailSettings()
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
            GraphicsSettings = Model.Load();
            SettingsCollapseScreen = CollapseScreenSetting.Load();
            SettingsScreen = PCResolution.Load();
            AudioSettings_BGM = BGMVolume.Load();
            AudioSettings_Master = MasterVolume.Load();
            AudioSettings_SFX = SFXVolume.Load();
            AudioSettings_VO = VOVolume.Load();
            AudioLanguage = LocalAudioLanguage.Load();
            TextLanguage = LocalTextLanguage.Load();
        }

        public void RevertSettings() => InitializeSettings();

        public void SaveSettings()
        {
            // Save Settings
            SettingsCustomArgument.Save();
            GraphicsSettings.Save();
            SettingsCollapseScreen.Save();
            SettingsScreen.Save();
            AudioSettings_BGM.Save();
            AudioSettings_Master.Save();
            AudioSettings_SFX.Save();
            AudioSettings_VO.Save();
            AudioLanguage.Save();
            TextLanguage.Save();
        }

        public IGameSettingsUniversal AsIGameSettingsUniversal() => this;
    }
}
