using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using static Hi3Helper.Locale;
using static Hi3Helper.Preset.PresetConfigV2;

namespace Hi3Helper
{
    [JsonSourceGenerationOptions(IncludeFields = true, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = false)]
    [JsonSerializable(typeof(Stamp))]
    [JsonSerializable(typeof(Metadata))]
    [JsonSerializable(typeof(BHI3LInfo))]
    [JsonSerializable(typeof(DataProperties))]
    [JsonSerializable(typeof(YSDispatchInfo))]
    [JsonSerializable(typeof(GeneralDataProp))]
    [JsonSerializable(typeof(PkgVersionProperties))]
    [JsonSerializable(typeof(DataPropertiesContent))]
    [JsonSerializable(typeof(FilePropertiesRemote[]))]
    [JsonSerializable(typeof(List<FilePropertiesRemote>))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    public sealed partial class CoreLibraryJSONContext : JsonSerializerContext;

    [JsonSourceGenerationOptions(IncludeFields = true, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(LocalizationParams))]
    internal sealed partial class CoreLibraryFieldsJSONContext : JsonSerializerContext;
}
