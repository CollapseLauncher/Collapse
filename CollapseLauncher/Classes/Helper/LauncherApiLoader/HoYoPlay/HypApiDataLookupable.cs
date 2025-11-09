using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
// ReSharper disable MemberCanBePrivate.Global

#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;

public abstract class HypApiDataLookupable<T>
    where T : HypApiIdentifiable
{
    [JsonIgnore]
    private Dictionary<string, T> _dictBiz = new(StringComparer.OrdinalIgnoreCase);
    [JsonIgnore]
    private Dictionary<string, T> _dictId = new(StringComparer.OrdinalIgnoreCase);

    public abstract List<T> List
    {
        get;
        set;
    }

    protected List<T> Init(List<T> value)
    {
        _dictBiz = value.ToDictionary(x => x.GameInfo?.GameBiz ?? "",
                                      StringComparer.OrdinalIgnoreCase);
        _dictId = value.ToDictionary(x => x.GameInfo?.GameId ?? "",
                                     StringComparer.OrdinalIgnoreCase);

        return value;
    }

    public bool TryFindByBiz(string? key, [NotNullWhen(true)] out T? result) =>
        _dictBiz.TryGetValue(key ?? "", out result);

    public bool TryFindById(string? key, [NotNullWhen(true)] out T? result) =>
        _dictId.TryGetValue(key ?? "", out result);

    public bool TryFindByBizOrId(string? biz, string? id, [NotNullWhen(true)] out T? result)
    {
        if (TryFindByBiz(biz, out result))
        {
            return true;
        }

        return TryFindById(id, out result);
    }
}
