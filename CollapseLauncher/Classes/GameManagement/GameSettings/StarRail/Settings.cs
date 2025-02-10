using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.Interfaces;
// ReSharper disable CheckNamespace

namespace CollapseLauncher.GameSettings.StarRail
{
    internal class StarRailSettings : SettingsBase
    {
        public Model GraphicsSettings { get; set; }
        public BGMVolume AudioSettingsBgm { get; set; }
        public MasterVolume AudioSettingsMaster { get; set; }
        public SFXVolume AudioSettingsSfx { get; set; }
        public VOVolume AudioSettingsVo { get; set; }
        public LocalAudioLanguage AudioLanguage { get; set; }
        public LocalTextLanguage TextLanguage { get; set; }

        public StarRailSettings(IGameVersion gameVersionManager)
            : base(gameVersionManager)
        {
            // Initialize and Load Settings
            InitializeSettings();
        }

        public sealed override void InitializeSettings()
        {
            // Load Settings required for MainPage
            base.InitializeSettings();
            SettingsScreen         = PCResolution.Load();
        }

        public override void ReloadSettings()
        {
            // Load rest of the settings for GSP
            AudioSettingsBgm    = BGMVolume.Load();
            AudioSettingsMaster = MasterVolume.Load();
            AudioSettingsSfx    = SFXVolume.Load();
            AudioSettingsVo     = VOVolume.Load();
            AudioLanguage        = LocalAudioLanguage.Load();
            TextLanguage         = LocalTextLanguage.Load();
            GraphicsSettings     = Model.Load();
            InitializeSettings();
        } 

        #nullable enable
        public override void SaveSettings()
        {
            // Save Settings
            base.SaveSettings();
            GraphicsSettings?.Save();
            SettingsScreen?.Save();
            AudioSettingsBgm?.Save();
            AudioSettingsMaster?.Save();
            AudioSettingsSfx?.Save();
            AudioSettingsVo?.Save();
            AudioLanguage?.Save();
            TextLanguage?.Save();
        }

        public override IGameSettingsUniversal AsIGameSettingsUniversal() => this;
    }
}
