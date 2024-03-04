using System.Text.Json.Serialization;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
namespace CollapseLauncher.GameSettings.Honkai.Enums
{
    /// <summary>
    /// This selection has 4 name types: Low (0), Middle (1), High (2), VHigh (3)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectResolutionQuality>))]
    internal enum SelectResolutionQuality { Low, Middle, High, VHigh }

    /// <summary>
    /// This selection has 5 name types: DISABLED (0), LOW (1), MIDDLE (2), HIGH (3), ULTRA (4)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectShadowLevel>))]
    internal enum SelectShadowLevel { DISABLED, LOW, MIDDLE, HIGH, ULTRA }

    /// <summary>
    /// This selection has 4 name types: DISABLED (0), LOW (1), MIDDLE (2), HIGH (3)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectReflectionQuality>))]
    internal enum SelectReflectionQuality { DISABLED, LOW, MIDDLE, HIGH }

    /// <summary>
    /// This selection has 5 name types: Off (0), Low (1), Middle (2), High (3), Ultra (4)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectLightningQuality>))]
    internal enum SelectLightningQuality { Off, Low, Middle, High, Ultra }
    
    /// <summary>
    /// This selection has 4 name types: Off, Low, Middle, High
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectPostFXQuality>))]
    internal enum SelectPostFXQuality { Off, Low, Middle, High }
    
    /// <summary>
    /// This selection has 3 name types: Off, FXAA, TAA
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectAAType>))]
    internal enum SelectAAType { Off, FXAA, TAA }
    
    /// <summary>
    /// This selection has 3 name tupes: Low, Middle, High
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectCharacterQuality>))]
    internal enum SelectCharacterQuality { Low, Middle, High }
    
    /// <summary>
    /// This selection has 3 name tupes: Low, Middle, High
    /// </summary>
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
    
    /// <summary>
    /// This selection has 3 name tupes: Low, Middle, High
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectParticleEmitLevel>))]
    internal enum SelectParticleEmitLevel { Low, Middle, High }
}
