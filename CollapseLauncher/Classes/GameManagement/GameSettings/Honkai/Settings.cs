﻿using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Honkai.Context;
using CollapseLauncher.GameSettings.Universal;
using CollapseLauncher.Interfaces;
using Hi3Helper.Preset;
using Microsoft.Win32;
using System.IO;
// ReSharper disable PossibleNullReferenceException

namespace CollapseLauncher.GameSettings.Honkai
{
    internal class HonkaiSettings : SettingsBase, IGameSettings, IGameSettingsUniversal
    {
        #region PresetProperties
        public Preset<PersonalGraphicsSettingV2, HonkaiSettingsJSONContext> Preset_SettingsGraphics { get; set; }
        #endregion

        #region SettingProperties
        public CustomArgs                SettingsCustomArgument { get; set; }
        public PersonalGraphicsSettingV2 SettingsGraphics       { get; set; }
        public PersonalAudioSetting      SettingsAudio          { get; set; }
        public PhysicsSimulation         SettingsPhysics        { get; set; }
        public GraphicsGrade             SettingsGraphicsGrade  { get; set; }
        public BaseScreenSettingData     SettingsScreen         { get; set; }
        public CollapseScreenSetting     SettingsCollapseScreen { get; set; }
        public CollapseMiscSetting       SettingsCollapseMisc   { get; set; }
        #endregion

        public HonkaiSettings(IGameVersionCheck GameVersionManager)
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
            SettingsGraphics       = PersonalGraphicsSettingV2.Load();
            SettingsGraphicsGrade  = GraphicsGrade.Load();
            SettingsPhysics        = PhysicsSimulation.Load();
            SettingsAudio          = PersonalAudioSetting.Load();
            SettingsCustomArgument = CustomArgs.Load();
            SettingsScreen         = ScreenSettingData.Load();
            SettingsCollapseScreen = CollapseScreenSetting.Load();
            SettingsCollapseMisc   = CollapseMiscSetting.Load();

            // Load Preset
            Preset_SettingsGraphics = Preset<PersonalGraphicsSettingV2, HonkaiSettingsJSONContext>.LoadPreset(GameType.Honkai, HonkaiSettingsJSONContext.Default);
        }

        public void ReloadSettings() => InitializeSettings();

        public void SaveSettings()
        {
            // Save Settings
            SettingsGraphics.Save();
            SettingsPhysics.Save();
            SettingsGraphicsGrade.Save();
            SettingsAudio.Save();
            SettingsCustomArgument.Save();
            SettingsScreen.Save();
            SettingsCollapseScreen.Save();
            SettingsCollapseMisc.Save();

            // Save Preset
            Preset_SettingsGraphics.SaveChanges();
        }

        public IGameSettingsUniversal AsIGameSettingsUniversal() => this;
    }
}
