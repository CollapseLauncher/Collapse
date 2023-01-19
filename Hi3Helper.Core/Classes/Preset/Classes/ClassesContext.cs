﻿using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using static Hi3Helper.Locale;
using static Hi3Helper.Preset.PresetConfigV2;
using static Hi3Helper.Shared.Region.GameConfig;

namespace Hi3Helper
{
    [JsonSerializable(typeof(YSDispatchInfo))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    internal partial class YSDispatchInfoContext : JsonSerializerContext { }

    [JsonSerializable(typeof(PkgVersionProperties))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    public partial class PkgVersionPropertiesContext : JsonSerializerContext { }

    [JsonSerializable(typeof(LocalizationParams))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    internal partial class LocalizationParamsContext : JsonSerializerContext { }

    [JsonSerializable(typeof(GeneralDataProp))]
    [JsonSourceGenerationOptions(WriteIndented = true, GenerationMode = JsonSourceGenerationMode.Default, IncludeFields = true)]
    internal partial class GeneralDataPropContext : JsonSerializerContext { }

    [JsonSerializable(typeof(Metadata))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    internal partial class MetadataContext : JsonSerializerContext { }

    [JsonSerializable(typeof(Stamp))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    internal partial class StampContext : JsonSerializerContext { }

    [JsonSerializable(typeof(BHI3LInfo))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    public partial class BHI3LInfoContext : JsonSerializerContext { }

    [JsonSerializable(typeof(ScreenSettingData))]
    internal partial class ScreenSettingDataContext : JsonSerializerContext { }

    [JsonSerializable(typeof(PersonalGraphicsSettingV2))]
    internal partial class PersonalGraphicsSettingV2Context : JsonSerializerContext { }

    [JsonSerializable(typeof(PersonalAudioSetting))]
    internal partial class PersonalAudioSettingContext : JsonSerializerContext { }

    [JsonSerializable(typeof(PersonalAudioVolumeValueSetting))]
    internal partial class PersonalAudioVolumeValueSettingContext : JsonSerializerContext { }

    [JsonSerializable(typeof(DataProperties))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    public partial class DataPropertiesContext : JsonSerializerContext { }

    [JsonSerializable(typeof(DataPropertiesContent))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    public partial class DataPropertiesContentContext : JsonSerializerContext { }

    [JsonSerializable(typeof(RegionResourceProp))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    public partial class RegionResourcePropContext : JsonSerializerContext { }

    [JsonSerializable(typeof(List<FilePropertiesRemote>))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    public partial class L_FilePropertiesRemoteContext : JsonSerializerContext { }

    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    public partial class D_StringString : JsonSerializerContext { }
}
