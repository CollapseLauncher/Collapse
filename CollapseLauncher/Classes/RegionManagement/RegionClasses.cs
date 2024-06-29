using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.JsonConverter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher
{
    public interface IRegionResourceCopyable<out T>
    {
        T Copy();
    }

    [JsonConverter(typeof(JsonStringEnumConverter<PostCarouselType>))]
    public enum PostCarouselType
    {
        POST_TYPE_INFO,
        POST_TYPE_ACTIVITY,
        POST_TYPE_ANNOUNCE
    }

    public static class RegionResourceListHelper
    {
        public static List<T>? Copy<T>(this List<T>? source)
            where T : IRegionResourceCopyable<T>
        {
            if (source == null)
            {
                return null;
            }

            return source.Count == 0 ? new List<T>() : new List<T>(source);
        }
    }

    public class RegionResourceProp : IRegionResourceCopyable<RegionResourceProp>
    {
        public RegionResourceGame? data         { get; set; }
        public string              imgLocalPath { get; set; } = string.Empty;

        public RegionResourceProp Copy()
        {
            return new RegionResourceProp()
            {
                data         = data?.Copy(),
                imgLocalPath = imgLocalPath
            };
        }
    }

    public class RegionResourceGame : IRegionResourceCopyable<RegionResourceGame>
    {
        public List<RegionResourcePlugin>? plugins           { get; set; }
        public RegionResourceLatest?       game              { get; set; }
        public RegionResourceLatest?       pre_download_game { get; set; }
        public RegionBackgroundProp?       adv               { get; set; }
        public RegionResourceVersion?      sdk               { get; set; }
        public List<RegionSocMedProp>?     banner            { get; set; }
        public List<RegionSocMedProp>?     icon              { get; set; }
        public List<RegionSocMedProp>?     post              { get; set; }

        public RegionResourceGame Copy()
        {
            return new RegionResourceGame()
            {
                plugins           = plugins?.Copy(),
                game              = game?.Copy(),
                pre_download_game = pre_download_game?.Copy(),
                adv               = adv?.Copy(),
                sdk               = sdk?.Copy(),
                banner            = banner?.Copy(),
                icon              = icon?.Copy(),
                post              = post?.Copy()
            };
        }
    }

    public class RegionResourcePlugin : IRegionResourceCopyable<RegionResourcePlugin>
    {
        public string?                release_id { get; set; }
        public string?                plugin_id  { get; set; }
        public string?                version    { get; set; }
        public RegionResourceVersion? package    { get; set; }

        public RegionResourcePlugin Copy()
        {
            return new RegionResourcePlugin()
            {
                release_id = release_id,
                plugin_id  = plugin_id,
                version    = version,
                package    = package?.Copy()
            };
        }
    }

    public class RegionResourcePluginValidate : IRegionResourceCopyable<RegionResourcePluginValidate>
    {
        public string? path { get; set; }
        public string? md5  { get; set; }

        public RegionResourcePluginValidate Copy()
        {
            return new RegionResourcePluginValidate()
            {
                path = path,
                md5  = md5
            };
        }
    }

    public class RegionResourceLatest : IRegionResourceCopyable<RegionResourceLatest>
    {
        public RegionResourceVersion?       latest { get; set; }
        public List<RegionResourceVersion>? diffs  { get; set; }

        public RegionResourceLatest Copy()
        {
            return new RegionResourceLatest()
            {
                latest = latest?.Copy(),
                diffs  = diffs?.Copy()
            };
        }
    }

    public class RegionResourceVersion : IRegionResourceCopyable<RegionResourceVersion>
    {
        public string? run_command       { get; set; }
        public string? version           { get; set; }
        public string? url               { get; set; }
        public string? path              { get; set; }
        public string? decompressed_path { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long size { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long package_size { get; set; }

        public string? md5                   { get; set; }
        public string? language              { get; set; }
        public bool    is_recommended_update { get; set; }
        public string? entry                 { get; set; }
        public string? pkg_version           { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? channel_id { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? sub_channel_id { get; set; }

        public List<RegionResourceVersion>? voice_packs { get; set; }
        public List<RegionResourceVersion>? segments    { get; set; }

        [JsonConverter(typeof(RegionResourcePluginValidateConverter))]
        public List<RegionResourcePluginValidate>? validate { get; set; }

        public RegionResourceVersion Copy()
        {
            return new RegionResourceVersion()
            {
                version               = version,
                pkg_version           = pkg_version,
                path                  = path,
                url                   = url,
                decompressed_path     = decompressed_path,
                size                  = size,
                package_size          = package_size,
                md5                   = md5,
                language              = language,
                is_recommended_update = is_recommended_update,
                entry                 = entry,
                voice_packs           = voice_packs?.Copy(),
                segments              = segments?.Copy(),
                validate              = validate?.Copy()
            };
        }
    }

    public class HomeMenuPanel : IRegionResourceCopyable<HomeMenuPanel>
    {
        public List<MenuPanelProp>?  sideMenuPanel      { get; set; }
        public List<MenuPanelProp>?  imageCarouselPanel { get; set; }
        public PostCarouselTypes?    articlePanel       { get; set; }
        public RegionBackgroundProp? eventPanel         { get; set; }

        public HomeMenuPanel Copy()
        {
            return new HomeMenuPanel()
            {
                sideMenuPanel      = sideMenuPanel?.Copy(),
                imageCarouselPanel = imageCarouselPanel?.Copy(),
                articlePanel       = articlePanel?.Copy(),
                eventPanel         = eventPanel?.Copy()
            };
        }
    }

    public class PostCarouselTypes : IRegionResourceCopyable<PostCarouselTypes>
    {
        public List<RegionSocMedProp>? Events  { get; set; } = new();
        public List<RegionSocMedProp>? Notices { get; set; } = new();
        public List<RegionSocMedProp>? Info    { get; set; } = new();

        public PostCarouselTypes Copy()
        {
            return new PostCarouselTypes()
            {
                Events  = Events?.Copy(),
                Notices = Notices?.Copy(),
                Info    = Info?.Copy()
            };
        }
    }

    public class LinkProp : IRegionResourceCopyable<LinkProp>
    {
        public string? title { get; set; }
        public string? url   { get; set; }

        public LinkProp Copy()
        {
            return new LinkProp()
            {
                title = title,
                url   = url
            };
        }
    }

    public struct MenuPanelProp(CancellationToken token = default) : IRegionResourceCopyable<MenuPanelProp>
    {
        private string? _icon;
        private string? _iconHover;
        private string? _qr;

        public string? URL { get; set; }

        public string? Icon
        {
            get => ImageLoaderHelper.GetCachedSprites(_icon, token);
            set => _icon = value;
        }

        public string? IconHover
        {
            get => ImageLoaderHelper.GetCachedSprites(_iconHover, token);
            set => _iconHover = value;
        }

        public string? QR
        {
            get => ImageLoaderHelper.GetCachedSprites(_qr, token);
            set => _qr = value;
        }

        public string?         QR_Description       { get; set; }
        public bool            IsQRExist            => !string.IsNullOrEmpty(QR);
        public string?         Description          { get; set; }
        public bool            IsDescriptionExist   => !string.IsNullOrEmpty(Description);
        public bool            IsQRDescriptionExist => !string.IsNullOrEmpty(QR_Description);
        public List<LinkProp>? Links                { get; set; }
        public bool            IsLinksExist         => Links?.Any() == true;
        public bool            ShowLinks            => IsLinksExist && Links?.Count > 1;
        public bool            ShowDescription      => IsDescriptionExist && !ShowLinks;

        public MenuPanelProp Copy()
        {
            return new MenuPanelProp(token)
            {
                URL            = URL,
                Icon           = _icon,
                IconHover      = _iconHover,
                QR             = _qr,
                QR_Description = QR_Description,
                Description    = Description,
                Links          = Links?.Copy()
            };
        }
    }

    public class RegionBackgroundProp : IRegionResourceCopyable<RegionBackgroundProp>
    {
        public string? background  { get; set; }
        public string? bg_checksum { get; set; }
        public string? icon        { get; set; }
        public string? url         { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? version { get; set; }

        public RegionBackgroundProp Copy()
        {
            return new RegionBackgroundProp()
            {
                background  = background,
                bg_checksum = bg_checksum,
                icon        = icon,
                url         = url,
                version     = version
            };
        }
    }

    public struct RegionSocMedProp : IRegionResourceCopyable<RegionSocMedProp>
    {
        private string          _url;
        private string          _icon_link;
        private string          _tittle;
        private List<LinkProp>? _links;
        private List<LinkProp>? _other_links;

        public string icon_id { get; set; }

        public string icon_link
        {
            get => StripTabsAndNewlines(string.IsNullOrEmpty(_icon_link) ? url : _icon_link);
            set => _icon_link = value;
        }

        public string img       { get; set; }
        public string img_hover { get; set; }
        public string qr_img    { get; set; }
        public string qr_desc   { get; set; }

        public string url
        {
            get => _url;
            set => _url = StripTabsAndNewlines(value);
        }

        public string name  { get; set; }
        public string title { get; set; }

        public string tittle
        {
            get => string.IsNullOrEmpty(_tittle) ? title : _tittle;
            set => _tittle = value;
        }

        public string           show_time { get; set; }
        public PostCarouselType type      { get; set; }

        public List<LinkProp>? links
        {
            get => _links;
            set
            {
                _links = value;
                if (_links == null)
                {
                    return;
                }

                foreach (var link in _links)
                {
                    link.url = StripTabsAndNewlines(link.url);
                }
            }
        }

        public List<LinkProp>? other_links
        {
            get => _other_links;
            set
            {
                _other_links = value;
                if (_other_links == null)
                {
                    return;
                }

                foreach (var link in _other_links)
                {
                    link.url = StripTabsAndNewlines(link.url);
                }
            }
        }

        private unsafe string StripTabsAndNewlines(ReadOnlySpan<char> s)
        {
            int   len         = s.Length;
            char* newChars    = stackalloc char[len];
            char* currentChar = newChars;

            for (int i = 0; i < len; ++i)
            {
                char c = s[i];

                if (c == '\r' || c == '\n' || c == '\t')
                {
                    continue;
                }

                *currentChar++ = c;
            }

            return new string(newChars, 0, (int)(currentChar - newChars));
        }

        public RegionSocMedProp Copy()
        {
            return new RegionSocMedProp()
            {
                icon_id     = icon_id,
                icon_link   = icon_link,
                img         = img,
                img_hover   = img_hover,
                qr_img      = qr_img,
                qr_desc     = qr_desc,
                url         = url,
                name        = name,
                title       = title,
                tittle      = tittle,
                show_time   = show_time,
                type        = type,
                links       = links?.Copy(),
                other_links = other_links?.Copy()
            };
        }
    }
}