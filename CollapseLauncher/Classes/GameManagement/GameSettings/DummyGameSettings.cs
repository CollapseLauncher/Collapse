using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.Interfaces;
using System.Text;
// ReSharper disable VirtualMemberCallInConstructor

namespace CollapseLauncher.Classes.GameManagement.GameSettings;

internal class DummyGameSettings : SettingsBase
{
    public DummyGameSettings() : base()
    {

    }

    public DummyGameSettings(IGameVersion gameVersionManager)
        : base(gameVersionManager)
    {
        // Initialize and Load Settings
        InitializeSettings();
    }

    public override string GetLaunchArguments(GamePresetProperty property)
    {
        StringBuilder parameter = new(1024);

        if (SettingsCollapseScreen.UseExclusiveFullscreen)
        {
            parameter.Append("-window-mode exclusive ");
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
}
