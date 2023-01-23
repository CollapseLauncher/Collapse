using Hi3Helper.Data;
using System;
using System.IO;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace Hi3Helper.Shared.Region
{
    public static partial class InstallationManagement
    {
        public struct GameIniStruct
        {
            public IniFile Profile, Config;
            public string ProfilePath, ConfigPath;
        }

        public static GameIniStruct gameIni = new GameIniStruct();
        public static string gamePath;

        public static void PrepareInstallation()
        {
            gameIni.Profile = new IniFile();
            BuildGameIniProfile();
        }

        public static void PrepareGameConfig()
        {
            gameIni.Config = new IniFile();
            gameIni.ConfigPath = Path.Combine(NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()), "config.ini");
            BuildGameIniConfig();
        }

        public static string LoadGameConfig()
        {
            try
            {
                gameIni.Config = new IniFile();
                string GamePath = gameIni.Profile["launcher"]["game_install_path"].ToString();
                gameIni.ConfigPath = Path.Combine(NormalizePath(GamePath), "config.ini");
                if (File.Exists(gameIni.ConfigPath))
                    gameIni.Config.Load(gameIni.ConfigPath);

                return GamePath;
            }
            catch (ArgumentNullException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogWriteLine($"The Game Profile config.ini seems to be messed up. Please check your Game Profile \"config.ini\" located in this folder:\r\n{gameIni.ProfilePath}\r\n{ex}", LogType.Error, true);
                throw new Exception($"The Game Profile config.ini seems to be messed up. Please check your Game Profile \"config.ini\" located in this folder:\r\n{gameIni.ProfilePath}", ex);
            }
        }
    }
}
