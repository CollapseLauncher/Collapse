using System;

#nullable enable
namespace CollapseLauncher.Interfaces
{
    internal interface IGameSettings : IGameSettingsUniversal
    {
        Exception? ImportSettings(string? gameBasePath = null);
        Exception? ExportSettings(bool isCompressed = true, string? parentPathToImport = null, string[]? relativePathToImport = null);
        void InitializeSettings();
        void ReloadSettings();
        void SaveSettings();
        IGameSettingsUniversal AsIGameSettingsUniversal();
    }
}
