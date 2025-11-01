using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Win32.Screen;
using System.Drawing;
using System.Text;
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

        public override string GetLaunchArguments(GamePresetProperty property)
        {
            StringBuilder parameter = new(1024);

            if (SettingsCollapseScreen.UseExclusiveFullscreen)
            {
                parameter.Append("-window-mode exclusive -screen-fullscreen 1 ");
            }

            /*
             * Force disable Mobile mode due to reported bannable offense in GI. Thank you HoYo.
             * Added pragma in-case this will be reused in the future.
             *
            // Enable mobile mode
            if (SettingsCollapseMisc.LaunchMobileMode) 
            {
                const string regLoc = GameSettings.StarRail.Model.ValueName;
                var regRoot = GameSettings.Base.SettingsBase.RegistryRoot;

                if (regRoot != null || !string.IsNullOrEmpty(regLoc))
                {
                    var regModel = (byte[])regRoot!.GetValue(regLoc, null);

                    if (regModel != null)
                    {
                        string regB64 = Convert.ToBase64String(regModel);
                        parameter.Append($"-is_cloud 1 -platform_type CLOUD_WEB_TOUCH -graphics_setting {regB64} ");
                    }
                    else
                    {
                        Logger.LogWriteLine("Failed enabling MobileMode for HSR: regModel is null.", LogType.Error, true);
                    }
                }
                else
                {
                    Logger.LogWriteLine("Failed enabling MobileMode for HSR: regRoot/regLoc is unexpectedly uninitialized.",
                                        LogType.Error, true);
                }
            }
            */

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
