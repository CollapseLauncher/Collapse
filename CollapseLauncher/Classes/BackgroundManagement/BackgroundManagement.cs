using ColorThiefDotNet;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
#if DEBUG
using static Hi3Helper.Logger;
#endif
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;
using Hi3Helper.Preset;
using Hi3Helper.Data;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        private RegionResourceProp _gameAPIProp { get; set; }

        private BitmapImage BackgroundBitmap;
        private Bitmap PaletteBitmap;
        private bool BGLastState = true;
        private bool IsFirstStartup = true;

        private async ValueTask FetchLauncherLocalizedResources(CancellationToken Token, PresetConfigV2 Preset)
        {
            regionBackgroundProp = Preset.LauncherSpriteURLMultiLang ?
                await TryGetMultiLangResourceProp(Token, Preset) :
                await TryGetSingleLangResourceProp(Token, Preset);

            await DownloadBackgroundImage(Token);

            await GetLauncherAdvInfo(Token, Preset);
            await GetLauncherCarouselInfo(Token);
            await GetLauncherEventInfo(Token);
            GetLauncherPostInfo();
        }

        private async Task ChangeBackgroundImageAsRegion()
        {
            IsCustomBG = GetAppConfigValue("UseCustomBG").ToBool();
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
                BackgroundImgChanger.ChangeBackground(regionBackgroundProp.imgLocalPath, IsCustomBG);
                await BackgroundImgChanger.WaitForBackgroundToLoad();
                IsFirstStartup = false;
            }

            ReloadPageTheme(this, ConvertAppThemeToElementTheme(CurrentAppTheme));
        }

        private async ValueTask FetchLauncherDownloadInformation(CancellationToken Token, PresetConfigV2 Preset)
        {
            using (Stream netStream = (await _httpClient.DownloadFromSessionStreamAsync(
                Preset.LauncherResourceURL,
                0,
                null,
                Token
                )).Item1)
            {
                _gameAPIProp = (RegionResourceProp)JsonSerializer.Deserialize(netStream, typeof(RegionResourceProp), RegionResourcePropContext.Default) ?? new RegionResourceProp();

#if DEBUG
                if (_gameAPIProp.data.game.latest.decompressed_path != null) LogWriteLine($"Decompressed Path: {_gameAPIProp.data.game.latest.decompressed_path}", LogType.Default, true);
                if (_gameAPIProp.data.game.latest.path != null) LogWriteLine($"ZIP Path: {_gameAPIProp.data.game.latest.path}", LogType.Default, true);
                if (_gameAPIProp.data.pre_download_game?.latest?.decompressed_path != null) LogWriteLine($"Decompressed Path Pre-load: {_gameAPIProp.data.pre_download_game?.latest?.decompressed_path}", LogType.Default, true);
                if (_gameAPIProp.data.pre_download_game?.latest?.path != null) LogWriteLine($"ZIP Path Pre-load: {_gameAPIProp.data.pre_download_game?.latest?.path}", LogType.Default, true);
#endif
            }
        }

        private async ValueTask<RegionResourceProp> TryGetMultiLangResourceProp(CancellationToken Token, PresetConfigV2 Preset)
        {
            RegionResourceProp ret = await GetMultiLangResourceProp(Lang.LanguageID.ToLower(), Token, Preset);

            return ret.data.adv == null
              || ((ret.data.adv.version ?? 5) <= 4
                && Preset.GameType == GameType.Honkai) ?
                    await GetMultiLangResourceProp(Preset.LauncherSpriteURLMultiLangFallback ?? "en-us", Token, Preset) :
                    ret;
        }

        private async ValueTask<RegionResourceProp> GetMultiLangResourceProp(string langID, CancellationToken token, PresetConfigV2 Preset)
        {
            using (Stream netStream = (await _httpClient.DownloadFromSessionStreamAsync(
                string.Format(Preset.LauncherSpriteURL, langID),
                0,
                null,
                token
                )).Item1)
            {
                return (RegionResourceProp)JsonSerializer.Deserialize(netStream, typeof(RegionResourceProp), RegionResourcePropContext.Default) ?? new RegionResourceProp();
            }
        }

        private async ValueTask<RegionResourceProp> TryGetSingleLangResourceProp(CancellationToken Token, PresetConfigV2 Preset)
        {
            using (Stream netStream = (await _httpClient.DownloadFromSessionStreamAsync(
                Preset.LauncherSpriteURL,
                0,
                null,
                Token
                )).Item1)
            {
                return (RegionResourceProp)JsonSerializer.Deserialize(netStream, typeof(RegionResourceProp), RegionResourcePropContext.Default) ?? new RegionResourceProp();
            }
        }

        private void ResetRegionProp()
        {
            LastRegionNewsProp = regionNewsProp.Copy();
            regionNewsProp = new HomeMenuPanel()
            {
                sideMenuPanel = null,
                imageCarouselPanel = null,
                articlePanel = null,
                eventPanel = null
            };
        }

        private async ValueTask GetLauncherAdvInfo(CancellationToken Token, PresetConfigV2 Preset)
        {
            if (regionBackgroundProp.data.icon.Count == 0) return;

            regionNewsProp.sideMenuPanel = new List<MenuPanelProp>();
            foreach (RegionSocMedProp item in regionBackgroundProp.data.icon)
            {
                regionNewsProp.sideMenuPanel.Add(new MenuPanelProp
                {
                    URL = item.url,
                    Icon = await GetCachedSprites(item.img, Token),
                    IconHover = await GetCachedSprites(item.img_hover, Token),
                    QR = string.IsNullOrEmpty(item.qr_img) ? null : await GetCachedSprites(item.qr_img, Token),
                    QR_Description = string.IsNullOrEmpty(item.qr_desc) ? null : item.qr_desc,
                    Description = string.IsNullOrEmpty(item.title) || Preset.IsHideSocMedDesc ? item.url : item.title
                });
            }
        }

        private async ValueTask GetLauncherCarouselInfo(CancellationToken Token)
        {
            if (regionBackgroundProp.data.banner.Count == 0) return;

            regionNewsProp.imageCarouselPanel = new List<MenuPanelProp>();
            foreach (RegionSocMedProp item in regionBackgroundProp.data.banner)
            {
                regionNewsProp.imageCarouselPanel.Add(new MenuPanelProp
                {
                    URL = item.url,
                    Icon = await GetCachedSprites(item.img, Token),
                    Description = string.IsNullOrEmpty(item.name) ? item.url : item.name
                });
            }
        }

        private async ValueTask GetLauncherEventInfo(CancellationToken Token)
        {
            if (string.IsNullOrEmpty(regionBackgroundProp.data.adv.icon)) return;

            regionNewsProp.eventPanel = new RegionBackgroundProp
            {
                url = regionBackgroundProp.data.adv.url,
                icon = await GetCachedSprites(regionBackgroundProp.data.adv.icon, Token)
            };
        }

        private void GetLauncherPostInfo()
        {
            if (regionBackgroundProp.data.post.Count == 0) return;

            regionNewsProp.articlePanel = new PostCarouselTypes();
            foreach (RegionSocMedProp item in regionBackgroundProp.data.post)
            {
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
        }

        public async ValueTask<string> GetCachedSprites(string URL, CancellationToken token)
        {
            string cacheFolder = Path.Combine(AppGameImgFolder, "cache");
            string cachePath = Path.Combine(cacheFolder, Path.GetFileNameWithoutExtension(URL));
            if (!Directory.Exists(cacheFolder))
                Directory.CreateDirectory(cacheFolder);

            FileInfo fInfo = new FileInfo(cachePath);

            if (!fInfo.Exists || fInfo.Length < (1 << 10))
            {
                using (FileStream fs = fInfo.Create())
                {
                    using (Stream netStream = (await _httpClient.DownloadFromSessionStreamAsync(URL, 0, null, token)).Item1)
                    {
                        netStream.CopyTo(fs);
                    }
                }
            }

            return cachePath;
        }

        public static async Task<Windows.UI.Color[]> ApplyAccentColor(Page page, Bitmap bitmapinput, int quality = 7)
        {
            Windows.UI.Color[] _colors;
            switch (CurrentAppTheme)
            {
                case AppThemeMode.Light:
                    _colors = await SetLightColors(bitmapinput, quality);
                    break;
                case AppThemeMode.Dark:
                    _colors = await SetDarkColors(bitmapinput, quality);
                    break;
                default:
                    if (SystemAppTheme.ToString() == "#FFFFFFFF")
                        _colors = await SetLightColors(bitmapinput, quality);
                    else
                        _colors = await SetDarkColors(bitmapinput, quality);
                    break;
            }

            ReloadPageTheme(page, ConvertAppThemeToElementTheme(CurrentAppTheme));
            return _colors;
        }

        private static async Task<Windows.UI.Color[]> SetLightColors(Bitmap bitmapinput, int quality = 3)
        {
            Windows.UI.Color[] _colors = await GetPaletteList(bitmapinput, 4, true, quality);
            Application.Current.Resources["SystemAccentColor"] = _colors[0];
            Application.Current.Resources["SystemAccentColorDark1"] = _colors[1];
            Application.Current.Resources["SystemAccentColorDark2"] = _colors[2];
            Application.Current.Resources["SystemAccentColorDark3"] = _colors[3];
            Application.Current.Resources["AccentColor"] = new SolidColorBrush(_colors[0]);

            return _colors;
        }

        private static async Task<Windows.UI.Color[]> SetDarkColors(Bitmap bitmapinput, int quality = 3)
        {
            Windows.UI.Color[] _colors = await GetPaletteList(bitmapinput, 4, false, quality);
            Application.Current.Resources["SystemAccentColor"] = _colors[0];
            Application.Current.Resources["SystemAccentColorLight1"] = _colors[1];
            Application.Current.Resources["SystemAccentColorLight2"] = _colors[2];
            Application.Current.Resources["SystemAccentColorLight3"] = _colors[3];
            Application.Current.Resources["AccentColor"] = new SolidColorBrush(_colors[0]);

            return _colors;
        }

        private static async Task<Windows.UI.Color[]> GetPaletteList(Bitmap bitmapinput, int ColorCount = 4, bool IsLight = false, int quality = 3)
        {
            byte DefVal = (byte)(IsLight ? 80 : 255);
            Windows.UI.Color[] output = new Windows.UI.Color[4];

            QuantizedColor? Single = null;
            ColorThief cT = new ColorThief();
            IEnumerable<QuantizedColor> Colors = await Task.Run(() => cT.GetPalette(bitmapinput, 10, quality));

            try
            {
                Single = Colors.Where(x => IsLight ? x.IsDark : !x.IsDark).FirstOrDefault();
            }
            catch
            {
                if (Single is null) Single = Colors.FirstOrDefault();
                if (Single is null) Single = new QuantizedColor(new CTColor { R = DefVal, G = DefVal, B = DefVal }, 1);
            }

            for (int i = 0; i < ColorCount; i++) output[i] = ColorThiefToColor(Single ?? new QuantizedColor());

            cT = null;

            return output;
        }

        private static Windows.UI.Color ColorThiefToColor(QuantizedColor i) => new Windows.UI.Color { R = i.Color.R, G = i.Color.G, B = i.Color.B, A = 255 };

        public static async Task<(Bitmap, BitmapImage)> GetResizedBitmap(FileStream stream, uint ToWidth, uint ToHeight)
        {
            Bitmap bitmapRet;
            BitmapImage bitmapImageRet;

            if (!Directory.Exists(AppGameImgCachedFolder)) Directory.CreateDirectory(AppGameImgCachedFolder);

            string cachedFileHash = ConverterTool.BytesToCRC32Simple(stream.Name + stream.Length);
            string cachedFilePath = Path.Combine(AppGameImgCachedFolder, cachedFileHash);

            FileInfo cachedFileInfo = new FileInfo(cachedFilePath);

            bool isCachedFileExist = cachedFileInfo.Exists && cachedFileInfo.Length > 4 << 15;
            FileStream cachedFileStream = isCachedFileExist ? cachedFileInfo.OpenRead() : cachedFileInfo.Create();

            try
            {
                if (!isCachedFileExist)
                {
                    await GetResizedImageStream(stream, cachedFileStream, ToWidth, ToHeight);
                }

                bitmapRet = await Task.Run(() => Stream2Bitmap(cachedFileStream.AsRandomAccessStream()));
                bitmapImageRet = await Stream2BitmapImage(cachedFileStream.AsRandomAccessStream());
            }
            catch { throw; }
            finally
            {
                stream?.Dispose();
                cachedFileStream?.Dispose();
                GC.Collect();
            }

            return (bitmapRet, bitmapImageRet);
        }

        private static async Task GetResizedImageStream(FileStream input, FileStream output, uint ToWidth, uint ToHeight)
        {
            uint ResizedSizeW;
            uint ResizedSizeH;

            IRandomAccessStream inputRandomStream = input.AsRandomAccessStream();
            IRandomAccessStream outputRandomStream = output.AsRandomAccessStream();

            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(inputRandomStream);
            BitmapPixelFormat pixFmt = decoder.BitmapPixelFormat;
            BitmapAlphaMode alpMod = decoder.DecoderInformation.CodecId != BitmapDecoder.PngDecoderId ?
                BitmapAlphaMode.Ignore :
                BitmapAlphaMode.Straight;

            (ResizedSizeW, ResizedSizeH) = GetPreservedImageRatio(ToWidth, ToHeight, decoder.PixelWidth, decoder.PixelHeight);

            if (decoder.PixelWidth < ResizedSizeW
             && decoder.PixelHeight < ResizedSizeH)
            {
                input.Seek(0, SeekOrigin.Begin);
                input.CopyTo(output);
                return;
            }

            BitmapTransform transform = new BitmapTransform()
            {
                ScaledWidth = ResizedSizeW,
                ScaledHeight = ResizedSizeH,
                InterpolationMode = BitmapInterpolationMode.Fant
            };

            PixelDataProvider pixelData = await decoder.GetPixelDataAsync(
                                                pixFmt,
                                                alpMod,
                                                transform,
                                                ExifOrientationMode.RespectExifOrientation,
                                                ColorManagementMode.DoNotColorManage);

            if (decoder.PixelWidth != decoder.OrientedPixelWidth) FlipSize(ref ResizedSizeW, ref ResizedSizeH);
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(alpMod == BitmapAlphaMode.Straight ? BitmapEncoder.PngEncoderId : decoder.DecoderInformation.CodecId, outputRandomStream);
            byte[] pixelDataBytes = pixelData.DetachPixelData();
            encoder.SetPixelData(pixFmt, alpMod, ResizedSizeW, ResizedSizeH, m_appDPIScale, m_appDPIScale, pixelDataBytes);

            await encoder.FlushAsync();
            outputRandomStream.Seek(0);
        }

        private static void FlipSize(ref uint w, ref uint h)
        {
            uint _w = w;
            uint _h = h;
            w = _h;
            h = _w;
        }

        public static async Task<BitmapImage> Stream2BitmapImage(IRandomAccessStream image)
        {
            BitmapImage ret = new BitmapImage();
            image.Seek(0);
            await ret.SetSourceAsync(image);
            return ret;
        }

        public static Bitmap Stream2Bitmap(IRandomAccessStream image)
        {
            image.Seek(0);
            return new Bitmap(image.AsStream());
        }

        // Reference:
        // https://stackoverflow.com/questions/1940581/c-sharp-image-resizing-to-different-size-while-preserving-aspect-ratio
        private static (uint, uint) GetPreservedImageRatio(uint canvasWidth, uint canvasHeight, uint imgWidth, uint imgHeight)
        {
            double ratioX = (double)canvasWidth / imgWidth;
            double ratioY = (double)canvasHeight / imgHeight;
            double ratio = ratioX < ratioY ? ratioX : ratioY;

            return ((uint)(imgWidth * ratio), (uint)(imgHeight * ratio));
        }

        private async ValueTask DownloadBackgroundImage(CancellationToken Token)
        {
            regionBackgroundProp.imgLocalPath = Path.Combine(AppGameImgFolder, "bg", Path.GetFileName(regionBackgroundProp.data.adv.background));
            SetAndSaveConfigValue("CurrentBackground", regionBackgroundProp.imgLocalPath);

            if (!Directory.Exists(Path.Combine(AppGameImgFolder, "bg")))
                Directory.CreateDirectory(Path.Combine(AppGameImgFolder, "bg"));

            FileInfo fI = new FileInfo(regionBackgroundProp.imgLocalPath);

            if (fI.Exists) return;

            using (Stream netStream = (await _httpClient.DownloadFromSessionStreamAsync(regionBackgroundProp.data.adv.background, 0, null, Token)).Item1)
            {
                using (Stream outStream = fI.Create())
                {
                    netStream.CopyTo(outStream);
                }
            }
        }

        private async void ApplyBackgroundAsync() => await ApplyBackground();

        private async Task ApplyBackground()
        {
            BackgroundBackBuffer.Source = BackgroundBitmap;

            uint Width = (uint)((double)m_actualMainFrameSize.Width * 1.5 * m_appDPIScale);
            uint Height = (uint)((double)m_actualMainFrameSize.Height * 1.5 * m_appDPIScale);

            FileStream stream = new FileStream(regionBackgroundProp.imgLocalPath, FileMode.Open, FileAccess.Read);

            (PaletteBitmap, BackgroundBitmap) = await GetResizedBitmap(stream, Width, Height);

            await ApplyAccentColor(this, PaletteBitmap);

            FadeOutFrontBg();
            FadeOutBackBg();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCComplete();
            GC.Collect();
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

        private async void HideLoadingPopup(bool hide, string title, string subtitle)
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
                await Task.Delay(250 * 2);
                LoadingPopup.Visibility = Visibility.Collapsed;
                LoadingCancelBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
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