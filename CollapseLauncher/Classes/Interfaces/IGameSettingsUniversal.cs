using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Universal;
using Microsoft.Win32;
using System;
using System.Threading.Tasks;

namespace CollapseLauncher.Interfaces
{
    internal interface IGameSettingsUniversal : IGameSettingsExportable
    {
        BaseScreenSettingData SettingsScreen         { get; }
        CollapseScreenSetting SettingsCollapseScreen { get; }
        CollapseMiscSetting   SettingsCollapseMisc   { get; }
        CustomArgs            SettingsCustomArgument { get; }
        void                  SaveBaseSettings();
        string                GetLaunchArguments(GamePresetProperty property);
    }

#nullable enable
    internal interface IGameSettingsExportable
    {
        RegistryKey? RegistryRoot { get; }

        RegistryKey? RefreshRegistryRoot();
        Task<Exception?> ImportSettings(string? gameBasePath = null);
        Task<Exception?> ExportSettings(bool isCompressed = true, string? parentPathToImport = null, string[]? relativePathToImport = null);
    }
}
