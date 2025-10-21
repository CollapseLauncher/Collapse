using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.RepairManagement;
using Hi3Helper.Shared.ClassStruct;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher;

internal partial class HonkaiRepairV2
{
    private async ValueTask CheckAssetBlockType(
        FilePropertiesRemote asset,
        bool                 useFastCheck,
        CancellationToken    token)
    {
        string filePath = Path.Combine(GamePath, asset.N);
        FileInfo fileInfo = new FileInfo(filePath)
           .EnsureNoReadOnly(out bool isFileExist);

        if ((!isFileExist || fileInfo.Length != asset.S) &&
            asset.IsPatchApplicable &&
            await CheckAssetBlockWithPatchableType(asset,
                                                   useFastCheck,
                                                   token)
               .ConfigureAwait(false))
        {
            return;
        }

        // Nullify the patch info so it won't be determined as patchable.
        asset.BlockPatchInfo    = null;
        asset.IsPatchApplicable = false;

        // Borrow generic type checker
        await CheckAssetGenericType(asset,
                                    useFastCheck,
                                    token)
           .ConfigureAwait(false);
    }

    private async ValueTask<bool> CheckAssetBlockWithPatchableType(
        FilePropertiesRemote asset,
        bool                 useFastCheck,
        CancellationToken    token)
    {
        if (asset.BlockPatchInfo?.PatchPairs.FirstOrDefault() is not { } patchInfo)
        {
            return false;
        }

        string oldFilePath = Path.Combine(GamePath, AssetBundleExtension.RelativePathBlock, patchInfo.OldName);
        FileInfo oldFileInfo = new FileInfo(oldFilePath)
           .EnsureNoReadOnly(out bool isOldFileExist);

        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if (isOldFileExist && useFastCheck)
        {
            return true;
        }

        if (!isOldFileExist)
        {
            return false;
        }

        int bufferSize = oldFileInfo.Length.GetFileStreamBufferSize();
        await using FileStream oldFileStream = oldFileInfo.Open(FileMode.Open,
                                                                FileAccess.Read,
                                                                FileShare.Read,
                                                                bufferSize);

        // Create dummy FilePropertiesRemote for old file to check
        FilePropertiesRemote oldFileProperties = new()
        {
            S        = oldFileInfo.Length,
            CRCArray = patchInfo.OldHash,
            FT       = FileType.Block,
            N        = Path.Combine(AssetBundleExtension.RelativePathBlock, patchInfo.OldName)
        };

        if (!await IsHashMatchedAuto(oldFileProperties,
                                     false,
                                     false,
                                     token: token))
        {
            // Artificially reset progress size to avoid wrong current counting if patch isn't applicable
            Interlocked.Add(ref ProgressAllSizeCurrent, -oldFileInfo.Length);
            return false;
        }

        // Artificially adjust progress size to avoid wrong total counting
        Interlocked.Add(ref ProgressAllSizeTotal, -asset.S);
        Interlocked.Add(ref ProgressAllSizeTotal, oldFileInfo.Length);

        // Add patch to list
        this.AddBrokenAssetToList(asset, patchInfo.OldHash, patchInfo.PatchSize);
        return true;
    }
}
