using ColorThiefDotNet;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Hi3Helper.Http;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        private BitmapImage BackgroundBitmap;
        private Bitmap ThumbnailBitmap;
        private Stream ThumbnailStream;
        private readonly Size ThumbnailSize = new Size(32, 32);
        private async Task ChangeBackgroundImageAsRegion(CancellationToken token)
        {
            regionBackgroundProp = CurrentRegion.LauncherSpriteURLMultiLang ?
                await TryGetMultiLangResourceProp(token) :
                await TryGetSingleLangResourceProp(token);

            regionBackgroundProp.imgLocalPath = Path.Combine(AppGameImgFolder, "bg", Path.GetFileName(regionBackgroundProp.data.adv.background));
            SetAndSaveConfigValue("CurrentBackground", regionBackgroundProp.imgLocalPath);

            await DownloadBackgroundImage();

            if (GetAppConfigValue("UseCustomBG").ToBool())
            {
                string BGPath = GetAppConfigValue("CustomBGPath").ToString();
                if (string.IsNullOrEmpty(BGPath))
                    regionBackgroundProp.imgLocalPath = AppDefaultBG;
                else
                    regionBackgroundProp.imgLocalPath = BGPath;
            }

            BackgroundImgChanger.ChangeBackground(regionBackgroundProp.imgLocalPath);
            await BackgroundImgChanger.WaitForBackgroundToLoad();

            await GetLauncherAdvInfo(token);
            await GetLauncherCarouselInfo(token);
            await GetLauncherEventInfo(token);
            GetLauncherPostInfo();

            ReloadPageTheme(ConvertAppThemeToElementTheme(CurrentAppTheme));
        }

        bool PassFirstTry = false;
        public async Task<RegionResourceProp> TryGetMultiLangResourceProp(CancellationToken token)
        {
            RegionResourceProp ret = new RegionResourceProp();
            bool NoData = true;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                if (!PassFirstTry)
                {
                    await httpHelper.DownloadStream(string.Format(CurrentRegion.LauncherSpriteURL, Lang.LanguageID.ToLower()), memoryStream, token);
                    ret = JsonConvert.DeserializeObject<RegionResourceProp>(Encoding.UTF8.GetString(memoryStream.GetBuffer()));

                    NoData = ret.data.adv == null;
                }

                if (NoData)
                {
                    PassFirstTry = true;
                    await httpHelper.DownloadStream(string.Format(CurrentRegion.LauncherSpriteURL, CurrentRegion.LauncherSpriteURLMultiLangFallback), memoryStream, token);
                    ret = JsonConvert.DeserializeObject<RegionResourceProp>(Encoding.UTF8.GetString(memoryStream.GetBuffer()));
                }
            }

            PassFirstTry = false;

            return ret;
        }

        public async Task<RegionResourceProp> TryGetSingleLangResourceProp(CancellationToken token)
        {
            RegionResourceProp ret = new RegionResourceProp();
            using (MemoryStream memoryStream = new MemoryStream())
            {
                await httpHelper.DownloadStream(CurrentRegion.LauncherSpriteURL, memoryStream, token);
                ret = JsonConvert.DeserializeObject<RegionResourceProp>(Encoding.UTF8.GetString(memoryStream.GetBuffer()));
            }

            return ret;
        }

        public void ResetRegionProp()
        {
            regionNewsProp.sideMenuPanel = null;
            regionNewsProp.imageCarouselPanel = null;
            regionNewsProp.articlePanel = null;
            regionNewsProp.eventPanel = null;
        }

        public async Task GetLauncherAdvInfo(CancellationToken token)
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
                    Description = string.IsNullOrEmpty(item.title) || CurrentRegion.IsHideSocMedDesc ? item.url : item.title
                });
        }

        public async Task GetLauncherCarouselInfo(CancellationToken token)
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

        public async Task GetLauncherEventInfo(CancellationToken token)
        {
            if (string.IsNullOrEmpty(regionBackgroundProp.data.adv.icon)) return;

            regionNewsProp.eventPanel = new RegionBackgroundProp
            {
                url = regionBackgroundProp.data.adv.url,
                icon = await GetCachedSprites(regionBackgroundProp.data.adv.icon)
            };
        }

        public void GetLauncherPostInfo()
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

        public async Task<string> GetCachedSprites(string URL, CancellationToken token = new CancellationToken())
        {
            string cacheFolder = Path.Combine(AppGameImgFolder, "cache");
            string cachePath = Path.Combine(cacheFolder, Path.GetFileNameWithoutExtension(URL));
            if (!Directory.Exists(cacheFolder))
                Directory.CreateDirectory(cacheFolder);

            if (!File.Exists(cachePath)) await httpHelper.Download(URL, cachePath, token);

            return cachePath;
        }

        private void ApplyAccentColor()
        {
            Windows.UI.Color _color = new Windows.UI.Color();
            DispatcherQueue.TryEnqueue(() =>
            {
                if (CurrentAppTheme == AppThemeMode.Light ||
                    (CurrentAppTheme == AppThemeMode.Default && SystemAppTheme.ToString() == "#FFFFFFFF"))
                {
                    try
                    {
                        _color = ColorThiefToColor(GetColorFromPaletteByTheme(2, false));
                    }
                    catch
                    {
                        try
                        {
                            _color = ColorThiefToColor(GetColorFromPaletteByTheme(1, false));
                        }
                        catch
                        {
                            try
                            {
                                _color = ColorThiefToColor(GetColorFromPaletteByThemeLow(0, false));
                            }
                            catch
                            {
                                _color = new Windows.UI.Color { R = 0, G = 0, B = 0, A = 0 };
                            }
                        }
                    }

                    Application.Current.Resources["SystemAccentColor"] = _color;
                    Application.Current.Resources["SystemAccentColorDark1"] = _color;
                    Application.Current.Resources["SystemAccentColorDark2"] = _color;
                    Application.Current.Resources["SystemAccentColorDark3"] = _color;
                }
                else
                {
                    try
                    {
                        _color = ColorThiefToColor(GetColorFromPaletteByTheme(0, true));
                    }
                    catch
                    {
                        _color = new Windows.UI.Color { R = 255, G = 255, B = 255, A = 255 };
                    }

                    Application.Current.Resources["SystemAccentColor"] = _color;
                    Application.Current.Resources["SystemAccentColorLight1"] = _color;
                    Application.Current.Resources["SystemAccentColorLight2"] = _color;
                    Application.Current.Resources["SystemAccentColorLight3"] = _color;
                }

                ReloadPageTheme(ConvertAppThemeToElementTheme(InnerLauncherConfig.CurrentAppTheme));
            });
        }

        private Windows.UI.Color ColorThiefToColor(QuantizedColor i) => new Windows.UI.Color { R = i.Color.R, G = i.Color.G, B = i.Color.B, A = i.Color.A };

        private async Task<BitmapImage> GetResizedBitmap(string path)
        {
            BitmapImage retBitmap = new BitmapImage();

            using (IRandomAccessStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read).AsRandomAccessStream())
            {
                var decoder = await BitmapDecoder.CreateAsync(fileStream);

                InMemoryRandomAccessStream resizedStream = new InMemoryRandomAccessStream();

                BitmapEncoder encoder = await BitmapEncoder.CreateForTranscodingAsync(resizedStream, decoder);

                encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;

                (
                    encoder.BitmapTransform.ScaledWidth,
                    encoder.BitmapTransform.ScaledHeight
                ) = GetPreservedImageRatio(
                    (uint)((double)m_actualMainFrameSize.Width * 1.45 * m_appDPIScale),
                    (uint)((double)m_actualMainFrameSize.Height * 1.45 * m_appDPIScale),
                    decoder.PixelWidth,
                    decoder.PixelHeight);

                await encoder.FlushAsync();
                resizedStream.Seek(0);

                retBitmap = Bitmap2BitmapImage(resizedStream.AsStreamForRead());
            }

            return retBitmap;
        }

        private BitmapImage Bitmap2BitmapImage(Stream image)
        {
            BitmapImage ret = new BitmapImage();
            ret.SetSource(image.AsRandomAccessStream());
            return ret;
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

        private async Task GenerateThumbnail()
        {
            try
            {
                using (IRandomAccessStream fileStream = new FileStream(regionBackgroundProp.imgLocalPath, FileMode.Open, FileAccess.Read).AsRandomAccessStream())
                {
                    var decoder = await BitmapDecoder.CreateAsync(fileStream);

                    InMemoryRandomAccessStream resizedStream = new InMemoryRandomAccessStream();

                    BitmapEncoder encoder = await BitmapEncoder.CreateForTranscodingAsync(resizedStream, decoder);

                    encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;

                    encoder.BitmapTransform.ScaledHeight = (uint)ThumbnailSize.Width;
                    encoder.BitmapTransform.ScaledWidth = (uint)ThumbnailSize.Height;

                    await encoder.FlushAsync();
                    resizedStream.Seek(0);

                    ThumbnailStream = new MemoryStream();
                    await resizedStream.AsStream().CopyToAsync(ThumbnailStream);

                    ThumbnailBitmap = new Bitmap(ThumbnailStream);
                    ThumbnailStream.Dispose();
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Cannot generate thumbnail: {ex}", Hi3Helper.LogType.Warning, true);
            }
        }

        private IEnumerable<QuantizedColor> GetPalette(byte paletteOrder = 0) => new ColorThief().GetPalette(ThumbnailBitmap);
        private QuantizedColor GetColorFromPalette(byte paletteOrder = 0) => new ColorThief().GetPalette(ThumbnailBitmap, 10)[paletteOrder];
        private QuantizedColor GetColorFromPaletteByTheme(byte paletteOrder = 0, bool alwaysLight = true) =>
            new ColorThief().GetPalette(ThumbnailBitmap, 10).Where(x => x.IsDark != alwaysLight).ToList()[paletteOrder];
        private QuantizedColor GetColorFromPaletteByThemeLow(byte paletteOrder = 0, bool alwaysLight = true) =>
            new ColorThief().GetPalette(ThumbnailBitmap, 50, 10).Where(x => x.IsDark != alwaysLight).ToArray()[paletteOrder];
        private QuantizedColor GetSingleColorPalette() => new ColorThief().GetColor(ThumbnailBitmap);

        private async Task DownloadBackgroundImage()
        {
            if (!Directory.Exists(Path.Combine(AppGameImgFolder, "bg")))
                Directory.CreateDirectory(Path.Combine(AppGameImgFolder, "bg"));

            FileInfo fI = new FileInfo(regionBackgroundProp.imgLocalPath);

            if (!fI.Exists)
            {
                await httpHelper.DownloadMultisession(regionBackgroundProp.data.adv.background, regionBackgroundProp.imgLocalPath, false, 4, tokenSource.Token);
                await httpHelper.MergeMultisession(regionBackgroundProp.imgLocalPath, 4, tokenSource.Token);
            }
        }

        private async Task ApplyBackground(bool rescale = true)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                BackgroundBackBuffer.Source = BackgroundBitmap;
                BackgroundBackBuffer.Visibility = Visibility.Visible;
                BackgroundFrontBuffer.Source = BackgroundBitmap;
                BackgroundFrontBuffer.Visibility = Visibility.Visible;
            });

            if (rescale)
                BackgroundBitmap = await GetResizedBitmap(regionBackgroundProp.imgLocalPath);
            else
                BackgroundBitmap = new BitmapImage(new Uri(regionBackgroundProp.imgLocalPath));

            DispatcherQueue.TryEnqueue(() =>
            {
                BackgroundBack.Source = BackgroundBitmap;
                BackgroundFront.Source = BackgroundBitmap;
            });

            FadeOutBackgroundBuffer();
        }

        public async void FadeOutBackgroundBuffer()
        {
            Storyboard storyboardBack = new Storyboard();
            Storyboard storyboardFront = new Storyboard();

            DoubleAnimation OpacityAnimationBack = new DoubleAnimation();
            OpacityAnimationBack.From = 0.30;
            OpacityAnimationBack.To = 0;
            OpacityAnimationBack.Duration = new Duration(TimeSpan.FromSeconds(0.25));

            DoubleAnimation OpacityAnimationFront = new DoubleAnimation();
            OpacityAnimationFront.From = 1;
            OpacityAnimationFront.To = 0;
            OpacityAnimationFront.Duration = new Duration(TimeSpan.FromSeconds(0.25));

            Storyboard.SetTarget(OpacityAnimationBack, BackgroundBackBuffer);
            Storyboard.SetTargetProperty(OpacityAnimationBack, "Opacity");
            storyboardBack.Children.Add(OpacityAnimationBack);

            Storyboard.SetTarget(OpacityAnimationFront, BackgroundFrontBuffer);
            Storyboard.SetTargetProperty(OpacityAnimationFront, "Opacity");
            storyboardFront.Children.Add(OpacityAnimationFront);

            storyboardBack.Begin();
            if (m_appCurrentFrameName == "HomePage")
                storyboardFront.Begin();

            await Task.Delay(250);
            DispatcherQueue.TryEnqueue(() =>
            {
                BackgroundBackBuffer.Visibility = Visibility.Collapsed;
                BackgroundFrontBuffer.Visibility = Visibility.Collapsed;
            });
        }

        private async Task HideLoadingPopup(bool hide, string title, string subtitle)
        {
            Storyboard storyboard = new Storyboard();

            DispatcherQueue.TryEnqueue(() =>
            {
                LoadingTitle.Text = title;
                LoadingSubtitle.Text = subtitle;
            });

            if (hide)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    LoadingRing.IsIndeterminate = false;
                });

                await Task.Delay(500);

                DoubleAnimation OpacityAnimation = new DoubleAnimation();
                OpacityAnimation.From = 1;
                OpacityAnimation.To = 0;
                OpacityAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.25));

                Storyboard.SetTarget(OpacityAnimation, LoadingPopup);
                Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
                storyboard.Children.Add(OpacityAnimation);

                storyboard.Begin();
                await Task.Delay(250);
                DispatcherQueue.TryEnqueue(() => LoadingPopup.Visibility = Visibility.Collapsed);
            }
            else
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    LoadingFooter.Text = "";
                    LoadingRing.IsIndeterminate = true;
                });

                DispatcherQueue.TryEnqueue(() => LoadingPopup.Visibility = Visibility.Visible);

                DoubleAnimation OpacityAnimation = new DoubleAnimation();
                OpacityAnimation.From = 0;
                OpacityAnimation.To = 1;
                OpacityAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.25));

                Storyboard.SetTarget(OpacityAnimation, LoadingPopup);
                Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
                storyboard.Children.Add(OpacityAnimation);

                storyboard.Begin();
                await Task.Delay(250);
            }
        }

        bool BGLastState = true;
        public async void HideBackgroundImage(bool hideImage = true, bool absoluteTransparent = true)
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
                OpacityAnimationBack.From = hideImage ? 1 : 0.30;
                OpacityAnimationBack.To = hideImage ? 0.30 : 1;
                OpacityAnimationBack.Duration = new Duration(TimeSpan.FromSeconds(0.25));

                Storyboard.SetTarget(OpacityAnimation, BackgroundFront);
                Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
                storyboardFront.Children.Add(OpacityAnimation);

                Storyboard.SetTarget(OpacityAnimationBack, BackgroundBack);
                Storyboard.SetTargetProperty(OpacityAnimationBack, "Opacity");
                storyboardBack.Children.Add(OpacityAnimationBack);
            }

            if (BGLastState != hideImage)
            {
                storyboardFront.Begin();
                storyboardBack.Begin();
                BGLastState = hideImage;

                await Task.Delay(250);
            }
        }
    }
}