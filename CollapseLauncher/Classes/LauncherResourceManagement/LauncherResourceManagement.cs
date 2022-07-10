using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Http;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        public async Task FetchLauncherResourceAsRegion(CancellationToken token)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                await httpHelper.DownloadStream(CurrentRegion.LauncherResourceURL, memoryStream, token);
                regionResourceProp = JsonConvert.DeserializeObject<RegionResourceProp>(Encoding.UTF8.GetString(memoryStream.ToArray()));
            }
        }
    }
}