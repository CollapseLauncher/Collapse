using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Serialization;

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;

public class HypLauncherGameResourceWpfApi : HypApiResponse<HypWpfPackageList>;

public class HypWpfPackageList : HypApiDataLookupable<HypWpfPackageData>
{
    [JsonPropertyName("wpf_packages")]
    public override List<HypWpfPackageData> List { get; init; } = [];
}

public class HypWpfPackageData : HypApiIdentifiable
{
    [JsonPropertyName("wpf_package")]
    public HypPackageData? PackageInfo { get; set; }
}