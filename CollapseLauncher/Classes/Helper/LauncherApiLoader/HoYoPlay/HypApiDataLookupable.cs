using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable CheckNamespace

#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;

public abstract class HypApiDataLookupable<T> : LookupableApiResponse<T>
    where T : HypApiIdentifiable
{
    protected override List<T> Init(List<T> value)
    {
        DictBiz = value.ToDictionary(x => x.GameInfo?.GameBiz ?? "", StringComparer.OrdinalIgnoreCase);
        DictId = value.ToDictionary(x => x.GameInfo?.GameId ?? "", StringComparer.OrdinalIgnoreCase);

        return value;
    }
}

public abstract class HypGameInfoDataLookupable<T> : LookupableApiResponse<T>
    where T : HypGameInfoData
{
    protected override List<T> Init(List<T> value)
    {
        DictBiz = value.ToDictionary(x => x.GameBiz ?? "", StringComparer.OrdinalIgnoreCase);
        DictId = value.ToDictionary(x => x.GameId ?? "", StringComparer.OrdinalIgnoreCase);

        return value;
    }
}

public abstract class LookupableApiResponse<T>
{
    [JsonIgnore]
    protected Dictionary<string, T> DictBiz = new(StringComparer.OrdinalIgnoreCase);
    [JsonIgnore]
    protected Dictionary<string, T> DictId = new(StringComparer.OrdinalIgnoreCase);

    public abstract List<T> List
    {
        get;
        set;
    }

    public bool TryFindByBiz(string? key, [NotNullWhen(true)] out T? result) =>
        DictBiz.TryGetValue(key ?? "", out result);

    public bool TryFindById(string? key, [NotNullWhen(true)] out T? result) =>
        DictId.TryGetValue(key ?? "", out result);

    protected abstract List<T> Init(List<T> value);

    public bool TryFindByBizOrId(string? biz, string? id, [NotNullWhen(true)] out T? result)
    {
        return TryFindByBiz(biz, out result) || TryFindById(id, out result);
    }
}