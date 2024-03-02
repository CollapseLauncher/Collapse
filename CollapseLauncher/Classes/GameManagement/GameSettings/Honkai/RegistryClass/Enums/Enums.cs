using System.Text.Json.Serialization;

namespace CollapseLauncher.GameSettings.Honkai.Enums
{
    /// <summary>
    /// This selection has 4 name types: Low (0), Middle (1), High (2), VHigh (3)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectResolutionQuality>))]
    internal enum SelectResolutionQuality : int { Low, Middle, High, VHigh }

    /// <summary>
    /// This selection has 5 name types: DISABLED (0), LOW (1), MIDDLE (2), HIGH (3), ULTRA (4)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectShadowLevel>))]
    internal enum SelectShadowLevel : int { DISABLED, LOW, MIDDLE, HIGH, ULTRA }

    /// <summary>
    /// This selection has 3 name types: DISABLED (0), LOW (1), HIGH (2)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectReflectionQuality>))]
    internal enum SelectReflectionQuality : int { DISABLED, LOW, MIDDLE, HIGH }

    [JsonConverter(typeof(JsonStringEnumConverter<SelectLightningQuality>))]
    internal enum SelectLightningQuality : int { Off, Low, Middle, High, Ultra }
    
    [JsonConverter(typeof(JsonStringEnumConverter<SelectPostFXQuality>))]
    internal enum SelectPostFXQuality : int { Off, Low, Middle, High }
    
    [JsonConverter(typeof(JsonStringEnumConverter<SelectAAType>))]
    internal enum SelectAAType : int { Off, FXAA, TAA }
    
    [JsonConverter(typeof(JsonStringEnumConverter<SelectCharacterQuality>))]
    internal enum SelectCharacterQuality { Low, Middle, High }
    
    [JsonConverter(typeof(JsonStringEnumConverter<SelectWeatherQuality>))]
    internal enum SelectWeatherQuality { Low, Middle, High }
    
    /// <summary>
    /// This selection has 2 name types: Low (0), High (1)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectGlobalIllumination>))]
    internal enum SelectGlobalIllumination : int { Low, High }

    /// <summary>
    /// This selection has 3 name types: OFF (0), LOW (1), HIGH (2)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectAmbientOcclusion>))]
    internal enum SelectAmbientOcclusion : int { OFF, LOW, HIGH }

    /// <summary>
    /// This selection has 3 name types: Low (0), Medium (1), High (2)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectVolumetricLight>))]
    internal enum SelectVolumetricLight : int { Low, Medium, High }

    /// <summary>
    /// This selection has 2 name types: Low (0), High (1)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectPostFXGrade>))]
    internal enum SelectPostFXGrade : int { Low, High }

    /// <summary>
    /// This selection has 3 name types: Low (2), Medium (1), High (0)<br/>
    /// </summary>
    internal enum SelectLodGrade : int { Low = 2, Medium = 1, High = 0 }
    
    [JsonConverter(typeof(JsonStringEnumConverter<SelectParticleEmitLevel>))]
    internal enum SelectParticleEmitLevel { Low, Middle, High }
}
