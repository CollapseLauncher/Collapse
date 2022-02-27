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
using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        public void FetchLauncherResourceAsRegion()
        {
            MemoryStream memoryStream = new MemoryStream();
            try
            {
                httpClient = new HttpClientTool(true);

                httpClient.DownloadStream(CurrentRegion.LauncherResourceURL, memoryStream);
                regionResourceProp = JsonConvert.DeserializeObject<RegionResourceProp>(Encoding.UTF8.GetString(memoryStream.ToArray()));
            }
            catch
            {
                LogWriteLine($"Cannot connect to the internet while fetching launcher resource");
            }
            memoryStream.Dispose();
        }
    }
}