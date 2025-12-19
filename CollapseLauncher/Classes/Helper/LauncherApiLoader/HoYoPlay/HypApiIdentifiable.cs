using System.Text.Json.Serialization;

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;

public class HypApiIdentifiable
{
    [JsonPropertyName("game")]
    public HypGameInfoData? GameInfo { get; init; }
}

public class HypGameInfoData
{
    [JsonPropertyName("id")]
    public string? GameId { get; init; }

    [JsonPropertyName("biz")]
    public string? GameBiz { get; init; }

    [JsonPropertyName("display")]
    public HypGameInfoDisplayData? Display { get; init; }

    [JsonPropertyName("reservation")]
    public HypLauncherMediaContentData? ReservationLink { get; init; }

    [JsonPropertyName("display_status")]
    public LauncherGameAvailabilityStatus DisplayStatus { get; init; } = LauncherGameAvailabilityStatus.LAUNCHER_GAME_DISPLAY_STATUS_AVAILABLE;

    public override string ToString() => $"Id: {GameId} | Biz: {GameBiz} | Status: {DisplayStatus}";
}

public class HypGameInfoDisplayData
{
    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("name")]
    public string? GameName { get; init; }

    [JsonPropertyName("title")]
    public string? DisplayTitle { get; init; }

    [JsonPropertyName("subtitle")]
    public string? DisplaySubtitle { get; init; }

    [JsonPropertyName("icon")]
    public HypLauncherMediaContentData? GamePreviewIcon { get; init; }

    [JsonPropertyName("background")]
    public HypLauncherMediaContentData? GamePreviewBackground { get; init; }

    [JsonPropertyName("logo")]
    public HypLauncherMediaContentData? GamePreviewLogo { get; init; }

    [JsonPropertyName("thumbnail")]
    public HypLauncherMediaContentData? GamePreviewThumbnail { get; init; }

    [JsonPropertyName("wpf_icon")]
    public HypLauncherMediaContentData? WpfIcon { get; init; }
}