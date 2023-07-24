using ColorThiefDotNet;
using Hi3Helper;
using Hi3Helper.Data;
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
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        private RegionResourceProp _gameAPIProp { get; set; }

        private BitmapImage BackgroundBitmap;
        private Bitmap PaletteBitmap;
        private bool BGLastState = true;
        private bool IsFirstStartup = true;

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

        public static async void ApplyAccentColor(Page page, Bitmap bitmapinput)
        {
            switch (CurrentAppTheme)
            {
                case AppThemeMode.Light:
                    await SetLightColors(bitmapinput);
                    break;
                case AppThemeMode.Dark:
                    await SetDarkColors(bitmapinput);
                    break;
                default:
                    if (SystemAppTheme.ToString() == "#FFFFFFFF")
                        await SetLightColors(bitmapinput);
                    else
                        await SetDarkColors(bitmapinput);
                    break;
            }

            ReloadPageTheme(page, ConvertAppThemeToElementTheme(CurrentAppTheme));
        }

        private static async Task SetLightColors(Bitmap bitmapinput)
        {
            Windows.UI.Color[] _colors = await GetPaletteList(bitmapinput, 10, true, 1);
            Application.Current.Resources["SystemAccentColor"] = _colors[0];
            Application.Current.Resources["SystemAccentColorDark1"] = _colors[0];
            Application.Current.Resources["SystemAccentColorDark2"] = _colors[1];
            Application.Current.Resources["SystemAccentColorDark3"] = _colors[1];
            Application.Current.Resources["AccentColor"] = new SolidColorBrush(_colors[1]);
        }

        private static async Task SetDarkColors(Bitmap bitmapinput)
        {
            Windows.UI.Color[] _colors = await GetPaletteList(bitmapinput, 10, false, 1);
            Application.Current.Resources["SystemAccentColor"] = _colors[0];
            Application.Current.Resources["SystemAccentColorLight1"] = _colors[0];
            Application.Current.Resources["SystemAccentColorLight2"] = _colors[1];
            Application.Current.Resources["SystemAccentColorLight3"] = _colors[0];
            Application.Current.Resources["AccentColor"] = new SolidColorBrush(_colors[0]);
        }


        private static List<QuantizedColor> _generatedColors = new List<QuantizedColor>();
        private static async Task<Windows.UI.Color[]> GetPaletteList(Bitmap bitmapinput, int ColorCount, bool IsLight, int quality)
        {
            byte DefVal = (byte)(IsLight ? 80 : 255);

            try
            {
                LumaUtils.DarkThreshold = IsLight ? 200f : 400f;
                LumaUtils.IgnoreWhiteThreshold = IsLight ? 900f : 800f;
                if (!IsLight)
                    LumaUtils.ChangeCoeToBT709();
                else
                    LumaUtils.ChangeCoeToBT601();

                return await Task.Run(() =>
                {
                    _generatedColors.Clear();

                    IEnumerable<QuantizedColor> averageColors = ColorThief.GetPalette(bitmapinput, ColorCount, quality, !IsLight)
                            .Where(x => IsLight ? x.IsDark : !x.IsDark)
                            .OrderBy(x => x.Population);

                    QuantizedColor dominatedColor = new QuantizedColor(
                            Color.FromArgb(
                                255,
                                (byte)averageColors.Average(a => a.Color.R),
                                (byte)averageColors.Average(a => a.Color.G),
                                (byte)averageColors.Average(a => a.Color.B)
                            ), (int)averageColors.Average(a => a.Population));

                    _generatedColors.Add(dominatedColor);
                    _generatedColors.AddRange(averageColors);

                    return EnsureLengthCopyLast(_generatedColors
                        .Select(DrawingColorToColor)
                        .ToArray(), 2);
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Warning, true);
            }

            Windows.UI.Color defColor = DrawingColorToColor(new QuantizedColor(Color.FromArgb(255, DefVal, DefVal, DefVal), 1));
            return new Windows.UI.Color[] { defColor, defColor, defColor, defColor };
        }

        private static T[] EnsureLengthCopyLast<T>(T[] array, int toLength)
        {
            if (array.Length == 0) throw new IndexOutOfRangeException("Array has no content in it");
            if (array.Length >= toLength) return array;

            T lastArray = array[array.Length - 1];
            T[] newArray = new T[toLength];
            Array.Copy(array, newArray, array.Length);

            for (int i = array.Length; i < newArray.Length; i++)
            {
                newArray[i] = lastArray;
            }

            return newArray;
        }

        private static Windows.UI.Color DrawingColorToColor(QuantizedColor i) => new Windows.UI.Color { R = i.Color.R, G = i.Color.G, B = i.Color.B, A = i.Color.A };

        public static async Task<(Bitmap, BitmapImage)> GetResizedBitmap(FileStream stream, uint ToWidth, uint ToHeight)
        {
            Bitmap bitmapRet;
            BitmapImage bitmapImageRet;

            if (!Directory.Exists(AppGameImgCachedFolder)) Directory.CreateDirectory(AppGameImgCachedFolder);

            string cachedFileHash = ConverterTool.BytesToCRC32Simple(stream.Name + stream.Length);
            string cachedFilePath = Path.Combine(AppGameImgCachedFolder, cachedFileHash);

            FileInfo cachedFileInfo = new FileInfo(cachedFilePath);

            bool isCachedFileExist = cachedFileInfo.Exists && cachedFileInfo.Length > 1 << 15;
            using (stream)
            using (FileStream cachedFileStream = isCachedFileExist ? cachedFileInfo.OpenRead() : cachedFileInfo.Create())
            {
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
            Guid decoderCodecID = decoder.DecoderInformation.CodecId;
            BitmapAlphaMode alpMod = decoderCodecID != BitmapDecoder.PngDecoderId ?
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
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(alpMod == BitmapAlphaMode.Straight ? BitmapEncoder.PngEncoderId : BitmapEncoder.BmpEncoderId, outputRandomStream);
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

        private async void ApplyBackgroundAsync() => await ApplyBackground();

        private async Task ApplyBackground()
        {
            BackgroundBackBuffer.Source = BackgroundBitmap;

            uint Width = (uint)((double)m_actualMainFrameSize.Width * 1.5 * m_appDPIScale);
            uint Height = (uint)((double)m_actualMainFrameSize.Height * 1.5 * m_appDPIScale);

            FileStream stream = new FileStream(regionBackgroundProp.imgLocalPath, FileMode.Open, FileAccess.Read);

            (PaletteBitmap, BackgroundBitmap) = await GetResizedBitmap(stream, Width, Height);

            ApplyAccentColor(this, PaletteBitmap);

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