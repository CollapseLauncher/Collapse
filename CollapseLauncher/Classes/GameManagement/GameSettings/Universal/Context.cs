using System.Text.Json.Serialization;

namespace CollapseLauncher.GameSettings.Universal
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(CollapseScreenSetting))]
    internal partial class CollapseScreenSettingContext : JsonSerializerContext { }
}
