using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;

using Hi3Helper.Screen;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.Preset.ConfigStore;

namespace Hi3Helper.Shared.Region
{
    public static class LauncherConfig
    {
        public static string startupBackgroundPath;
        public static RegionBackgroundProp regionBackgroundProp = new RegionBackgroundProp();
        public static RegionResourceProp regionResourceProp = new RegionResourceProp();
        public static PresetConfigClasses CurrentRegion = new PresetConfigClasses();
        public static List<string> GameConfigName = new List<string>();
        public static List<string> ScreenResolutionsList = new List<string>();

        public static AppIniStruct appIni = new AppIniStruct();

        public static string AppFolder = AppDomain.CurrentDomain.BaseDirectory;
        public static string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "CollapseLauncher");
        public static string AppGameFolder = Path.Combine(AppDataFolder, "GameFolder");
        public static string AppConfigFile = Path.Combine(AppDataFolder, "config.ini");

        public static string GameAppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "miHoYo");

        public static bool RequireAdditionalDataDownload;
        public static bool IsThisRegionInstalled;
        public static GameInstallStateEnum GameInstallationState = GameInstallStateEnum.NotInstalled;

        public static void LoadGamePreset()
        {
            AppGameFolder = Path.Combine(GetAppConfigValue("GameFolder").ToString());
            LoadConfigFromFile(Path.Combine(AppFolder, "config", "fileconfig.json"));
            GameConfigName = Config.Select(x => x.ZoneName).ToList();
        }

        public static void GetScreenResolutionString()
        {
            foreach (Size res in ScreenProp.screenResolutions)
                ScreenResolutionsList.Add($"{res.Width}x{res.Height}");
        }

        public static void LoadAppPreset()
        {
            ScreenProp.InitScreenResolution();
            // GetCurrentScreenResolution();
            GetScreenResolutionString();
            if (!Directory.Exists(AppDataFolder))
                Directory.CreateDirectory(AppDataFolder);

            appIni.Profile = new IniFile();
            appIni.ProfilePath = AppConfigFile;

            if (!File.Exists(appIni.ProfilePath))
            {
                PrepareAppInstallation();
                SaveAppConfig();
            }

            LoadAppConfig();
            startupBackgroundPath = GetAppConfigValue("CurrentBackground").ToString();
        }

        public static IniValue GetAppConfigValue(string key) => appIni.Profile["app"][key];

        public static void LoadAppConfig() => appIni.Profile.Load(appIni.ProfileStream = new FileStream(appIni.ProfilePath, FileMode.Open, FileAccess.Read));
        public static void SaveAppConfig() => appIni.Profile.Save(appIni.ProfileStream = new FileStream(appIni.ProfilePath, FileMode.OpenOrCreate, FileAccess.Write));

        public static void PrepareAppInstallation() => BuildAppIniProfile();

        static void BuildAppIniProfile()
        {
            appIni.Profile.Add("app", new Dictionary<string, IniValue>
            {
                { "CurrentRegion", new IniValue(0) },
                { "CurrentBackground", new IniValue(@"Assets\BG\default.png") },
                { "DownloadThread", new IniValue(8) },
                { "GameFolder", new IniValue(AppGameFolder) }
            });

            SaveAppConfig();
        }
    }
}
