using Hi3Helper.Shared.ClassStruct;
using System.Text.Json.Serialization;

namespace CollapseLauncher
{
    [JsonSerializable(typeof(Prop))]
    internal partial class PropContext : JsonSerializerContext { }

    [JsonSerializable(typeof(NotificationPush))]
    internal partial class NotificationPushContext : JsonSerializerContext { }
}
