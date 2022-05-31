using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using Hi3Helper.Data;
using static Hi3Helper.Logger;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace Hi3Helper.Shared.Region
{
    public static partial class InstallationManagement
    {
        public struct GameIniStruct
        {
            public IniFile Profile, Config, Settings;
            public Stream ProfileStream, ConfigStream, SettingsStream;
            public string ProfilePath, ConfigPath, SettingsPath;
        }

        public static GameIniStruct gameIni = new GameIniStruct();
        public static string gamePath;

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
            try
            {
                gameIni.Config = new IniFile();
                gameIni.ConfigPath = Path.Combine(NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()), "config.ini");
                if (File.Exists(gameIni.ConfigPath))
                    gameIni.Config.Load(gameIni.ConfigStream = new FileStream(gameIni.ConfigPath, FileMode.Open, FileAccess.Read));
            }
            catch (Exception ex)
            {
                LogWriteLine($"The Game Profile config.ini seems to be messed up. Please check your Game Profile \"config.ini\" located in this folder:\r\n{gameIni.ProfilePath}\r\n{ex}", LogType.Error, true);
                throw new Exception($"The Game Profile config.ini seems to be messed up. Please check your Game Profile \"config.ini\" located in this folder:\r\n{gameIni.ProfilePath}", ex);
            }
        }
        public static void SaveGameConfig() => gameIni.Config.Save(gameIni.ConfigStream = new FileStream(gameIni.ConfigPath, FileMode.OpenOrCreate, FileAccess.Write));

        public static void LoadGameProfile() => appIni.Profile.Load(gameIni.ProfileStream = new FileStream(gameIni.ProfilePath, FileMode.Open, FileAccess.Read));
        public static void SaveGameProfile() => gameIni.Profile.Save(gameIni.ProfileStream = new FileStream(gameIni.ProfilePath, FileMode.OpenOrCreate, FileAccess.Write));
    }
}
