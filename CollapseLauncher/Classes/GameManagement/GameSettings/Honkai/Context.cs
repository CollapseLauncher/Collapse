using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CollapseLauncher.GameSettings.Honkai.Context
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(ScreenSettingData))]
    [JsonSerializable(typeof(PersonalAudioSetting))]
    [JsonSerializable(typeof(PersonalGraphicsSettingV2))]
    [JsonSerializable(typeof(PersonalAudioSettingVolume))]
    [JsonSerializable(typeof(Dictionary<string, PersonalGraphicsSettingV2>))]
    internal sealed partial class HonkaiSettingsJSONContext : JsonSerializerContext { }
}
