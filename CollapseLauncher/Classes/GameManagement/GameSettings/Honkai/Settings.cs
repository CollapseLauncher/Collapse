using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Honkai.Context;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Win32.Screen;
using System.Drawing;
using System.Text;

// ReSharper disable PossibleNullReferenceException

namespace CollapseLauncher.GameSettings.Honkai
{
    internal class HonkaiSettings : SettingsBase
    {
        #region PresetProperties
        public Preset<PersonalGraphicsSettingV2, HonkaiSettingsJsonContext> PresetSettingsGraphics { get; private set; }
        #endregion

        #region SettingProperties
        public  PersonalGraphicsSettingV2 SettingsGraphics      { get; set; }
        public  PersonalAudioSetting      SettingsAudio         { get; private set; }
        public  PhysicsSimulation         SettingsPhysics       { get; private set; }
        private GraphicsGrade             SettingsGraphicsGrade { get; set; }
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
            SettingsGraphics      = PersonalGraphicsSettingV2.Load(this);
            SettingsGraphicsGrade = GraphicsGrade.Load(this);
            SettingsPhysics       = PhysicsSimulation.Load(this);
            SettingsAudio         = PersonalAudioSetting.Load(this);
            SettingsScreen        = ScreenSettingData.Load(this);
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
            PresetSettingsGraphics.SaveChanges(this);
        }

        public override string GetLaunchArguments(GamePresetProperty property)
        {
            StringBuilder parameter = new(1024);

            if (SettingsCollapseScreen.UseExclusiveFullscreen)
            {
                parameter.Append("-window-mode exclusive ");
            }

            Size screenSize = SettingsScreen.sizeRes;

            int apiID = SettingsCollapseScreen.GameGraphicsAPI;

            if (apiID == 4)
            {
                Logger.LogWriteLine("You are going to use DX12 mode in your game.\r\n\tUsing CustomScreenResolution or FullscreenExclusive value may break the game!", LogType.Warning);
                if (SettingsCollapseScreen.UseCustomResolution && SettingsScreen.isfullScreen)
                {
                    var size = ScreenProp.CurrentResolution;
                    parameter.Append($"-screen-width {size.Width} -screen-height {size.Height} ");
                }
                else
                    parameter.Append($"-screen-width {screenSize.Width} -screen-height {screenSize.Height} ");
            }
            else
                parameter.Append($"-screen-width {screenSize.Width} -screen-height {screenSize.Height} ");

            switch (apiID)
            {
                case 0:
                    parameter.Append("-force-feature-level-10-1 ");
                    break;
                // case 1 is default
                default:
                    parameter.Append("-force-feature-level-11-0 -force-d3d11-no-singlethreaded ");
                    break;
                case 2:
                    parameter.Append("-force-feature-level-11-1 ");
                    break;
                case 3:
                    parameter.Append("-force-feature-level-11-1 -force-d3d11-no-singlethreaded ");
                    break;
                case 4:
                    parameter.Append("-force-d3d12 ");
                    break;
            }

            if (SettingsCollapseScreen.UseBorderlessScreen)
            {
                parameter.Append("-popupwindow ");
            }

            string customArgs = SettingsCustomArgument.CustomArgumentValue;
            if (SettingsCollapseMisc.UseCustomArguments &&
                !string.IsNullOrEmpty(customArgs))
            {
                parameter.Append(customArgs);
            }

            return parameter.ToString();
        }

        public override IGameSettingsUniversal AsIGameSettingsUniversal() => this;
    }
}
