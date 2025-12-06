using System.Text.Json.Serialization;
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedType.Global

namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;

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
