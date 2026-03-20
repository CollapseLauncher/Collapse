using CollapseLauncher.Helper;
using CollapseLauncher.Interfaces;
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
    extension(ProgressBase<FilePropertiesRemote> progressBase)
    {
        internal void AddBrokenAssetToList(FilePropertiesRemote asset,
                                           byte[]?              finalHash    = null,
                                           long?                useFoundSize = null)
        {
            AssetProperty<RepairAssetType> property =
                new(Path.GetFileName(asset.N),
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

        internal void PopBrokenAssetFromList(FilePropertiesRemote asset)
        {
            if (asset.AssociatedAssetProperty is IAssetProperty assetProperty)
            {
                progressBase.PopRepairAssetEntry(assetProperty);
            }
        }

        internal void UpdateCurrentRepairStatus(FilePropertiesRemote asset, bool isCacheUpdateMode = false)
        {
            // Increment total count current
            progressBase.ProgressAllCountCurrent++;
            progressBase.Status.ActivityStatus = string.Format(isCacheUpdateMode ? Locale.Current.Lang?._Misc?.Downloading + ": {0}" : Locale.Current.Lang?._GameRepairPage?.Status8 ?? "", asset.N);
            progressBase.UpdateStatus();
        }
    }

    private static RepairAssetType GetRepairAssetType(this FilePropertiesRemote asset) =>
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
