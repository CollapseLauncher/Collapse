using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading.Tasks;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.Pages.StartupPage_SelectGameBGProp;
using static Hi3Helper.Preset.ConfigV2Store;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class StartupPage_SelectGame : Page
    {
        private string _selectedCategory { get; set; }
        private string _selectedRegion { get; set; }

        public StartupPage_SelectGame()
        {
            this.InitializeComponent();
            LoadConfigV2();
            GameCategorySelect.ItemsSource = ConfigV2GameCategory;
            BackgroundFrame.Navigate(typeof(StartupPage_SelectGameBG));
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            // Set and Save CurrentRegion in AppConfig
            SetAppConfigValue("GameCategory", (string)GameCategorySelect.SelectedValue);
            SetPreviousGameRegion((string)GameCategorySelect.SelectedValue, GetComboBoxGameRegionValue(GameRegionSelect.SelectedValue), false);
            SaveAppConfig();

            (m_window as MainWindow).rootFrame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            (m_window as MainWindow).rootFrame.GoBack();
        }

        private string lastSelectedCategory = "";
        private async void GameSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            object value = ((ComboBox)sender).SelectedValue;
            if (value is not null)
            {
                _selectedRegion = GetComboBoxGameRegionValue(value);

                NextPage.IsEnabled = true;

                BarBGLoading.Visibility = Visibility.Visible;
                BarBGLoading.IsIndeterminate = true;
                FadeBackground(1, 0.25);
                bool IsSuccess = await TryLoadGameDetails(ConfigV2.MetadataV2[_selectedCategory][_selectedRegion]);

                if (_gamePosterBitmap is not null && IsSuccess)
                {
                    await MainPage.ApplyAccentColor(this, _gamePosterBitmap, 1);
                    MainPage.ReloadPageTheme(this, MainPage.ConvertAppThemeToElementTheme(CurrentAppTheme));
                }

                NavigationTransitionInfo transition = lastSelectedCategory == _selectedCategory ? new SuppressNavigationTransitionInfo() : new DrillInNavigationTransitionInfo();

                this.BackgroundFrame.Navigate(typeof(StartupPage_SelectGameBG), null, transition);
                FadeBackground(0.25, 1);
                BarBGLoading.IsIndeterminate = false;
                BarBGLoading.Visibility = Visibility.Collapsed;

                lastSelectedCategory = _selectedCategory;

                return;
            }
            else
            {
                NextPage.IsEnabled = true;
                return;
            }
        }

        private async void FadeBackground(double from, double to)
        {
            double dur = 0.250;
            Storyboard storyBufBack = new Storyboard();

            DoubleAnimation OpacityBufBack = new DoubleAnimation();
            OpacityBufBack.Duration = new Duration(TimeSpan.FromSeconds(dur));

            OpacityBufBack.From = from; OpacityBufBack.To = to;

            Storyboard.SetTarget(OpacityBufBack, BackgroundFrame);
            Storyboard.SetTargetProperty(OpacityBufBack, "Opacity");
            storyBufBack.Children.Add(OpacityBufBack);

            storyBufBack.Begin();

            await Task.Delay((int)(dur * 1000));
        }

        private void GameCategorySelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedCategory = (string)((ComboBox)sender).SelectedValue;
            GetConfigV2Regions(_selectedCategory);
            GameRegionSelect.ItemsSource = BuildGameRegionListUI(_selectedCategory);
            GameRegionSelect.IsEnabled = true;
            NextPage.IsEnabled = false;
        }
    }
}
