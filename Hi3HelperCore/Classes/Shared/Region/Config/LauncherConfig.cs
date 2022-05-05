using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Numerics;

using Hi3Helper.Screen;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.Preset.ConfigStore;
using static Hi3Helper.InvokeProp;
using static Hi3Helper.Logger;

using Aiursoft.HSharp.Models;

namespace Hi3Helper.Shared.Region
{
    public static class LauncherConfig
    {
        public static Vector3 Shadow16 = new Vector3(0, 0, 16);
        public static Vector3 Shadow32 = new Vector3(0, 0, 32);
        public static Vector3 Shadow48 = new Vector3(0, 0, 48);
        public enum AppLanguage { EN, ID }
        public static Dictionary<string, IniValue> AppSettingsTemplate = new Dictionary<string, IniValue>
        {
            { "CurrentRegion", new IniValue(0) },
            { "CurrentBackground", new IniValue("/Assets/BG/default.png") },
            { "DownloadThread", new IniValue(8) },
            { "ExtractionThread", new IniValue(0) },
            { "GameFolder", new IniValue(AppGameFolder) },
#if DEBUG
            { "EnableConsole", new IniValue(true) },
#else
            { "EnableConsole", new IniValue(false) },
#endif
            { "DontAskUpdate", new IniValue(false) },
            { "ThemeMode", new IniValue(AppThemeMode.Light) },
            { "Language", new IniValue(AppLanguage.EN) }
        };

        const string SectionName = "app";
        public static string startupBackgroundPath;
        public static RegionBackgroundProp regionBackgroundProp = new RegionBackgroundProp();
        public static RegionResourceProp regionResourceProp = new RegionResourceProp();
        public static HomeMenuPanel regionNewsProp = new HomeMenuPanel();
        public static PresetConfigClasses CurrentRegion = new PresetConfigClasses();
        public static List<string> GameConfigName = new List<string>();
        public static List<string> ScreenResolutionsList = new List<string>();

        public static AppIniStruct appIni = new AppIniStruct();

        public static string AppCurrentVersion;
        public static string AppFolder = AppDomain.CurrentDomain.BaseDirectory;
        public static string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "CollapseLauncher");
        public static string AppGameFolder = Path.Combine(AppDataFolder, "GameFolder");
        public static string AppGameImgFolder = Path.Combine(AppDataFolder, "img");
        public static string AppGameLogsFolder = Path.Combine(AppDataFolder, "logs");
        public static string AppConfigFile = Path.Combine(AppDataFolder, "config.ini");
        public static string AppNotifIgnoreFile = Path.Combine(AppDataFolder, "ignore_notif_ids.json");
        public static string AppNotifURLPrefix = "https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/raw/main/notification_{0}.json";
        public static string GamePathOnSteam;

        public static string GameAppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "miHoYo");

        public static bool RequireAdditionalDataDownload;
        public static bool IsThisRegionInstalled;
        public static bool ForceInvokeUpdate = false;
        public static string UpdateRepoChannel = "https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/raw/main/";
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
            CheckAndSetDefaultConfigValue();
            startupBackgroundPath = GetAppConfigValue("CurrentBackground").ToString();

            InitConsoleSetting();
        }

        public static void InitConsoleSetting()
        {
            if (GetAppConfigValue("EnableConsole").ToBool())
                ShowConsoleWindow();
            else
                HideConsoleWindow();
        }

        public static int GetAppExtractConfigValue() => GetAppConfigValue("ExtractionThread").ToInt() == 0 ? Environment.ProcessorCount : GetAppConfigValue("ExtractionThread").ToInt();
        public static IniValue GetAppConfigValue(string key) => appIni.Profile[SectionName][key];
        public static void SetAppConfigValue(string key, object? value)
        {
            appIni.Profile[SectionName][key] = new IniValue(value);
            SaveAppConfig();
        }
        public static void SetAppConfigValue(string key, IniValue value) => appIni.Profile[SectionName][key] = value;

        public static void LoadAppConfig() => appIni.Profile.Load(appIni.ProfileStream = new FileStream(appIni.ProfilePath, FileMode.Open, FileAccess.Read));
        public static void SaveAppConfig() => appIni.Profile.Save(appIni.ProfileStream = new FileStream(appIni.ProfilePath, FileMode.OpenOrCreate, FileAccess.Write));

        public static void PrepareAppInstallation() => BuildAppIniProfile();

        public static void CheckAndSetDefaultConfigValue()
        {
            foreach (KeyValuePair<string, IniValue> Entry in AppSettingsTemplate)
            {
                if (GetAppConfigValue(Entry.Key).Value == null)
                    SetAppConfigValue(Entry.Key, Entry.Value);
            }
            SaveAppConfig();
        }

        static void BuildAppIniProfile()
        {
            appIni.Profile.Add(SectionName, AppSettingsTemplate);

            SaveAppConfig();
        }
    }
}
