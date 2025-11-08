using CollapseLauncher.Helper.JsonConverter;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using WinRT;

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;

[JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
[JsonSerializable(typeof(HypLauncherContentApi))]
internal sealed partial class HypLauncherContentApiJsonContext : JsonSerializerContext;

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
    }

    [JsonIgnore]
    [field: AllowNull, MaybeNull]
    internal List<HypLauncherMediaContentData> NewsAnnouncementKind
    {
        get =>
            field ??= News
                     .Where(x => x.ContentType == LauncherGameNewsPostType.POST_TYPE_ANNOUNCE)
                     .ToList();
    }

    [JsonIgnore]
    [field: AllowNull, MaybeNull]
    internal List<HypLauncherMediaContentData> NewsInformationKind
    {
        get =>
            field ??= News
                     .Where(x => x.ContentType == LauncherGameNewsPostType.POST_TYPE_INFO)
                     .ToList();
    }
}

public class HypLauncherCarouselContentData
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(EmptyStringAsNullConverter))]
    public string? Id { get; init; }

    [JsonPropertyName("image")]
    public HypLauncherMediaContentData? Image { get; init; }

    [JsonPropertyName("i18n_identifier")]
    [JsonConverter(typeof(EmptyStringAsNullConverter))]
    public string? LocalizationIdentifier { get; init; }
}

public class HypLauncherSocialMediaContentData
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(EmptyStringAsNullConverter))]
    public string? Id { get; init; }

    [JsonPropertyName("icon")]
    public HypLauncherMediaContentData? Icon { get; init; }

    [JsonPropertyName("qr_image")]
    public HypLauncherMediaContentData? QrImage { get; init; }

    [JsonPropertyName("qr_desc")]
    [JsonConverter(typeof(EmptyStringAsNullConverter))]
    public string? QrDescription { get; init; }

    [JsonPropertyName("links")]
    [JsonConverter(typeof(EmptyStringAsNullConverter))]
    public List<HypLauncherMediaContentData> ChildrenLinks { get; init; } = [];
}