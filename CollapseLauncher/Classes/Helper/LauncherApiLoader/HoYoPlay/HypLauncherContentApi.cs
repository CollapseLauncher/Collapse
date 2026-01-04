using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.JsonConverter;
using CollapseLauncher.Interfaces.Class;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using WinRT;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;

public class HypLauncherContentApi : HypApiResponse<HypLauncherContentData>;

public class HypLauncherContentData
{
    [JsonPropertyName("content")]
    public HypLauncherContentKind? Content { get; set; }
}

public class HypLauncherContentKind : HypApiIdentifiable
{
    [JsonPropertyName("language")]
    [JsonConverter(typeof(EmptyStringAsNullConverter))]
    public string? Language { get; init; }

    [JsonPropertyName("banners")]
    public List<HypLauncherCarouselContentData> Carousel { get; init; } = [];

    [JsonPropertyName("posts")]
    public List<HypLauncherMediaContentData> News { get; init; } = [];

    [JsonPropertyName("social_media_list")]
    public List<HypLauncherSocialMediaContentData> SocialMedia { get; init; } = [];

    // Extensions
    [JsonIgnore]
    [field: AllowNull, MaybeNull]
    internal List<HypLauncherMediaContentData> NewsEventKind
    {
        get =>
            field ??= News
                     .Where(x => x.ContentType == LauncherGameNewsPostType.POST_TYPE_ACTIVITY)
                     .ToList();
        private set;
    }

    [JsonIgnore]
    [field: AllowNull, MaybeNull]
    internal List<HypLauncherMediaContentData> NewsAnnouncementKind
    {
        get =>
            field ??= News
                     .Where(x => x.ContentType == LauncherGameNewsPostType.POST_TYPE_ANNOUNCE)
                     .ToList();
        private set;
    }

    [JsonIgnore]
    [field: AllowNull, MaybeNull]
    internal List<HypLauncherMediaContentData> NewsInformationKind
    {
        get =>
            field ??= News
                     .Where(x => x.ContentType == LauncherGameNewsPostType.POST_TYPE_INFO)
                     .ToList();
        private set;
    }

    public void ResetCachedNews()
    {
        NewsEventKind = null;
        NewsAnnouncementKind = null;
        NewsInformationKind = null;
    }
}

[GeneratedBindableCustomProperty]
public partial class HypLauncherCarouselContentData : NotifyPropertyChanged
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(EmptyStringAsNullConverter))]
    public string? Id
    {
        get;
        init
        {
            OnPropertyChanged();
            field = value;
        }
    }

    [JsonPropertyName("image")]
    public HypLauncherMediaContentData? Image
    {
        get;
        init
        {
            OnPropertyChanged();
            field = value;
        }
    }

    [JsonPropertyName("i18n_identifier")]
    [JsonConverter(typeof(EmptyStringAsNullConverter))]
    public string? LocalizationIdentifier
    {
        get;
        init
        {
            OnPropertyChanged();
            field = value;
        }
    }
}

[GeneratedBindableCustomProperty]
public partial class HypLauncherSocialMediaContentData : NotifyPropertyChanged
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(EmptyStringAsNullConverter))]
    public string? Id
    {
        get;
        init
        {
            OnPropertyChanged();
            field = value;
        }
    }

    [JsonPropertyName("icon")]
    public HypLauncherMediaContentData? Icon
    {
        get;
        init
        {
            OnPropertyChanged();
            field = value;
        }
    }

    [JsonPropertyName("qr_image")]
    public HypLauncherMediaContentData? QrImage
    {
        get;
        init
        {
            OnPropertyChanged();
            field = value;
        }
    }

    [JsonPropertyName("qr_desc")]
    [JsonConverter(typeof(EmptyStringAsNullConverter))]
    public string? QrDescription
    {
        get;
        init
        {
            OnPropertyChanged();
            field = value;
        }
    }

    [JsonPropertyName("links")]
    public List<HypLauncherMediaContentData> ChildrenLinks
    {
        get;
        init
        {
            OnPropertyChanged();
            field = value;
        }
    } = [];

    [JsonIgnore]
    public bool CanShowFlyout
    {
        get => QrImage?.ImageUrl is not null ||
               ChildrenLinks.Count != 0;
    }
}