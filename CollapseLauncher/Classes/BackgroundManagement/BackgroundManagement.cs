using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.ComponentModel;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Runtime.CompilerServices;

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;

using ColorThiefDotNet;

using Newtonsoft.Json;

using Hi3Helper.Data;

using static Hi3Helper.Shared.Region.LauncherConfig;
using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        private BitmapImage BackgroundBitmap;
        private Bitmap ThumbnailBitmap;
        private Stream ThumbnailStream;
        private readonly Size ThumbnailSize = new Size(32, 32);

        // Always use startupBackgroundPath on startup.
        private bool startUp = true;
        private string previousPath = startupBackgroundPath;
        private void ChangeBackgroundImageAsRegion(CancellationToken token)
        {
            try
            {
                httpHelper = new HttpClientHelper(true);

                if (startUp)
                {
                    regionBackgroundProp = new RegionBackgroundProp { imgLocalPath = startupBackgroundPath };
                    if (File.Exists(startupBackgroundPath))
                    {
                        ApplyBackground();

                        GenerateThumbnail();
                        ApplyAccentColor();
                    }

                    startUp = false;
                }

                MemoryStream memoryStream = new MemoryStream();

                httpHelper.DownloadFile(CurrentRegion.LauncherSpriteURL, memoryStream, token);
                regionBackgroundProp = JsonConvert.DeserializeObject<RegionBackgroundProp>(Encoding.UTF8.GetString(memoryStream.ToArray()));

                regionBackgroundProp.imgLocalPath = Path.Combine(AppGameImgFolder, "bg", Path.GetFileName(regionBackgroundProp.data.adv.background));

                if (DownloadBackgroundImage())
                    ApplyBackground();

                GenerateThumbnail();
                ApplyAccentColor();
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException($"Background Image fetch canceled!");
            }
            catch (Exception ex)
            {
                LogWriteLine($"Something wrong happen while fetching Background Image\r\n{ex}");
            }
        }

        private void ApplyAccentColor()
        {
            Windows.UI.Color _color = new Windows.UI.Color();
            DispatcherQueue.TryEnqueue(() =>
            {
                if (InnerLauncherConfig.CurrentAppTheme == AppThemeMode.Light ||
                    (InnerLauncherConfig.CurrentAppTheme == AppThemeMode.Default && InnerLauncherConfig.SystemAppTheme.ToString() == "#FFFFFFFF"))
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
                            _color = ColorThiefToColor(GetColorFromPaletteByTheme(0, false));
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

        private void GenerateThumbnail()
        {
            try
            {
                Task.Run(async () =>
                {
                    using (IRandomAccessStream fileStream = new FileStream(regionBackgroundProp.imgLocalPath, FileMode.Open, FileAccess.Read).AsRandomAccessStream())
                    {
                        var decoder = await BitmapDecoder.CreateAsync(fileStream);

                        InMemoryRandomAccessStream resizedStream = new InMemoryRandomAccessStream();

                        BitmapEncoder encoder = await BitmapEncoder.CreateForTranscodingAsync(resizedStream, decoder);

                        encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Linear;

                        encoder.BitmapTransform.ScaledHeight = (uint)ThumbnailSize.Width;
                        encoder.BitmapTransform.ScaledWidth = (uint)ThumbnailSize.Height;

                        await encoder.FlushAsync();
                        resizedStream.Seek(0);

                        ThumbnailStream = new MemoryStream();
                        resizedStream.AsStream().CopyTo(ThumbnailStream);

                        ThumbnailBitmap = new Bitmap(ThumbnailStream);
                        ThumbnailStream.Dispose();
                    }
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Cannot generate thumbnail: {ex}", Hi3Helper.LogType.Warning, true);
            }
        }

        private IEnumerable<QuantizedColor> GetPalette(byte paletteOrder = 0) => new ColorThief().GetPalette(ThumbnailBitmap);
        private QuantizedColor GetColorFromPalette(byte paletteOrder = 0) => new ColorThief().GetPalette(ThumbnailBitmap, 10)[paletteOrder];
        private QuantizedColor GetColorFromPaletteByTheme(byte paletteOrder = 0, bool alwaysLight = true) =>
            new ColorThief().GetPalette(ThumbnailBitmap, 10).Where(x => x.IsDark != alwaysLight).ToArray()[paletteOrder];
        private QuantizedColor GetSingleColorPalette() => new ColorThief().GetColor(ThumbnailBitmap);

        private bool DownloadBackgroundImage()
        {
            if (!Directory.Exists(Path.Combine(AppGameImgFolder, "bg")))
                Directory.CreateDirectory(Path.Combine(AppGameImgFolder, "bg"));

            FileInfo fI = new FileInfo(regionBackgroundProp.imgLocalPath);

            if (!fI.Exists)
            {
                httpHelper.DownloadFile(regionBackgroundProp.data.adv.background, regionBackgroundProp.imgLocalPath, 4, tokenSource.Token);
                previousPath = regionBackgroundProp.imgLocalPath;
            }

            return true;
        }

        private void ApplyBackground()
        {
            DispatcherQueue.TryEnqueue(() => {
                // HideBackgroundImage();
                BackgroundBackBuffer.Source = BackgroundBitmap;
                BackgroundBackBuffer.Visibility = Visibility.Visible;
                BackgroundBitmap = new BitmapImage(new Uri(regionBackgroundProp.imgLocalPath));
                BackgroundBack.Source = BackgroundBitmap;
                BackgroundFront.Source = BackgroundBitmap;

                FadeOutBackgroundBuffer();

                // HideBackgroundImage(false);
                appIni.Profile["app"]["CurrentBackground"] = regionBackgroundProp.imgLocalPath;

                SaveAppConfig();
            });
        }

        public async void FadeOutBackgroundBuffer()
        {
            Storyboard storyboardBack = new Storyboard();

            DoubleAnimation OpacityAnimationBack = new DoubleAnimation();
            OpacityAnimationBack.From = 0.30;
            OpacityAnimationBack.To = 0;
            OpacityAnimationBack.Duration = new Duration(TimeSpan.FromSeconds(0.25));

            Storyboard.SetTarget(OpacityAnimationBack, BackgroundBackBuffer);
            Storyboard.SetTargetProperty(OpacityAnimationBack, "Opacity");
            storyboardBack.Children.Add(OpacityAnimationBack);

            storyboardBack.Begin();

            await Task.Delay(250);
            DispatcherQueue.TryEnqueue(() =>
            {
                BackgroundBackBuffer.Visibility = Visibility.Collapsed;
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
            }
        }

        bool BGLastState = true;
        public void HideBackgroundImage(bool hideImage = true, bool absoluteTransparent = true)
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
                OpacityAnimationBack.From = hideImage ? 0.50 : 0.30;
                OpacityAnimationBack.To = hideImage ? 0.30 : 0.50;
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
            }
        }

        public class SystemAccentColorSetting : INotifyPropertyChanged
        {
            private SolidColorBrush systemAccentColor = new SolidColorBrush(Colors.Red);
            public SolidColorBrush SystemAccentColor
            {
                get
                {
                    return systemAccentColor;
                }
                set
                {
                    systemAccentColor = value; OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}