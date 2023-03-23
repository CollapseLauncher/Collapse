using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CollapseLauncher.GameSettings.Honkai.Context
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(PersonalAudioSetting))]
    internal partial class PersonalAudioSettingContext : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(PersonalAudioSettingVolume))]
    internal partial class PersonalAudioSettingVolumeContext : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(PersonalGraphicsSettingV2))]
    internal partial class PersonalGraphicsSettingV2Context : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(Dictionary<string, PersonalGraphicsSettingV2>))]
    internal partial class D_PersonalGraphicsSettingV2Context : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(ScreenSettingData))]
    internal partial class ScreenSettingDataContext : JsonSerializerContext { }
}
