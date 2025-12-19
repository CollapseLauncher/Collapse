// ReSharper disable UnusedMemberInSuper.Global

#nullable enable
namespace CollapseLauncher.Interfaces
{
    internal interface IGameSettings : IGameSettingsUniversal
    {
        void InitializeSettings();
        void ReloadSettings();
        void SaveSettings();
        IGameSettingsUniversal AsIGameSettingsUniversal();
    }
}
