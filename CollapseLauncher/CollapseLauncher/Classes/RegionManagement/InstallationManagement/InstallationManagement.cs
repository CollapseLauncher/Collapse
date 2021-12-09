using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;

using Hi3Helper;
using Hi3Helper.Preset;
using Hi3Helper.Data;

using static CollapseLauncher.LauncherConfig;
using static Hi3Helper.Logger;

namespace CollapseLauncher.Region
{
    internal static partial class InstallationManagement
    {
        public struct GameIniStruct
        {
            public IniFile Profile, Config;
            public Stream ProfileStream, ConfigStream;
            public string ProfilePath, ConfigPath;
        }

        internal static GameIniStruct gameIni;
        internal static string gamePath;

        public static void PrepareInstallation()
        {
            gameIni.Profile = new IniFile();
            gameIni.ProfileStream = new FileStream(gameIni.ProfilePath, FileMode.Create, FileAccess.ReadWrite);
            BuildIniProfile();
        }

        public static void GetInstallation()
        {

        }
    }
}
