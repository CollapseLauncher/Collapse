using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Universal;

namespace CollapseLauncher.Interfaces
{
    internal interface IGameSettingsUniversal
    {
        BaseScreenSettingData SettingsScreen { get; set; }
        CollapseScreenSetting SettingsCollapseScreen { get; set; }
        CollapseMiscSetting SettingsCollapseMisc { get; set; }
        CustomArgs SettingsCustomArgument { get; set; }
        void SaveBaseSettings();
    }
}
