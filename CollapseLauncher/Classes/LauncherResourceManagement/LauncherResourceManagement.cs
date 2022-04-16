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

using Aiursoft.HSharp;
using Aiursoft.HSharp.Methods;
using Aiursoft.HSharp.Models;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        public void FetchLauncherResourceAsRegion(CancellationToken token)
        {
            MemoryStream memoryStream = new MemoryStream();
            try
            {
                httpClient = new HttpClientToolLegacy();
                httpHelper = new HttpClientHelper(false);
                httpClient4Img = new HttpClientToolLegacy();
                regionNewsProp = new HomeMenuPanel();
                regionNewsProp.sideMenuPanel = new List<MenuPanelProp>();
                regionNewsProp.imageCarouselPanel = new List<MenuPanelProp>();

                httpHelper.DownloadFile(CurrentRegion.LauncherResourceURL, memoryStream, token);
                // httpClient.DownloadStream(CurrentRegion.LauncherResourceURL, memoryStream, token);
                regionResourceProp = JsonConvert.DeserializeObject<RegionResourceProp>(Encoding.UTF8.GetString(memoryStream.ToArray()));

                if (CurrentRegion.LauncherInfoURL != null)
                {
                    httpHelper.DownloadFile(CurrentRegion.LauncherInfoURL, memoryStream = new MemoryStream(), token);
                    // httpClient4Img.DownloadStream(CurrentRegion.LauncherInfoURL, memoryStream = new MemoryStream(), token);
                    HDoc infoProp = HtmlConvert.DeserializeHtml(Encoding.UTF8.GetString(memoryStream.ToArray()));
                    try
                    {
                        regionNewsProp.sideMenuPanel = GetSideMenuPanel(infoProp["html"]["body"]["div"]["div"]["div"]["div"]["div", 1]);
                    }
                    catch
                    {
                        regionNewsProp.sideMenuPanel = GetSideMenuPanelV2(infoProp["html"]["body"]["div"]["div"]["div"]["div"]["div", 1]);
                    }

                    try
                    {
                        regionNewsProp.imageCarouselPanel = GetCarouselPanel(infoProp["html"]["body"]["div"]["div"]["div"]["div"]["div"]["div"]["div"]["div"]);
                    }
                    catch (Exception)
                    {
#if DEBUG
                        LogWriteLine($"This region {CurrentRegion.ZoneName} doesn't have banner to load");
#endif
                    }
                }
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

        public List<MenuPanelProp> GetSideMenuPanel(HTag input)
        {
            List<MenuPanelProp> panel = new List<MenuPanelProp>();
            foreach (var tag in input)
            {
                panel.Add(new MenuPanelProp
                {
                    URL = tag.Properties["href"],
                    Icon = GetCachedSprites(tag.Where(x => x.Properties.ContainsValue("home-menu__icon")).First().Properties["src"]),
                    IconHover = GetCachedSprites(tag.Where(x => x.Properties.ContainsValue("home-menu__icon--hover")).First().Properties["src"])
                });
            }

            return panel;
        }

        public List<MenuPanelProp> GetCarouselPanel(HTag input)
        {
            List<MenuPanelProp> panel = new List<MenuPanelProp>();
            foreach (var tag in input)
            {
                panel.Add(new MenuPanelProp
                {
                    URL = tag["a"].Properties["href"],
                    Icon = GetCachedSprites(tag["a"]["img"].Properties["src"])
                });
            }

            return panel;
        }

        public List<MenuPanelProp> GetSideMenuPanelV2(HTag input)
        {
            List<MenuPanelProp> panel = new List<MenuPanelProp>();
            foreach (var tag in input.Where(x => x.TagName == "div"))
            {
                try
                {
                    panel.Add(new MenuPanelProp
                    {
                        Icon = GetCachedSprites(tag.Where(x => x.Properties.ContainsValue("home-menu__icon")).First().Properties["src"]),
                        IconHover = GetCachedSprites(tag.Where(x => x.Properties.ContainsValue("home-menu__icon--hover")).First().Properties["src"]),
                        URL = tag.Where(x => x.Properties.ContainsValue("home-menu-popover")).First()["a"].Properties["href"]
                    });
                }
                catch { }
            }

            return panel;
        }

        public string GetCachedSprites(string URL)
        {
            string cacheFolder = Path.Combine(AppGameImgFolder, "cache");
            string cachePath = Path.Combine(cacheFolder, Path.GetFileNameWithoutExtension(URL));
            if (!Directory.Exists(cacheFolder))
                Directory.CreateDirectory(cacheFolder);

            if (!File.Exists(cachePath)) httpHelper.DownloadFile(URL, cachePath, 4, new CancellationToken());

            return cachePath;
        }
    }
}