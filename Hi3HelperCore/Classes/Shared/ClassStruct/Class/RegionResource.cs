using System.Collections.Generic;

namespace Hi3Helper.Shared.ClassStruct
{
    public class RegionResourceProp
    {
        public RegionResourceGame data { get; set; }
        public string imgLocalPath { get; set; } = string.Empty;
    }
    public class RegionResourceGame
    {
        public RegionResourceLatest game { get; set; }
        public RegionResourceLatest pre_download_game { get; set; }
        public RegionBackgroundProp adv { get; set; }
        public List<RegionSocMedProp> icon { get; set; }
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
        public int? languageID { get; set; }
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

    public class RegionBackgroundProp
    {
        public string background { get; set; }
        public string bg_checksum { get; set; }
    }

    public class RegionSocMedProp
    {
        public string icon_id { get; set; }
        public string img { get; set; }
        public string img_hover { get; set; }
        public string url { get; set; }
    }
}
