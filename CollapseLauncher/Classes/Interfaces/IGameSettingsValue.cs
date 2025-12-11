using CollapseLauncher.GameSettings.Base;
using System.Diagnostics;
using System.Text.Json.Serialization.Metadata;
// ReSharper disable UnusedMemberInSuper.Global

namespace CollapseLauncher.Interfaces
{
    internal interface IGameSettingsValue<out T>
    {
        IGameSettings     ParentGameSettings { get; }
        static abstract T Load(IGameSettings gameSettings);
        void              Save();
    }

    internal interface IGameSettingsValueMagic<T> : IGameSettingsValue<T>
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        byte[] Magic { get; }

#nullable enable
        static abstract T LoadWithMagic(byte[] magic, SettingsGameVersionManager versionManager, JsonTypeInfo<T?> typeInfo);
    }
}
