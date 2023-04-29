using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CollapseLauncher.GameSettings.StarRail.Context
    //Modified from Honkai codes because of similarities in reg structure
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(BGMVolume))]
    internal partial class BGMVolume : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(MasterVolume))]
    internal partial class MasterVolume : JsonSerializerContext { }
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(SFXVolume))]
    internal partial class SFXVolume : JsonSerializerContext { }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true, WriteIndented = false)]
    [JsonSerializable(typeof(VOVolume))]
    internal partial class VOVolume : JsonSerializerContext { }


}
