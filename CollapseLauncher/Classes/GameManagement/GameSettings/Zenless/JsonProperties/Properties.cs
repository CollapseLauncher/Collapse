using System.Text.Json.Serialization;

namespace CollapseLauncher.GameSettings.Zenless.JsonProperties;

public class SystemSettingDataMap
{
    [JsonPropertyName("$Type")]   
    public string Type { get; set; }
    
    [JsonPropertyName("Version")]
    public int Version { get; set; }
    
    [JsonPropertyName("Data")]
    public int Data { get; set; }
}