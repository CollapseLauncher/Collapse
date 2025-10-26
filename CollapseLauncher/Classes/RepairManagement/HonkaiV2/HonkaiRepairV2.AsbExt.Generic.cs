using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using System.IO;
using System.Threading;
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
        byte[]?                                 finalHash    = null,
        long?                                   useFoundSize = null)
    {
        AssetProperty<RepairAssetType> property =
            new AssetProperty<RepairAssetType>(Path.GetFileName(asset.N),
                                               asset.GetRepairAssetType(),
                                               Path.GetDirectoryName(asset.N) ?? "\\",
                                               useFoundSize ?? asset.S,
                                               finalHash,
                                               asset.CRCArray);

        asset.AssociatedAssetProperty = property;
        progressBase.Dispatch(AddToUITable);
        lock (progressBase.AssetIndex)
        {
            progressBase.AssetIndex.Add(asset);
        }

        progressBase.Status.IsAssetEntryPanelShow = progressBase.AssetIndex.Count > 0;
        progressBase.UpdateStatus();
        Interlocked.Add(ref progressBase.ProgressAllSizeFound, useFoundSize ?? asset.S);
        Interlocked.Increment(ref progressBase.ProgressAllCountFound);

        return;

        void AddToUITable()
        {
            progressBase.AssetEntry.Add(property);
        }
    }

    internal static void PopBrokenAssetFromList(
        this ProgressBase<FilePropertiesRemote> progressBase,
        FilePropertiesRemote                    asset)
    {
        if (asset.AssociatedAssetProperty is IAssetProperty assetProperty)
        {
            progressBase.PopRepairAssetEntry(assetProperty);
        }
    }

    internal static void UpdateCurrentRepairStatus(
        this ProgressBase<FilePropertiesRemote> progressBase,
        FilePropertiesRemote                    asset)
    {
        // Increment total count current
        progressBase.ProgressAllCountCurrent++;
        progressBase.Status.ActivityStatus = string.Format(Locale.Lang._GameRepairPage.Status8, asset.N);
        progressBase.UpdateStatus();
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

    /*
    internal static long GetDownloadableSize(this List<FilePropertiesRemote> assetList)
    {
        if (assetList.Count == 0)
        {
            return 0;
        }

        IEnumerable<FilePropertiesRemote> nonPatchableQuery =
            assetList.Where(x => x.FT != FileType.Unused && !x.IsPatchApplicable);

        IEnumerable<uint> patchableBlockLengthQuery =
            assetList.Where(x => x.FT != FileType.Unused && x is { IsPatchApplicable: true, BlockPatchInfo: not null })
                     .Select(x => x.BlockPatchInfo)
                     .SelectMany(x => x?.PatchPairs ?? [])
                     .Select(x => x.PatchSize);

        IEnumerable<uint> patchableAudioLengthQuery =
            assetList.Where(x => x.FT != FileType.Unused && x is { IsPatchApplicable: true, AudioPatchInfo: not null })
                     .Select(x => x.AudioPatchInfo)
                     .Select(x => x?.PatchFileSize ?? 0);

        return nonPatchableQuery.Sum(x => x.S)
            + patchableBlockLengthQuery.Sum(x => x)
            + patchableAudioLengthQuery.Sum(x => x);
    }
    */
}
