using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Zenless.Enums;
using CollapseLauncher.GameSettings.Zenless.JsonProperties;
using CollapseLauncher.Interfaces;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

#nullable enable
namespace CollapseLauncher.GameSettings.Zenless;

internal class GeneralData : MagicNodeBaseValues<GeneralData>, IGameSettingsValueMagic<GeneralData>
{
    #region Node Based Properties
    private JsonNode? _systemSettingDataMap;
    private JsonNode? _keyboardBindingMap;
    private JsonNode? _mouseBindingMap;
    private JsonNode? _gamepadBindingMap;

    [JsonPropertyName("SystemSettingDataMap")]
    [JsonIgnore] // We ignore this one from getting serialized to default JSON value
    public JsonNode? SystemSettingDataMap
    {
        // Cache the SystemSettingDataMap inside of the parent SettingsJsonNode
        // and ensure that the node for SystemSettingDataMap exists. If not exist,
        // create a new one (via EnsureCreated<T>()).
        get => _systemSettingDataMap ??= SettingsJsonNode?.EnsureCreated<JsonNode>("SystemSettingDataMap");
    }

    [JsonPropertyName("KeyboardBindingMap")]
    [JsonIgnore] // We ignore this one from getting serialized to default JSON value
    public JsonNode? KeyboardBindingMap
    {
        // Cache the KeyboardBindingMap inside of the parent SettingsJsonNode
        // and ensure that the node for KeyboardBindingMap exists. If not exist,
        // create a new one (via EnsureCreated<T>()).
        get => _keyboardBindingMap ??= SettingsJsonNode?.EnsureCreated<JsonNode>("KeyboardBindingMap");
    }

    [JsonPropertyName("MouseBindingMap")]
    [JsonIgnore] // We ignore this one from getting serialized to default JSON value
    public JsonNode? MouseBindingMap
    {
        // Cache the MouseBindingMap inside of the parent SettingsJsonNode
        // and ensure that the node for MouseBindingMap exists. If not exist,
        // create a new one (via EnsureCreated<T>()).
        get => _mouseBindingMap ??= SettingsJsonNode?.EnsureCreated<JsonNode>("MouseBindingMap");
    }

    [JsonPropertyName("GamepadBindingMap")]
    [JsonIgnore] // We ignore this one from getting serialized to default JSON value
    public JsonNode? GamepadBindingMap
    {
        // Cache the GamepadBindingMap inside of the parent SettingsJsonNode
        // and ensure that the node for GamepadBindingMap exists. If not exist,
        // create a new one (via EnsureCreated<T>()).
        get => _gamepadBindingMap ??= SettingsJsonNode?.EnsureCreated<JsonNode>("GamepadBindingMap");
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
        set => SettingsJsonNode.SetNodeValueEnum("DeviceLanguageType", value, JsonEnumStoreType.AsNumber);
    }

    [JsonPropertyName("DeviceLanguageVoiceType")]
    public LanguageVoice DeviceLanguageVoiceType
    {
        get => SettingsJsonNode.GetNodeValueEnum("DeviceLanguageVoiceType", LanguageVoice.Unset);
        set => SettingsJsonNode.SetNodeValueEnum("DeviceLanguageVoiceType", value, JsonEnumStoreType.AsNumber);
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
    private SystemSettingLocalData<FpsOption>? _fpsData;

    [JsonIgnore]
    public FpsOption Fps
    {
        // Initialize the field under _fpsData as SystemSettingLocalData<TValue>
        get => (_fpsData.HasValue ? _fpsData : _fpsData = SystemSettingDataMap!
            .AsSystemSettingLocalData<FpsOption>("110", FpsOption.Hi60)).Value.GetDataEnum<FpsOption>();
        set => _fpsData?.SetDataEnum(value);
    }

    private SystemSettingLocalData<bool>? _vSyncData;

    [JsonIgnore]
    public bool VSync
    {
        // Initialize the field under _vSyncData as SystemSettingLocalData<TValue>
        get => (_vSyncData.HasValue ? _vSyncData : _vSyncData = SystemSettingDataMap!
            .AsSystemSettingLocalData<bool>("8")).Value.GetData();
        set => _vSyncData?.SetData(value);
    }

    private SystemSettingLocalData<QualityOption3>? _renderResolutionData;

    [JsonIgnore]
    public QualityOption3 RenderResolution
    {
        // Initialize the field under _renderResolutionData as SystemSettingLocalData<TValue>
        get => (_renderResolutionData.HasValue ? _renderResolutionData : _renderResolutionData = SystemSettingDataMap!
            .AsSystemSettingLocalData<QualityOption3>("9", QualityOption3.Medium)).Value.GetDataEnum<QualityOption3>();
        set => _renderResolutionData?.SetDataEnum(value);
    }

    private SystemSettingLocalData<AntiAliasingOption>? _antiAliasingData;

    [JsonIgnore]
    public AntiAliasingOption AntiAliasing
    {
        // Initialize the field under _antiAliasingData as SystemSettingLocalData<TValue>
        get => (_antiAliasingData.HasValue ? _antiAliasingData : _antiAliasingData = SystemSettingDataMap!
            .AsSystemSettingLocalData<AntiAliasingOption>("12")).Value.GetDataEnum<AntiAliasingOption>();
        set => _antiAliasingData?.SetDataEnum(value);
    }

    private SystemSettingLocalData<AntiAliasingOption>? _shadowQualityData;

    [JsonIgnore]
    public AntiAliasingOption ShadowQuality
    {
        // Initialize the field under _shadowQualityData as SystemSettingLocalData<TValue>
        get => (_shadowQualityData.HasValue ? _shadowQualityData : _shadowQualityData = SystemSettingDataMap!
            .AsSystemSettingLocalData<AntiAliasingOption>("12")).Value.GetDataEnum<AntiAliasingOption>();
        set => _shadowQualityData?.SetDataEnum(value);
    }
    #endregion
}
