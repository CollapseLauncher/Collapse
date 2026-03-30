using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.Shared.ClassStruct;
using System.Collections.Generic;
using System.Text.Json.Serialization;
// ReSharper disable PartialTypeWithSinglePart

namespace Hi3Helper
{
    [JsonSourceGenerationOptions(IncludeFields = true,
                                 GenerationMode = JsonSourceGenerationMode.Metadata,
                                 IgnoreReadOnlyFields = false,
                                 DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(DataProperties))]
    [JsonSerializable(typeof(PkgVersionProperties))]
    [JsonSerializable(typeof(DataPropertiesContent))]
    [JsonSerializable(typeof(FilePropertiesRemote[]))]
    [JsonSerializable(typeof(List<FilePropertiesRemote>))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    public sealed partial class CoreLibraryJsonContext : JsonSerializerContext;
}
