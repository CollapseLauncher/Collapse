using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using Hi3Helper.Data;

namespace Hi3Helper.Shared.ClassStruct
{
    public struct AppIniStruct
    {
        public IniFile Profile;
        public Stream ProfileStream;
        public string ProfilePath;
    }

    public enum GameInstallStateEnum
    {
        Installed = 0,
        InstalledHavePreload = 1,
        NotInstalled = 2,
        NeedsUpdate = 3,
        GameBroken = 4,
    }

    public enum CachesType
    {
        Data = 0,
        Event = 1,
        AI = 2
    }

    public enum CachesDataStatus
    {
        Missing = 0,
        Broken = 1,
        Unecessary = 2
    }
}
