using CollapseLauncher.GameSettings.Zenless;
using CollapseLauncher.GameSettings.Zenless.Enums;
using CollapseLauncher.Interfaces.Class;
using WinRT;

#nullable enable
namespace CollapseLauncher.Pages;

[GeneratedBindableCustomProperty]
internal partial class ZenlessGameSettingsContext(ZenlessSettings settings) : NotifyPropertyChanged
{
    public ZenlessSettings Settings { get; init; } = settings;

    public GeneralData GenericData { get; init; } = settings.GeneralData;

    public LocalUiLayoutPlatform LocalUILayoutPlatform
    {
        get => GenericData.LocalUILayoutPlatform;
        set => GenericData.LocalUILayoutPlatform = value;
    }

    #region Language Settings - GENERAL_DATA
    public int Lang_Text
    {
        get => (int)GenericData.DeviceLanguageType is var v && v <= 0 ? 1 : v;
        set
        {
            GenericData.DeviceLanguageType = (LanguageText)value;
            OnPropertyChanged();
        }
    }

    public int Lang_Audio
    {
        get => (int)GenericData.DeviceLanguageVoiceType is var v && v <= 0 ? 1 : v;
        set
        {
            GenericData.DeviceLanguageVoiceType = (LanguageVoice)value;
            OnPropertyChanged();
        }
    }
    #endregion

    #region Graphics Settings - GENERAL_DATA > SystemSettingDataMap
    public bool EnableVSync
    {
        get => GenericData.VSync;
        set
        {
            GenericData.VSync = value;
            OnPropertyChanged();
        }
    }

    public int Graphics_Preset
    {
        get => (int)GenericData.GraphicsPreset;
        set
        {
            GenericData.GraphicsPreset = (GraphicsPresetOption)value;
            OnPropertyChanged();
        }
    }

    public int Graphics_RenderRes
    {
        get => (int)GenericData.RenderResolution;
        set
        {
            GenericData.RenderResolution = (RenderResOption)value;
            OnPropertyChanged();
        }
    }

    public int Graphics_Shadow
    {
        get => (int)GenericData.ShadowQuality;
        set
        {
            GenericData.ShadowQuality = (QualityOption3)value;
            OnPropertyChanged();
        }
    }

    public int Graphics_AntiAliasing
    {
        get => (int)GenericData.AntiAliasing;
        set
        {
            GenericData.AntiAliasing = (AntiAliasingOption)value;
            OnPropertyChanged();
        }
    }

    public int Graphics_VolFog
    {
        get => (int)GenericData.VolumetricFogQuality;
        set
        {
            GenericData.VolumetricFogQuality = (QualityOption4)value;
            OnPropertyChanged();
        }
    }

    public bool Graphics_Bloom
    {
        get => GenericData.Bloom;
        set
        {
            GenericData.Bloom = value;
            OnPropertyChanged();
        }
    }

    public bool Graphics_MotionBlur
    {
        get => GenericData.MotionBlur;
        set
        {
            GenericData.MotionBlur = value;
            OnPropertyChanged();
        }
    }

    public int Graphics_Reflection
    {
        get => (int)GenericData.ReflectionQuality;
        set
        {
            GenericData.ReflectionQuality = (QualityOption4)value;
            OnPropertyChanged();
        }
    }

    public int Graphics_Effects
    {
        get => (int)GenericData.FxQuality;
        set
        {
            GenericData.FxQuality = (QualityOption5)value;
            OnPropertyChanged();
        }
    }

    public int Graphics_ColorFilter
    {
        get => GenericData.ColorFilter;
        set
        {
            GenericData.ColorFilter = value;
            OnPropertyChanged();
        }
    }

    public int Graphics_Character
    {
        get => (int)GenericData.CharacterQuality;
        set
        {
            GenericData.CharacterQuality = (QualityOption2)value;
            OnPropertyChanged();
        }
    }

    public bool Graphics_Distortion
    {
        get => GenericData.Distortion;
        set
        {
            GenericData.Distortion = value;
            OnPropertyChanged();
        }
    }

    public int Graphics_Shading
    {
        get => (int)GenericData.ShadingQuality;
        set
        {
            GenericData.ShadingQuality = (QualityOption3)value;
            OnPropertyChanged();
        }
    }

    public int Graphics_Environment
    {
        get => (int)GenericData.EnvironmentQuality;
        set
        {
            GenericData.EnvironmentQuality = (QualityOption2)value;
            OnPropertyChanged();
        }
    }

    public int Graphics_AnisotropicSampling
    {
        get => (int)GenericData.AnisotropicSampling;
        set
        {
            GenericData.AnisotropicSampling = (AnisotropicSamplingOption)value;
            OnPropertyChanged();
        }
    }

    public int Graphics_GlobalIllumination
    {
        get => (int)GenericData.GlobalIllumination;
        set
        {
            GenericData.GlobalIllumination = (QualityOption3)value;
            OnPropertyChanged();
        }
    }

    public int Graphics_Fps
    {
        get => (int)GenericData.Fps;
        set
        {
            GenericData.Fps = (FpsOption)value;
            OnPropertyChanged();
        }
    }

    /// <inheritdoc cref="GameSettings.Zenless.GeneralData.HiPrecisionCharaAnim"/>
    public int Graphics_HiPreCharaAnim
    {
        get => (int)GenericData.HiPrecisionCharaAnim;
        set
        {
            GenericData.HiPrecisionCharaAnim = (HiPrecisionCharaAnimOption)value;
            OnPropertyChanged();
        }
    }

    public bool AdvancedGraphics_UseRayTracing
    {
        get => GenericData.RayTracing_Enabled;
        set
        {
            GenericData.RayTracing_Enabled = value;
            OnPropertyChanged();
        }
    }

    public int AdvancedGraphics_RayTracingQuality
    {
        get => (int)GenericData.RayTracing_Quality;
        set
        {
            GenericData.RayTracing_Quality = (QualityOption3_4)value;
            OnPropertyChanged();
        }
    }

    public int AdvancedGraphics_SuperResolutionOption
    {
        get => (int)GenericData.SuperResolution_Option;
        set
        {
            GenericData.SuperResolution_Option = (SuperResolutionScalingOption)value;
            OnPropertyChanged();

            if (value <= 0 ||
                GenericData.FrameGeneration_Type == FrameGenerationType.Disabled)
            {
                return;
            }

            GenericData.FrameGeneration_Type = value switch
            {
                1 => FrameGenerationType.DLSS,
                _ => FrameGenerationType.FSR
            };
            OnPropertyChanged(nameof(AdvancedGraphics_FrameGenerationType));
        }
    }

    public int AdvancedGraphics_SuperResolutionQuality
    {
        get => (int)GenericData.SuperResolution_Quality;
        set
        {
            GenericData.SuperResolution_Quality = (SuperResolutionScalingQuality)value;
            OnPropertyChanged();
        }
    }

    public int AdvancedGraphics_FrameGenerationType
    {
        get => (int)GenericData.FrameGeneration_Type;
        set
        {
            GenericData.FrameGeneration_Type = (FrameGenerationType)value;
            OnPropertyChanged();

            if (value <= 0 ||
                GenericData.SuperResolution_Option == SuperResolutionScalingOption.Disabled)
            {
                return;
            }

            GenericData.SuperResolution_Option = value switch
            {
                1 => SuperResolutionScalingOption.DLSS,
                _ => SuperResolutionScalingOption.FSR
            };
            OnPropertyChanged(nameof(AdvancedGraphics_SuperResolutionOption));
        }
    }

    #endregion

    #region Audio Settings - GENERAL_DATA > SystemSettingDataMap

    public int Audio_VolMain
    {
        get => GenericData.Audio_MainVolume;
        set
        {
            GenericData.Audio_MainVolume = value;
            OnPropertyChanged();
        }
    }

    public int Audio_VolMusic
    {
        get => GenericData.Audio_MusicVolume;
        set
        {
            GenericData.Audio_MusicVolume = value;
            OnPropertyChanged();
        }
    }

    public int Audio_VolDialog
    {
        get => GenericData.Audio_DialogVolume;
        set
        {
            GenericData.Audio_DialogVolume = value;
            OnPropertyChanged();
        }
    }

    public int Audio_VolSfx
    {
        get => GenericData.Audio_SfxVolume;
        set
        {
            GenericData.Audio_SfxVolume = value;
            OnPropertyChanged();
        }
    }

    public int Audio_VolAmbient
    {
        get => GenericData.Audio_AmbientVolume;
        set
        {
            GenericData.Audio_AmbientVolume = value;
            OnPropertyChanged();
        }
    }

    public int Audio_PlaybackDevice
    {
        get => (int)GenericData.Audio_PlaybackDevice;
        set
        {
            GenericData.Audio_PlaybackDevice = (AudioPlaybackDevice)value;
            OnPropertyChanged();
        }
    }

    public bool Audio_MuteOnMinimize
    {
        get => GenericData.Audio_MuteOnMinimize;
        set
        {
            GenericData.Audio_MuteOnMinimize = value;
            OnPropertyChanged();
        }
    }
    #endregion
}
