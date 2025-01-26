using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.Interfaces;
// ReSharper disable CheckNamespace

namespace CollapseLauncher.GameSettings.StarRail
{
    internal class StarRailSettings : SettingsBase
    {
        public Model GraphicsSettings { get; set; }
        public BGMVolume AudioSettings_BGM { get; set; }
        public MasterVolume AudioSettings_Master { get; set; }
        public SFXVolume AudioSettings_SFX { get; set; }
        public VOVolume AudioSettings_VO { get; set; }
        public LocalAudioLanguage AudioLanguage { get; set; }
        public LocalTextLanguage TextLanguage { get; set; }

        public StarRailSettings(IGameVersion GameVersionManager)
            : base(GameVersionManager)
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
            AudioSettings_BGM    = BGMVolume.Load();
            AudioSettings_Master = MasterVolume.Load();
            AudioSettings_SFX    = SFXVolume.Load();
            AudioSettings_VO     = VOVolume.Load();
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
            AudioSettings_BGM?.Save();
            AudioSettings_Master?.Save();
            AudioSettings_SFX?.Save();
            AudioSettings_VO?.Save();
            AudioLanguage?.Save();
            TextLanguage?.Save();
        }

        public override IGameSettingsUniversal AsIGameSettingsUniversal() => this;
    }
}
