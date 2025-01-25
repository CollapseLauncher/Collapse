using System.Text.Json.Serialization;
// ReSharper disable PartialTypeWithSinglePart

namespace CollapseLauncher.Helper.Metadata
{
#nullable enable
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(MasterKeyConfig))]
    internal sealed partial class MesterKeyConfigJsonContext : JsonSerializerContext;

    public sealed class MasterKeyConfig : Hashable
    {
        public byte[]? Key { get; set; }
        public int BitSize { get; set; }
    }
}
