using Hi3Helper.Shared.ClassStruct;
using System.Text.Json.Serialization;

namespace CollapseLauncher
{
    [JsonSerializable(typeof(Prop))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    internal partial class PropContext : JsonSerializerContext { }

    [JsonSerializable(typeof(NotificationPush))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    internal partial class NotificationPushContext : JsonSerializerContext { }
}
