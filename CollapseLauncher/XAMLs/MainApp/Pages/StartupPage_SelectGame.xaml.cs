using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.UI;
using static CollapseLauncher.FileDialogNative;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Preset.ConfigStore;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class StartupPage_SelectGame : Page
    {
        bool AbortTransition = false;
        public StartupPage_SelectGame()
        {
            AbortTransition = false;
            this.InitializeComponent();
            LoadConfigTemplate();
            GameSelect.ItemsSource = GameConfigName;
        }
        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            (m_window as MainWindow).rootFrame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            (m_window as MainWindow).rootFrame.GoBack();
        }

        private void GameSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Set CurrentRegion in AppConfig
            SetAndSaveConfigValue("CurrentRegion", (sender as ComboBox).SelectedIndex);
            NextPage.IsEnabled = true;
        }
    }
}
