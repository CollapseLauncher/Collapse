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

using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

using Newtonsoft.Json;

using ColorThiefDotNet;

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
        private void ChangeBackgroundImageAsRegion()
        {
            try
            {
                httpClient = new HttpClientTool();

                MemoryStream memoryStream = new MemoryStream();

                httpClient.DownloadStream(CurrentRegion.LauncherSpriteURL, memoryStream);
                regionBackgroundProp = JsonConvert.DeserializeObject<RegionBackgroundProp>(Encoding.UTF8.GetString(memoryStream.ToArray()));

                regionBackgroundProp.imgLocalPath = Path.Combine(AppDataFolder, "bg", Path.GetFileName(regionBackgroundProp.data.adv.background));

                DownloadBackgroundImage();

                ApplyBackground();
            }
            catch
            {
                LogWriteLine($"Cannot connect to the internet while fetching background image");
            }
        }

        private void DownloadBackgroundImage()
        {
            if (!Directory.Exists(Path.Combine(AppDataFolder, "bg")))
                Directory.CreateDirectory(Path.Combine(AppDataFolder, "bg"));
            
            if (!File.Exists(regionBackgroundProp.imgLocalPath))
                httpClient.DownloadFile(regionBackgroundProp.data.adv.background, regionBackgroundProp.imgLocalPath);
        }

        private void ApplyBackground()
        {
            SixLabors.ImageSharp.Image image = SixLabors.ImageSharp.Image.Load(regionBackgroundProp.imgLocalPath);
            Windows.UI.Color accentColor = GetBackgroundColor(regionBackgroundProp.imgLocalPath).GetAwaiter().GetResult();
            
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                HideBackgroundImage();
                BackgroundBitmap = new BitmapImage(new Uri(regionBackgroundProp.imgLocalPath));
                BackgroundBack.Source = BackgroundBitmap;
                BackgroundFront.Source = BackgroundBitmap;

                this.Resources["SystemAccentColorLight2"] = accentColor;
                this.Resources["SystemAccentColorLight1"] = accentColor;

                this.Resources["SystemAccentColor"] = accentColor;

                HideBackgroundImage(false);
            }).AsTask().GetAwaiter();
            Task.Run(() => GetBackgroundColor(regionBackgroundProp.imgLocalPath));
        }
        
        private async Task<Windows.UI.Color> GetBackgroundColor(string inPath)
        {
            Stream stream = new FileStream(inPath, FileMode.Open, FileAccess.Read);
            IRandomAccessStream randomStream = stream.AsRandomAccessStream();
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(randomStream);

            var colorThief = new ColorThief();
            var palette = colorThief.GetColor(decoder).GetAwaiter().GetResult();

            return new Windows.UI.Color
            {
                A = palette.Color.A,
                B = palette.Color.B,
                G = palette.Color.G,
                R = palette.Color.R
            };
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