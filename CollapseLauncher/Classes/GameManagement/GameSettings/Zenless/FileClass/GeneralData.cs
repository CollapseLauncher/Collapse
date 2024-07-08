using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Zenless.JsonProperties;
using CollapseLauncher.Interfaces;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CollapseLauncher.GameSettings.Zenless;

internal class GeneralData : MagicNodeBaseValues<GeneralData>, IGameSettingsValueMagic<GeneralData>
{
    #region Fields
#nullable enable
    #endregion

    #region Properties
    [JsonPropertyName("$Type")]
    public string? TypeString { get; set; } = "MoleMole.GeneralLocalDataItem";

    [JsonPropertyName("deviceUUID")]
    public string? DeviceUUID { get; set; }

    [JsonPropertyName("userLocalDataVersionId")]
    public string? UserLocalDataVersionId { get; set; } = "0.0.1";

    [JsonPropertyName("curAccountName")]
    public string? CurrentAccountName { get; set; }

    [JsonPropertyName("selectedServerIndex")]
    public int SelectedServerIndex { get; set; } = 0;

    [JsonPropertyName("DeviceLanguageType")]
    public int DeviceLanguageType { get; set; } = -1;

    [JsonPropertyName("DeviceLanguageVoiceType")]
    public int DeviceLanguageVoiceType
    {
        get => GetValue(SettingsJsonNode, "DeviceLanguageVoiceType", -1); // Set the default value here
        set => SetValue(SettingsJsonNode, "DeviceLanguageVoiceType", value);
    }

    [JsonPropertyName("selectServerName")]
    public string? SelectedServerName
    {
        get => GetValue(SettingsJsonNode, "selectServerName", "prod_gf_jp"); // Set the default value here
        set => SetValue(SettingsJsonNode, "selectServerName", value);
    }

    [JsonPropertyName("SystemSettingDataMap")]
    public Dictionary<string, SystemSettingDataMap> SystemSettingDataMap { get; set; } = new Dictionary<string, SystemSettingDataMap>();

    [JsonPropertyName("KeyboardBindingMap")]
    public Dictionary<string, SystemSettingDataMap> KeyboardBindingMap { get; set; } = new Dictionary<string, SystemSettingDataMap>();

    [JsonPropertyName("MouseBindingMap")]
    public Dictionary<string, SystemSettingDataMap> MouseBindingMap { get; set; } = new Dictionary<string, SystemSettingDataMap>();

    [JsonPropertyName("GamepadBindingMap")]
    public Dictionary<string, SystemSettingDataMap> GamepadBindingMap { get; set; } = new Dictionary<string, SystemSettingDataMap>();

    [JsonPropertyName("HDRSettingRecordState")]
    public int HDRSettingRecordState { get; set; } = 0;

    [JsonPropertyName("HDRMaxLuminosityLevel")]
    public int HDRMaxLuminosityLevel { get; set; } = -1;

    [JsonPropertyName("HDRUIPaperWhiteLevel")]
    public int HDRUIPaperWhiteLevel { get; set; } = -1;

    [JsonPropertyName("LastVHSStoreOpenTime")]
    public string? LastVHSStoreOpenTime { get; set; } = "01/01/0001 00:00:00";

    [JsonPropertyName("DisableBattleUIOptimization")]
    public bool DisableBattleUIOptimization { get; set; } = false;
    #endregion
}
