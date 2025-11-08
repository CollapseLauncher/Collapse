using CollapseLauncher.Helper.JsonConverter;
using CollapseLauncher.Interfaces.Class;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using WinRT;
// ReSharper disable StringLiteralTypo
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable UnusedMember.Global
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(HypLauncherBackgroundApi))]
    internal sealed partial class HypLauncherBackgroundApiJsonContext : JsonSerializerContext;

    public class HypLauncherBackgroundApi : HypApiResponse<HypLauncherBackgroundList>;

    public class HypLauncherBackgroundList : IList<HypLauncherBackgroundContentList>
    {
        [JsonPropertyName("game_info_list")]
        public List<HypLauncherBackgroundContentList> Backgrounds { get; set; } = new(8);

        public IEnumerator<HypLauncherBackgroundContentList> GetEnumerator() =>
            Backgrounds.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(HypLauncherBackgroundContentList item) => Backgrounds.Add(item);

        public void Clear() => Backgrounds.Clear();

        public bool Contains(HypLauncherBackgroundContentList item) => Backgrounds.Contains(item);

        public void CopyTo(HypLauncherBackgroundContentList[] array, int arrayIndex) => Backgrounds.CopyTo(array, arrayIndex);

        public bool Remove(HypLauncherBackgroundContentList item) => Backgrounds.Remove(item);

        public int IndexOf(HypLauncherBackgroundContentList item) => Backgrounds.IndexOf(item);

        public void Insert(int index, HypLauncherBackgroundContentList item) => Backgrounds.Insert(index, item);

        public void RemoveAt(int index) => Backgrounds.RemoveAt(index);

        [JsonIgnore]
        public int Count => Backgrounds.Count;

        [JsonIgnore]
        public bool IsReadOnly => false;

        [JsonIgnore]
        public bool IsEmpty => Count == 0;

        public HypLauncherBackgroundContentList this[int index]
        {
            get => Backgrounds[index];
            set => Backgrounds[index] = value;
        }

        [JsonIgnore]
        public string? BackgroundImageUrl
        {
            get => Backgrounds.FirstOrDefault()?.FirstOrDefault()?.BackgroundImage?.ImageUrl;
        }

        [JsonIgnore]
        public string? FeaturedEventIconUrl
        {
            get => Backgrounds.FirstOrDefault()?.FirstOrDefault()?.FeaturedEventIcon?.ImageUrl;
        }

        [JsonIgnore]
        public string? FeaturedEventIconHoverUrl
        {
            get => Backgrounds.FirstOrDefault()?.FirstOrDefault()?.FeaturedEventIcon?.ImageHoverUrl;
        }

        [JsonIgnore]
        public string? FeaturedEventIconClickLink
        {
            get => Backgrounds.FirstOrDefault()?.FirstOrDefault()?.FeaturedEventIcon?.ClickLink;
        }
    }

    public class HypLauncherBackgroundContentList : IList<HypLauncherBackgroundContentKindData>
    {
        [JsonPropertyName("backgrounds")]
        public List<HypLauncherBackgroundContentKindData> Backgrounds { get; init; } = [];

        [JsonPropertyName("game")]
        public HypGameInfoData? GameInfo { get; set; }

        public IEnumerator<HypLauncherBackgroundContentKindData> GetEnumerator() =>
            Backgrounds.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(HypLauncherBackgroundContentKindData item) => Backgrounds.Add(item);

        public void Clear() => Backgrounds.Clear();

        public bool Contains(HypLauncherBackgroundContentKindData item) => Backgrounds.Contains(item);

        public void CopyTo(HypLauncherBackgroundContentKindData[] array, int arrayIndex) => Backgrounds.CopyTo(array, arrayIndex);

        public bool Remove(HypLauncherBackgroundContentKindData item) => Backgrounds.Remove(item);

        public int IndexOf(HypLauncherBackgroundContentKindData item) => Backgrounds.IndexOf(item);

        public void Insert(int index, HypLauncherBackgroundContentKindData item) => Backgrounds.Insert(index, item);

        public void RemoveAt(int index) => Backgrounds.RemoveAt(index);

        [JsonIgnore]
        public int Count => Backgrounds.Count;

        [JsonIgnore]
        public bool IsReadOnly => false;

        [JsonIgnore]
        public bool IsEmpty => Count == 0;

        public HypLauncherBackgroundContentKindData this[int index]
        {
            get => Backgrounds[index];
            set => Backgrounds[index] = value;
        }
    }

    public class HypLauncherBackgroundContentKindData
    {
        [JsonPropertyName("background")]
        public HypLauncherMediaContentData? BackgroundImage
        {
            get => string.IsNullOrEmpty(field?.ImageUrl) && string.IsNullOrEmpty(field?.ImageHoverUrl) &&
                   string.IsNullOrEmpty(field?.Title)
                   && string.IsNullOrEmpty(field?.ClickLink) && string.IsNullOrEmpty(field?.Date) &&
                   field?.ContentType == null
                ? null
                : field;
            set;
        }

        [JsonPropertyName("icon")]
        public HypLauncherMediaContentData? FeaturedEventIcon
        {
            get => string.IsNullOrEmpty(field?.ImageUrl) && string.IsNullOrEmpty(field?.ImageHoverUrl) &&
                   string.IsNullOrEmpty(field?.Title)
                   && string.IsNullOrEmpty(field?.ClickLink) && string.IsNullOrEmpty(field?.Date) &&
                   field?.ContentType == null
                ? null
                : field;
            set;
        }

        [JsonPropertyName("video")]
        public HypLauncherMediaContentData? BackgroundVideo
        {
            get;
            set;
        }

        [JsonPropertyName("theme")]
        public HypLauncherMediaContentData? BackgroundOverlay
        {
            get;
            set;
        }

        [JsonPropertyName("id")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Id { get; set; }
    }

    [GeneratedBindableCustomProperty]
    public partial class HypLauncherMediaContentData : NotifyPropertyChanged
    {
        [JsonPropertyName("type")]
        public LauncherGameNewsPostType ContentType
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        [JsonPropertyName("date")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Date
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        [JsonPropertyName("title")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Title
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        [JsonPropertyName("description")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Description
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        [JsonPropertyName("link")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? ClickLink
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        [JsonPropertyName("url")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? ImageUrl
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        [JsonPropertyName("hover_url")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? ImageHoverUrl
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    [GeneratedBindableCustomProperty]
    public partial class LauncherSocialMedia : NotifyPropertyChanged
    {
        [JsonPropertyName("enable_red_dot")]
        public bool NotificationDotEnabled
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        [JsonPropertyName("red_dot_content")]
        public string? NotificationDotContent
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        [JsonPropertyName("icon")]
        public HypLauncherMediaContentData? SocialMediaIcon
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        [JsonPropertyName("id")]
        public string? SocialMediaId
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        [JsonPropertyName("links")]
        public List<HypLauncherMediaContentData>? SocialMediaLinks
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        [JsonPropertyName("qr_desc")]
        public string? SocialMediaQrDescription
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        [JsonPropertyName("qr_image")]
        public HypLauncherMediaContentData? SocialMediaQrImage
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public class HypLauncherNewsContentData
    {
        // Note from @neon-nyan:
        // We preallocate List<T> here to avoid inner buffer resizing while adding items.

        [JsonPropertyName("banners")]
        public List<LauncherNewsBanner> NewsCarouselList { get; set; } = new(8);

        [JsonPropertyName("posts")]
        public List<HypLauncherMediaContentData> NewsPostsList { get; set; } = new(16);

        [JsonPropertyName("social_media_list")]
        public List<LauncherSocialMedia> SocialMediaList { get; set; } = new(8);
    }

    public class LauncherNewsBanner
    {
        [JsonPropertyName("id")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Id { get; set; }

        [JsonPropertyName("image")]
        public HypLauncherMediaContentData? Image { get; set; }
    }
}
