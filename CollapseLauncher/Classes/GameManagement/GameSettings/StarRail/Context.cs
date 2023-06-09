using System.Text.Json.Serialization;

namespace CollapseLauncher.GameSettings.StarRail.Context
//Modified from Honkai codes because of similarities in reg structure
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(Model))]
    internal sealed partial class ModelContext : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(PCResolution))]
    internal sealed partial class PCResolutionContext : JsonSerializerContext { }
}
