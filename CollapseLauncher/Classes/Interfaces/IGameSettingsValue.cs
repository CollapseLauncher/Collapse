namespace CollapseLauncher.Interfaces
{
    internal interface IGameSettingsValue<T>
    {
        abstract static T Load();
        void Save();
    }
}
