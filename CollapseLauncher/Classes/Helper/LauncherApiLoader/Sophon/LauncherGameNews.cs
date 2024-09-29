#nullable enable
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.JsonConverter;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using WinRT;
// ReSharper disable PartialTypeWithSinglePart

namespace CollapseLauncher.Helper.LauncherApiLoader.Sophon
{
    [JsonConverter(typeof(JsonStringEnumConverter<LauncherGameNewsPostType>))]
    public enum LauncherGameNewsPostType
    {
        POST_TYPE_INFO,
        POST_TYPE_ACTIVITY,
        POST_TYPE_ANNOUNCE
    }

    [JsonConverter(typeof(JsonStringEnumConverter<LauncherGameAvailabilityStatus>))]
    public enum LauncherGameAvailabilityStatus
    {
        LAUNCHER_GAME_DISPLAY_STATUS_AVAILABLE,
        LAUNCHER_GAME_DISPLAY_STATUS_RESERVATION_ENABLED,
        LAUNCHER_GAME_DISPLAY_STATUS_COMING_SOON
    }

    public interface ILauncherGameNewsDataTokenized
    {
        public CancellationToken? InnerToken { get; set; }
    }

    public class LauncherGameNews
    {
        [JsonPropertyName("retcode")] public int? ReturnCode { get; init; }

        [JsonPropertyName("message")] public string? ReturnMessage { get; init; }

        [JsonPropertyName("data")] public LauncherGameNewsData? Content { get; set; }
    }

    public class LauncherGameNewsData
    {
        private List<LauncherGameNewsPost>? _newsPostTypeInfo;
        private List<LauncherGameNewsPost>? _newsPostTypeActivity;
        private List<LauncherGameNewsPost>? _newsPostTypeAnnouncement;

        private List<LauncherGameNewsCarousel>? _newsCarousel;
        private List<LauncherGameNewsPost>?     _newsPost;

        public HoYoPlayGameInfoField GameInfoField { get; init; } = new HoYoPlayGameInfoField();

        [JsonPropertyName("adv")] public LauncherGameNewsBackground? Background { get; set; }

        [JsonPropertyName("banner")]
        public List<LauncherGameNewsCarousel>? NewsCarousel 
        {
            get => _newsCarousel;
            set => _newsCarousel = value?.OrderBy(x => x.CarouselOrder).ToList();
        }

        [JsonPropertyName("post")]
        public List<LauncherGameNewsPost>? NewsPost
        {
            get => _newsPost;
            set => _newsPost = value?.OrderBy(x => x.PostOrder).ToList();
        }

        [JsonPropertyName("icon")] public List<LauncherGameNewsSocialMedia>? SocialMedia { get; set; }

        [JsonIgnore]
        public List<LauncherGameNewsPost>? NewsPostTypeInfo
        {
            get
            {
                if (NewsPost == null)
                {
                    return null;
                }

                if (_newsPostTypeInfo != null)
                {
                    return _newsPostTypeInfo;
                }

                _newsPostTypeInfo = NewsPost.Where(x => x.PostType == LauncherGameNewsPostType.POST_TYPE_INFO)
                                            .OrderBy(x => x.PostOrder)
                                            .ToList();
                return _newsPostTypeInfo;
            }
        }

        [JsonIgnore]
        public List<LauncherGameNewsPost>? NewsPostTypeActivity
        {
            get
            {
                if (NewsPost == null)
                {
                    return null;
                }

                if (_newsPostTypeActivity != null)
                {
                    return _newsPostTypeActivity;
                }

                _newsPostTypeActivity = NewsPost.Where(x => x.PostType == LauncherGameNewsPostType.POST_TYPE_ACTIVITY)
                                                .OrderBy(x => x.PostOrder)
                                                .ToList();
                return _newsPostTypeActivity;
            }
        }

        [JsonIgnore]
        public List<LauncherGameNewsPost>? NewsPostTypeAnnouncement
        {
            get
            {
                if (NewsPost == null)
                {
                    return null;
                }

                if (_newsPostTypeAnnouncement != null)
                {
                    return _newsPostTypeAnnouncement;
                }

                _newsPostTypeAnnouncement = NewsPost
                                           .Where(x => x.PostType == LauncherGameNewsPostType.POST_TYPE_ANNOUNCE)
                                           .OrderBy(x => x.PostOrder)
                                           .ToList();
                return _newsPostTypeAnnouncement;
            }
        }

        public void InjectDownloadableItemCancelToken(CancellationToken token)
        {
            InjectDownloadableItemCancelTokenInner(NewsCarousel, token);
            InjectDownloadableItemCancelTokenInner(SocialMedia,  token);
        }

        private void InjectDownloadableItemCancelTokenInner<T>(IEnumerable<T>? prop, CancellationToken token)
            where T : ILauncherGameNewsDataTokenized
        {
            if (prop == null)
            {
                return;
            }

            foreach (T? propValue in prop)
            {
                InjectDownloadableItemCancelTokenInner(propValue, token);
            }
        }

        private static void InjectDownloadableItemCancelTokenInner<T>(T? prop, CancellationToken token)
            where T : ILauncherGameNewsDataTokenized
        {
            if (prop == null)
            {
                return;
            }

            prop.InnerToken = token;
        }
    }

    public class LauncherGameNewsBackground
    {
        [JsonPropertyName("background")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? BackgroundImg { get; set; }

        [JsonPropertyName("version")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? BackgroundVersion { get; init; }

        [JsonPropertyName("bg_checksum")] public string? BackgroundChecksum { get; init; }

        [JsonPropertyName("icon")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? FeaturedEventIconBtnImg { get; set; }

        [JsonPropertyName("url")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]

        public string? FeaturedEventIconBtnUrl { get; set; }
    }

    public class LauncherGameNewsCarousel : ILauncherGameNewsDataTokenized
    {
        private readonly string? _carouselImg;

        [JsonIgnore] public CancellationToken? InnerToken { get; set; }

        [JsonPropertyName("banner_id")] public string? CarouselId { get; init; }

        [JsonPropertyName("name")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? CarouselTitle { get; init; }

        [JsonPropertyName("img")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? CarouselImg
        {
            get => ImageLoaderHelper.GetCachedSprites(_carouselImg, InnerToken ?? default);
            init => _carouselImg = value;
        }

        [JsonPropertyName("url")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? CarouselUrl { get; init; }

        // TODO: Make sure that the value always be a number. If yes, then we
        //       will change the type from string? to int?
        [JsonPropertyName("order")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? CarouselOrder { get; init; }
    }

    [GeneratedBindableCustomProperty]
    public partial class LauncherGameNewsSocialMedia : ILauncherGameNewsDataTokenized
    {
        private readonly string? _qrImg;
        private readonly List<LauncherGameNewsSocialMediaQrLinks>? _qrLinks;
        private readonly string? _iconImg;
        private readonly string? _iconImgHover;
        private readonly string? _socialMediaUrl;
        private readonly string? _title;

        [JsonIgnore] public CancellationToken? InnerToken { get; set; }

        [JsonPropertyName("icon_id")] public string? IconId { get; init; }

        [JsonPropertyName("img")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? IconImg
        {
            get => ImageLoaderHelper.GetCachedSprites(_iconImg, InnerToken ?? default);
            init => _iconImg = value;
        }

        [JsonPropertyName("img_hover")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? IconImgHover
        {
            get => ImageLoaderHelper.GetCachedSprites(_iconImgHover, InnerToken ?? default);
            init => _iconImgHover = value;
        }

        [JsonPropertyName("url")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? SocialMediaUrl
        {
            get => string.IsNullOrEmpty(_socialMediaUrl) && QrLinks != null && QrLinks.Count != 0
                ? QrLinks[0].Url
                : _socialMediaUrl;

            init => _socialMediaUrl = value;
        }

        [JsonPropertyName("title")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Title
        {
            get => string.IsNullOrEmpty(_title) || (QrLinks?.Any(x => x.Title == _title) ?? false) ? null : _title;
            init => _title = value;
        }

        [JsonPropertyName("qr_img")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? QrImg
        {
            get => ImageLoaderHelper.GetCachedSprites(_qrImg, InnerToken ?? default);
            init => _qrImg = value;
        }

        [JsonPropertyName("qr_desc")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? QrTitle { get; init; }

        [JsonPropertyName("links")]
        public List<LauncherGameNewsSocialMediaQrLinks>? QrLinks
        {
            get => _qrLinks;
            init
            {
                _qrLinks = value?.Where(x => !(string.IsNullOrEmpty(x.Url) || string.IsNullOrEmpty(x.Title))).ToList();
                if (_qrLinks?.Count == 0)
                    _qrLinks = null;
            }
        }

        [JsonIgnore] public bool IsHasDescription => !string.IsNullOrEmpty(Title);

        [JsonIgnore]
        public bool IsHasLinks => (QrLinks?.Count ?? 0) != 0 || (!string.IsNullOrEmpty(QrLinks?[0].Url) &&
                                  QrLinks?[0].Url != _socialMediaUrl);

        [JsonIgnore] public bool IsHasQr => !string.IsNullOrEmpty(QrImg);

        [JsonIgnore] public bool IsHasQrDescription => !string.IsNullOrEmpty(QrTitle);
    }

    [GeneratedBindableCustomProperty]
    public partial class LauncherGameNewsSocialMediaQrLinks
    {
        [JsonPropertyName("title")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Title { get; init; }

        [JsonPropertyName("url")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? Url { get; init; }
    }

    public class LauncherGameNewsPost
    {
        private readonly string? _title;

        [JsonPropertyName("post_id")]
        public string? PostId { get; init; }

        [JsonPropertyName("type")]
        public LauncherGameNewsPostType? PostType { get; init; }

        [JsonPropertyName("url")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? PostUrl { get; init; }

        [JsonPropertyName("show_time")]
        public string? PostDate { get; init; }

        [JsonPropertyName("title")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Title
        {
            get => string.IsNullOrEmpty(_title) ? PostUrl : _title;
            init => _title = value;
        }

        // TODO: Make sure that the value always be a number. If yes, then we
        //       will change the type from string? to int?
        [JsonPropertyName("order")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? PostOrder { get; init; }
    }
}