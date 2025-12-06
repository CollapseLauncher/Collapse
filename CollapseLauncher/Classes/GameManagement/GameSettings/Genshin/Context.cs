using System.Text.Json.Serialization;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable IdentifierTypo

namespace CollapseLauncher.GameSettings.Genshin.Context
{
    [JsonSerializable(typeof(GeneralData))]
    [JsonSerializable(typeof(GraphicsData))]
    [JsonSerializable(typeof(GlobalPerfData))]
    [JsonSourceGenerationOptions(AllowTrailingCommas = true, PropertyNameCaseInsensitive = true)]
    internal sealed partial class GenshinSettingsJsonContext : JsonSerializerContext;
}
