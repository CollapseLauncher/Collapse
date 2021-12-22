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
using static Hi3Helper.Data.ConverterTool;

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

        internal static GameIniStruct gameIni = new GameIniStruct();
        internal static string gamePath;

        public static void PrepareInstallation()
        {
            gameIni.Profile = new IniFile();
            gameIni.ProfileStream = new FileStream(gameIni.ProfilePath, FileMode.Create, FileAccess.ReadWrite);
            BuildGameIniProfile();
        }

        public static void PrepareGameConfig()
        {
            gameIni.Config = new IniFile();
            gameIni.ConfigPath = Path.Combine(NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()), "config.ini");
            gameIni.ConfigStream = new FileStream(gameIni.ConfigPath, FileMode.OpenOrCreate, FileAccess.Write);
            BuildGameIniConfig();
        }

        public static void LoadGameConfig()
        {
            gameIni.Config = new IniFile();
            gameIni.ConfigPath = Path.Combine(NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()), "config.ini");
            if (File.Exists(gameIni.ConfigPath))
                gameIni.Config.Load(gameIni.ConfigStream = new FileStream(gameIni.ConfigPath, FileMode.Open, FileAccess.Read));
        }
        public static void SaveGameConfig() => gameIni.Config.Save(gameIni.ConfigStream = new FileStream(gameIni.ConfigPath, FileMode.OpenOrCreate, FileAccess.Write));

        public static void LoadGameProfile() => appIni.Profile.Load(gameIni.ProfileStream = new FileStream(gameIni.ProfilePath, FileMode.Open, FileAccess.Read));
        public static void SaveGameProfile() => gameIni.Profile.Save(gameIni.ProfileStream = new FileStream(gameIni.ProfilePath, FileMode.OpenOrCreate, FileAccess.Write));

        public static void CheckAndUpdateGameIniValue()
        {
        }
    }
}
