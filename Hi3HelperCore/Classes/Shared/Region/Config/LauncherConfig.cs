using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Screen;
using Hi3Helper.Shared.ClassStruct;
using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using static Hi3Helper.InvokeProp;
using static Hi3Helper.Preset.ConfigStore;

namespace Hi3Helper.Shared.Region
{
    public static class LauncherConfig
    {
        public static Vector3 Shadow16 = new Vector3(0, 0, 16);
        public static Vector3 Shadow32 = new Vector3(0, 0, 32);
        public static Vector3 Shadow48 = new Vector3(0, 0, 48);
        // Format in milliseconds
        public static int RefreshTime = 250;
        public enum AppLanguage { EN, ID }

        const string SectionName = "app";
        public static string startupBackgroundPath;
        public static RegionResourceProp regionBackgroundProp = new RegionResourceProp();
        public static RegionResourceProp regionResourceProp = new RegionResourceProp();
        public static HomeMenuPanel regionNewsProp = new HomeMenuPanel();
        public static PresetConfigClasses CurrentRegion = new PresetConfigClasses();
        public static List<string> GameConfigName = new List<string>();
        public static List<string> ScreenResolutionsList = new List<string>();

        public static AppIniStruct appIni = new AppIniStruct();

        public static string AppCurrentVersion;
        public static string AppFolder = AppDomain.CurrentDomain.BaseDirectory;
        public static string AppDefaultBG = Path.Combine(AppFolder, "Assets", "BG", "default.png");
        public static string AppLangFolder = Path.Combine(AppFolder, @"Lang");
        public static string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "CollapseLauncher");
        public static string AppGameFolder = Path.Combine(AppDataFolder, "GameFolder");
        public static string AppGameImgFolder { get => Path.Combine(AppGameFolder, "_img"); }
        public static string AppGameLogsFolder { get => Path.Combine(AppGameFolder, "_logs"); }
        public static string AppConfigFile = Path.Combine(AppDataFolder, "config.ini");
        public static string AppNotifIgnoreFile = Path.Combine(AppDataFolder, "ignore_notif_ids.json");
        public static string AppNotifURLPrefix = "https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/raw/main/notification_{0}.json";
        public static string GamePathOnSteam;

        public static string GameAppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "miHoYo");

        public static bool RequireAdditionalDataDownload;
        public static bool IsThisRegionInstalled = false;
        public static bool IsPreview = false;
        public static bool IsAppThemeNeedRestart = false;
        public static bool IsAppLangNeedRestart = false;
        public static bool IsFirstInstall = false;
        public static bool ForceInvokeUpdate = false;
        public static string UpdateRepoChannel = "https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/raw/main/";
        public static GameInstallStateEnum GameInstallationState = GameInstallStateEnum.NotInstalled;

        public static Dictionary<string, IniValue> AppSettingsTemplate = new Dictionary<string, IniValue>
        {
            { "CurrentRegion", new IniValue(0) },
            { "CurrentBackground", new IniValue("/Assets/BG/default.png") },
            { "DownloadThread", new IniValue(4) },
            { "ExtractionThread", new IniValue(0) },
            { "GameFolder", new IniValue() },
#if DEBUG
            { "EnableConsole", new IniValue(true) },
#else
            { "EnableConsole", new IniValue(false) },
#endif
            { "DontAskUpdate", new IniValue(false) },
            { "ThemeMode", new IniValue(AppThemeMode.Default) },
            { "AppLanguage", new IniValue("en-us") },
            { "UseCustomBG", new IniValue(false) },
            { "ShowEventsPanel", new IniValue(true) },
            { "CustomBGPath", new IniValue() }
        };

        public static void LoadGamePreset()
        {
            AppGameFolder = Path.Combine(GetAppConfigValue("GameFolder").ToString());
            LoadConfigTemplate();
            // LoadConfigFromFile(Path.Combine(AppFolder, "config", "fileconfig.json"));
            GameConfigName = Config.Select(x => x.ZoneName).ToList();
        }

        public static void GetScreenResolutionString()
        {
            foreach (Size res in ScreenProp.screenResolutions)
                ScreenResolutionsList.Add($"{res.Width}x{res.Height}");
        }

        public static void InitAppPreset()
        {
            IsFirstInstall = !File.Exists(AppConfigFile);
            InitScreenResSettings();

            if (!Directory.Exists(AppDataFolder))
                Directory.CreateDirectory(AppDataFolder);

            appIni.Profile = new IniFile();
            appIni.ProfilePath = AppConfigFile;

            if (IsFirstInstall)
            {
                appIni.Profile.Add(SectionName, AppSettingsTemplate);
                SaveAppConfig();
            }
            else
            {
                LoadAppConfig();
                CheckAndSetDefaultConfigValue();
            }
            startupBackgroundPath = GetAppConfigValue("CurrentBackground").ToString();

            bool IsGameFolderExist = Directory.Exists(GetAppConfigValue("GameFolder").ToString());

            // The app will also set IsFirstInstall to True if GameFolder value is empty.
            // Or if the directory doesn't exist or user doesn't have permission.
            IsFirstInstall = !(IsGameFolderExist
                && ConverterTool.IsUserHasPermission(GetAppConfigValue("GameFolder").ToString()))
                || string.IsNullOrEmpty(GetAppConfigValue("GameFolder").ToString());

            try
            {
                if (IsGameFolderExist)
                    AppGameFolder = Path.Combine(GetAppConfigValue("GameFolder").ToString());
                else
                    AppGameFolder = Path.Combine(AppDataFolder, "GameFolder");
            }
            catch (ArgumentNullException)
            {
                IsFirstInstall = true;

            }
        }

        private static void InitScreenResSettings()
        {
            ScreenProp.InitScreenResolution();
            GetScreenResolutionString();
        }

        public static void InitConsoleSetting(bool forceShowConsole = false)
        {
            if (GetAppConfigValue("EnableConsole").ToBool() || forceShowConsole)
                ShowConsoleWindow();
            else
                HideConsoleWindow();
        }

        public static int GetAppExtractConfigValue() => GetAppConfigValue("ExtractionThread").ToInt() == 0 ? Environment.ProcessorCount : GetAppConfigValue("ExtractionThread").ToInt();
        public static IniValue GetAppConfigValue(string key) => appIni.Profile[SectionName][key];
        public static void SetAndSaveConfigValue(string key, IniValue value)
        {
            SetAppConfigValue(key, value);
            SaveAppConfig();
        }
        public static void SetAppConfigValue(string key, IniValue value) => appIni.Profile[SectionName][key] = value;

        public static void LoadAppConfig() => appIni.Profile.Load(appIni.ProfilePath);
        public static void SaveAppConfig() => appIni.Profile.Save(appIni.ProfilePath);

        public static void CheckAndSetDefaultConfigValue()
        {
            foreach (KeyValuePair<string, IniValue> Entry in AppSettingsTemplate)
            {
                if (GetAppConfigValue(Entry.Key).Value == null)
                    SetAppConfigValue(Entry.Key, Entry.Value);
            }
            if (GetAppConfigValue("DownloadThread").ToInt() > 8)
                SetAppConfigValue("DownloadThread", 8);
            SaveAppConfig();
        }
    }
}
