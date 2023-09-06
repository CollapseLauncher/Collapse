﻿using System.Text.Json.Serialization;

namespace CollapseLauncher.GameSettings.Universal
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(CollapseScreenSetting))]
    [JsonSerializable(typeof(CollapseMiscSetting))]
    internal sealed partial class UniversalSettingsJSONContext : JsonSerializerContext { }
}
