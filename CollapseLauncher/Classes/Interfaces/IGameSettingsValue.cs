using System;
using Windows.Gaming.Input;

namespace CollapseLauncher.Interfaces
{
    internal interface IGameSettingsValue<T>
    {
        abstract static T Load();
        void Save();
    }
}
