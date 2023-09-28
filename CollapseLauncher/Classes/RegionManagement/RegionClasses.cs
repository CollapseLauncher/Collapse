using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;

namespace CollapseLauncher
{
    public interface IRegionResourceCopyable<T>
    {
        T Copy();
    }

#if NET8_0_OR_GREATER
    [JsonConverter(typeof(JsonStringEnumConverter<PostCarouselType>))]
#else
    [JsonConverter(typeof(JsonStringEnumConverter))]
#endif
    public enum PostCarouselType
    {
        POST_TYPE_INFO,
        POST_TYPE_ACTIVITY,
        POST_TYPE_ANNOUNCE
    }

    public static class RegionResourceListHelper
    {
        public static RegionResourceProp regionBackgroundProp = new RegionResourceProp();
        public static HomeMenuPanel regionNewsProp = new HomeMenuPanel();
        public static IList<T> Copy<T>(this IList<T> source)
            where T : IRegionResourceCopyable<T>
        {
            if (source == null) return null;
            if (source.Count == 0) return new List<T>();
            return new List<T>(source);
        }
    }

    public class RegionResourceProp : IRegionResourceCopyable<RegionResourceProp>
    {
        public RegionResourceGame data { get; set; }
        public string imgLocalPath { get; set; } = string.Empty;
        public RegionResourceProp Copy() => new RegionResourceProp()
        {
            data = data?.Copy(),
            imgLocalPath = imgLocalPath
        };
    }

    public class RegionResourceGame : IRegionResourceCopyable<RegionResourceGame>
    {
        public RegionResourceLatest game { get; set; }
        public RegionResourceLatest pre_download_game { get; set; }
        public RegionBackgroundProp adv { get; set; }
        public IList<RegionSocMedProp> banner { get; set; }
        public IList<RegionSocMedProp> icon { get; set; }
        public IList<RegionSocMedProp> post { get; set; }
        public RegionResourceGame Copy() => new RegionResourceGame()
        {
            game = game?.Copy(),
            pre_download_game = pre_download_game?.Copy(),
            adv = adv?.Copy(),
            banner = banner?.Copy(),
            icon = icon?.Copy(),
            post = post?.Copy()
        };
    }

    public class RegionResourceLatest : IRegionResourceCopyable<RegionResourceLatest>
    {
        public RegionResourceVersion latest { get; set; }
        public IList<RegionResourceVersion> diffs { get; set; }
        public RegionResourceLatest Copy() => new RegionResourceLatest()
        {
            latest = latest?.Copy(),
            diffs = diffs?.Copy()
        };
    }

    public class RegionResourceVersion : IRegionResourceCopyable<RegionResourceVersion>
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
        public IList<RegionResourceVersion> voice_packs { get; set; }
        public IList<RegionResourceVersion> segments { get; set; }
        public RegionResourceVersion Copy() => new RegionResourceVersion()
        {
            version = version,
            path = path,
            decompressed_path = decompressed_path,
            size = size,
            package_size = package_size,
            md5 = md5,
            language = language,
            languageID = languageID,
            is_recommended_update = is_recommended_update,
            entry = entry,
            voice_packs = voice_packs?.Copy(),
            segments = segments?.Copy()
        };
    }

    public class HomeMenuPanel : IRegionResourceCopyable<HomeMenuPanel>
    {
        public IList<MenuPanelProp> sideMenuPanel { get; set; }
        public IList<MenuPanelProp> imageCarouselPanel { get; set; }
        public PostCarouselTypes articlePanel { get; set; }
        public RegionBackgroundProp eventPanel { get; set; }
        public HomeMenuPanel Copy() => new HomeMenuPanel()
        {
            sideMenuPanel = sideMenuPanel?.Copy(),
            imageCarouselPanel = imageCarouselPanel?.Copy(),
            articlePanel = articlePanel?.Copy(),
            eventPanel = eventPanel?.Copy()
        };
    }

    public class PostCarouselTypes : IRegionResourceCopyable<PostCarouselTypes>
    {
        public IList<RegionSocMedProp> Events { get; set; } = new List<RegionSocMedProp>();
        public IList<RegionSocMedProp> Notices { get; set; } = new List<RegionSocMedProp>();
        public IList<RegionSocMedProp> Info { get; set; } = new List<RegionSocMedProp>();
        public PostCarouselTypes Copy() => new PostCarouselTypes()
        {
            Events = Events?.Copy(),
            Notices = Notices?.Copy(),
            Info = Info?.Copy()
        };
    }

    public class LinkProp : IRegionResourceCopyable<LinkProp>
    {
        public string title { get; set; }
        public string url { get; set; }
        public LinkProp Copy() => new LinkProp
        {
            title = title,
            url = url
        };
    }

    public struct MenuPanelProp : IRegionResourceCopyable<MenuPanelProp>
    {
        private string _icon;
        private string _iconHover;
        private string _qr;
        private CancellationToken _innerToken;

        public MenuPanelProp(CancellationToken token = default) => _innerToken = token;

        public string URL { get; set; }
        public string Icon { get => MainPage.GetCachedSprites(_icon, _innerToken); set => _icon = value; }
        public string IconHover { get => MainPage.GetCachedSprites(_iconHover, _innerToken); set => _iconHover = value; }
        public string QR { get => MainPage.GetCachedSprites(_qr, _innerToken); set => _qr = value; }
        public string QR_Description { get; set; }
        public bool IsQRExist => !string.IsNullOrEmpty(QR);
        public string Description { get; set; }
        public bool IsDescriptionExist => !string.IsNullOrEmpty(Description);
        public bool IsQRDescriptionExist => !string.IsNullOrEmpty(QR_Description);
        public IList<LinkProp> Links { get; set; }
        public bool IsLinksExist => Links?.Any() == true;
        public bool ShowLinks => IsLinksExist && Links.Count > 1;
        public bool ShowDescription => IsDescriptionExist && !ShowLinks;
        public MenuPanelProp Copy() => new MenuPanelProp(_innerToken)
        {
            URL = URL,
            Icon = _icon,
            IconHover = _iconHover,
            QR = _qr,
            QR_Description = QR_Description,
            Description = Description,
            Links = Links?.Copy()
    };
    }

    public class RegionBackgroundProp : IRegionResourceCopyable<RegionBackgroundProp>
    {
        public string background { get; set; }
        public string bg_checksum { get; set; }
        public string icon { get; set; }
        public string url { get; set; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? version { get; set; }

        public RegionBackgroundProp Copy() => new RegionBackgroundProp
        {
            background = background,
            bg_checksum = bg_checksum,
            icon = icon,
            url = url,
            version = version
        };
    }

    public struct RegionSocMedProp : IRegionResourceCopyable<RegionSocMedProp>
    {
        private string _url;
        private string _icon_link;
        private string _tittle;
        private CancellationToken _innerToken;
        private IList<LinkProp> _links;
        private IList<LinkProp> _other_links;
        private string _img;
        private string _img_hover;
        private string _qr_img;

        public RegionSocMedProp(CancellationToken token = default)
        {
            _innerToken = token;
        }

        public string icon_id { get; set; }
        public string icon_link
        { 
            get => StripTabsAndNewlines(string.IsNullOrEmpty(_icon_link) ? url : _icon_link);
            set => _icon_link = value;
        }
        public string img { get => MainPage.GetCachedSprites(_img, _innerToken); set => _img = value; }
        public string img_hover { get => MainPage.GetCachedSprites(_img_hover, _innerToken); set => _img_hover = value; }
        public string qr_img { get => MainPage.GetCachedSprites(_qr_img, _innerToken); set => _qr_img = value; }
        public string qr_desc { get; set; }
        public string url
        {
            get => _url;
            set => _url = StripTabsAndNewlines(value);
        }
        public string name { get; set; }
        public string title { get; set; }
        public string tittle
        {
            get => string.IsNullOrEmpty(_tittle) ? title : _tittle;
            set => _tittle = value;
        }
        public string show_time { get; set; }
        public PostCarouselType type { get; set; }

        public IList<LinkProp> links
        {
            get => _links;
            set
            {
                _links = value;
                foreach (var link in _links)
                {
                    link.url = StripTabsAndNewlines(link.url);
                }
            }
        }

        public IList<LinkProp> other_links
        {
            get => _other_links;
            set
            {
                _other_links = value;
                foreach (var link in _other_links)
                {
                    link.url = StripTabsAndNewlines(link.url);
                }
            }
        }

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

        public RegionSocMedProp Copy() => new RegionSocMedProp(_innerToken)
        {
            icon_id = icon_id,
            icon_link = icon_link,
            img = _img,
            img_hover = _img_hover,
            qr_img = _qr_img,
            qr_desc = qr_desc,
            url = url,
            name = name,
            title = title,
            tittle = tittle,
            show_time = show_time,
            type = type,
            links = links?.Copy(),
            other_links = other_links?.Copy()
        };
    }
}
