using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using static Hi3Helper.Locale;
using static Hi3Helper.Preset.PresetConfigV2;
using static Hi3Helper.Shared.Region.GameConfig;

namespace Hi3Helper
{
    [JsonSerializable(typeof(YSDispatchInfo))]
    internal partial class YSDispatchInfoContext : JsonSerializerContext { }

    [JsonSerializable(typeof(PkgVersionProperties))]
    public partial class PkgVersionPropertiesContext : JsonSerializerContext { }

    [JsonSerializable(typeof(LocalizationParams))]
    internal partial class LocalizationParamsContext : JsonSerializerContext { }

    [JsonSerializable(typeof(GeneralDataProp))]
    internal partial class GeneralDataPropContext : JsonSerializerContext { }

    [JsonSerializable(typeof(Metadata))]
    internal partial class MetadataContext : JsonSerializerContext { }

    [JsonSerializable(typeof(Stamp))]
    internal partial class StampContext : JsonSerializerContext { }

    [JsonSerializable(typeof(BHI3LInfo))]
    public partial class BHI3LInfoContext : JsonSerializerContext { }

    [JsonSerializable(typeof(ScreenSettingData))]
    internal partial class ScreenSettingDataContext : JsonSerializerContext { }

    [JsonSerializable(typeof(PersonalGraphicsSettingV2))]
    internal partial class PersonalGraphicsSettingV2Context : JsonSerializerContext { }

    [JsonSerializable(typeof(PersonalAudioSetting))]
    internal partial class PersonalAudioSettingContext : JsonSerializerContext { }

    [JsonSerializable(typeof(DataProperties))]
    public partial class DataPropertiesContext : JsonSerializerContext { }

    [JsonSerializable(typeof(DataPropertiesContent))]
    public partial class DataPropertiesContentContext : JsonSerializerContext { }

    [JsonSerializable(typeof(RegionResourceProp))]
    public partial class RegionResourcePropContext : JsonSerializerContext { }

    [JsonSerializable(typeof(FilePropertiesRemote[]))]
    public partial class Array_FilePropertiesRemoteContext : JsonSerializerContext { }

    [JsonSerializable(typeof(List<FilePropertiesRemote>))]
    public partial class L_FilePropertiesRemoteContext : JsonSerializerContext { }

    [JsonSerializable(typeof(Dictionary<string, string>))]
    public partial class D_StringString : JsonSerializerContext { }
}
