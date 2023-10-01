using System;

namespace CollapseLauncher.Interfaces
{
    internal interface IGameSettingsValue<T> : IEquatable<T>
    {
#if NET7_0_OR_GREATER
        abstract static T Load();
#endif
        void Save();
    }
}
