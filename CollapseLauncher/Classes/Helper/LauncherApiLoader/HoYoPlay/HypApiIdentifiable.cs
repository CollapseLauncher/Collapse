using CollapseLauncher.Interfaces.Class;
using System.Text.Json.Serialization;
using WinRT;

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;

[GeneratedBindableCustomProperty]
public partial class HypApiIdentifiable : NotifyPropertyChanged
{
    [JsonPropertyName("game")]
    public HypGameInfoData? GameInfo
    {
        get;
        init
        {
            field = value;
            OnPropertyChanged();
        }
    }
}

[GeneratedBindableCustomProperty]
public partial class HypGameInfoData : NotifyPropertyChanged
{
    [JsonPropertyName("id")]
    public string? GameId
    {
        get;
        init
        {
            field = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("biz")]
    public string? GameBiz
    {
        get;
        init
        {
            field = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("display")]
    public HypGameInfoDisplayData? Display
    {
        get;
        init
        {
            field = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("reservation")]
    public HypLauncherMediaContentData? ReservationLink
    {
        get;
        init
        {
            field = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("display_status")]
    public LauncherGameAvailabilityStatus DisplayStatus
    {
        get;
        init
        {
            field = value;
            OnPropertyChanged();
        }
    } = LauncherGameAvailabilityStatus.LAUNCHER_GAME_DISPLAY_STATUS_AVAILABLE;

    public override string ToString() => $"Id: {GameId} | Biz: {GameBiz} | Status: {DisplayStatus}";
}

[GeneratedBindableCustomProperty]
public partial class HypGameInfoDisplayData : NotifyPropertyChanged
{
    [JsonPropertyName("language")]
    public string? Language
    {
        get;
        init
        {
            field = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("name")]
    public string? GameName
    {
        get;
        init
        {
            field = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("title")]
    public string? DisplayTitle
    {
        get;
        init
        {
            field = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("subtitle")]
    public string? DisplaySubtitle
    {
        get;
        init
        {
            field = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("icon")]
    public HypLauncherMediaContentData? GamePreviewIcon
    {
        get;
        init
        {
            field = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("background")]
    public HypLauncherMediaContentData? GamePreviewBackground
    {
        get;
        init
        {
            field = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("logo")]
    public HypLauncherMediaContentData? GamePreviewLogo
    {
        get;
        init
        {
            field = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("thumbnail")]
    public HypLauncherMediaContentData? GamePreviewThumbnail
    {
        get;
        init
        {
            field = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("wpf_icon")]
    public HypLauncherMediaContentData? WpfIcon
    {
        get;
        init
        {
            field = value;
            OnPropertyChanged();
        }
    }
}