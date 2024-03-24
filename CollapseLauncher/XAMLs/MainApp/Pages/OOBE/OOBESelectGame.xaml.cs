using CollapseLauncher.Helper.Background;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading.Tasks;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.Pages.OOBE.OOBESelectGameBGProp;
using static Hi3Helper.Preset.ConfigV2Store;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages.OOBE
{
    public sealed partial class OOBESelectGame : Page
    {
        private string _selectedCategory { get; set; }
        private string _selectedRegion { get; set; }

        public OOBESelectGame()
        {
            this.InitializeComponent();
            LoadConfigV2();
            GameCategorySelect.ItemsSource = BuildGameTitleListUI();
            BackgroundFrame.Navigate(typeof(OOBESelectGameBG));
            this.RequestedTheme = IsAppThemeLight ? ElementTheme.Light : ElementTheme.Dark;
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            // Set and Save CurrentRegion in AppConfig
            string categorySelected = GetComboBoxGameRegionValue(GameCategorySelect.SelectedValue);
            SetAppConfigValue("GameCategory", categorySelected);
            SetPreviousGameRegion(categorySelected, GetComboBoxGameRegionValue(GameRegionSelect.SelectedValue), false);
            SaveAppConfig();

            (m_window as MainWindow).rootFrame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromBottom });
            MainWindow.ToggleAcrylic();
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            OOBEStartUpMenu.thisCurrent.OverlayFrameGoBack();
            // (m_window as MainWindow).rootFrame.GoBack();
        }

        private string lastSelectedCategory = "";
        private async void GameSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            object value = ((ComboBox)sender).SelectedValue;
            if (value is not null)
            {
                _selectedRegion = GetComboBoxGameRegionValue(value);

                NextPage.IsEnabled = true;
                NextPage.Opacity = 1;

                BarBGLoading.Visibility = Visibility.Visible;
                BarBGLoading.IsIndeterminate = true;
                FadeBackground(1, 0.25);
                bool IsSuccess = await TryLoadGameDetails(ConfigV2.MetadataV2[_selectedCategory][_selectedRegion]);

                if (_gamePosterBitmap != null && IsSuccess)
                    await ColorPaletteUtility.ApplyAccentColor(this, _gamePosterBitmap, _gamePosterPath);

                NavigationTransitionInfo transition = lastSelectedCategory == _selectedCategory ? new SuppressNavigationTransitionInfo() : new DrillInNavigationTransitionInfo();

                this.BackgroundFrame.Navigate(typeof(OOBESelectGameBG), null, transition);
                FadeBackground(0.25, 1);
                BarBGLoading.IsIndeterminate = false;
                BarBGLoading.Visibility = Visibility.Collapsed;

                lastSelectedCategory = _selectedCategory;

                return;
            }
            else
            {
                NextPage.IsEnabled = true;
                NextPage.Opacity = 1;
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
            _selectedCategory = GetComboBoxGameRegionValue(((ComboBox)sender).SelectedValue);
            GetConfigV2Regions(_selectedCategory);
            GameRegionSelect.ItemsSource = BuildGameRegionListUI(_selectedCategory);
            GameRegionSelect.IsEnabled = true;
            NextPage.IsEnabled = false;
            NextPage.Opacity = 0;
        }
    }
}
