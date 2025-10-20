using CollapseLauncher.Interfaces;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable CheckNamespace
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.RepairManagement;

internal static partial class AssetBundleExtension
{
    internal static void AddBrokenAssetToList(
        this ProgressBase<FilePropertiesRemote> progressBase,
        FilePropertiesRemote                    asset,
        byte[]?                                 finalHash = null)
    {
        progressBase.Dispatch(AddToUITable);
        lock (progressBase.AssetIndex)
        {
            progressBase.AssetIndex.Add(asset);
        }

        progressBase.Status.IsAssetEntryPanelShow = progressBase.AssetIndex.Count > 0;
        Interlocked.Add(ref progressBase.ProgressAllSizeFound, asset.S);
        Interlocked.Increment(ref progressBase.ProgressAllCountFound);

        return;

        void AddToUITable()
        {
            AssetProperty<RepairAssetType> property =
                new(Path.GetFileName(asset.N),
                    asset.GetRepairAssetType(),
                    Path.GetDirectoryName(asset.N) ?? "\\",
                    asset.S,
                    finalHash,
                    asset.CRCArray);

            progressBase.AssetEntry.Add(property);
        }
    }

    internal static RepairAssetType GetRepairAssetType(this FilePropertiesRemote asset) =>
        asset switch
        {
            { FT: FileType.Audio, IsPatchApplicable: true } => RepairAssetType.AudioUpdate,
            { FT: FileType.Audio } => RepairAssetType.Audio,
            { FT: FileType.Block, IsPatchApplicable: true } => RepairAssetType.BlockUpdate,
            { FT: FileType.Block } => RepairAssetType.Block,
            { FT: FileType.Video } => RepairAssetType.Video,
            { FT: FileType.Unused } => RepairAssetType.Unused,
            _ => RepairAssetType.Generic
        };
}
