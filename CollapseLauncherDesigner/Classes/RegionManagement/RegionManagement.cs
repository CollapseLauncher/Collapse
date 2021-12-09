using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

using Hi3Helper.Preset;
using Hi3Helper.Data;

using static Hi3Helper.Logger;
using static CollapseLauncher.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        HttpClientTool httpClient;
        public async Task LoadRegion(int regionIndex = 0)
        {
            CurrentRegion = ConfigStore.Config[regionIndex];
            await Task.Run(() => ChangeBackgroundImageAsRegion());
        }

        public async void ChangeRegion(object sender, RoutedEventArgs e)
        {
            ChangeRegionConfirmProgressBar.Visibility = Visibility.Visible;
            await LoadRegion(ComboBoxGameRegion.SelectedIndex);
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
    }
}
