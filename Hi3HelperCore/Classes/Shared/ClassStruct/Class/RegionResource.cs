using System;
using System.Collections.Generic;
using System.Text;

namespace Hi3Helper.Shared.ClassStruct
{
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
        public List<RegionResourceVersion> diffs { get; set; }
    }

    public class RegionResourceVersion
    {
        public string version { get; set; }
        public string path { get; set; }
        public string decompressed_path { get; set; }
        public long size { get; set; }
        public long package_size { get; set; }
        public string md5 { get; set; }
        public string language { get; set; }
        public bool is_recommended_update { get; set; }
        public string entry { get; set; }
        public List<RegionResourceVersion> voice_packs { get; set; }
    }

    public class HomeMenuPanel
    {
        public List<MenuPanelProp> sideMenuPanel { get; set; }
        public List<MenuPanelProp> imageCarouselPanel { get; set; }
    }

    public class MenuPanelProp
    {
        public string URL { get; set; }
        public string Icon { get; set; }
        public string IconHover { get; set; }
    }
}
