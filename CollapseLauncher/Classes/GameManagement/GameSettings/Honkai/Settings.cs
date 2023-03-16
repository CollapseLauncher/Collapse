using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Honkai.Context;
using CollapseLauncher.GameSettings.Universal;
using CollapseLauncher.Interfaces;
using Hi3Helper.Preset;
using Microsoft.Win32;
using System.IO;
using static CollapseLauncher.GameSettings.Statics;

namespace CollapseLauncher.GameSettings.Honkai
{
    internal class HonkaiSettings : ImportExportBase, IGameSettings, IGameSettingsUniversal
    {
        #region PresetProperties
        public Preset<PersonalGraphicsSettingV2, D_PersonalGraphicsSettingV2Context> Preset_SettingsGraphics { get; set; }
        #endregion

        #region SettingProperties
        public CustomArgs SettingsCustomArgument { get; set; }
        public PersonalGraphicsSettingV2 SettingsGraphics { get; set; }
        public PersonalAudioSetting SettingsAudio { get; set; }
        public BaseScreenSettingData SettingsScreen { get; set; }
        public CollapseScreenSetting SettingsCollapseScreen { get; set; }
        #endregion

        public HonkaiSettings(PresetConfigV2 gameConfig)
        {
            // Init Root Registry Key
            RegistryPath = Path.Combine(RegistryRootPath, gameConfig.InternalGameNameInConfig);
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
            SettingsGraphics = PersonalGraphicsSettingV2.Load();
            SettingsAudio = PersonalAudioSetting.Load();
            SettingsCustomArgument = CustomArgs.Load();
            SettingsScreen = ScreenSettingData.Load();
            SettingsCollapseScreen = CollapseScreenSetting.Load();

            // Load Preset
            Preset_SettingsGraphics = Preset<PersonalGraphicsSettingV2, D_PersonalGraphicsSettingV2Context>.LoadPreset(GameType.Honkai, D_PersonalGraphicsSettingV2Context.Default);
        }

        public void RevertSettings() => InitializeSettings();

        public void SaveSettings()
        {
            // Save Settings
            SettingsGraphics.Save();
            SettingsAudio.Save();
            SettingsCustomArgument.Save();
            SettingsScreen.Save();
            SettingsCollapseScreen.Save();

            // Save Preset
            Preset_SettingsGraphics.SaveChanges();
        }

        public IGameSettingsUniversal AsIGameSettingsUniversal() => this;
    }
}
