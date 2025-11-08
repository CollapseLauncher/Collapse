using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Serialization;

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;

[JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
[JsonSerializable(typeof(HypLauncherResourceWpfApi))]
internal sealed partial class HypWpfPackagesApiJsonContext : JsonSerializerContext;

public class HypLauncherResourceWpfApi : HypApiResponse<HypWpfPackageList>;

public class HypWpfPackageList : IList<HypWpfPackageData>
{
    [JsonPropertyName("wpf_packages")]
    public List<HypWpfPackageData> Packages { get; init; } = [];

    public IEnumerator<HypWpfPackageData> GetEnumerator() =>
        Packages.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(HypWpfPackageData item) => Packages.Add(item);

    public void Clear() => Packages.Clear();

    public bool Contains(HypWpfPackageData item) => Packages.Contains(item);

    public void CopyTo(HypWpfPackageData[] array, int arrayIndex) => Packages.CopyTo(array, arrayIndex);

    public bool Remove(HypWpfPackageData item) => Packages.Remove(item);

    public int IndexOf(HypWpfPackageData item) => Packages.IndexOf(item);

    public void Insert(int index, HypWpfPackageData item) => Packages.Insert(index, item);

    public void RemoveAt(int index) => Packages.RemoveAt(index);

    [JsonIgnore]
    public int Count => Packages.Count;

    [JsonIgnore]
    public bool IsReadOnly => false;

    [JsonIgnore]
    public bool IsEmpty => Count == 0;

    public HypWpfPackageData this[int index]
    {
        get => Packages[index];
        set => Packages[index] = value;
    }
}

public class HypWpfPackageData
{
    [JsonPropertyName("wpf_package")]
    public HypPackageData? PackageInfo { get; set; }
}