using System.Text.Json.Serialization;

namespace CollapseLauncher.GamePlaytime
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(CollapsePlaytime))]
    internal sealed partial class UniversalPlaytimeJSONContext : JsonSerializerContext { }
}
