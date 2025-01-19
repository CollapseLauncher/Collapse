// ReSharper disable InconsistentNaming
using System.Text.Json.Serialization;

namespace CollapseLauncher.Helper.Metadata
{
    [JsonSerializable(typeof(BHI3LInfo), GenerationMode = JsonSourceGenerationMode.Metadata)]
    internal sealed partial class BHI3LInfoJSONContext : JsonSerializerContext;

    internal sealed class BHI3LInfo
    {
        public BHI3LInfo_GameInfo game_info { get; set; }
    }

    internal sealed class BHI3LInfo_GameInfo
    {
        public string version { get; set; }
        public string install_path { get; set; }
        public bool installed { get; set; }
    }
}
