using Hi3Helper.Preset;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

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
        public List<RegionSocMedProp> banner { get; set; }
        public List<RegionSocMedProp> icon { get; set; }
        public List<RegionSocMedProp> post { get; set; }
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
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long size { get; set; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long package_size { get; set; }
        public string md5 { get; set; }
        public string language { get; set; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? languageID { get; set; }
        public bool is_recommended_update { get; set; }
        public string entry { get; set; }
        public List<RegionResourceVersion> voice_packs { get; set; }
        public List<RegionResourceVersion> segments { get; set; }
    }

    public class HomeMenuPanel
    {
        public List<MenuPanelProp> sideMenuPanel { get; set; }
        public List<MenuPanelProp> imageCarouselPanel { get; set; }
        public PostCarouselTypes articlePanel { get; set; }
        public RegionBackgroundProp eventPanel { get; set; }
        public HomeMenuPanel Copy() => this;
    }

    public class PostCarouselTypes
    {
        public List<RegionSocMedProp> Events { get; set; } = new List<RegionSocMedProp>();
        public List<RegionSocMedProp> Notices { get; set; } = new List<RegionSocMedProp>();
        public List<RegionSocMedProp> Info { get; set; } = new List<RegionSocMedProp>();
    }

    public class MenuPanelProp
    {
        public string URL { get; set; }
        public string Icon { get; set; }
        public string IconHover { get; set; }
        public string QR { get; set; }
        public string QR_Description { get; set; }
        public bool IsQRExist => !string.IsNullOrEmpty(QR);
        public string Description { get; set; }
        public bool IsDescriptionExist => !string.IsNullOrEmpty(Description);
        public bool IsQRDescriptionExist => !string.IsNullOrEmpty(QR_Description);
    }

    public class RegionBackgroundProp
    {
        public string background { get; set; }
        public string bg_checksum { get; set; }
        public string icon { get; set; }
        public string url { get; set; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? version { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PostCarouselType
    {
        POST_TYPE_INFO,
        POST_TYPE_ACTIVITY,
        POST_TYPE_ANNOUNCE
    }

    public class RegionSocMedProp
    {
        private string _url;

        public string icon_id { get; set; }
        public string icon_link { get; set; }
        public string img { get; set; }
        public string img_hover { get; set; }
        public string qr_img { get; set; }
        public string qr_desc { get; set; }
        public string url
        {
            get => StripTabsAndNewlines(string.IsNullOrEmpty(_url) ? icon_link : _url);
            set => _url = value;
        }
        public string name { get; set; }
        public string title { get; set; }
        public string show_time { get; set; }
        public PostCarouselType type { get; set; }

        private unsafe string StripTabsAndNewlines(ReadOnlySpan<char> s)
        {
            int len = s.Length;
            char* newChars = stackalloc char[len];
            char* currentChar = newChars;

            for (int i = 0; i < len; ++i)
            {
                char c = s[i];

                if (c == '\r' || c == '\n' || c == '\t') continue;
                *currentChar++ = c;
            }
            return new string(newChars, 0, (int)(currentChar - newChars));
        }
    }

    public class YSDispatchInfo
    {
        public string content { get; set; }
        public string sign { get; set; }
    }

    public class QueryProperty
    {
        public string GameServerName { get; set; }
        public string ClientGameResURL { get; set; }
        public string ClientDesignDataURL { get; set; }
        public string ClientDesignDataSilURL { get; set; }
        public string ClientAudioAssetsURL { get; set; }
        public uint AudioRevisionNum { get; set; }
        public uint DataRevisionNum { get; set; }
        public uint ResRevisionNum { get; set; }
        public uint SilenceRevisionNum { get; set; }
        public string GameVersion { get; set; }
        public string ChannelName { get; set; }
        public IEnumerable<PkgVersionProperties> ClientGameRes { get; set; }
        public PkgVersionProperties ClientDesignData { get; set; }
        public PkgVersionProperties ClientDesignDataSil { get; set; }
    }
}
