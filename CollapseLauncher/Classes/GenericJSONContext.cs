using Hi3Helper.EncTool.Parser.AssetMetadata;
using System.Collections.Generic;
using System.Text.Json.Serialization;

#nullable enable
namespace CollapseLauncher
{
    [JsonSerializable(typeof(int[]), GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(AudioPCKType[]), GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(Dictionary<string, Dictionary<string, string>>), GenerationMode = JsonSourceGenerationMode.Metadata)]
    internal sealed partial class GenericJSONContext : JsonSerializerContext;
}
