using CollapseLauncher.Helper.JsonConverter;
using CollapseLauncher.Helper.LauncherApiLoader.Sophon;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay
{
    public class HoYoPlayLauncherNews
    {
        [JsonPropertyName("data")]
        public LauncherInfoData? Data { get; set; }

        [JsonPropertyName("message")]
        public string? ResultMessage { get; set; }

        [JsonPropertyName("retcode")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? ReturnCode { get; set; }
    }

    public class LauncherInfoData
    {
        [JsonPropertyName("game_info_list")]
        public List<LauncherGameInfo>? GameInfoList { get; set; }

        [JsonPropertyName("content")]
        public LauncherNewsContent? GameNewsContent { get; set; }

        [JsonIgnore]
        public string? BackgroundImageUrl { get => GameInfoList?.FirstOrDefault()?.BackgroundsDetail?.FirstOrDefault()?.BackgroundImage?.ImageUrl; }

        [JsonIgnore]
        public string? FeaturedEventIconUrl { get => GameInfoList?.FirstOrDefault()?.BackgroundsDetail?.FirstOrDefault()?.FeaturedEventIcon?.ImageUrl; }

        [JsonIgnore]
        public string? FeaturedEventIconHoverUrl { get => GameInfoList?.FirstOrDefault()?.BackgroundsDetail?.FirstOrDefault()?.FeaturedEventIcon?.ImageHoverUrl; }

        [JsonIgnore]
        public string? FeaturedEventIconClickLink { get => GameInfoList?.FirstOrDefault()?.BackgroundsDetail?.FirstOrDefault()?.FeaturedEventIcon?.ClickLink; }
    }

    public class LauncherGameInfo
    {
        [JsonPropertyName("backgrounds")]
        public List<LauncherBackgroundsDetail>? BackgroundsDetail { get; set; }

        [JsonPropertyName("game")]
        public GameDetail? GameBiz { get; set; }
    }

    public class LauncherBackgroundsDetail
    {
        private LauncherContentData? _backgroundImage;
        private LauncherContentData? _featuredEventIcon;

        [JsonPropertyName("background")]
        public LauncherContentData? BackgroundImage
        {
            get => string.IsNullOrEmpty(_backgroundImage?.ImageUrl) && string.IsNullOrEmpty(_backgroundImage?.ImageHoverUrl) && string.IsNullOrEmpty(_backgroundImage?.Title)
                && string.IsNullOrEmpty(_backgroundImage?.ClickLink) && string.IsNullOrEmpty(_backgroundImage?.Date) && _backgroundImage?.ContentType == null ? null : _backgroundImage;
            set => _backgroundImage = value;
        }

        [JsonPropertyName("icon")]
        public LauncherContentData? FeaturedEventIcon
        {
            get => string.IsNullOrEmpty(_featuredEventIcon?.ImageUrl) && string.IsNullOrEmpty(_featuredEventIcon?.ImageHoverUrl) && string.IsNullOrEmpty(_featuredEventIcon?.Title)
                && string.IsNullOrEmpty(_featuredEventIcon?.ClickLink) && string.IsNullOrEmpty(_featuredEventIcon?.Date) && _featuredEventIcon?.ContentType == null ? null : _featuredEventIcon;
            set => _featuredEventIcon = value;
        }

        [JsonPropertyName("id")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Id { get; set; }
    }

    public class LauncherContentData
    {
        [JsonPropertyName("type")]
        public LauncherGameNewsPostType? ContentType { get; set; }

        [JsonPropertyName("date")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Date { get; set; }

        [JsonPropertyName("title")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Title { get; set; }

        [JsonPropertyName("link")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? ClickLink { get; set; }

        [JsonPropertyName("url")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("hover_url")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? ImageHoverUrl { get; set; }
    }

    public class LauncherNewsContent
    {
        [JsonPropertyName("banners")]
        public List<LauncherNewsBanner>? NewsCarouselList { get; set; }

        [JsonPropertyName("posts")]
        public List<LauncherContentData>? NewsPostsList { get; set; }
    }

    public class LauncherNewsBanner
    {
        [JsonPropertyName("id")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Id { get; set; }

        [JsonPropertyName("image")]
        public LauncherContentData? Image { get; set; }
    }
}
