using System.Text.Json.Serialization;

namespace CollapseLauncher.GamePlaytime.Universal
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(Playtime))]
    internal sealed partial class UniversalPlaytimeJSONContext : JsonSerializerContext { }
}
