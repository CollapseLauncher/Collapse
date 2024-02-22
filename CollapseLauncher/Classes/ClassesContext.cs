using CollapseLauncher.Interfaces;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.Shared.ClassStruct;
using System.Text.Json.Serialization;

namespace CollapseLauncher
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(RegionResourcePluginValidate))]
    [JsonSerializable(typeof(CommunityToolsProperty))]
    [JsonSerializable(typeof(AppUpdateVersionProp))]
    [JsonSerializable(typeof(RegionResourceProp))]
    [JsonSerializable(typeof(NotificationPush))]
    [JsonSerializable(typeof(CacheAsset))]
    [JsonSerializable(typeof(AudioPCKType[]))]
    [JsonSerializable(typeof(int[]))]
    internal sealed partial class InternalAppJSONContext : JsonSerializerContext;
}
