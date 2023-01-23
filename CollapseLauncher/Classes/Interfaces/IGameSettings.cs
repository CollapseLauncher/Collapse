using System.Collections.Generic;

namespace CollapseLauncher.Interfaces
{
    internal interface IGameSettings
    {
        void ImportSettings();
        void ExportSettings();
        void RevertSettings();
        void SaveSettings();
        IGameSettingsUniversal AsIGameSettingsUniversal();
    }
}
