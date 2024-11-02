using System.Text.Json.Serialization;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
namespace CollapseLauncher.GameSettings.Honkai.Enums
{
    /// <summary>
    /// This selection has 10 name types:
    /// - 0.6 (Quality06)
    /// - 0.8 (Quality08)
    /// - 0.9 (Quality09)
    /// - 1.0 (Low)
    /// - 1.1 (Quality11)
    /// - 1.2 (Middle)
    /// - 1.3 (Quality13)
    /// - 1.4 (Quality14)
    /// - 1.5 (High)
    /// - 1.6 (VHigh)
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectResolutionQuality>))]
    internal enum SelectResolutionQuality
    { 
        Quality06,
        Quality08,
        Quality09,
        Low,
        Quality11,
        Middle,
        Quality13,
        Quality14,
        High,
        VHigh
    }

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
    internal enum SelectGlobalIllumination { Low, High }

    /// <summary>
    /// This selection has 3 name types: OFF (0), LOW (1), HIGH (2)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectAmbientOcclusion>))]
    internal enum SelectAmbientOcclusion { OFF, LOW, HIGH }

    /// <summary>
    /// This selection has 3 name types: Low (0), Medium (1), High (2)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectVolumetricLight>))]
    internal enum SelectVolumetricLight { Low, Medium, High }

    /// <summary>
    /// This selection has 2 name types: Low (0), High (1)<br/>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectPostFXGrade>))]
    internal enum SelectPostFXGrade { Low, High }

    /// <summary>
    /// This selection has 3 name types: Low (2), Medium (1), High (0)<br/>
    /// </summary>
    internal enum SelectLodGrade { Low = 2, Medium = 1, High = 0 }
    
    /// <summary>
    /// This selection has 3 name tupes: Low, Middle, High
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SelectParticleEmitLevel>))]
    internal enum SelectParticleEmitLevel { Low, Middle, High }
}
