using CollapseLauncher.Helper.JsonConverter;
using CollapseLauncher.Interfaces.Class;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using WinRT;
// ReSharper disable StringLiteralTypo
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable UnusedMember.Global
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;

public class HypLauncherBackgroundApi : HypApiResponse<HypLauncherBackgroundList>;

[GeneratedBindableCustomProperty]
public partial class HypLauncherBackgroundList : NotifyPropertyChanged
{
    [JsonPropertyName("game_info_list")]
    public List<HypLauncherBackgroundContentList> GameContentList { get; init; } = [];

    [JsonIgnore]
    public string? BackgroundImageUrl => GameContentList.FirstOrDefault()?.Backgrounds.FirstOrDefault()?.BackgroundImage?.ImageUrl;

    [JsonIgnore]
    public string? FeaturedEventIconUrl => GameContentList.FirstOrDefault()?.Backgrounds.FirstOrDefault()?.FeaturedEventIcon?.ImageUrl;

    [JsonIgnore]
    public string? FeaturedEventIconHoverUrl => GameContentList.FirstOrDefault()?.Backgrounds.FirstOrDefault()?.FeaturedEventIcon?.ImageHoverUrl;

    [JsonIgnore]
    public string? FeaturedEventIconClickLink => GameContentList.FirstOrDefault()?.Backgrounds.FirstOrDefault()?.FeaturedEventIcon?.ClickLink;
}

[GeneratedBindableCustomProperty]
public partial class HypLauncherBackgroundContentList : NotifyPropertyChanged
{
    [JsonPropertyName("backgrounds")]
    public List<HypLauncherBackgroundContentKindData> Backgrounds
    {
        get;
        init
        {
            field = value;
            OnPropertyChanged();
        }
    } = [];

    [JsonPropertyName("game")]
    public HypGameInfoData? GameInfo
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
public partial class HypLauncherBackgroundContentKindData : NotifyPropertyChanged
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
        set
        {
            field = value;
            OnPropertyChanged();
        }
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
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("video")]
    public HypLauncherMediaContentData? BackgroundVideo
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("theme")]
    public HypLauncherMediaContentData? BackgroundOverlay
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("id")]
    [JsonConverter(typeof(EmptyStringAsNullConverter))]
    public string? Id
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
        get => field ?? Title;
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
        get => field ?? ImageUrl;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }
}
