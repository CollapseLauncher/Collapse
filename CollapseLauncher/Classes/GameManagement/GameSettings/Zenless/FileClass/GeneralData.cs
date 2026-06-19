using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Zenless.Enums;
using CollapseLauncher.GameSettings.Zenless.JsonProperties;
using Hi3Helper;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using WinRT;

// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo

#nullable enable
namespace CollapseLauncher.GameSettings.Zenless
{
    [GeneratedBindableCustomProperty]
    internal sealed partial class GeneralData : MagicNodeBaseValues<GeneralData>, IDisposable
    {
        #region Disposer

        ~GeneralData()
        {
            _systemSettingDataMap = null;
            _keyboardBindingMap = null;
            _mouseBindingMap = null;
            _gamepadBindingMap = null;

            GC.Collect();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
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
            // create a new one (via GetAsJsonNode<T>()).
            get => _systemSettingDataMap ??= SettingsJsonNode.GetAsJsonNode<JsonObject>("SystemSettingDataMap");
            set => _systemSettingDataMap?.SetAsJsonNode("SystemSettingDataMap", value);
        }

        [JsonPropertyName("KeyboardBindingMap")]
        [JsonIgnore] // We ignore this one from getting serialized to default JSON value
        public JsonNode KeyboardBindingMap
        {
            // Cache the KeyboardBindingMap inside the parent SettingsJsonNode
            // and ensure that the node for KeyboardBindingMap exists. If not exist,
            // create a new one (via GetAsJsonNode<T>()).
            get => _keyboardBindingMap ??= SettingsJsonNode.GetAsJsonNode<JsonObject>("KeyboardBindingMap");
            set => _keyboardBindingMap?.SetAsJsonNode("KeyboardBindingMap", value);
        }

        [JsonPropertyName("MouseBindingMap")]
        [JsonIgnore] // We ignore this one from getting serialized to default JSON value
        public JsonNode MouseBindingMap
        {
            // Cache the MouseBindingMap inside the parent SettingsJsonNode
            // and ensure that the node for MouseBindingMap exists. If not exist,
            // create a new one (via GetAsJsonNode<T>()).
            get => _mouseBindingMap ??= SettingsJsonNode.GetAsJsonNode<JsonObject>("MouseBindingMap");
            set => _mouseBindingMap?.SetAsJsonNode("MouseBindingMap", value);
        }

        [JsonPropertyName("GamepadBindingMap")]
        [JsonIgnore] // We ignore this one from getting serialized to default JSON value
        public JsonNode GamepadBindingMap
        {
            // Cache the GamepadBindingMap inside the parent SettingsJsonNode
            // and ensure that the node for GamepadBindingMap exists. If not exist,
            // create a new one (via GetAsJsonNode<T>()).
            get => _gamepadBindingMap ??= SettingsJsonNode.GetAsJsonNode<JsonObject>("GamepadBindingMap");
            set => _gamepadBindingMap?.SetAsJsonNode("GamepadBindingMap", value);
        }

        [JsonPropertyName("PlayerPrefs_StringContainer")]
        [JsonIgnore]
        [field: AllowNull, MaybeNull] // We ignore this one from getting serialized to default JSON value
        public JsonNode PlayerPrefsStringContainer
        {
            // Cache the PlayerPrefsStringContainer inside the parent SettingsJsonNode
            // and ensure that the node for PlayerPrefsStringContainer exists. If not exist,
            // create a new one (via GetAsJsonNode<T>()).
            get => field ??=
                SettingsJsonNode.GetAsJsonNode<JsonObject>("PlayerPrefs_StringContainer");
            set => field?.SetAsJsonNode("PlayerPrefs_StringContainer", value);
        }

        [JsonPropertyName("PlayerPrefs_IntContainer")]
        [JsonIgnore]
        [field: AllowNull, MaybeNull] // We ignore this one from getting serialized to default JSON value
        public JsonNode PlayerPrefsIntContainer
        {
            // Cache the PlayerPrefsIntContainer inside the parent SettingsJsonNode
            // and ensure that the node for PlayerPrefsIntContainer exists. If not exist,
            // create a new one (via GetAsJsonNode<T>()).
            get => field ??= SettingsJsonNode.GetAsJsonNode<JsonObject>("PlayerPrefs_IntContainer");
            set => field?.SetAsJsonNode("PlayerPrefs_IntContainer", value);
        }

        [JsonPropertyName("PlayerPrefs_FloatContainer")]
        [JsonIgnore]
        [field: AllowNull, MaybeNull] // We ignore this one from getting serialized to default JSON value
        public JsonNode PlayerPrefsFloatContainer
        {
            // Cache the PlayerPrefsFloatContainer inside the parent SettingsJsonNode
            // and ensure that the node for PlayerPrefsFloatContainer exists. If not exist,
            // create a new one (via GetAsJsonNode<T>()).
            get => field ??=
                SettingsJsonNode.GetAsJsonNode<JsonObject>("PlayerPrefs_FloatContainer");
            set => field?.SetAsJsonNode("PlayerPrefs_FloatContainer", value);
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

        [JsonPropertyName("LocalUILayoutPlatform")]
        public LocalUiLayoutPlatform LocalUILayoutPlatform
        {
            get => SettingsJsonNode.GetNodeValueEnum("LocalUILayoutPlatform", LocalUiLayoutPlatform.PC);
            set => SettingsJsonNode.SetNodeValueEnum("LocalUILayoutPlatform", value);
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
        /// <summary>
        ///     Sets the preset for Graphics Settings
        /// </summary>
        /// <see cref="GraphicsPresetOption" />
        [JsonIgnore]
        public GraphicsPresetOption GraphicsPreset
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("3", GraphicsPresetOption.Medium).GetDataEnum<GraphicsPresetOption>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("3", GraphicsPresetOption.Medium).SetDataEnum(value);
        }

        // Key 5 Resolution Select
        /// <summary>
        ///     Sets the resolution based on the in-game logic.
        /// </summary>
        [JsonIgnore]
        public int ResolutionIndex
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("5", -1).GetData();
            set => SystemSettingDataMap.AsSystemSettingLocalData("5", -1).SetData(value);
        }

        // Key 8 VSync
        /// <summary>
        ///     Set VSync mode
        /// </summary>
        [JsonIgnore]
        public bool VSync
        {
            // Initialize the field under _vSyncData as SystemSettingLocalData<TValue>
            get => SystemSettingDataMap.AsSystemSettingLocalData("8", 1).GetData() == 1;
            set => SystemSettingDataMap.AsSystemSettingLocalData("8", 1).SetData(value ? 1 : 0);
        }

        // Key 9 Render Resolution
        /// <summary>
        ///     Sets the render resolution used in-game
        /// </summary>
        /// <see cref="RenderResOption" />
        [JsonIgnore]
        public RenderResOption RenderResolution
        {
            // Initialize the field under _renderResolutionData as SystemSettingLocalData<TValue>
            get => SystemSettingDataMap.AsSystemSettingLocalData("9", RenderResOption.f10).GetDataEnum<RenderResOption>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("9", RenderResOption.f10).SetDataEnum(value);
        }

        // Key 10 Shadow
        /// <summary>
        ///     Sets the in-game quality settings for Shadow
        /// </summary>
        /// <see cref="QualityOption3" />
        [JsonIgnore]
        public QualityOption3 ShadowQuality
        {
            // Initialize the field under _shadowQualityData as SystemSettingLocalData<TValue>
            get => SystemSettingDataMap.AsSystemSettingLocalData("10", QualityOption3.Medium).GetDataEnum<QualityOption3>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("10", QualityOption3.Medium).SetDataEnum(value);
        }

        // Key 12 Anti Aliasing
        [JsonIgnore]
        public AntiAliasingOption AntiAliasing
        {
            // Initialize the field under _antiAliasingData as SystemSettingLocalData<TValue>
            get => SystemSettingDataMap.AsSystemSettingLocalData("12", AntiAliasingOption.TAA).GetDataEnum<AntiAliasingOption>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("12", AntiAliasingOption.TAA).SetDataEnum(value);
        }

        // Key 13 Volumetric Fog
        /// <summary>
        ///     Sets the in-game quality settings for Volumetric Fog
        /// </summary>
        /// <see cref="QualityOption4" />
        [JsonIgnore]
        public QualityOption4 VolumetricFogQuality
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("13", QualityOption4.Medium).GetDataEnum<QualityOption4>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("13", QualityOption4.Medium).SetDataEnum(value);
        }

        // Key 14 Bloom
        [JsonIgnore]
        public bool Bloom
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("14", 1).GetData() == 1;
            set => SystemSettingDataMap.AsSystemSettingLocalData("14", 1).SetData(value ? 1 : 0);
        }

        // Key 15 Reflection
        /// <summary>
        ///     Sets the in-game quality settings for Reflection
        /// </summary>
        /// <see cref="QualityOption4" />
        [JsonIgnore]
        public QualityOption4 ReflectionQuality
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("15", QualityOption4.Medium).GetDataEnum<QualityOption4>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("15", QualityOption4.Medium).SetDataEnum(value);
        }

        // Key 16 Effects
        /// <summary>
        ///     Sets the in-game quality settings for Effects
        /// </summary>
        /// <see cref="QualityOption5" />
        [JsonIgnore]
        public QualityOption5 FxQuality
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("16", QualityOption5.Medium).GetDataEnum<QualityOption5>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("16", QualityOption5.Medium).SetDataEnum(value);
        }

        // Key 95 Color Filter Effect
        public int ColorFilter
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("95", 10).GetData();
            set => SystemSettingDataMap.AsSystemSettingLocalData("95", 10).SetData(value);
        }

        // Key 99 Character Quality
        /// <summary>
        ///     Sets the in-game quality settings for Character
        /// </summary>
        /// <see cref="QualityOption2" />
        [JsonIgnore]
        public QualityOption2 CharacterQuality
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("99", QualityOption2.High).GetDataEnum<QualityOption2>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("99", QualityOption2.High).SetDataEnum(value);
        }

        // Key 107 Distortion
        [JsonIgnore]
        public bool Distortion
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("107", 1).GetData() == 1;
            set => SystemSettingDataMap.AsSystemSettingLocalData("107", 1).SetData(value ? 1 : 0);
        }

        // Key 108 Shading Quality
        /// <summary>
        ///     Sets the in-game quality settings for Color
        /// </summary>
        /// <see cref="QualityOption3" />
        [JsonIgnore]
        public QualityOption3 ShadingQuality
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("108", QualityOption3.Medium).GetDataEnum<QualityOption3>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("108", QualityOption3.Medium).SetDataEnum(value);
        }

        // Key 109 Environment Quality
        /// <summary>
        ///     Sets the in-game quality settings for Environment
        /// </summary>
        /// <see cref="QualityOption2" />
        [JsonIgnore]
        public QualityOption2 EnvironmentQuality
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("109", QualityOption2.High).GetDataEnum<QualityOption2>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("109", QualityOption2.High).SetDataEnum(value);
        }
        
        // Key 16184 Anisotropic Sampling
        /// <summary>
        ///     Sets the in-game quality settings for Anisotropic Sampling
        /// </summary>
        /// <see cref="AnisotropicSamplingOption"/>
        [JsonIgnore]
        public AnisotropicSamplingOption AnisotropicSampling
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("16184", AnisotropicSamplingOption.x8).GetDataEnum<AnisotropicSamplingOption>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("16184", AnisotropicSamplingOption.x8).SetDataEnum(value);
        }
        
        // Key 12155 Global Illumination
        /// <summary>
        ///     Sets the in-game global illumination settings for Environment
        /// </summary>
        /// <see cref="QualityOption3" />
        [JsonIgnore]
        public QualityOption3 GlobalIllumination
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("12155", QualityOption3.High).GetDataEnum<QualityOption3>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("12155", QualityOption3.High).SetDataEnum(value);
        }

        // Key 106 Motion Blur
        /// <summary>
        ///     Set Motion Blur mode
        /// </summary>
        [JsonIgnore]
        public bool MotionBlur
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("106", 1).GetData() == 1;
            set => SystemSettingDataMap.AsSystemSettingLocalData("106", 1).SetData(value ? 1 : 0);
        }

        // Key 110 FPS
        /// <summary>
        ///     Sets the in-game frame limiter
        /// </summary>
        /// <see cref="FpsOption" />
        [JsonIgnore]
        public FpsOption Fps
        {
            // Initialize the field under _fpsData as SystemSettingLocalData<TValue>
            get => SystemSettingDataMap.AsSystemSettingLocalData("110", FpsOption.Hi60).GetDataEnum<FpsOption>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("110", FpsOption.Hi60).SetDataEnum(value);
        }

        // Key 13162 High-Precision Character Animation
        /// <summary>
        ///     Sets in-game settings for High-Precision Character Animation. <br />
        ///     Whatever that is ¯\_(ツ)_/¯
        /// </summary>
        /// <see cref="HiPrecisionCharaAnimOption" />
        [JsonIgnore]
        public HiPrecisionCharaAnimOption HiPrecisionCharaAnim
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("13162", HiPrecisionCharaAnimOption.Off).GetDataEnum<HiPrecisionCharaAnimOption>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("13162", HiPrecisionCharaAnimOption.Off).SetDataEnum(value);
        }

        #endregion

        #region Advanced Graphics Settings

        // Key 22003 Ray Tracing
        /// <summary>
        ///     Sets whether Ray Tracing is enabled or not
        /// </summary>
        /// <see cref="GraphicsPresetOption" />
        [JsonIgnore]
        public bool RayTracing_Enabled
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("22003", 0).GetData() == 1;
            set => SystemSettingDataMap.AsSystemSettingLocalData("22003", 0).SetData(value ? 1 : 0);
        }

        // Key 23001 Ray Tracing Quality
        /// <summary>
        ///     Sets the Ray Tracing quality level
        /// </summary>
        /// <see cref="QualityOption3_4" />
        [JsonIgnore]
        public QualityOption3_4 RayTracing_Quality
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("23001", QualityOption3_4.Medium).GetDataEnum<QualityOption3_4>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("23001", QualityOption3_4.Medium).SetDataEnum(value);
        }

        // Key 22004 Super Resolution
        /// <summary>
        ///     Sets the mode of available Super Resolution scaling
        /// </summary>
        /// <see cref="SuperResolutionScalingOption" />
        [JsonIgnore]
        public SuperResolutionScalingOption SuperResolution_Option
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("22004", SuperResolutionScalingOption.Disabled).GetDataEnum<SuperResolutionScalingOption>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("22004", SuperResolutionScalingOption.Disabled).SetDataEnum(value);
        }

        // Key 22005 Super Resolution Quality
        /// <summary>
        ///     Sets the mode of available quality for Super Resolution scaling
        /// </summary>
        /// <see cref="SuperResolutionScalingQuality" />
        [JsonIgnore]
        public SuperResolutionScalingQuality SuperResolution_Quality
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("22005", SuperResolutionScalingQuality.Balanced).GetDataEnum<SuperResolutionScalingQuality>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("22005", SuperResolutionScalingQuality.Balanced).SetDataEnum(value);
        }

        // Key 22006 Super Resolution Quality
        /// <summary>
        ///     Sets the mode of available quality for Super Resolution scaling
        /// </summary>
        /// <see cref="FrameGenerationType" />
        [JsonIgnore]
        public FrameGenerationType FrameGeneration_Type
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("22006", FrameGenerationType.Disabled).GetDataEnum<FrameGenerationType>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("22006", FrameGenerationType.Disabled).SetDataEnum(value);
        }

        #endregion

        #region Audio

        // Key 31 Main Volume
        [JsonIgnore]
        public int Audio_MainVolume
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("31", 10).GetData();
            set => SystemSettingDataMap.AsSystemSettingLocalData("31", 10).SetData(value);
        }

        // Key 32 Music
        [JsonIgnore]
        public int Audio_MusicVolume
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("32", 10).GetData();
            set => SystemSettingDataMap.AsSystemSettingLocalData("32", 10).SetData(value);
        }

        // Key 33 Dialog
        [JsonIgnore]
        public int Audio_DialogVolume
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("33", 10).GetData();
            set => SystemSettingDataMap.AsSystemSettingLocalData("33", 10).SetData(value);
        }

        // Key 34 SFX
        [JsonIgnore]
        public int Audio_SfxVolume
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("34", 10).GetData();
            set => SystemSettingDataMap.AsSystemSettingLocalData("34", 10).SetData(value);
        }

        // Key 20192 Ambient
        [JsonIgnore]
        public int Audio_AmbientVolume
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("20192", 10).GetData();
            set => SystemSettingDataMap.AsSystemSettingLocalData("20192", 10).SetData(value);
        }

        // Key 10104 Playback Device Type
        [JsonIgnore]
        public AudioPlaybackDevice Audio_PlaybackDevice
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("10104", AudioPlaybackDevice.Headphones).GetDataEnum<AudioPlaybackDevice>();
            set => SystemSettingDataMap.AsSystemSettingLocalData("10104", AudioPlaybackDevice.Headphones).SetDataEnum(value);
        }

        // Key 10113 Mute on Background
        [JsonIgnore]
        public bool Audio_MuteOnMinimize
        {
            get => SystemSettingDataMap.AsSystemSettingLocalData("10113", 1).GetData() == 1;
            set => SystemSettingDataMap.AsSystemSettingLocalData("10113", 1).SetData(value ? 1 : 0);
        }

        #endregion

        #endregion

        [Obsolete("Loading settings with Load() is not supported for IGameSettingsValueMagic<T> member. Use LoadWithMagic() instead!",
                  true)]
        public static GeneralData Load()
        {
            throw new
                NotSupportedException("Loading settings with Load() is not supported for IGameSettingsValueMagic<T> member. Use LoadWithMagic() instead!");
        }

        public new static GeneralData LoadWithMagic(byte[] magic, SettingsGameVersionManager versionManager,
                                                    JsonTypeInfo<GeneralData?> typeInfo)
        {
            var returnVal = MagicNodeBaseValues<GeneralData>.LoadWithMagic(magic, versionManager, typeInfo);

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
}