using Hi3Helper.Data;
#if !DISABLEDISCORD
using Hi3Helper.DiscordPresence;
#endif
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
    public struct CDNURLProperty : IEquatable<CDNURLProperty>
    {
        public string URLPrefix { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Equals(CDNURLProperty other) => URLPrefix == other.URLPrefix && Name == other.Name && Description == other.Description;
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
            },
            new CDNURLProperty
            {
                Name = "jsDelivr",
                URLPrefix = "https://cdn.jsdelivr.net/gh/neon-nyan/CollapseLauncher-ReleaseRepo@latest",
                Description = Lang._Misc.CDNDescription_jsDelivr
            }
        };

        public static CDNURLProperty GetCurrentCDN() => CDNList[GetAppConfigValue("CurrentCDN").ToInt()];

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

#if PORTABLE
        public static string AppFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
        public static string AppConfigFolder = Path.Combine(AppFolder, "Config");
#else
        public static string AppFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
#endif
        public static string AppDefaultBG = Path.Combine(AppFolder, "Assets", "BG", "default.png");
        public static string AppLangFolder = Path.Combine(AppFolder, "Lang");
        public static string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "CollapseLauncher");
        public static string AppGameFolder
        {
            get => GetAppConfigValue("GameFolder").ToString();
            set => SetAppConfigValue("GameFolder", value);
        }
        public static string[] AppCurrentArgument;
        public static string AppExecutablePath
        {
            get
            {
                string execName = Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location);
                string dirPath = AppFolder;
                return Path.Combine(dirPath, execName + ".exe");
            }
        }
        public static string AppGameImgFolder { get => Path.Combine(AppGameFolder, "_img"); }
        public static string AppGameLogsFolder { get => Path.Combine(AppGameFolder, "_logs"); }
#if PORTABLE
        public static string AppConfigFile = Path.Combine(AppConfigFolder, "config.ini");
        public static string AppConfigFileOld = Path.Combine(AppDataFolder, "config.ini");
        public static string AppNotifIgnoreFile = Path.Combine(AppConfigFolder, "ignore_notif_ids.json");
#else
        public static string AppConfigFile = Path.Combine(AppDataFolder, "config.ini");
        public static string AppNotifIgnoreFile = Path.Combine(AppDataFolder, "ignore_notif_ids.json");
#endif
        public static string GamePathOnSteam;

        public const long AppDiscordApplicationID = 1089467141096484955;
        public const string AppNotifURLPrefix = "/notification_{0}.json";
        public const string AppGameConfigV2URLPrefix = "/metadata/metadatav2_{0}.json";
        public const string AppGameRepairIndexURLPrefix = "/metadata/repair_indexes/{0}/{1}/index";
        public const string AppGameRepoIndexURLPrefix = "/metadata/repair_indexes/{0}/repo";

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

#if !DISABLEDISCORD
        public static DiscordPresenceManager AppDiscordPresence;
#endif

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
        public static GameInstallStateEnum GameInstallationState = GameInstallStateEnum.NotInstalled;

        public static Dictionary<string, IniValue> AppSettingsTemplate = new Dictionary<string, IniValue>
        {
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
#if PORTABLE
            TryCopyOldConfig();
#endif

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

        public static bool IsConfigKeyExist(string key) => appIni.Profile[SectionName].ContainsKey(key);
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
                if (!appIni.Profile[SectionName].ContainsKey(Entry.Key) || string.IsNullOrEmpty(appIni.Profile[SectionName][Entry.Key].Value))
                {
                    SetAppConfigValue(Entry.Key, Entry.Value);
                }
            }
            if (GetAppConfigValue("DownloadThread").ToInt() > 8)
                SetAppConfigValue("DownloadThread", 8);
        }
    }
}
