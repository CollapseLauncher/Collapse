using Hi3Helper.Data;
using Hi3Helper.Plugin.Core.Utility.Json.Converters;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.RepairManagement.StarRail.Struct.Assets;

public class StarRailAssetGenericFileInfo
{
    /// <summary>
    /// The filename of the asset.
    /// </summary>
    [JsonPropertyName("Path")]
    public virtual required string? Filename { get; init; }

    /// <summary>
    /// Size of the file in bytes.
    /// </summary>
    [JsonPropertyName("Size")]
    public required long FileSize { get; init; }

    /// <summary>
    /// The MD5 hash checksum of the file.
    /// </summary>
    [JsonPropertyName("Md5")]
    [JsonConverter(typeof(HexStringToArrayJsonConverter<byte>))]
    public required byte[] MD5Checksum { get; init; }

    public override string ToString() =>
        $"{Filename} | Hash: {HexTool.BytesToHexUnsafe(MD5Checksum)} | Size: {FileSize} bytes";
}
