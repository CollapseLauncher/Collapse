using Hi3Helper.Data;
using System;
using System.IO;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.GameConfig;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace Hi3Helper.Shared.Region
{
    public static partial class InstallationManagement
    {
        public struct GameIniStruct
        {
            public IniFile Profile, Config, Settings;
            public string ProfilePath, ConfigPath, SettingsPath;
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

        public static void LoadGameConfig()
        {
            try
            {
                gameIni.Config = new IniFile();
                gameIni.ConfigPath = Path.Combine(NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()), "config.ini");
                if (File.Exists(gameIni.ConfigPath))
                    gameIni.Config.Load(gameIni.ConfigPath);

                if (!(CurrentRegion.IsGenshin ?? false))
                    Task.Run(() => CheckExistingGameSettings());
            }
            catch (Exception ex)
            {
                LogWriteLine($"The Game Profile config.ini seems to be messed up. Please check your Game Profile \"config.ini\" located in this folder:\r\n{gameIni.ProfilePath}\r\n{ex}", LogType.Error, true);
                throw new Exception($"The Game Profile config.ini seems to be messed up. Please check your Game Profile \"config.ini\" located in this folder:\r\n{gameIni.ProfilePath}", ex);
            }
        }
        public static void SaveGameConfig() => gameIni.Config.Save(gameIni.ConfigPath);

        public static void LoadGameProfile() => appIni.Profile.Load(gameIni.ProfilePath);
        public static void SaveGameProfile() => gameIni.Profile.Save(gameIni.ProfilePath);
    }
}
