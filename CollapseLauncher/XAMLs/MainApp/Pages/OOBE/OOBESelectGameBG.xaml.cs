using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.Helper.Background;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Plugins;
using CommunityToolkit.WinUI;
using Hi3Helper;
using Hi3Helper.Win32.WinRT.WindowsStream;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.Pages.OOBE.OOBESelectGameBGProp;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.Pages.OOBE
{
    public sealed partial class OOBESelectGameBG
    {
        internal static bool IsUseBackgroundMask;

        public OOBESelectGameBG()
        {
            Loaded += LoadedRoutine;
        }

        private void LoadedRoutine(object sender, RoutedEventArgs e)
        {
            InitializeComponent();

            BackgroundMask.Visibility = IsUseBackgroundMask ? Visibility.Visible : Visibility.Collapsed;
            if (IsAppThemeLight && Resources["DetailsLogoShadowController"] is AttachedDropShadow attachedShadow)
            {
                attachedShadow.Opacity = 0.75;
            }

            if (!IsLoadDescription || !IsSuccess)
            {
                GameDetails.Visibility = Visibility.Collapsed;
                GameDetailsDummy.Visibility = Visibility.Visible;

                return;
            }

            GameDetailsDescription.Text = _gameDescription;
            GameDetailsPoster.Source = _gamePosterBitmapImage;
            GameDetailsLogo.Source = _gameLogoBitmapImage;
        }

        private void GameDetailsHomepage_Click(object sender, RoutedEventArgs e)
        {
            string  link = (string)((Button)sender).Tag;
            Process proc = new() { StartInfo = new ProcessStartInfo(link) { UseShellExecute = true } };

            proc.Start();
        }

        internal static string? GameDetailsHomepageLink => _gameHomepageLink;
    }

    public static class OOBESelectGameBGProp
    {
        internal static string?      _gameDescription;
        internal static string?      _gamePosterPath;
        private static  string?      _gameLogoPath;
        internal static string?      _gameHomepageLink;
        internal static BitmapImage? _gamePosterBitmapImage;
        internal static BitmapImage? _gameLogoBitmapImage;
        internal static bool         IsLoadDescription;
        internal static bool         IsSuccess;

        private static string DecideOtherPosterPath(PresetConfig config)
        {
            return config is not PluginPresetConfigWrapper { ZonePosterURL: { } pluginZonePoster }
                ? ImageBackgroundManager.GetRandomPlaceholderImage()
                : pluginZonePoster;
        }

        internal static async Task<bool> TryLoadGameDetails(PresetConfig? config)
        {
            IsLoadDescription = false;

            try
            {
                if (config?.ZoneDescription == null ||
                    config.ZonePosterURL == null ||
                    config.ZoneLogoURL == null ||
                    config.ZoneURL == null)
                {
                    _gamePosterBitmapImage = null;
                    _gameLogoBitmapImage   = null;
                    _gameDescription       = null;
                    _gamePosterPath        = null;
                    _gameLogoPath          = null;
                    _gameHomepageLink      = null;
                    return IsSuccess = true;
                }

                _gameHomepageLink = config.ZoneURL;
                _gameDescription  = config.ZoneDescription;

                // -- Use background mask for plugin games.
                OOBESelectGameBG.IsUseBackgroundMask = config is PluginPresetConfigWrapper;

                _gamePosterPath = config.GameType switch
                                {
                                    GameNameType.Honkai => Path.Combine(AppExecutableDir,   @"Assets\Images\GamePoster\poster_honkai.png"),
                                    GameNameType.Genshin => Path.Combine(AppExecutableDir,  @"Assets\Images\GamePoster\poster_genshin.png"),
                                    GameNameType.StarRail => Path.Combine(AppExecutableDir, @"Assets\Images\GamePoster\poster_starrail.png"),
                                    GameNameType.Zenless => Path.Combine(AppExecutableDir,  @"Assets\Images\GamePoster\poster_zzz.png"),
                                    _ => DecideOtherPosterPath(config)
                                };
                _gameLogoPath = config.ZoneLogoURL;

                // -- Load poster images.
                _gamePosterPath = await ImageBackgroundManager.GetLocalOrDownloadedFilePath(_gamePosterPath, CancellationToken.None);
                (_gamePosterPath, _) = await ImageBackgroundManager.GetNativeOrDecodedImagePath(_gamePosterPath, CancellationToken.None);
                _gamePosterBitmapImage = new BitmapImage(new Uri(_gamePosterPath));

                if (!Path.IsPathFullyQualified(_gameLogoPath))
                {
                    _gameLogoPath = FallbackCDNUtil.TryGetAbsoluteToRelativeCDNURL(config.ZoneLogoURL, "metadata/");
                }

                // -- Load logo images.
                _gameLogoPath = await ImageBackgroundManager.GetLocalOrDownloadedFilePath(_gameLogoPath, CancellationToken.None);
                (_gameLogoPath, _) = await ImageBackgroundManager.GetNativeOrDecodedImagePath(_gameLogoPath, CancellationToken.None);
                _gameLogoBitmapImage = new BitmapImage(new Uri(_gameLogoPath));
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while loading poster image!\r\n{ex}", LogType.Error, true);
                return IsSuccess = false;
            }

            IsLoadDescription = true;
            return IsSuccess = true;
        }
    }
}
