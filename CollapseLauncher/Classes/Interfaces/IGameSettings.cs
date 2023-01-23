using System;

namespace CollapseLauncher.Interfaces
{
    internal interface IGameSettings
    {
        Exception ImportSettings();
        Exception ExportSettings();
        void RevertSettings();
        void SaveSettings();
        IGameSettingsUniversal AsIGameSettingsUniversal();
    }
}
