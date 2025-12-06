using CollapseLauncher.Helper.JsonConverter;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;

public class HypApiResponse<T>
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    [JsonPropertyName("retcode")]
    public int? ReturnCode { get; init; }

    [JsonPropertyName("message")]
    [JsonConverter(typeof(EmptyStringAsNullConverter))]
    public string? ReturnMessage { get; init; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }
}
