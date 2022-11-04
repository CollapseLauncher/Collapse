using ColorThiefDotNet;
using Hi3Helper;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Preset.ConfigV2Store;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        private BitmapImage BackgroundBitmap;
        private Bitmap PaletteBitmap;
        private bool BGLastState = true;
        private bool IsFirstStartup = true;

        private async Task FetchLauncherLocalizedResources()
        {
            regionBackgroundProp = CurrentConfigV2.LauncherSpriteURLMultiLang ?
                await TryGetMultiLangResourceProp() :
                await TryGetSingleLangResourceProp();

            await DownloadBackgroundImage();

            await GetLauncherAdvInfo();
            await GetLauncherCarouselInfo();
            await GetLauncherEventInfo();
            GetLauncherPostInfo();
        }

        private async Task ChangeBackgroundImageAsRegion()
        {
            bool IsCustomBG = GetAppConfigValue("UseCustomBG").ToBool();
            if (IsCustomBG)
            {
                string BGPath = GetAppConfigValue("CustomBGPath").ToString();
                if (string.IsNullOrEmpty(BGPath))
                    regionBackgroundProp.imgLocalPath = AppDefaultBG;
                else
                    regionBackgroundProp.imgLocalPath = BGPath;
            }

            if (!IsCustomBG || IsFirstStartup)
            {
                BackgroundImgChanger.ChangeBackground(regionBackgroundProp.imgLocalPath);
                await BackgroundImgChanger.WaitForBackgroundToLoad();
                IsFirstStartup = false;
            }

            ReloadPageTheme(ConvertAppThemeToElementTheme(CurrentAppTheme));
        }

        private async Task FetchLauncherResourceAsRegion()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                await Http.Download(CurrentConfigV2.LauncherResourceURL, memoryStream, null, null, default);
                memoryStream.Position = 0;
                regionResourceProp = (RegionResourceProp)JsonSerializer.Deserialize(memoryStream, typeof(RegionResourceProp), RegionResourcePropContext.Default);
            }
        }

        private async Task<RegionResourceProp> TryGetMultiLangResourceProp()
        {
            RegionResourceProp? ret = (RegionResourceProp?)await GetMultiLangResourceProp(Lang.LanguageID.ToLower());

            return ret.data.adv == null ? (RegionResourceProp?)await GetMultiLangResourceProp(CurrentConfigV2.LauncherSpriteURLMultiLangFallback ?? "en-us") : ret;
        }

        private async Task<object?> GetMultiLangResourceProp(string langID)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                await Http.Download(string.Format(CurrentConfigV2.LauncherSpriteURL, langID), memoryStream, null, null, default);
                memoryStream.Position = 0;
                return JsonSerializer.Deserialize(memoryStream, typeof(RegionResourceProp), RegionResourcePropContext.Default);
            }
        }

        private async Task<RegionResourceProp> TryGetSingleLangResourceProp()
        {
            RegionResourceProp ret = new RegionResourceProp();
            using (MemoryStream memoryStream = new MemoryStream())
            {
                await Http.Download(CurrentConfigV2.LauncherSpriteURL, memoryStream, null, null, default);
                memoryStream.Position = 0;
                ret = (RegionResourceProp)JsonSerializer.Deserialize(memoryStream, typeof(RegionResourceProp), RegionResourcePropContext.Default);
            }

            return ret;
        }

        private void ResetRegionProp()
        {
            regionNewsProp = new HomeMenuPanel()
            {
                sideMenuPanel = null,
                imageCarouselPanel = null,
                articlePanel = null,
                eventPanel = null
            };
        }

        private async Task GetLauncherAdvInfo()
        {
            if (regionBackgroundProp.data.icon.Count == 0) return;

            regionNewsProp.sideMenuPanel = new List<MenuPanelProp>();
            foreach (RegionSocMedProp item in regionBackgroundProp.data.icon)
                regionNewsProp.sideMenuPanel.Add(new MenuPanelProp
                {
                    URL = item.url,
                    Icon = await GetCachedSprites(item.img),
                    IconHover = await GetCachedSprites(item.img_hover),
                    QR = string.IsNullOrEmpty(item.qr_img) ? null : await GetCachedSprites(item.qr_img),
                    QR_Description = string.IsNullOrEmpty(item.qr_desc) ? null : item.qr_desc,
                    Description = string.IsNullOrEmpty(item.title) || CurrentConfigV2.IsHideSocMedDesc ? item.url : item.title
                });
        }

        private async Task GetLauncherCarouselInfo()
        {
            if (regionBackgroundProp.data.banner.Count == 0) return;

            regionNewsProp.imageCarouselPanel = new List<MenuPanelProp>();
            foreach (RegionSocMedProp item in regionBackgroundProp.data.banner)
                regionNewsProp.imageCarouselPanel.Add(new MenuPanelProp
                {
                    URL = item.url,
                    Icon = await GetCachedSprites(item.img),
                    Description = string.IsNullOrEmpty(item.name) ? item.url : item.name
                });
        }

        private async Task GetLauncherEventInfo()
        {
            if (string.IsNullOrEmpty(regionBackgroundProp.data.adv.icon)) return;

            regionNewsProp.eventPanel = new RegionBackgroundProp
            {
                url = regionBackgroundProp.data.adv.url,
                icon = await GetCachedSprites(regionBackgroundProp.data.adv.icon)
            };
        }

        private void GetLauncherPostInfo()
        {
            if (regionBackgroundProp.data.post.Count == 0) return;

            regionNewsProp.articlePanel = new PostCarouselTypes();
            foreach (RegionSocMedProp item in regionBackgroundProp.data.post)
                switch (item.type)
                {
                    case PostCarouselType.POST_TYPE_ACTIVITY:
                        regionNewsProp.articlePanel.Events.Add(item);
                        break;
                    case PostCarouselType.POST_TYPE_ANNOUNCE:
                        regionNewsProp.articlePanel.Notices.Add(item);
                        break;
                    case PostCarouselType.POST_TYPE_INFO:
                        regionNewsProp.articlePanel.Info.Add(item);
                        break;
                }
        }

        private async Task<string> GetCachedSprites(string URL, CancellationToken token = new CancellationToken())
        {
            string cacheFolder = Path.Combine(AppGameImgFolder, "cache");
            string cachePath = Path.Combine(cacheFolder, Path.GetFileNameWithoutExtension(URL));
            if (!Directory.Exists(cacheFolder))
                Directory.CreateDirectory(cacheFolder);

            if (!File.Exists(cachePath)) await Http.Download(URL, cachePath, true, null, null, token);

            return cachePath;
        }

        private async Task ApplyAccentColor()
        {
            switch (CurrentAppTheme)
            {
                case AppThemeMode.Light:
                    await SetLightColors();
                    break;
                case AppThemeMode.Dark:
                    await SetDarkColors();
                    break;
                default:
                    if (SystemAppTheme.ToString() == "#FFFFFFFF")
                        await SetLightColors();
                    else
                        await SetDarkColors();
                    break;
            }

            ReloadPageTheme(ConvertAppThemeToElementTheme(CurrentAppTheme));
        }

        private async Task SetLightColors()
        {
            Windows.UI.Color[] _colors = await GetPaletteList(4, true);
            Application.Current.Resources["SystemAccentColor"] = _colors[0];
            Application.Current.Resources["SystemAccentColorDark1"] = _colors[1];
            Application.Current.Resources["SystemAccentColorDark2"] = _colors[2];
            Application.Current.Resources["SystemAccentColorDark3"] = _colors[3];
        }

        private async Task SetDarkColors()
        {
            Windows.UI.Color[] _colors = await GetPaletteList(4, false);
            Application.Current.Resources["SystemAccentColor"] = _colors[0];
            Application.Current.Resources["SystemAccentColorLight1"] = _colors[1];
            Application.Current.Resources["SystemAccentColorLight2"] = _colors[2];
            Application.Current.Resources["SystemAccentColorLight3"] = _colors[3];
        }

        private async Task<Windows.UI.Color[]> GetPaletteList(int ColorCount = 4, bool IsLight = false)
        {
            byte DefVal = (byte)(IsLight ? 80 : 255);
            Windows.UI.Color[] output = new Windows.UI.Color[4];
            IEnumerable<QuantizedColor> Colors = await Task.Run(() => new ColorThief().GetPalette(PaletteBitmap, 10, 3));

            QuantizedColor Single = null;

            try
            {
                Single = Colors.Where(x => IsLight ? x.IsDark : !x.IsDark).FirstOrDefault();
            }
            catch
            {
                if (Single is null) Single = Colors.FirstOrDefault();
                if (Single is null) Single = new QuantizedColor(new CTColor { R = DefVal, G = DefVal, B = DefVal }, 1);
            }

            for (int i = 0; i < ColorCount; i++) output[i] = ColorThiefToColor(Single);

            return output;
        }

        private Windows.UI.Color ColorThiefToColor(QuantizedColor i) => new Windows.UI.Color { R = i.Color.R, G = i.Color.G, B = i.Color.B, A = 255 };

        private async Task GetResizedBitmap(IRandomAccessStream stream, uint ToWidth, uint ToHeight)
        {
            using (InMemoryRandomAccessStream BackgroundStream = new InMemoryRandomAccessStream())
            {
                using (stream)
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                    (uint, uint) ResizedSize = GetPreservedImageRatio(ToWidth, ToHeight, decoder.PixelWidth, decoder.PixelHeight);
                    BitmapTransform transform = new BitmapTransform()
                    {
                        ScaledWidth = ResizedSize.Item1,
                        ScaledHeight = ResizedSize.Item2,
                        InterpolationMode = BitmapInterpolationMode.Fant
                    };
                    PixelDataProvider pixelData = await decoder.GetPixelDataAsync(
                                                        BitmapPixelFormat.Rgba8,
                                                        BitmapAlphaMode.Straight,
                                                        transform,
                                                        ExifOrientationMode.RespectExifOrientation,
                                                        ColorManagementMode.DoNotColorManage);

                    if (decoder.PixelWidth != decoder.OrientedPixelWidth) FlipSize(ref ResizedSize);
                    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, BackgroundStream);
                    encoder.SetPixelData(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Straight, ResizedSize.Item1, ResizedSize.Item2, m_appDPIScale, m_appDPIScale, pixelData.DetachPixelData());

                    await encoder.FlushAsync();
                }

                PaletteBitmap = await Task.Run(() => Stream2Bitmap(BackgroundStream));
                BackgroundBitmap = await Stream2BitmapImage(BackgroundStream);
            }
        }

        private void FlipSize(ref (uint, uint) b)
        {
            (uint, uint) _b = b;
            b.Item1 = _b.Item2;
            b.Item2 = _b.Item1;
        }

        private async Task<BitmapImage> Stream2BitmapImage(IRandomAccessStream image)
        {
            BitmapImage ret = new BitmapImage();
            image.Seek(0);
            await ret.SetSourceAsync(image);
            return ret;
        }

        private Bitmap Stream2Bitmap(IRandomAccessStream image)
        {
            image.Seek(0);
            return new Bitmap(image.AsStream());
        }

        // Reference:
        // https://stackoverflow.com/questions/1940581/c-sharp-image-resizing-to-different-size-while-preserving-aspect-ratio
        private (uint, uint) GetPreservedImageRatio(uint canvasWidth, uint canvasHeight, uint imgWidth, uint imgHeight)
        {
            double ratioX = (double)canvasWidth / imgWidth;
            double ratioY = (double)canvasHeight / imgHeight;
            double ratio = ratioX < ratioY ? ratioX : ratioY;

            return ((uint)(imgWidth * ratio), (uint)(imgHeight * ratio));
        }

        private async Task DownloadBackgroundImage()
        {
            regionBackgroundProp.imgLocalPath = Path.Combine(AppGameImgFolder, "bg", Path.GetFileName(regionBackgroundProp.data.adv.background));
            SetAndSaveConfigValue("CurrentBackground", regionBackgroundProp.imgLocalPath);

            if (!Directory.Exists(Path.Combine(AppGameImgFolder, "bg")))
                Directory.CreateDirectory(Path.Combine(AppGameImgFolder, "bg"));

            FileInfo fI = new FileInfo(regionBackgroundProp.imgLocalPath);

            if (!fI.Exists)
            {
                await Http.Download(regionBackgroundProp.data.adv.background, regionBackgroundProp.imgLocalPath, 4, false, default);
                await Http.Merge();
            }
        }

        private async void ApplyBackgroundAsync() => await ApplyBackground();

        private async Task ApplyBackground()
        {
            BackgroundBackBuffer.Source = BackgroundBitmap;

            uint Width = (uint)((double)m_actualMainFrameSize.Width * 1.5 * m_appDPIScale);
            uint Height = (uint)((double)m_actualMainFrameSize.Height * 1.5 * m_appDPIScale);

            await GetResizedBitmap(new FileStream(regionBackgroundProp.imgLocalPath, FileMode.Open, FileAccess.Read).AsRandomAccessStream(), Width, Height);

            await ApplyAccentColor();

            FadeOutFrontBg();
            FadeOutBackBg();
        }

        private async void FadeOutFrontBg()
        {
            BackgroundFront.Source = BackgroundBitmap;
            BackgroundFrontBuffer.Visibility = Visibility.Visible;

            double dur = 0.125;
            Storyboard storyBufFront = new Storyboard();

            DoubleAnimation OpacityBufFront = new DoubleAnimation();
            OpacityBufFront.Duration = new Duration(TimeSpan.FromSeconds(dur));

            OpacityBufFront.From = 1; OpacityBufFront.To = 0;

            Storyboard.SetTarget(OpacityBufFront, BackgroundFrontBuffer);
            Storyboard.SetTargetProperty(OpacityBufFront, "Opacity");
            storyBufFront.Children.Add(OpacityBufFront);

            if (m_appCurrentFrameName == "HomePage")
            {
                storyBufFront.Begin();
            }

            await Task.Delay((int)(dur * 1000));
            BackgroundFrontBuffer.Visibility = Visibility.Collapsed;

            BackgroundFrontBuffer.Source = BackgroundBitmap;
        }

        private async void FadeOutBackBg()
        {
            BackgroundBack.Source = BackgroundBitmap;

            BackgroundBack.Opacity = 0;

            double dur = 0.125;
            Storyboard storyBufBack = new Storyboard();
            Storyboard storyBgBack = new Storyboard();

            DoubleAnimation OpacityBufBack = new DoubleAnimation();
            OpacityBufBack.Duration = new Duration(TimeSpan.FromSeconds(dur));
            DoubleAnimation OpacityBgBack = new DoubleAnimation();
            OpacityBgBack.Duration = new Duration(TimeSpan.FromSeconds(dur));

            OpacityBufBack.From = 1; OpacityBufBack.To = 0;
            OpacityBgBack.From = 0; OpacityBgBack.To = 1;

            Storyboard.SetTarget(OpacityBufBack, BackgroundBackBuffer);
            Storyboard.SetTargetProperty(OpacityBufBack, "Opacity");
            storyBufBack.Children.Add(OpacityBufBack);
            Storyboard.SetTarget(OpacityBgBack, BackgroundBack);
            Storyboard.SetTargetProperty(OpacityBgBack, "Opacity");
            storyBgBack.Children.Add(OpacityBgBack);

            storyBufBack.Begin();
            storyBgBack.Begin();

            await Task.Delay((int)(dur * 1000));
        }

        private void HideLoadingPopup(bool hide, string title, string subtitle)
        {
            Storyboard storyboard = new Storyboard();

            LoadingTitle.Text = title;
            LoadingSubtitle.Text = subtitle;

            if (hide)
            {
                LoadingRing.IsIndeterminate = false;

                DoubleAnimation OpacityAnimation = new DoubleAnimation();
                OpacityAnimation.From = 1;
                OpacityAnimation.To = 0;
                OpacityAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.25));

                Storyboard.SetTarget(OpacityAnimation, LoadingPopup);
                Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
                storyboard.Children.Add(OpacityAnimation);

                storyboard.Begin();
                LoadingPopup.Visibility = Visibility.Collapsed;
            }
            else
            {
                LoadingFooter.Text = "";
                LoadingRing.IsIndeterminate = true;

                LoadingPopup.Visibility = Visibility.Visible;

                DoubleAnimation OpacityAnimation = new DoubleAnimation();
                OpacityAnimation.From = 0;
                OpacityAnimation.To = 1;
                OpacityAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.25));

                Storyboard.SetTarget(OpacityAnimation, LoadingPopup);
                Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
                storyboard.Children.Add(OpacityAnimation);

                storyboard.Begin();
            }
        }


        private void HideBackgroundImage(bool hideImage = true, bool absoluteTransparent = true)
        {
            Storyboard storyboardFront = new Storyboard();
            Storyboard storyboardBack = new Storyboard();

            if (!(hideImage && BackgroundFront.Opacity == 0))
            {
                DoubleAnimation OpacityAnimation = new DoubleAnimation();
                OpacityAnimation.From = hideImage ? 1 : 0;
                OpacityAnimation.To = hideImage ? 0 : 1;
                OpacityAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.25));

                DoubleAnimation OpacityAnimationBack = new DoubleAnimation();
                OpacityAnimationBack.From = hideImage ? 1 : 0.4;
                OpacityAnimationBack.To = hideImage ? 0.4 : 1;
                OpacityAnimationBack.Duration = new Duration(TimeSpan.FromSeconds(0.25));

                Storyboard.SetTarget(OpacityAnimation, BackgroundFront);
                Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
                storyboardFront.Children.Add(OpacityAnimation);

                Storyboard.SetTarget(OpacityAnimationBack, Background);
                Storyboard.SetTargetProperty(OpacityAnimationBack, "Opacity");
                storyboardBack.Children.Add(OpacityAnimationBack);
            }

            if (BGLastState != hideImage)
            {
                storyboardFront.Begin();
                storyboardBack.Begin();
                BGLastState = hideImage;
            }
        }
    }
}