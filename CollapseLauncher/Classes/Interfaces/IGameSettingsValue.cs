using CollapseLauncher.GameSettings.Base;
using System;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace CollapseLauncher.Interfaces
{
    internal interface IGameSettingsValue<T> : IEquatable<T>
    {
        abstract static T Load();
        void Save();
    }

    internal interface IGameSettingsValueMagic<T> : IGameSettingsValue<T>
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        byte[] Magic { get; }

        abstract static T LoadWithMagic(byte[] magic, SettingsGameVersionManager versionManager, JsonSerializerContext context);
    }
}
