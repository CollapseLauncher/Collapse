using CollapseLauncher.Classes.GameManagement.GameSettings.Genshin.RegistryClass;
using System.Text.Json.Serialization;

namespace CollapseLauncher.GameSettings.Genshin.Context
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(GeneralData))]
    internal sealed partial class GeneralDataContext : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(GraphicsData))]
    internal sealed partial class GraphicsDataContext : JsonSerializerContext { }
}
