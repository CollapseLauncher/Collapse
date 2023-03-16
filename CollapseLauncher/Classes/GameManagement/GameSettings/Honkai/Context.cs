using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CollapseLauncher.GameSettings.Honkai.Context
{
    [JsonSerializable(typeof(PersonalAudioSetting))]
    internal partial class PersonalAudioSettingContext : JsonSerializerContext { }

    [JsonSerializable(typeof(PersonalAudioSettingVolume))]
    internal partial class PersonalAudioSettingVolumeContext : JsonSerializerContext { }

    [JsonSerializable(typeof(PersonalGraphicsSettingV2))]
    internal partial class PersonalGraphicsSettingV2Context : JsonSerializerContext { }

    [JsonSerializable(typeof(Dictionary<string, PersonalGraphicsSettingV2>))]
    internal partial class D_PersonalGraphicsSettingV2Context : JsonSerializerContext { }

    [JsonSerializable(typeof(ScreenSettingData))]
    internal partial class ScreenSettingDataContext : JsonSerializerContext { }
}
