using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Zenless.Enums;
using CollapseLauncher.GameSettings.Zenless.JsonProperties;
using Hi3Helper;
using System;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.GameSettings.Zenless;
internal class GeneralData : MagicNodeBaseValues<GeneralData>
{
    #region Disposer
    public void Dispose() => DisposeAsync().GetAwaiter().GetResult();
    
    public async Task DisposeAsync()
    {
        _systemSettingDataMap = null;
        _keyboardBindingMap = null;
        _mouseBindingMap = null;
        _gamepadBindingMap = null;
        _graphicsPresData = null;
        _resolutionIndexData = null;
        _vSyncData = null;
        _renderResolutionData = null;
        _shadowQualityData = null;
        _antiAliasingData = null;
        _volFogQualityData = null;
        _bloomData = null;
        _reflQualityData = null;
        _fxQualityData = null;
        _colorFilterData = null;
        _charQualityData = null;
        _distortionData = null;
        _shadingQualityData = null;
        _envQualityData = null;
        _envGlobalIllumination = null;
        _vMotionBlur = null;
        _fpsData = null;
        _hpcaData = null;
        _mainVolData = null;
        _musicVolData = null;
        _dialogVolData = null;
        _sfxVolData = null;
        _playDevData = null;
        _muteAudOnMinimizeData = null;

        await Task.CompletedTask;
    }

    #endregion
    #region Node Based Properties
    private JsonNode? _systemSettingDataMap;
    private JsonNode? _keyboardBindingMap;
    private JsonNode? _mouseBindingMap;
    private JsonNode? _gamepadBindingMap;

    [JsonPropertyName("SystemSettingDataMap")]
    [JsonIgnore] // We ignore this one from getting serialized to default JSON value
    public JsonNode SystemSettingDataMap
    {
        // Cache the SystemSettingDataMap inside the parent SettingsJsonNode
        // and ensure that the node for SystemSettingDataMap exists. If not exist,
        // create a new one (via EnsureCreated<T>()).
        get => _systemSettingDataMap ??= SettingsJsonNode.EnsureCreatedObject("SystemSettingDataMap");
    }

    [JsonPropertyName("KeyboardBindingMap")]
    [JsonIgnore] // We ignore this one from getting serialized to default JSON value
    public JsonNode KeyboardBindingMap
    {
        // Cache the KeyboardBindingMap inside the parent SettingsJsonNode
        // and ensure that the node for KeyboardBindingMap exists. If not exist,
        // create a new one (via EnsureCreated<T>()).
        get => _keyboardBindingMap ??= SettingsJsonNode.EnsureCreatedObject("KeyboardBindingMap");
    }

    [JsonPropertyName("MouseBindingMap")]
    [JsonIgnore] // We ignore this one from getting serialized to default JSON value
    public JsonNode MouseBindingMap
    {
        // Cache the MouseBindingMap inside the parent SettingsJsonNode
        // and ensure that the node for MouseBindingMap exists. If not exist,
        // create a new one (via EnsureCreated<T>()).
        get => _mouseBindingMap ??= SettingsJsonNode.EnsureCreatedObject("MouseBindingMap");
    }

    [JsonPropertyName("GamepadBindingMap")]
    [JsonIgnore] // We ignore this one from getting serialized to default JSON value
    public JsonNode GamepadBindingMap
    {
        // Cache the GamepadBindingMap inside the parent SettingsJsonNode
        // and ensure that the node for GamepadBindingMap exists. If not exist,
        // create a new one (via EnsureCreated<T>()).
        get => _gamepadBindingMap ??= SettingsJsonNode.EnsureCreatedObject("GamepadBindingMap");
    }
    #endregion

    #region Properties
    [JsonPropertyName("$Type")]
    public string? TypeString
    {
        get => SettingsJsonNode.GetNodeValue("$Type", "MoleMole.GeneralLocalDataItem");
        set => SettingsJsonNode.SetNodeValue("$Type", value);
    }

    [JsonPropertyName("deviceUUID")]
    public string? DeviceUUID
    {
        get => SettingsJsonNode.GetNodeValue("deviceUUID", string.Empty);
        set => SettingsJsonNode.SetNodeValue("deviceUUID", value);
    }

    [JsonPropertyName("userLocalDataVersionId")]
    public string? UserLocalDataVersionId
    {
        get => SettingsJsonNode.GetNodeValue("userLocalDataVersionId", "0.0.1");
        set => SettingsJsonNode.SetNodeValue("userLocalDataVersionId", value);
    }

    [JsonPropertyName("curAccountName")]
    public string? CurrentAccountName
    {
        get => SettingsJsonNode.GetNodeValue("curAccountName", string.Empty);
        set => SettingsJsonNode.SetNodeValue("curAccountName", value);
    }

    [JsonPropertyName("selectedServerIndex")]
    public int SelectedServerIndex
    {
        get => SettingsJsonNode.GetNodeValue("selectedServerIndex", 0);
        set => SettingsJsonNode.SetNodeValue("selectedServerIndex", value);
    }

    [JsonPropertyName("DeviceLanguageType")]
    public LanguageText DeviceLanguageType
    {
        get => SettingsJsonNode.GetNodeValueEnum("DeviceLanguageType", LanguageText.Unset);
        set => SettingsJsonNode.SetNodeValueEnum("DeviceLanguageType", value);
    }

    [JsonPropertyName("DeviceLanguageVoiceType")]
    public LanguageVoice DeviceLanguageVoiceType
    {
        get => SettingsJsonNode.GetNodeValueEnum("DeviceLanguageVoiceType", LanguageVoice.Unset);
        set => SettingsJsonNode.SetNodeValueEnum("DeviceLanguageVoiceType", value);
    }
    
    [JsonPropertyName("PlayerPrefs_StringContainer")]
    public string? PlayerPrefsStringContainer
    {
        get => SettingsJsonNode.GetNodeValue("PlayerPrefs_StringContainer", "");
        set => SettingsJsonNode.SetNodeValue("DeviceLanguageVoiceType", value);
    }

    [JsonPropertyName("PlayerPrefs_IntContainer")]
    public string? PlayerPrefsIntContainer
    {
        get => SettingsJsonNode.GetNodeValue("PlayerPrefs_IntContainer", "");
        set => SettingsJsonNode.SetNodeValue("PlayerPrefs_IntContainer", value);
    }

    [JsonPropertyName("PlayerPrefs_FloatContainer")]
    public string? PlayerPrefsFloatContainer
    {
        get => SettingsJsonNode.GetNodeValue("PlayerPrefs_FloatContainer", "");
        set => SettingsJsonNode.SetNodeValue("PlayerPrefs_FloatContainer", value);
    }

    [JsonPropertyName("LocalUILayoutPlatform ")]
    public int LocalUILayoutPlatform
    {
        get => SettingsJsonNode.GetNodeValue("LocalUILayoutPlatform", 3);
        set => SettingsJsonNode.SetNodeValue("LocalUILayoutPlatform", value);
    }

    [JsonPropertyName("UILayoutManualSetRecordState")]
    public int UILayoutManualSetRecordState
    {
        get => SettingsJsonNode.GetNodeValue("UILayoutManualSetRecordState", 1);
        set => SettingsJsonNode.SetNodeValue("UILayoutManualSetRecordState", value);
    }

    [JsonPropertyName("ControlChoosePopWindowRecordState")]
    public int ControlChoosePopWindowRecordState
    {
        get => SettingsJsonNode.GetNodeValue("ControlChoosePopWindowRecordState", 0);
        set => SettingsJsonNode.SetNodeValue("ControlChoosePopWindowRecordState", value);
    }
    
    [JsonPropertyName("selectServerName")]
    public string? SelectedServerName
    {
        get => SettingsJsonNode.GetNodeValue("selectServerName", "prod_gf_jp");
        set => SettingsJsonNode.SetNodeValue("selectServerName", value);
    }

    [JsonPropertyName("HDRSettingRecordState")]
    public int HDRSettingRecordState
    {
        get => SettingsJsonNode.GetNodeValue("HDRSettingRecordState", 0);
        set => SettingsJsonNode.SetNodeValue("HDRSettingRecordState", value);
    }

    [JsonPropertyName("HDRMaxLuminosityLevel")]
    public int HDRMaxLuminosityLevel
    {
        get => SettingsJsonNode.GetNodeValue("HDRMaxLuminosityLevel", -1);
        set => SettingsJsonNode.SetNodeValue("HDRMaxLuminosityLevel", value);
    }

    [JsonPropertyName("HDRUIPaperWhiteLevel")]
    public int HDRUIPaperWhiteLevel
    {
        get => SettingsJsonNode.GetNodeValue("HDRUIPaperWhiteLevel", -1);
        set => SettingsJsonNode.SetNodeValue("HDRUIPaperWhiteLevel", value);
    }

    [JsonPropertyName("LastVHSStoreOpenTime")]
    public string? LastVHSStoreOpenTime
    {
        get => SettingsJsonNode.GetNodeValue("LastVHSStoreOpenTime", "01/01/0001 00:00:00");
        set => SettingsJsonNode.SetNodeValue("LastVHSStoreOpenTime", value);
    }

    [JsonPropertyName("DisableBattleUIOptimization")]
    public bool DisableBattleUIOptimization
    {
        get => SettingsJsonNode.GetNodeValue("DisableBattleUIOptimization", false);
        set => SettingsJsonNode.SetNodeValue("DisableBattleUIOptimization", value);
    }
    #endregion

    #region Single Node Dependent Properties

    #region Graphics
    // Key 3 Preset
    private SystemSettingLocalData<GraphicsPresetOption>? _graphicsPresData;

    /// <summary>
    /// Sets the preset for Graphics Settings
    /// </summary>
    /// <see cref="GraphicsPresetOption"/>
    [JsonIgnore]
    public GraphicsPresetOption GraphicsPreset
    {
        get => (_graphicsPresData ??= SystemSettingDataMap
                  .AsSystemSettingLocalData("3", GraphicsPresetOption.Medium))
                  .GetDataEnum<GraphicsPresetOption>();
        set => _graphicsPresData?.SetDataEnum(value);
    }

    // Key 5 Resolution Select
    private SystemSettingLocalData<int>? _resolutionIndexData;

    /// <summary>
    /// Sets the resolution based on the in-game logic.
    /// </summary>
    [JsonIgnore]
    public int ResolutionIndex
    {
        get => (_resolutionIndexData ??= SystemSettingDataMap
                  .AsSystemSettingLocalData("5", -1)).GetData();
        set => _resolutionIndexData?.SetData(value);
    }

    // Key 8 VSync
    private SystemSettingLocalData<int>? _vSyncData;

    /// <summary>
    /// Set VSync mode
    /// </summary>
    [JsonIgnore]
    public bool VSync
    {
        // Initialize the field under _vSyncData as SystemSettingLocalData<TValue>
        get =>
            (_vSyncData ??= SystemSettingDataMap
               .AsSystemSettingLocalData("8", 1)).GetData() == 1;
        set =>
            _vSyncData?.SetData(value ? 1 : 0);
    }

    // Key 9 Render Resolution
    private SystemSettingLocalData<RenderResOption>? _renderResolutionData;

    /// <summary>
    /// Sets the render resolution used in-game
    /// </summary>
    /// <see cref="RenderResOption"/>
    [JsonIgnore]
    public RenderResOption RenderResolution
    {
        // Initialize the field under _renderResolutionData as SystemSettingLocalData<TValue>
        get =>
            (_renderResolutionData ??= SystemSettingDataMap
               .AsSystemSettingLocalData("9",
                                         RenderResOption.f10)).GetDataEnum<RenderResOption>();
        set =>
            _renderResolutionData?.SetDataEnum(value);
    }
    
    // Key 10 Shadow
    private SystemSettingLocalData<QualityOption3>? _shadowQualityData;

    /// <summary>
    /// Sets the in-game quality settings for Shadow
    /// </summary>
    /// <see cref="QualityOption3"/>
    [JsonIgnore]
    public QualityOption3 ShadowQuality
    {
        // Initialize the field under _shadowQualityData as SystemSettingLocalData<TValue>
        get =>
            (_shadowQualityData ??= SystemSettingDataMap.AsSystemSettingLocalData("10",
                QualityOption3.Medium)).GetDataEnum<QualityOption3>();
        set =>
            _shadowQualityData?.SetDataEnum(value);
    }
    
    // Key 12 Anti Aliasing
    private SystemSettingLocalData<AntiAliasingOption>? _antiAliasingData;

    [JsonIgnore]
    public AntiAliasingOption AntiAliasing
    {
        // Initialize the field under _antiAliasingData as SystemSettingLocalData<TValue>
        get =>
            (_antiAliasingData ??= SystemSettingDataMap
               .AsSystemSettingLocalData("12",
                                         AntiAliasingOption.TAA)).GetDataEnum<AntiAliasingOption>();
        set =>
            _antiAliasingData?.SetDataEnum(value);
    }
    
    // Key 13 Volumetric Fog
    private SystemSettingLocalData<QualityOption4>? _volFogQualityData;

    /// <summary>
    /// Sets the in-game quality settings for Volumetric Fog
    /// </summary>
    /// <see cref="QualityOption4"/>
    [JsonIgnore]
    public QualityOption4 VolumetricFogQuality
    {
        get => (_volFogQualityData ??= SystemSettingDataMap
               .AsSystemSettingLocalData("13", QualityOption4.Medium)).GetDataEnum<QualityOption4>();
        set => _volFogQualityData?.SetDataEnum(value);
    }
    
    // Key 14 Bloom
    private SystemSettingLocalData<int>? _bloomData;

    [JsonIgnore]
    public bool Bloom
    {
        get => (_bloomData ??= SystemSettingDataMap.AsSystemSettingLocalData("14", 1)).GetData() == 1;
        set => _bloomData?.SetData(value ? 1 : 0);
    }
    
    // Key 15 Reflection
    private SystemSettingLocalData<QualityOption4>? _reflQualityData;

    /// <summary>
    /// Sets the in-game quality settings for Reflection
    /// </summary>
    /// <see cref="QualityOption4"/>
    [JsonIgnore]
    public QualityOption4 ReflectionQuality
    {
        get =>
            (_reflQualityData ??= SystemSettingDataMap.AsSystemSettingLocalData("15",
                QualityOption4.Medium)).GetDataEnum<QualityOption4>();
        set =>
            _reflQualityData?.SetDataEnum(value);
    }
    
    // Key 16 Effects
    private SystemSettingLocalData<QualityOption3>? _fxQualityData;

    /// <summary>
    /// Sets the in-game quality settings for Effects
    /// </summary>
    /// <see cref="QualityOption3"/>
    [JsonIgnore]
    public QualityOption3 FxQuality
    {
        get =>
            (_fxQualityData ??= SystemSettingDataMap.AsSystemSettingLocalData("16", 
                                                                               QualityOption3.Medium))
                                        .GetDataEnum<QualityOption3>();
        set =>
            _fxQualityData?.SetDataEnum(value);
    }
    
    // Key 95 Color Filter Effect
    private SystemSettingLocalData<int>? _colorFilterData;

    public int ColorFilter
    {
        get => (_colorFilterData ??= SystemSettingDataMap.AsSystemSettingLocalData("95", 10)).GetData();
        set => _colorFilterData?.SetData(value);
    }
    
    // Key 99 Character Quality
    private SystemSettingLocalData<QualityOption2>? _charQualityData;

    /// <summary>
    /// Sets the in-game quality settings for Character
    /// </summary>
    /// <see cref="QualityOption2"/>
    [JsonIgnore]
    public QualityOption2 CharacterQuality
    {
        get =>
            (_charQualityData ??= SystemSettingDataMap.AsSystemSettingLocalData("99",
                QualityOption2.High)).GetDataEnum<QualityOption2>();
        set =>
            _charQualityData?.SetDataEnum(value);
    }
    
    // Key 107 Distortion
    private SystemSettingLocalData<int>? _distortionData;
    
    [JsonIgnore]
    public bool Distortion
    {
        get => (_distortionData ??= SystemSettingDataMap.AsSystemSettingLocalData("107", 1))
            .GetData() == 1;
        set => _distortionData?.SetData(value ? 1 : 0);
    }
    
    // Key 108 Shading Quality
    private SystemSettingLocalData<QualityOption3>? _shadingQualityData;

    /// <summary>
    /// Sets the in-game quality settings for Color
    /// </summary>
    /// <see cref="QualityOption3"/>
    [JsonIgnore]
    public QualityOption3 ShadingQuality
    {
        get =>
            (_shadingQualityData ??= SystemSettingDataMap.AsSystemSettingLocalData("108",
                QualityOption3.Medium)).GetDataEnum<QualityOption3>();
        set =>
            _shadingQualityData?.SetDataEnum(value);
    }
    
    // Key 109 Environment Quality
    private SystemSettingLocalData<QualityOption2>? _envQualityData;

    /// <summary>
    /// Sets the in-game quality settings for Environment
    /// </summary>
    /// <see cref="QualityOption2"/>
    [JsonIgnore]
    public QualityOption2 EnvironmentQuality
    {
        get =>
            (_envQualityData ??= SystemSettingDataMap.AsSystemSettingLocalData("109",
                                                                                    QualityOption2.High))
                                      .GetDataEnum<QualityOption2>();
        set =>
            _envQualityData?.SetDataEnum(value);
    }

    // Key 12155 Global Illumination
    private SystemSettingLocalData<QualityOption3>? _envGlobalIllumination;

    /// <summary>
    /// Sets the in-game global illumination settings for Environment
    /// </summary>
    /// <see cref="QualityOption3"/>
    [JsonIgnore]
    public QualityOption3 GlobalIllumination
    {
        get =>
            (_envGlobalIllumination ??= SystemSettingDataMap.AsSystemSettingLocalData("12155",
                QualityOption3.High)).GetDataEnum<QualityOption3>();
        set =>
            _envGlobalIllumination?.SetDataEnum(value);
    }

    // Key 8 VSync
    private SystemSettingLocalData<int>? _vMotionBlur;

    /// <summary>
    /// Set Motion Blur mode
    /// </summary>
    [JsonIgnore]
    public bool MotionBlur
    {
        get =>
            (_vMotionBlur ??= SystemSettingDataMap
               .AsSystemSettingLocalData("106", 1)).GetData() == 1;
        set =>
            _vMotionBlur?.SetData(value ? 1 : 0);
    }

    // Key 110 FPS
    private SystemSettingLocalData<FpsOption>? _fpsData;

    /// <summary>
    /// Sets the in-game frame limiter
    /// </summary>
    /// <see cref="FpsOption"/>
    [JsonIgnore]
    public FpsOption Fps
    {
        // Initialize the field under _fpsData as SystemSettingLocalData<TValue>
        get => (_fpsData ??= SystemSettingDataMap
           .AsSystemSettingLocalData("110", FpsOption.Hi60)).GetDataEnum<FpsOption>();
        set => _fpsData?.SetDataEnum(value);
    }
    
    // Key 13162 High-Precision Character Animation
    private SystemSettingLocalData<bool>? _hpcaData;

    /// <summary>
    /// Sets in-game settings for High-Precision Character Animation. <br/>
    /// Whatever that is ¯\_(ツ)_/¯
    /// </summary>
    [JsonIgnore]
    public bool HiPrecisionCharaAnim
    {
        get => (_hpcaData ??= SystemSettingDataMap.AsSystemSettingLocalData("13162", true)).GetData();
        set => _hpcaData?.SetData(value);
    }

    #endregion

    #region Audio
    // Key 31 Main Volume
    private SystemSettingLocalData<int>? _mainVolData;

    [JsonIgnore]
    public int Audio_MainVolume
    {
        get => (_mainVolData ??= SystemSettingDataMap.AsSystemSettingLocalData("31", 10)).GetData();
        set => _mainVolData?.SetData(value);
    }
    
    // Key 32 Music
    private SystemSettingLocalData<int>? _musicVolData;

    [JsonIgnore]
    public int Audio_MusicVolume
    {
        get => (_musicVolData ??= SystemSettingDataMap.AsSystemSettingLocalData("32", 10)).GetData();
        set => _musicVolData?.SetData(value);
    }
    
    // Key 33 Dialog
    private SystemSettingLocalData<int>? _dialogVolData;

    [JsonIgnore]
    public int Audio_DialogVolume
    {
        get => (_dialogVolData ??= SystemSettingDataMap.AsSystemSettingLocalData("33", 10)).GetData();
        set => _dialogVolData?.SetData(value);
    }

    // Key 34 SFX
    private SystemSettingLocalData<int>? _sfxVolData;
    
    [JsonIgnore]
    public int Audio_SfxVolume
    {
        get => (_sfxVolData ??= SystemSettingDataMap.AsSystemSettingLocalData("34", 10)).GetData();
        set => _sfxVolData?.SetData(value);
    }
    
    // Key 10104 Playback Device Type
    private SystemSettingLocalData<AudioPlaybackDevice>? _playDevData;

    [JsonIgnore]
    public AudioPlaybackDevice Audio_PlaybackDevice
    {
        get => (_playDevData ??= SystemSettingDataMap.AsSystemSettingLocalData("10104",
                                                                                    AudioPlaybackDevice.Headphones))
           .GetDataEnum<AudioPlaybackDevice>();
        set => _playDevData?.SetDataEnum(value);
    }
    
    // Key 10113 Mute on Background
    private SystemSettingLocalData<int>? _muteAudOnMinimizeData;

    [JsonIgnore]
    public bool Audio_MuteOnMinimize
    {
        get => (_muteAudOnMinimizeData ??= SystemSettingDataMap.AsSystemSettingLocalData("10113", 1)).GetData() == 1;
        set => _muteAudOnMinimizeData?.SetData(value ? 1 : 0);
    }
    
    #endregion
    #endregion

    [Obsolete("Loading settings with Load() is not supported for IGameSettingsValueMagic<T> member. Use LoadWithMagic() instead!", true)]
    public new static GeneralData Load() => throw new NotSupportedException("Loading settings with Load() is not supported for IGameSettingsValueMagic<T> member. Use LoadWithMagic() instead!");

    public new static GeneralData LoadWithMagic(byte[]                    magic, SettingsGameVersionManager versionManager,
                                                JsonTypeInfo<GeneralData?> typeInfo)
    {
        var returnVal    = MagicNodeBaseValues<GeneralData>.LoadWithMagic(magic, versionManager, typeInfo);
        
    #if DEBUG
        const bool isPrintDebug = true;
        if (isPrintDebug)
        {
            Logger.LogWriteLine($"Zenless GeneralData parsed value:\r\n\t" +
                                $"FPS   : {returnVal.Fps}\r\n\t" +
                                $"VSync : {returnVal.VSync}\r\n\t" +
                                $"RenRes: {returnVal.RenderResolution}\r\n\t" +
                                $"AA    : {returnVal.AntiAliasing}\r\n\t" +
                                $"Shadow: {returnVal.ShadowQuality}\r\n\t" +
                                $"CharQ : {returnVal.CharacterQuality}\r\n\t" +
                                $"RelfQ : {returnVal.ReflectionQuality}\r\n\t",
                                LogType.Debug, true);
        }
    #endif
        
        return returnVal;
    }
}