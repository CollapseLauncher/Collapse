using System;
using CollapseLauncher.RepairManagement.StarRail.Struct.Assets;
// ReSharper disable CommentTypo

#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.RepairManagement.StarRail.Struct;

/// <summary>
/// Star Rail Binary Metadata (SRBM) data parser. This parser is an abstract, read-only and cannot be written back.<br/>
/// This implementation inherit these subtypes:<br/>
/// - <see cref="StarRailAssetBundleMetadata"/><br/>
/// - <see cref="StarRailAssetBlockMetadata"/><br/>
/// - <see cref="StarRailAssetNativeDataMetadata"/><br/>
/// - <see cref="StarRailAssetJsonMetadata"/> (This type, however, doesn't actually parse raw binary data, rather parsing a JSON entry).
/// </summary>
public abstract class StarRailAssetBinaryMetadata<TAsset> : StarRailBinaryData<TAsset>
    where TAsset : StarRailAssetGenericFileInfo
{
    protected StarRailAssetBinaryMetadata(
        short parentTypeFlag,
        short typeVersionFlag,
        int   headerOrDataLength,
        short subStructCount,
        short subStructSize)
        : base(MagicSignatureStatic,
               parentTypeFlag,
               typeVersionFlag,
               headerOrDataLength,
               subStructCount,
               subStructSize) { }

    private static     ReadOnlySpan<byte> MagicSignatureStatic => "SRBM"u8;
    protected override ReadOnlySpan<byte> MagicSignature       => MagicSignatureStatic;
}
