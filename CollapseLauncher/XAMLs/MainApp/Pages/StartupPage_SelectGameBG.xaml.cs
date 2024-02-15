using CollapseLauncher.Helper.Image;
using CommunityToolkit.WinUI;
using Hi3Helper;
using Hi3Helper.Preset;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.Pages.StartupPage_SelectGameBGProp;
using static Hi3Helper.Logger;

namespace CollapseLauncher.Pages
{
    public sealed partial class StartupPage_SelectGameBG : Page
    {
        public StartupPage_SelectGameBG()
        {
            this.Loaded += LoadedRoutine;
        }

        private void LoadedRoutine(object sender, RoutedEventArgs e)
        {
            this.InitializeComponent();

            if (IsAppThemeLight)
                (this.Resources["DetailsLogoShadowController"] as AttachedDropShadow).Opacity = 0.25;

            if (!IsLoadDescription || !IsSuccess)
            {
                GameDetails.Visibility = Visibility.Collapsed;
                GameDetailsDummy.Visibility = Visibility.Visible;

                return;
            }

            this.GameDetailsDescription.Text = _gameDescription;
            this.GameDetailsPoster.Source = _gamePosterBitmapImage;
            this.GameDetailsLogo.Source = _gameLogoBitmapImage;
        }

        private void GameDetailsHomepage_Click(object sender, RoutedEventArgs e)
        {
            string link = (string)((Button)sender).Tag;
            Process proc = new Process() { StartInfo = new ProcessStartInfo(link) { UseShellExecute = true } };

            proc.Start();
        }

        private string GameDetailsHomepageLink { get => _gameHomepageLink; }
    }

    public static class StartupPage_SelectGameBGProp
    {
        public static string _gameDescription;
        public static string _gamePosterPath;
        public static string _gameLogoPath;
        public static string _gameHomepageLink;
        public static Bitmap _gamePosterBitmap;
        public static BitmapImage _gamePosterBitmapImage;
        public static BitmapImage _gameLogoBitmapImage;
        public static bool IsLoadDescription = false;
        public static bool IsNeedLoad = false;
        public static bool IsSuccess = false;

        public static async Task<bool> TryLoadGameDetails(PresetConfigV2 config = null)
        {
            try
            {
                if (!(IsLoadDescription = !(config is null
                                         || config.ZoneDescription is null
                                         || config.ZonePosterURL is null
                                         || config.ZoneLogoURL is null
                                         || config.ZoneURL is null)))
                {
                    _gamePosterBitmap = null;
                    _gamePosterBitmapImage = null;
                    _gameLogoBitmapImage = null;
                    _gameDescription = null;
                    _gamePosterPath = null;
                    _gameLogoPath = null;
                    _gameHomepageLink = null;
                    return IsSuccess = true;
                }

                _gameHomepageLink = config.ZoneURL;
                _gameDescription = config.ZoneDescription;

                // TODO: Use FallbackCDNUtil to get the sprites
                _gamePosterPath = await MainPage.GetCachedSpritesAsync(config.ZonePosterURL, default);
                _gameLogoPath = await MainPage.GetCachedSpritesAsync(config.ZoneLogoURL, default);

                using (IRandomAccessStream fs2 = new FileStream(_gameLogoPath, FileMode.Open, FileAccess.Read, FileShare.Read).AsRandomAccessStream())
                {
                    uint Width = (uint)((double)m_actualMainFrameSize.Width * m_appDPIScale);
                    uint Height = (uint)((double)m_actualMainFrameSize.Height * m_appDPIScale);

                    _gameLogoBitmapImage = await ImageLoaderHelper.Stream2BitmapImage(fs2);
                    (_gamePosterBitmap, _gamePosterBitmapImage) = await ImageLoaderHelper.GetResizedBitmapNew(_gamePosterPath, Width, Height);
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while loading poster image!\r\n{ex}", LogType.Error, true);
                return IsSuccess = false;
            }

            return IsSuccess = true;
        }
    }
}
