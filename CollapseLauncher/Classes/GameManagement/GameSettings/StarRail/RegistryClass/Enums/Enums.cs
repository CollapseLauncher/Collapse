using System.Text.Json.Serialization;

namespace CollapseLauncher.GameSettings.StarRail.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum SelectShadowQuality : int { Off, Low = 2, Medium, High }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum SelectLightQuality : int { VeryLow = 1, Low, Medium, High, VeryHigh }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum SelectCharacterQuality : int { Low = 1, Medium, High }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum SelectEnvDetailQuality : int { VeryLow = 1, Low, Medium, High, VeryHigh }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum SelectReflectionQuality : int { VeryLow = 1, Low, Medium, High, VeryHigh }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum SelectBloomQuality : int { VeryLow, Low, Medium, High, VeryHigh }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum SelectAAMode : int { Off, TAA, FXAA }

}
