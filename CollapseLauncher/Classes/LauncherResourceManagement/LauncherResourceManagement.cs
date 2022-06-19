using Aiursoft.HSharp.Methods;
using Aiursoft.HSharp.Models;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                regionNewsProp.sideMenuPanel = new List<MenuPanelProp>();
                regionNewsProp.imageCarouselPanel = new List<MenuPanelProp>();

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

        public async Task GetLauncherAdvInfoOld(CancellationToken token)
        {
            if (CurrentRegion.LauncherInfoURL == null) return;

            MemoryStream memoryStream = new MemoryStream();

            await httpHelper.DownloadFileAsync(CurrentRegion.LauncherInfoURL, memoryStream = new MemoryStream(), token);
            HDoc infoProp = new HDoc();
            await Task.Run(async () =>
            {
                infoProp = HtmlConvert.DeserializeHtml(Encoding.UTF8.GetString(memoryStream.ToArray()));

                try
                {
                    regionNewsProp.sideMenuPanel = await GetSideMenuPanel(infoProp["html"]["body"]["div"]["div"]["div"]["div"]["div", 1]);
                }
                catch
                {
                    regionNewsProp.sideMenuPanel = await GetSideMenuPanelV2(infoProp["html"]["body"]["div"]["div"]["div"]["div"]["div", 1]);
                }

                try
                {
                    regionNewsProp.imageCarouselPanel = await GetCarouselPanel(infoProp["html"]["body"]["div"]["div"]["div"]["div"]["div"]["div"]["div"]["div"]);
                }
                catch (Exception)
                {
                    LogWriteLine($"This region {CurrentRegion.ZoneName} doesn't have banner to load");
                }
            });
        }

        public async Task<List<MenuPanelProp>> GetSideMenuPanel(HTag input)
        {
            List<MenuPanelProp> panel = new List<MenuPanelProp>();
            foreach (var tag in input)
            {
                panel.Add(new MenuPanelProp
                {
                    URL = tag.Properties["href"],
                    Icon = await GetCachedSprites(tag.Where(x => x.Properties.ContainsValue("home-menu__icon")).First().Properties["src"]),
                    IconHover = await GetCachedSprites(tag.Where(x => x.Properties.ContainsValue("home-menu__icon--hover")).First().Properties["src"])
                });
            }

            return panel;
        }

        public async Task<List<MenuPanelProp>> GetCarouselPanel(HTag input)
        {
            List<MenuPanelProp> panel = new List<MenuPanelProp>();
            foreach (var tag in input)
            {
                panel.Add(new MenuPanelProp
                {
                    URL = tag["a"].Properties["href"],
                    Icon = await GetCachedSprites(tag["a"]["img"].Properties["src"])
                });
            }

            return panel;
        }

        public async Task<List<MenuPanelProp>> GetSideMenuPanelV2(HTag input)
        {
            List<MenuPanelProp> panel = new List<MenuPanelProp>();
            foreach (var tag in input.Where(x => x.TagName == "div"))
            {
                try
                {
                    panel.Add(new MenuPanelProp
                    {
                        Icon = await GetCachedSprites(tag.Where(x => x.Properties.ContainsValue("home-menu__icon")).First().Properties["src"]),
                        IconHover = await GetCachedSprites(tag.Where(x => x.Properties.ContainsValue("home-menu__icon--hover")).First().Properties["src"]),
                        URL = tag.Where(x => x.Properties.ContainsValue("home-menu-popover")).First()["a"].Properties["href"]
                    });
                }
                catch { }
            }

            return panel;
        }
    }
}