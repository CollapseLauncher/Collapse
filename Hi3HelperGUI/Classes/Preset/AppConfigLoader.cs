using System.Threading.Tasks;
using System.Windows.Threading;
using System.IO;
using Newtonsoft.Json;
using Hi3HelperGUI.Preset;

using static Hi3HelperGUI.Preset.ConfigStore;

namespace Hi3HelperGUI
{
    public partial class MainWindow
    {
        public void LoadAppConfig() => AppConfigData = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(Path.Combine("config", "appconfig.json")));

        public async void ApplyAppConfig() => await Task.Run(() =>
        {
            LoadAppConfig();
            Dispatcher.Invoke(() =>
            {
                ConfigEnableConsole.IsChecked = AppConfigData.ShowConsole;
            });

            if (AppConfigData.ShowConsole)
                ShowConsoleWindow();
            else
                HideConsoleWindow();

            Logger.InitLog();
        });

        public void SaveAppConfig()
        {
            Dispatcher.Invoke(() =>
            {
                AppConfigData.ShowConsole = ConfigEnableConsole.IsChecked ?? false;
            });
            File.WriteAllText(Path.Combine("config", "appconfig.json"), JsonConvert.SerializeObject(AppConfigData, Formatting.Indented).ToString());
        }
    }
}
