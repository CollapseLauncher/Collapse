using System.Text.Json.Serialization;

namespace CollapseLauncher.GameSettings.Honkai.Enums
{
    /// <summary>
    /// This selection has 4 name types: Low (0), Middle (1), High (2), VHigh (3)<br/>
    /// </summary>
#if NET8_0_OR_GREATER
    [JsonConverter(typeof(JsonStringEnumConverter<SelectResolutionQuality>))]
#else
    [JsonConverter(typeof(JsonStringEnumConverter))]
#endif
    internal enum SelectResolutionQuality : int { Low, Middle, High, VHigh }

    /// <summary>
    /// This selection has 5 name types: DISABLED (0), LOW (1), MIDDLE (2), HIGH (3), ULTRA (4)<br/>
    /// </summary>
#if NET8_0_OR_GREATER
    [JsonConverter(typeof(JsonStringEnumConverter<SelectShadowLevel>))]
#else
    [JsonConverter(typeof(JsonStringEnumConverter))]
#endif
    internal enum SelectShadowLevel : int { DISABLED, LOW, MIDDLE, HIGH, ULTRA }

    /// <summary>
    /// This selection has 3 name types: DISABLED (0), LOW (1), HIGH (2)<br/>
    /// </summary>
#if NET8_0_OR_GREATER
    [JsonConverter(typeof(JsonStringEnumConverter<SelectReflectionQuality>))]
#else
    [JsonConverter(typeof(JsonStringEnumConverter))]
#endif
    internal enum SelectReflectionQuality : int { DISABLED, LOW, HIGH }

    /// <summary>
    /// This selection has 2 name types: Low (0), High (1)<br/>
    /// </summary>
#if NET8_0_OR_GREATER
    [JsonConverter(typeof(JsonStringEnumConverter<SelectGlobalIllumination>))]
#else
    [JsonConverter(typeof(JsonStringEnumConverter))]
#endif
    internal enum SelectGlobalIllumination : int { Low, High }

    /// <summary>
    /// This selection has 3 name types: OFF (0), LOW (1), HIGH (2)<br/>
    /// </summary>
#if NET8_0_OR_GREATER
    [JsonConverter(typeof(JsonStringEnumConverter<SelectAmbientOcclusion>))]
#else
    [JsonConverter(typeof(JsonStringEnumConverter))]
#endif
    internal enum SelectAmbientOcclusion : int { OFF, LOW, HIGH }

    /// <summary>
    /// This selection has 3 name types: Low (0), Medium (1), High (2)<br/>
    /// </summary>
#if NET8_0_OR_GREATER
    [JsonConverter(typeof(JsonStringEnumConverter<SelectVolumetricLight>))]
#else
    [JsonConverter(typeof(JsonStringEnumConverter))]
#endif
    internal enum SelectVolumetricLight : int { Low, Medium, High }

    /// <summary>
    /// This selection has 2 name types: Low (0), High (1)<br/>
    /// </summary>
#if NET8_0_OR_GREATER
    [JsonConverter(typeof(JsonStringEnumConverter<SelectPostFXGrade>))]
#else
    [JsonConverter(typeof(JsonStringEnumConverter))]
#endif
    internal enum SelectPostFXGrade : int { Low, High }

    /// <summary>
    /// This selection has 3 name types: Low (2), Medium (1), High (0)<br/>
    /// </summary>
    internal enum SelectLodGrade : int { Low = 2, Medium = 1, High = 0 }
}
