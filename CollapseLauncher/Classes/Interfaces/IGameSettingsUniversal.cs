using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Universal;

namespace CollapseLauncher.Interfaces
{
    internal interface IGameSettingsUniversal
    {
        BaseScreenSettingData SettingsScreen { get; set; }
        CollapseScreenSetting SettingsCollapseScreen { get; set; }
        CustomArgs SettingsCustomArgument { get; set; }
        Playtime SettingsPlaytime {get; set;}
    }
}
