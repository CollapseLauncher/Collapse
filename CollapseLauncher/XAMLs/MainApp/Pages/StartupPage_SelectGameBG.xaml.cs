using Hi3Helper.Http;
using Hi3Helper.Preset;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using CommunityToolkit.WinUI.UI;
using Hi3Helper.Shared.ClassStruct;
using static CollapseLauncher.Pages.StartupPage_SelectGameBGProp;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static CollapseLauncher.InnerLauncherConfig;

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

            if (!IsDarkTheme)
            {
                (this.Resources["DetailsLogoShadowController"] as AttachedDropShadow).Opacity = 0.25;
                (this.Resources["DetailsTextShadowController"] as AttachedDropShadow).Opacity = 0.25;
            }

            if (!IsLoadDescription)
            {
                GameDetails.Visibility = Visibility.Collapsed;
                GameDetailsDummy.Visibility = Visibility.Visible;

                return;
            }

            this.GameDetailsDescription.Text = _gameDescription;
            this.GameDetailsPoster.Source = _gamePosterBitmapImage;
            this.GameDetailsLogo.Source = _gameLogoBitmapImage;
        }

        private bool IsDarkTheme { get => CurrentAppTheme == AppThemeMode.Light
                ? false : CurrentAppTheme == AppThemeMode.Default && SystemAppTheme.ToString() == "#FFFFFFFF"
                ? false : true; }
    }

    public static class StartupPage_SelectGameBGProp
    {
        public static string _gameDescription;
        public static string _gamePosterPath;
        public static string _gameLogoPath;
        public static Bitmap _gamePosterBitmap;
        public static BitmapImage _gamePosterBitmapImage;
        public static BitmapImage _gameLogoBitmapImage;
        public static bool IsLoadDescription = false;
        public static bool IsNeedLoad = false;

        public static async Task TryLoadGameDetails(PresetConfigV2 config = null)
        {
            if (!(IsLoadDescription = !(config is null
                                     || config.ZoneDescription is null
                                     || config.ZonePosterURL is null
                                     || config.ZoneLogoURL is null)))
            {
                _gamePosterBitmap = null;
                _gamePosterBitmapImage = null;
                _gameLogoBitmapImage = null;
                _gameDescription = null;
                _gamePosterPath = null;
                _gameLogoPath = null;
                return;
            }

            _gameDescription = config.ZoneDescription;
            _gamePosterPath = await GetCachedSprites(config.ZonePosterURL);
            _gameLogoPath = await GetCachedSprites(config.ZoneLogoURL);

            using (IRandomAccessStream fs1 = new FileStream(_gamePosterPath, FileMode.Open, FileAccess.Read, FileShare.Read).AsRandomAccessStream())
            {
                using (IRandomAccessStream fs2 = new FileStream(_gameLogoPath, FileMode.Open, FileAccess.Read, FileShare.Read).AsRandomAccessStream())
                {
                    _gamePosterBitmapImage = await MainPage.Stream2BitmapImage(fs1);
                    _gameLogoBitmapImage = await MainPage.Stream2BitmapImage(fs2);

                    fs1.Seek(0);
                    _gamePosterBitmap = MainPage.Stream2Bitmap(fs1);
                }
            }
        }

        private static async Task<string> GetCachedSprites(string URL)
        {
            Http _client = new Http();
            string cacheFolder = Path.Combine(AppGameImgFolder, "cache");
            string cachePath = Path.Combine(cacheFolder, Path.GetFileNameWithoutExtension(URL));
            if (!Directory.Exists(cacheFolder))
                Directory.CreateDirectory(cacheFolder);

            if (!File.Exists(cachePath)) await _client.Download(URL, cachePath, true, null, null, new CancellationToken());

            return cachePath;
        }
    }
}
