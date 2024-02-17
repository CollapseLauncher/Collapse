﻿using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Universal;
using CollapseLauncher.Interfaces;
using Microsoft.Win32;
using System.IO;

namespace CollapseLauncher.GameSettings.StarRail
{
    internal class StarRailSettings : SettingsBase, IGameSettings, IGameSettingsUniversal
    {
        public CustomArgs SettingsCustomArgument { get; set; }
        public BaseScreenSettingData SettingsScreen { get; set; }
        public CollapseScreenSetting SettingsCollapseScreen { get; set; }
        public CollapseMiscSetting SettingsCollapseMisc { get; set; }
        public Model GraphicsSettings { get; set; }
        public BGMVolume AudioSettings_BGM { get; set; }
        public MasterVolume AudioSettings_Master { get; set; }
        public SFXVolume AudioSettings_SFX { get; set; }
        public VOVolume AudioSettings_VO { get; set; }
        public LocalAudioLanguage AudioLanguage { get; set; }
        public LocalTextLanguage TextLanguage { get; set; }

        public StarRailSettings(IGameVersionCheck GameVersionManager)
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
            // Load Settings required for MainPage
            SettingsCustomArgument = CustomArgs.Load();
            SettingsCollapseScreen = CollapseScreenSetting.Load();
            SettingsCollapseMisc   = CollapseMiscSetting.Load();
            SettingsScreen         = PCResolution.Load();
            
        }

        public void ReloadSettings()
        {
            // Load rest of the settings for GSP
            AudioSettings_BGM    = BGMVolume.Load();
            AudioSettings_Master = MasterVolume.Load();
            AudioSettings_SFX    = SFXVolume.Load();
            AudioSettings_VO     = VOVolume.Load();
            AudioLanguage        = LocalAudioLanguage.Load();
            TextLanguage         = LocalTextLanguage.Load();
            GraphicsSettings     = Model.Load();
            InitializeSettings();
        } 

        public void SaveSettings()
        {
            // Save Settings
            SettingsCustomArgument.Save();
            GraphicsSettings.Save();
            SettingsCollapseScreen.Save();
            SettingsCollapseMisc.Save();
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
