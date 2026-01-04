using Hi3Helper.Data;
using Hi3Helper.Plugin.Core.Utility.Json.Converters;
using System.Text.Json.Serialization;

#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.RepairManagement.StarRail.Struct.Assets;

/// <summary>
/// Star Rail Asset Generic and Flaggable File Info
/// </summary>
public class StarRailAssetFlaggable : StarRailAssetGenericFileInfo
{
    /// <summary>
    /// Defined flags of the asset bundle block file.
    /// </summary>
    public uint Flags { get; init; }

    /// <summary>
    /// To indicate whether this asset is persistent.
    /// </summary>
    public virtual bool IsPersistent => (Flags & 0b00000000_00010000_00000000_00000000u) != 0;

    public override string ToString() =>
        $"{Filename} | Flags: {ConverterTool.ToBinaryString(Flags)} | IsPersistent: {IsPersistent} | Hash: {HexTool.BytesToHexUnsafe(MD5Checksum)} | Size: {FileSize}";
}

/// <summary>
/// Star Rail Asset Generic File Info
/// </summary>
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
