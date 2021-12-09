using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;

using Hi3Helper.Preset;

using static Hi3Helper.Preset.ConfigStore;

namespace CollapseLauncher
{
    public static class LauncherConfig
    {
        public static AppWindow _apw;
        public static OverlappedPresenter _presenter;

        public static RegionBackgroundProp regionBackgroundProp = new RegionBackgroundProp();
        public static RegionResourceProp regionResourceProp = new RegionResourceProp();
        public static PresetConfigClasses CurrentRegion = new PresetConfigClasses();
        public static List<string> GameConfigName = new List<string>();

        public static string AppFolder = AppDomain.CurrentDomain.BaseDirectory;
        public static string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "CollapseLauncher");
        public static string AppGameFolder = Path.Combine(AppDataFolder, "GameFolder");
        public static void LoadGamePreset()
        {
            if (!Directory.Exists(AppDataFolder))
                Directory.CreateDirectory(AppDataFolder);

            LoadConfigFromFile(Path.Combine(AppFolder, "config", "fileconfig.json"));
            GameConfigName = Config.Select(x => x.ZoneName).ToList();
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
