using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.LauncherApiLoader.Sophon;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.Shared.ClassStruct;
using System.Collections.Generic;
using System.Text.Json.Serialization;
// ReSharper disable PartialTypeWithSinglePart

namespace CollapseLauncher
{
    [JsonSourceGenerationOptions(
        IncludeFields = false,
        GenerationMode = JsonSourceGenerationMode.Metadata,
        IgnoreReadOnlyFields = true,
        // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true)]
    [JsonSerializable(typeof(Dictionary<string, Dictionary<string, string>>))]
    [JsonSerializable(typeof(RegionResourcePluginValidate))]
    [JsonSerializable(typeof(HoYoPlayLauncherResources))]
    [JsonSerializable(typeof(HoYoPlayLauncherGameInfo))]
    [JsonSerializable(typeof(CommunityToolsProperty))]
    [JsonSerializable(typeof(HoYoPlayLauncherNews))]
    [JsonSerializable(typeof(AppUpdateVersionProp))]
    [JsonSerializable(typeof(RegionResourceProp))]
    [JsonSerializable(typeof(NotificationPush))]
    [JsonSerializable(typeof(LauncherGameNews))]
    [JsonSerializable(typeof(GeneralDataProp))]
    [JsonSerializable(typeof(MasterKeyConfig))]
    [JsonSerializable(typeof(AudioPCKType[]))]
    [JsonSerializable(typeof(LocalFileInfo))]
    [JsonSerializable(typeof(PresetConfig))]
    [JsonSerializable(typeof(List<Stamp>))]
    [JsonSerializable(typeof(CacheAsset))]
    [JsonSerializable(typeof(BHI3LInfo))]
    [JsonSerializable(typeof(int[]))]
    internal sealed partial class InternalAppJSONContext : JsonSerializerContext;
}
