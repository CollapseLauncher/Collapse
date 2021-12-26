using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;

using Windows.UI.ViewManagement;
using Windows.Graphics.Display;

using Hi3Helper.Preset;
using Hi3Helper.Data;
using Hi3Helper.Screen;

using static Hi3Helper.Preset.ConfigStore;

namespace CollapseLauncher
{
    public struct AppIniStruct
    {
        public IniFile Profile;
        public Stream ProfileStream;
        public string ProfilePath;
    }

    public enum GameInstallStateEnum
    {
        Installed = 0,
        InstalledHavePreload = 1,
        NotInstalled = 2,
        NeedsUpdate = 3,
        GameBroken = 4,
    }

    public static class LauncherConfig
    {
        public static AppWindow _apw;
        public static OverlappedPresenter _presenter;

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
            foreach (ScreenResolution res in ScreenProp.screenResolutions)
                ScreenResolutionsList.Add(res.ToString());
        }

        public static void LoadAppPreset()
        {
            ScreenProp.InitScreenResolution();
            GetCurrentScreenResolution();
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

        private static void GetCurrentScreenResolution()
        {
            var bounds = ApplicationView.GetForCurrentView().VisibleBounds;
            var scaleFactor = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
            var size = new Size((int)((double)bounds.Width * scaleFactor), (int)((double)bounds.Height * scaleFactor));
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
                { "CurrentBackground", new IniValue(@"Assets\BG\default_bg.png") },
                { "DownloadThread", new IniValue(16) },
                { "GameFolder", new IniValue(AppGameFolder) }
            });

            SaveAppConfig();
        }
    }
    
    public partial class MainPage : Page
    {
        public void LoadConfig()
        {
            ComboBoxGameRegion.ItemsSource = LauncherConfig.GameConfigName;
        }
    }

}
