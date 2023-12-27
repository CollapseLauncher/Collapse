using System;

namespace CollapseLauncher.Interfaces
{
    internal interface IGameSettings
    {
        Exception ImportSettings();
        Exception ExportSettings(bool isCompressed = true);
        void ReloadSettings();
        void SaveSettings();
        IGameSettingsUniversal AsIGameSettingsUniversal();
    }
}
