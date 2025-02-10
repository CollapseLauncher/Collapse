using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Honkai.Context;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces;

// ReSharper disable PossibleNullReferenceException

namespace CollapseLauncher.GameSettings.Honkai
{
    internal class HonkaiSettings : SettingsBase
    {
        #region PresetProperties
        public Preset<PersonalGraphicsSettingV2, HonkaiSettingsJsonContext> PresetSettingsGraphics { get; set; }
        #endregion

        #region SettingProperties
        public PersonalGraphicsSettingV2 SettingsGraphics       { get; set; }
        public PersonalAudioSetting      SettingsAudio          { get; set; }
        public PhysicsSimulation         SettingsPhysics        { get; set; }
        public GraphicsGrade             SettingsGraphicsGrade  { get; set; }
        #endregion

        public HonkaiSettings(IGameVersion gameVersionManager)
            : base(gameVersionManager)
        {
            // Initialize and Load Settings
            InitializeSettings();
        }

        public sealed override void InitializeSettings()
        {
            // Load Settings
            SettingsGraphics       = PersonalGraphicsSettingV2.Load();
            SettingsGraphicsGrade  = GraphicsGrade.Load();
            SettingsPhysics        = PhysicsSimulation.Load();
            SettingsAudio          = PersonalAudioSetting.Load();
            SettingsScreen         = ScreenSettingData.Load();
            base.InitializeSettings();

            // Load Preset
            PresetSettingsGraphics = Preset<PersonalGraphicsSettingV2, HonkaiSettingsJsonContext>.LoadPreset(GameNameType.Honkai, HonkaiSettingsJsonContext.Default.DictionaryStringPersonalGraphicsSettingV2);
        }

        public override void ReloadSettings() => InitializeSettings();

        public override void SaveSettings()
        {
            // Save Settings
            SettingsGraphics.Save();
            SettingsPhysics.Save();
            SettingsGraphicsGrade.Save();
            SettingsAudio.Save();
            SettingsScreen.Save();
            base.SaveSettings();

            // Save Preset
            PresetSettingsGraphics.SaveChanges();
        }

        public override IGameSettingsUniversal AsIGameSettingsUniversal() => this;
    }
}
