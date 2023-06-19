using System;

namespace CollapseLauncher.Interfaces
{
    internal interface IGameSettings
    {
        Exception ImportSettings();
        Exception ExportSettings();
        void ReloadSettings();
        void SaveSettings();
        IGameSettingsUniversal AsIGameSettingsUniversal();
    }
}
