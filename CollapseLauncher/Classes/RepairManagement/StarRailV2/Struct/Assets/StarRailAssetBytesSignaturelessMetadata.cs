#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.RepairManagement.StarRail.Struct.Assets;

/// <summary>
/// Star Rail .Bytes Signatureless Metadata parser for DesignDataV and LuaV. This parser is read-only and cannot be written back.<br/>
/// </summary>
public sealed class StarRailAssetBytesSignaturelessMetadata : StarRailAssetSignaturelessMetadata
{
    public StarRailAssetBytesSignaturelessMetadata() : base(".bytes") { }
}
