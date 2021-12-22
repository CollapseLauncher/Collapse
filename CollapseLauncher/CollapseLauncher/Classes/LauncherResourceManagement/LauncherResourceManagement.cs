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

using Hi3Helper.Data;
using Hi3Helper.Preset;

using static Hi3Helper.Logger;
using static CollapseLauncher.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        public void FetchLauncherResourceAsRegion()
        {
            try
            {
                httpClient = new HttpClientTool(true);

                MemoryStream memoryStream = new MemoryStream();

                httpClient.DownloadStream(CurrentRegion.LauncherResourceURL, memoryStream);
                regionResourceProp = JsonConvert.DeserializeObject<RegionResourceProp>(Encoding.UTF8.GetString(memoryStream.ToArray()));
            }
            catch
            {
                LogWriteLine($"Cannot connect to the internet while fetching launcher resource");
            }
        }
    }

    public class RegionResourceProp
    {
        public RegionResourceGame data { get; set; }
    }
    public class RegionResourceGame
    {
        public RegionResourceLatest game { get; set; }
        public RegionResourceLatest pre_download_game { get; set; }
    }

    public class RegionResourceLatest
    {
        public RegionResourceVersion latest { get; set; }
    }

    public class RegionResourceVersion
    {
        public string version { get; set; }
        public string path { get; set; }
        public string size { get; set; }
        public string md5 { get; set; }
    }
}