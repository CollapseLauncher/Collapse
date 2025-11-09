using System.Text.Json.Serialization;
// ReSharper disable CheckNamespace

namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;

[JsonSerializable(typeof(HypLauncherBackgroundApi))]
[JsonSerializable(typeof(HypLauncherContentApi))]
[JsonSerializable(typeof(HypLauncherGameResourcePackageApi))]
[JsonSerializable(typeof(HypLauncherGameResourcePluginApi))]
[JsonSerializable(typeof(HypLauncherGameResourceSdkApi))]
[JsonSerializable(typeof(HypLauncherGameResourceWpfApi))]
[JsonSerializable(typeof(HypLauncherGetGameApi))]
[JsonSerializable(typeof(HypLauncherSophonBranchesApi))]
[JsonSourceGenerationOptions(IncludeFields = false,
                             GenerationMode = JsonSourceGenerationMode.Metadata,
                             DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                             IgnoreReadOnlyFields = true)]
internal partial class HypApiJsonContext : JsonSerializerContext;
