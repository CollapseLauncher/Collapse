using System.Text.Json.Serialization;

namespace CollapseLauncher.GameSettings.Universal
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(CollapseScreenSetting))]
    internal sealed partial class UniversalSettingsJSONContext : JsonSerializerContext { }
}
