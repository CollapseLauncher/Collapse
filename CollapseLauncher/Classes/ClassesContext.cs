using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.LauncherApiLoader.Sophon;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.Shared.ClassStruct;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CollapseLauncher
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(RegionResourcePluginValidate))]
    [JsonSerializable(typeof(HoYoPlayLauncherResources))]
    [JsonSerializable(typeof(CommunityToolsProperty))]
    [JsonSerializable(typeof(AppUpdateVersionProp))]
    [JsonSerializable(typeof(RegionResourceProp))]
    [JsonSerializable(typeof(NotificationPush))]
    [JsonSerializable(typeof(LauncherGameNews))]
    [JsonSerializable(typeof(GeneralDataProp))]
    [JsonSerializable(typeof(MasterKeyConfig))]
    [JsonSerializable(typeof(AudioPCKType[]))]
    [JsonSerializable(typeof(PresetConfig))]
    [JsonSerializable(typeof(List<Stamp>))]
    [JsonSerializable(typeof(CacheAsset))]
    [JsonSerializable(typeof(BHI3LInfo))]
    [JsonSerializable(typeof(int[]))]
    internal sealed partial class InternalAppJSONContext : JsonSerializerContext;
}
