using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

using Hi3Helper.Preset;

using static Hi3Helper.Preset.ConfigStore;

namespace CollapseLauncher
{
    public static class LauncherConfig
    {
        public static RegionBackgroundProp regionBackgroundProp = new RegionBackgroundProp();
        public static PresetConfigClasses CurrentRegion = new PresetConfigClasses();
        public static List<string> GameConfigName = new List<string>();

        public static string AppFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static string AppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static void LoadGamePreset()
        {
            if (!Directory.Exists(AppFolder))
                Directory.CreateDirectory(AppFolder);

            LoadConfigFromFile(Path.Combine("config", "fileconfig.json"));
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
