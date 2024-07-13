using System.Text.Json.Serialization;

namespace CollapseLauncher.GameSettings.Zenless.Context;

[JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
[JsonSerializable(typeof(GeneralData))]
internal sealed partial class ZenlessSettingsJSONContext : JsonSerializerContext {}