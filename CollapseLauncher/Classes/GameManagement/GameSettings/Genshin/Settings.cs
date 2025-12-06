using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Win32.Screen;
using System.Drawing;
using System.Text;
// ReSharper disable IdentifierTypo

namespace CollapseLauncher.GameSettings.Genshin
{
    internal class GenshinSettings : SettingsBase
    {
        public GeneralData       SettingsGeneralData      { get; private set; }
        public VisibleBackground SettingVisibleBackground { get; private set; }
        public WindowsHDR        SettingsWindowsHDR       { get; private set; }

        public GenshinSettings(IGameVersion gameVersionManager)
            : base(gameVersionManager)
        {
            // Initialize and Load Settings
            InitializeSettings();
        }

        public sealed override void InitializeSettings()
        {
            // Load Settings
            base.InitializeSettings();
            SettingsScreen           = ScreenManager.Load(this);
            SettingVisibleBackground = VisibleBackground.Load(this);
            SettingsWindowsHDR       = WindowsHDR.Load(this);
        }

        public override void ReloadSettings()
        {
            // To ease up resource and prevent bad JSON locking up launcher
            SettingsGeneralData = GeneralData.Load(this);
            InitializeSettings();
        } 

        #nullable enable
        public override void SaveSettings()
        {
            // Save Settings
            base.SaveSettings();
            SettingsScreen?.Save();
            SettingsGeneralData?.Save();
            SettingVisibleBackground?.Save();
            SettingsWindowsHDR?.Save();
        }

        public override string GetLaunchArguments(GamePresetProperty property)
        {
            StringBuilder parameter = new(1024);

            if (SettingsCollapseScreen.UseExclusiveFullscreen)
            {
                parameter.Append("-window-mode exclusive -screen-fullscreen 1 ");
                Logger.LogWriteLine("Exclusive mode is enabled in Genshin Impact, stability may suffer!\r\nTry not to Alt+Tab when game is on its loading screen :)", LogType.Warning, true);
            }

            /*
             * Force disable Mobile mode due to reported bannable offense in GI. Thank you HoYo.
             * Added pragma in-case this will be reused in the future.

            // Enable mobile mode
            if (SettingsCollapseMisc.LaunchMobileMode)
                parameter.Append("use_mobile_platform -is_cloud 1 -platform_type CLOUD_THIRD_PARTY_MOBILE ");
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
