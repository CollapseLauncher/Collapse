using System.Collections.Generic;
using System.Text.Json.Serialization;
// ReSharper disable StringLiteralTypo
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable UnusedMember.Global
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(HypLauncherGameInfoApi))]
    internal sealed partial class HoYoPlayLauncherGameInfoJsonContext : JsonSerializerContext;

    public sealed class HypLauncherGameInfoApi : HypApiResponse<HypGameInfo>;

    public sealed class HypGameInfo
    {
        [JsonPropertyName("games")] public List<HypGameInfoData>? Data { get; init; }
        [JsonPropertyName("game_branches")] public List<HypGameInfoBranchesKind>? GameBranchesInfo { get; init; }
    }

    public sealed class HypGameInfoBranchesKind : HypApiIdentifiable
    {
        [JsonPropertyName("main")] public HypGameInfoBranchData? GameMainField { get; init; }
        [JsonPropertyName("pre_download")] public HypGameInfoBranchData? GamePreloadField { get; init; }
    }

    public sealed class HypGameInfoBranchData
    {
        [JsonPropertyName("package_id")] public string? PackageId { get; init; }
        [JsonPropertyName("branch")] public string? Branch { get; init; }
        [JsonPropertyName("password")] public string? Password { get; init; }
        [JsonPropertyName("tag")] public string? Tag { get; init; }
        [JsonPropertyName("diff_tags")] public List<string>? DiffTags { get; init; }
    }
}
