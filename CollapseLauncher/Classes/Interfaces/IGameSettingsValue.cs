using System;

namespace CollapseLauncher.Interfaces
{
    internal interface IGameSettingsValue<T> : IEquatable<T>
    {
        abstract static T Load();
        void Save();
    }
}
