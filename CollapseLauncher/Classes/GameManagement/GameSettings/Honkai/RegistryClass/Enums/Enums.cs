using System.Text.Json.Serialization;

namespace CollapseLauncher.GameSettings.Honkai.Enums
{
    /// <summary>
    /// This selection has 4 name types: Low (0), Middle (1), High (2), VHigh (3)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum SelectResolutionQuality : int { Low, Middle, High, VHigh }

    /// <summary>
    /// This selection has 4 name types: LOW (0), MIDDLE (1), HIGH (2), ULTRA (3)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum SelectShadowLevel : int { LOW, MIDDLE, HIGH, ULTRA }

    /// <summary>
    /// This selection has 3 name types: DISABLED (0), LOW (1), HIGH (2)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum SelectReflectionQuality : int { DISABLED, LOW, HIGH }

    /// <summary>
    /// This selection has 2 name types: Low (0), High (1)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum SelectGlobalIllumination : int { Low, High }

    /// <summary>
    /// This selection has 3 name types: DISABLED (0), LOW (1), HIGH (2)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum SelectAmbientOcclusion : int { OFF, LOW, HIGH }

    /// <summary>
    /// This selection has 3 name types: Low (0), Medium (1), High (2)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum SelectVolumetricLight : int { Low, Medium, High }

    /// <summary>
    /// This selection has 2 name types: Low (0), High (1)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum SelectPostFXGrade : int { Low, High }

    /// <summary>
    /// This selection has 3 name types: Low (2), Medium (1), High (0)<br/>
    /// </summary>
    internal enum SelectLodGrade : int { Low = 2, Medium = 1, High = 0 }
}
