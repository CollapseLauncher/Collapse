using System.Text.Json.Serialization;

namespace CollapseLauncher.GameSettings.Zenless.JsonProperties;

public class SystemSettingDataMap
{
    [JsonPropertyName("$Type")]
    public string TypeString { get; set; } = "MoleMole.SystemSettingLocalData";

    [JsonPropertyName("Version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("Data")]
    public int Data { get; set; } = 0;
}