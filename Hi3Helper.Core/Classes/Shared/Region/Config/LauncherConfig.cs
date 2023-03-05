using Hi3Helper.Data;
using Hi3Helper.Screen;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Text;
using static Hi3Helper.Locale;

namespace Hi3Helper.Shared.Region
{
    public struct CDNURLProperty
    {
        public string URLPrefix { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public static class LauncherConfig
    {
        public static List<CDNURLProperty> CDNList => new List<CDNURLProperty>
        {
            new CDNURLProperty
            {
                Name = "GitHub",
                URLPrefix = "https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/raw/main",
                Description = Lang._Misc.CDNDescription_Github
            },
            new CDNURLProperty
            {
                Name = "Statically",
                URLPrefix = "https://cdn.statically.io/gh/neon-nyan/CollapseLauncher-ReleaseRepo/main",
                Description = Lang._Misc.CDNDescription_Statically
            }
        };

        public static CDNURLProperty GetCurrentCDN() => CDNList[GetAppConfigValue("CurrentCDN").ToInt()];
        public static void SetCurrentCDN(int index) => SetAppConfigValue("CurrentCDN", index);

        public static Vector3 Shadow16 = new Vector3(0, 0, 16);
        public static Vector3 Shadow32 = new Vector3(0, 0, 32);
        public static Vector3 Shadow48 = new Vector3(0, 0, 48);
        // Format in milliseconds
        public static int RefreshTime = 250;

        const string SectionName = "app";
        public static string startupBackgroundPath;
        public static RegionResourceProp regionBackgroundProp = new RegionResourceProp();
        public static HomeMenuPanel regionNewsProp = new HomeMenuPanel();
        public static List<string> ScreenResolutionsList = new List<string>();

        public static AppIniStruct appIni = new AppIniStruct();

        public static string AppCurrentVersion;
        public static string AppFolder = AppDomain.CurrentDomain.BaseDirectory;
        public static string AppDefaultBG = Path.Combine(AppFolder, "Assets", "BG", "default.png");
        public static string AppLangFolder = Path.Combine(AppFolder, "Lang");
        public static string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "CollapseLauncher");
        public static string AppGameFolder {
            get => GetAppConfigValue("GameFolder").ToString();
            set => SetAppConfigValue("GameFolder", value);
        }
        public static string[] AppCurrentArgument;
        public static string AppExecutablePath {
            get {
                string execName = Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location);
                string dirPath = AppFolder;
                return Path.Combine(dirPath, execName + ".exe");
            }
        }
        public static string AppGameImgFolder { get => Path.Combine(AppGameFolder, "_img"); }
        public static string AppGameLogsFolder { get => Path.Combine(AppGameFolder, "_logs"); }
        public static string AppConfigFile = Path.Combine(AppDataFolder, "config.ini");
        public static string AppNotifIgnoreFile = Path.Combine(AppDataFolder, "ignore_notif_ids.json");
        public static string GamePathOnSteam;

        public static string AppNotifURLPrefix => GetCurrentCDN().URLPrefix + "/notification_{0}.json";
        public static string AppGameConfigV2URLPrefix => GetCurrentCDN().URLPrefix + "/metadata/metadatav2_{0}.json";
        public static string AppGameRepairIndexURLPrefix => GetCurrentCDN().URLPrefix + "/metadata/repair_indexes/{0}/{1}/index";
        public static string AppGameRepoIndexURLPrefix => GetCurrentCDN().URLPrefix + "/metadata/repair_indexes/{0}/repo";

        public static long AppGameConfigLastUpdate;
        public static int AppCurrentThread
        {
            get
            {
                int val = GetAppConfigValue("ExtractionThread").ToInt();
                return val <= 0 ? Environment.ProcessorCount : val;
            }
        }
        public static int AppCurrentDownloadThread => GetAppConfigValue("DownloadThread").ToInt();
        public static string AppGameConfigMetadataFolder { get => Path.Combine(AppGameFolder, "_metadata"); }
        public static string AppGameConfigV2StampPath { get => Path.Combine(AppGameConfigMetadataFolder, "stampv2.json"); }
        public static string AppGameConfigV2MetadataPath { get => Path.Combine(AppGameConfigMetadataFolder, "metadatav2.json"); }

        public static string GameAppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "miHoYo");

        public static bool RequireAdditionalDataDownload;
        public static bool IsThisRegionInstalled = false;
        public static bool IsPreview = false;
        public static bool IsPortable = false;
        public static bool IsAppThemeNeedRestart = false;
        public static bool IsAppLangNeedRestart = false;
        public static bool IsFirstInstall = false;
        public static bool IsConsoleEnabled
        {
            get => GetAppConfigValue("EnableConsole").ToBoolNullable() ?? false;
            set => SetAppConfigValue("EnableConsole", value);
        }
        public static bool IsMultipleInstanceEnabled
        {
            get => GetAppConfigValue("EnableMultipleInstance").ToBoolNullable() ?? false;
            set => SetAndSaveConfigValue("EnableMultipleInstance", value);
        }
        public static bool ForceInvokeUpdate = false;
        public static string UpdateRepoChannel = "https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/raw/main/";
        public static GameInstallStateEnum GameInstallationState = GameInstallStateEnum.NotInstalled;

        public static Dictionary<string, IniValue> AppSettingsTemplate = new Dictionary<string, IniValue>
        {
            { "CurrentRegion", new IniValue(0) },
            { "CurrentBackground", new IniValue("ms-appx:///Assets/BG/default.png") },
            { "DownloadThread", new IniValue(4) },
            { "ExtractionThread", new IniValue(0) },
            { "GameFolder", new IniValue(Path.Combine(AppDataFolder, "GameFolder")) },
#if DEBUG
            { "EnableConsole", new IniValue(true) },
#else
            { "EnableConsole", new IniValue(false) },
#endif
            { "EnableMultipleInstance", new IniValue(false) },
            { "DontAskUpdate", new IniValue(false) },
            { "ThemeMode", new IniValue(AppThemeMode.Dark) },
            { "AppLanguage", new IniValue("en-us") },
            { "UseCustomBG", new IniValue(false) },
            { "ShowEventsPanel", new IniValue(true) },
            { "ShowSocialMediaPanel", new IniValue(true) },
            { "CustomBGPath", new IniValue() },
            { "GameCategory", new IniValue("Honkai Impact 3rd") },
            { "GameRegion", new IniValue("Southeast Asia") },
            { "WindowSizeProfile", new IniValue("Normal") },
            { "CurrentCDN", new IniValue(0) },
        };

        public static void LoadGamePreset()
        {
            AppGameFolder = Path.Combine(GetAppConfigValue("GameFolder").ToString());
        }

        public static void GetScreenResolutionString()
        {
            foreach (Size res in ScreenProp.screenResolutions)
                ScreenResolutionsList.Add($"{res.Width}x{res.Height}");
        }

        public static void InitAppPreset()
        {
            // Initialize resolution settings first and assign AppConfigFile to ProfilePath
            InitScreenResSettings();
            appIni.ProfilePath = AppConfigFile;

            // Set user permission check to its default and check for the existence of config file.
            bool IsConfigFileExist = File.Exists(appIni.ProfilePath);

            // If the config file is exist, then continue to load the file
            appIni.Profile = new IniFile();
            if (IsConfigFileExist)
            {
                appIni.Profile.Load(appIni.ProfilePath);
            }

            // If the section doesn't exist, then add the section template
            if (!appIni.Profile.ContainsSection(SectionName))
            {
                appIni.Profile.Add(SectionName, AppSettingsTemplate);
            }

            // Check and assign default for the null and non-existence values.
            CheckAndSetDefaultConfigValue();

            // Set the startup background path and GameFolder to check if user has permission.
            startupBackgroundPath = GetAppConfigValue("CurrentBackground").ToString();
            string GameFolder = GetAppConfigValue("GameFolder").ToString();

            // Check if the drive is exist. If not, then reset the GameFolder variable and set IsFirstInstall to true;
            if (!IsDriveExist(GameFolder))
            {
                IsFirstInstall = true;

                // Reset GameFolder to default value
                SetAppConfigValue("GameFolder", AppSettingsTemplate["GameFolder"]);

                // Force enable Console Log and return
                Logger._log = new LoggerConsole(AppGameLogsFolder, Encoding.UTF8);
                Logger.LogWriteLine($"Game App Folder path: {GameFolder} doesn't exist! The launcher will be reinitialize the setup.", LogType.Error, true);
                return;
            }

            // Check if user has permission
            bool IsUserHasPermission = ConverterTool.IsUserHasPermission(GameFolder);

            // Assign boolean if IsConfigFileExist and IsUserHasPermission.
            IsFirstInstall = !(IsConfigFileExist && IsUserHasPermission);
        }

        private static bool IsDriveExist(string path)
        {
            return new DriveInfo(Path.GetPathRoot(path)).IsReady;
        }

        private static void InitScreenResSettings()
        {
            ScreenProp.InitScreenResolution();
            GetScreenResolutionString();
        }

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
                if (string.IsNullOrEmpty(GetAppConfigValue(Entry.Key).Value))
                    SetAppConfigValue(Entry.Key, Entry.Value);
            }
            if (GetAppConfigValue("DownloadThread").ToInt() > 8)
                SetAppConfigValue("DownloadThread", 8);
        }
    }
}
