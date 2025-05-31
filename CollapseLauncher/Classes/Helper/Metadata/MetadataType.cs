using System.Text.Json.Serialization;

namespace CollapseLauncher.Helper.Metadata
{
    [JsonConverter(typeof(JsonStringEnumConverter<MetadataType>))]
    public enum MetadataType
    {
        Unknown,
        PresetConfigV2,
        MasterKey,
        CommunityTools,
        PresetConfigPlugin
    }
}
