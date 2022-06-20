using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        public async Task FetchLauncherResourceAsRegion(CancellationToken token)
        {
            MemoryStream memoryStream = new MemoryStream();
            try
            {
                httpHelper = new HttpClientHelper(false);
                regionNewsProp = new HomeMenuPanel();

                await httpHelper.DownloadFileAsync(CurrentRegion.LauncherResourceURL, memoryStream, token);
                regionResourceProp = JsonConvert.DeserializeObject<RegionResourceProp>(Encoding.UTF8.GetString(memoryStream.ToArray()));
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException($"Fetching launcher resource is canceled!");
            }
            catch (Exception ex)
            {
                LogWriteLine($"Cannot connect to the internet while fetching launcher resource.\r\n{ex}");
            }
            memoryStream.Dispose();
        }
    }
}