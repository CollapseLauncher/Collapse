
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Preset.PresetConfigV2;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using static Hi3Helper.Shared.Region.GameConfig;

namespace Hi3Helper
{
    [JsonSerializable(typeof(YSDispatchInfo))]
    internal partial class YSDispatchInfoContext : JsonSerializerContext { }

    [JsonSerializable(typeof(PkgVersionProperties))]
    internal partial class PkgVersionPropertiesContext : JsonSerializerContext { }

    [JsonSerializable(typeof(LocalizationParams))]
    internal partial class LocaleContext : JsonSerializerContext { }

    [JsonSerializable(typeof(GeneralDataProp))]
    internal partial class GeneralDataPropContext : JsonSerializerContext { }

    [JsonSerializable(typeof(Metadata))]
    internal partial class MetadataContext : JsonSerializerContext { }

    [JsonSerializable(typeof(Stamp))]
    internal partial class StampContext : JsonSerializerContext { }

    [JsonSerializable(typeof(BHI3LInfo))]
    internal partial class BHI3LInfoContext : JsonSerializerContext { }

    [JsonSerializable(typeof(ScreenSettingData))]
    internal partial class ScreenSettingDataContext : JsonSerializerContext { }

    [JsonSerializable(typeof(PersonalGraphicsSettingV2))]
    internal partial class PersonalGraphicsSettingV2Context : JsonSerializerContext { }

    [JsonSerializable(typeof(PersonalAudioSetting))]
    internal partial class PersonalAudioSettingContext : JsonSerializerContext { }

    [JsonSerializable(typeof(List<FilePropertiesRemote>))]
    internal partial class L_FilePropertiesRemoteContext : JsonSerializerContext { }
}
