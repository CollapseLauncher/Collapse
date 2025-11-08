using System.Collections.Generic;
using System.Text.Json.Serialization;
// ReSharper disable StringLiteralTypo
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable UnusedMember.Global
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;

public sealed class HypLauncherSophonBranchesApi : HypApiResponse<HypLauncherSophonBranchesData>;

public class HypLauncherSophonBranchesData : HypApiDataLookupable<HypLauncherSophonBranchesKind>
{
    [JsonPropertyName("game_branches")]
    public override List<HypLauncherSophonBranchesKind> List { get; init; } = [];
}

public sealed class HypLauncherSophonBranchesKind : HypApiIdentifiable
{
    [JsonPropertyName("main")]
    public HypGameInfoBranchData? GameMainField { get; init; }

    [JsonPropertyName("pre_download")]
    public HypGameInfoBranchData? GamePreloadField { get; init; }
}

public sealed class HypGameInfoBranchData
{
    [JsonPropertyName("package_id")]
    public string? PackageId { get; init; }

    [JsonPropertyName("branch")]
    public string? Branch { get; init; }

    [JsonPropertyName("password")]
    public string? Password { get; init; }

    [JsonPropertyName("tag")]
    public string? Tag { get; init; }

    [JsonPropertyName("diff_tags")]
    public List<string>? DiffTags { get; init; }
}
