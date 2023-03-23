using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region.Honkai;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using static Hi3Helper.Locale;
using static Hi3Helper.Preset.PresetConfigV2;

namespace Hi3Helper
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(YSDispatchInfo))]
    internal partial class YSDispatchInfoContext : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(PkgVersionProperties))]
    public partial class PkgVersionPropertiesContext : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(LocalizationParams))]
    internal partial class LocalizationParamsContext : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(GeneralDataProp))]
    internal partial class GeneralDataPropContext : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(Metadata))]
    internal partial class MetadataContext : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(Stamp))]
    public partial class StampContext : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(BHI3LInfo))]
    public partial class BHI3LInfoContext : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(DataProperties))]
    public partial class DataPropertiesContext : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(DataPropertiesContent))]
    public partial class DataPropertiesContentContext : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(RegionResourceProp))]
    public partial class RegionResourcePropContext : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(FilePropertiesRemote[]))]
    public partial class Array_FilePropertiesRemoteContext : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(List<FilePropertiesRemote>))]
    public partial class L_FilePropertiesRemoteContext : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    public partial class D_StringString : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(Dispatcher))]
    public partial class DispatcherContext : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(Gateway))]
    public partial class GatewayContext : JsonSerializerContext { }
}
