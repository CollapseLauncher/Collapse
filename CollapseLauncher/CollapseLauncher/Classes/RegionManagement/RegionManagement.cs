using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Hi3Helper.Preset;
using Hi3Helper.Data;

using static Hi3Helper.Logger;
using static Hi3Helper.Data.ConverterTool;
using static CollapseLauncher.LauncherConfig;
using static CollapseLauncher.Region.InstallationManagement;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        HttpClientTool httpClient;

        public async Task LoadRegion(int regionIndex = 0)
        {
            CurrentRegion = ConfigStore.Config[regionIndex];
            LogWriteLine($"Initializing Region {CurrentRegion.ZoneName}...");
            LoadGameRegionFile();
            await Task.Run(() =>
            {
                FetchLauncherResourceAsRegion();
                ChangeBackgroundImageAsRegion();
            });
        }

        private void LoadGameRegionFile()
        {
            gameIni = new GameIniStruct();

            gamePath = Path.Combine(AppGameFolder, CurrentRegion.ProfileName);
            DispatcherQueue.TryEnqueue(() => ComboBoxGameRegion.PlaceholderText = CurrentRegion.ZoneName);
            gameIni.ProfilePath = Path.Combine(gamePath, $"config.ini");

            if (!Directory.Exists(gamePath))
                Directory.CreateDirectory(gamePath);

            if (!File.Exists(gameIni.ProfilePath))
                PrepareInstallation();

            gameIni.Profile = new IniFile();
            gameIni.ProfileStream = new FileStream(gameIni.ProfilePath, FileMode.Open, FileAccess.ReadWrite);
            gameIni.Profile.Load(gameIni.ProfileStream);

            // CurrentRegion.CheckExistingGame();
        }

        public async void ChangeRegion(object sender, RoutedEventArgs e)
        {
            ChangeRegionConfirmProgressBar.Visibility = Visibility.Visible;
            appIni.Profile["app"]["CurrentRegion"] = ComboBoxGameRegion.SelectedIndex;
            SaveAppConfig();
            await InvokeLoadRegion(ComboBoxGameRegion.SelectedIndex);
            if (ChangeRegionConfirmBtn.Flyout is Flyout f)
            {
                LauncherFrame.Navigate(typeof(Pages.HomePage));

                ChangeRegionConfirmProgressBar.Visibility = Visibility.Collapsed;
                Thread.Sleep(1000);
                f.Hide();
                ChangeRegionConfirmBtn.IsEnabled = false;
                LogWriteLine($"Region changed to {ComboBoxGameRegion.SelectedValue}", Hi3Helper.LogType.Scheme);
            }
        }

        private async Task InvokeLoadRegion(int index)
        {
            await LoadRegion(index);
        }
    }
}
