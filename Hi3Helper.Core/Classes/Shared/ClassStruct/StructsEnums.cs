using Hi3Helper.Data;
using System.IO;

namespace Hi3Helper.Shared.ClassStruct
{
    public struct AppIniStruct
    {
        public IniFile Profile;
        public Stream ProfileStream;
        public string ProfilePath;
    }

    public enum AppThemeMode
    {
        Default = 0,
        Light   = 1,
        Dark    = 2,
    }

    public enum GameInstallStateEnum
    {
        Installed = 0,
        InstalledHavePreload = 1,
        NotInstalled = 2,
        NeedsUpdate = 3,
        GameBroken = 4,
        InstalledHavePlugin = 5,
    }

    public enum CachesType
    {
        Data = 0,
        Event = 1,
        AI = 2
    }

    public enum CachesDataStatus
    {
        New = 0,
        Obsolete = 1,
        Unused = 2
    }
}
