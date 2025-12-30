using System;
using CollapseLauncher.RepairManagement.StarRail.Struct.Assets;
// ReSharper disable CommentTypo
#pragma warning disable IDE0290
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.RepairManagement.StarRail.Struct;

/// <summary>
/// Star Rail Binary Metadata (SRBM) data parser. This parser is an abstract, read-only and cannot be written back.<br/>
/// This implementation inherit these subtypes:<br/>
///   <see cref="StarRailAssetNativeDataMetadata"/>
///   <see cref="StarRailAssetBlockMetadata"/>
/// </summary>
public abstract class StarRailAssetBinaryMetadata<TAsset> : StarRailBinaryData<TAsset>
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
