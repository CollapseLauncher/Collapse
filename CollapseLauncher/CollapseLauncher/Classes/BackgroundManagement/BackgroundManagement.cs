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

using Newtonsoft.Json;


using SixLabors.ImageSharp;


using Hi3Helper.Data;
using Hi3Helper.Preset;

using static Hi3Helper.Logger;
using static CollapseLauncher.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        private BitmapImage BackgroundBitmap;

        // Always use startupBackgroundPath on startup.
        private bool startUp = true;
        private string previousPath = startupBackgroundPath;
        private void ChangeBackgroundImageAsRegion()
        {
            try
            {
                httpClient = new HttpClientTool();

                if (startUp)
                {
                    regionBackgroundProp = new RegionBackgroundProp { imgLocalPath = startupBackgroundPath };
                    if (File.Exists(startupBackgroundPath))
                        ApplyBackground();

                    startUp = false;
                }

                MemoryStream memoryStream = new MemoryStream();
                
                httpClient.DownloadStream(CurrentRegion.LauncherSpriteURL, memoryStream);
                regionBackgroundProp = JsonConvert.DeserializeObject<RegionBackgroundProp>(Encoding.UTF8.GetString(memoryStream.ToArray()));

                regionBackgroundProp.imgLocalPath = Path.Combine(AppDataFolder, "bg", Path.GetFileName(regionBackgroundProp.data.adv.background));

                if (DownloadBackgroundImage())
                    ApplyBackground();
            }
            catch
            {
                LogWriteLine($"Cannot connect to the internet while fetching background image");
            }
        }

        private bool DownloadBackgroundImage()
        {
            if (!Directory.Exists(Path.Combine(AppDataFolder, "bg")))
                Directory.CreateDirectory(Path.Combine(AppDataFolder, "bg"));

            if (!File.Exists(regionBackgroundProp.imgLocalPath)
                || Path.GetFileName(regionBackgroundProp.data.adv.background) != Path.GetFileName(regionBackgroundProp.imgLocalPath)
                || Path.GetFileName(previousPath) != Path.GetFileName(regionBackgroundProp.data.adv.background))
            {
                httpClient.DownloadFile(regionBackgroundProp.data.adv.background, regionBackgroundProp.imgLocalPath);
                previousPath = regionBackgroundProp.imgLocalPath;
                return true;
            }

            return false;
        }

        private void ApplyBackground()
        {
            DispatcherQueue.TryEnqueue(() => {
                HideBackgroundImage();
                BackgroundBitmap = new BitmapImage(new Uri(regionBackgroundProp.imgLocalPath));
                BackgroundBack.Source = BackgroundBitmap;
                BackgroundFront.Source = BackgroundBitmap;

                HideBackgroundImage(false);
                appIni.Profile["app"]["CurrentBackground"] = regionBackgroundProp.imgLocalPath;

                SaveAppConfig();
            });
        }

        private void HideBackgroundImage(bool hideImage = true)
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

            storyboardFront.Begin();
            storyboardBack.Begin();
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

    public class RegionBackgroundProp
    {
        public RegionBackgroundProp_data data { get; set; }
        public string imgLocalPath { get; set; } = string.Empty;
    }

    public class RegionBackgroundProp_data
    {
        public RegionBackgroundProp_data_adv adv { get; set; }
    }
    public class RegionBackgroundProp_data_adv
    {
        public string background { get; set; }
    }
}