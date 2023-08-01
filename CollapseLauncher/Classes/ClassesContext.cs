using CollapseLauncher.Interfaces;
using Hi3Helper.Shared.ClassStruct;
using System.Text.Json.Serialization;

namespace CollapseLauncher
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(CommunityToolsProperty))]
    [JsonSerializable(typeof(AppUpdateVersionProp))]
    [JsonSerializable(typeof(NotificationPush))]
    [JsonSerializable(typeof(CacheAsset))]
    internal sealed partial class InternalAppJSONContext : JsonSerializerContext { }
}
