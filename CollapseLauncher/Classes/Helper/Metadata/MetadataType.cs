using System.Text.Json.Serialization;
#pragma warning disable IDE0130

namespace CollapseLauncher.Helper.Metadata;

[JsonConverter(typeof(JsonStringEnumConverter<MetadataType>))]
public enum MetadataType
{
    Unknown,
    PresetConfigV2,
    MasterKey,
    CommunityTools,
    PresetConfigPlugin
}
