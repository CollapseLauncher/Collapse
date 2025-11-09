using System.Collections.Generic;
using System.Text.Json.Serialization;
// ReSharper disable CheckNamespace

namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;

public class HypLauncherGetGameApi : HypApiResponse<HypLauncherGetGameData>;

public class HypLauncherGetGameData : HypGameInfoDataLookupable<HypGameInfoData>
{
    [JsonPropertyName("games")]
    public override List<HypGameInfoData> List
    {
        get;
        set => field = Init(value);
    } = [];
}