using System.Text.Json.Serialization;

namespace CollapseLauncher.GameSettings.Universal
{

    [JsonSerializable(typeof(CollapseScreenSetting))]
    internal partial class CollapseScreenSettingContext : JsonSerializerContext { }
}
