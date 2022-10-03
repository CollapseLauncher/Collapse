using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Preset.ConfigV2Store;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class StartupPage_SelectGame : Page
    {
        public StartupPage_SelectGame()
        {
            this.InitializeComponent();
            LoadConfigV2();
            GameCategorySelect.ItemsSource = ConfigV2GameCategory;
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            // Set and Save CurrentRegion in AppConfig
            SetAndSaveConfigValue("GameCategory", (string)GameCategorySelect.SelectedValue);
            SetAndSaveConfigValue("GameRegion", GetComboBoxGameRegionValue(GameRegionSelect.SelectedValue));

            (m_window as MainWindow).rootFrame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            (m_window as MainWindow).rootFrame.GoBack();
        }

        private void GameSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            NextPage.IsEnabled = true;
        }

        private void GameCategorySelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string value = (string)((ComboBox)sender).SelectedValue;
            GetConfigV2Regions(value);
            GameRegionSelect.ItemsSource = BuildGameRegionListUI(value);
            GameRegionSelect.IsEnabled = true;
            NextPage.IsEnabled = false;
        }
    }
}
