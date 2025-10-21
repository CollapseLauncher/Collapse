using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.RepairManagement;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher;

internal partial class HonkaiRepairV2
{
    private async ValueTask CheckAssetAudioType(
        FilePropertiesRemote asset,
        bool                 useFastCheck,
        CancellationToken    token)
    {
        string   filePath = Path.Combine(GamePath, asset.N);
        FileInfo fileInfo = new FileInfo(filePath).EnsureNoReadOnly(out bool isFileExist);

        // Skip hash check and use only length check if fast check is enabled
        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if (useFastCheck &&
            isFileExist &&
            asset.S == fileInfo.Length)
        {
            Interlocked.Add(ref ProgressAllSizeCurrent, asset.S);
            return;
        }

        // If the file doesn't exist. Invalidate patch and add it to broken asset
        if (useFastCheck || !isFileExist)
        {
            Interlocked.Add(ref ProgressAllSizeCurrent, asset.S);
            asset.AudioPatchInfo    = null;
            asset.IsPatchApplicable = false;

            this.AddBrokenAssetToList(asset);
            return;
        }

        // Use custom hash check manually
        byte[] hashBuffer = new byte[SHA1.HashSizeInBytes]; // Use SHA1 for max buffer size
        (bool isHashMatched, int hashBufferSize) =
            await IsHashMatchedAuto(asset,
                                    hashBuffer,
                                    useFastCheck: false,
                                    addAssetIfBroken: false,
                                    isUpdateTotalProgressCounter: true, // Keep the counter updated even asset isn't yet added to broken list
                                    hmacKey: null,
                                    true,
                                    token);

        Memory<byte> hashBufferMemory = hashBuffer.AsMemory(0, hashBufferSize);

        // Step 1: If asset has no patch applicable and hash is matched, skip
        if (isHashMatched)
        {
            return;
        }

        // Step 2: If asset has no patch applicable, add it to broken asset
        if (!asset.IsPatchApplicable ||                                                                           // 1st Check: Ensure if patch is applicable
             asset.AssociatedObject is not ManifestAssetInfo audioAssetInfo ||                                    // 2nd Check: Ensure if AssociatedObject is ManifestAssetInfo
             audioAssetInfo.AllPatchInfo                                                                          // 3rd Check: Ensure if matching patch info is available
                           .FirstOrDefault(x =>
                                               IsBytesEqualReversible(hashBufferMemory.Span,
                                                                      x.OldAudioMD5Array))
                 is not { } patchInfo)
        {
            byte[] finalBytesToDisplay = new byte[hashBufferSize];
            hashBufferMemory.CopyTo(finalBytesToDisplay);

            // Invalidate patch info
            asset.IsPatchApplicable = false;
            asset.AudioPatchInfo    = null;

            this.AddBrokenAssetToList(asset, finalBytesToDisplay);
            return;
        }

        // Step 3: Once the patch info is found, assign the corresponded patch info
        asset.AudioPatchInfo = patchInfo;
        this.AddBrokenAssetToList(asset, asset.AudioPatchInfo.OldAudioMD5Array, asset.AudioPatchInfo.PatchFileSize);
    }

    private static bool IsBytesEqualReversible(Span<byte> hash, Span<byte> toCheck)
    {
        bool isNeedReverse = true;
    StartCheck:
        if (hash.SequenceEqual(toCheck))
        {
            if (!isNeedReverse)
            {
                toCheck.Reverse(); // Restore original order
            }
            return true;
        }

        if (isNeedReverse)
        {
            toCheck.Reverse(); // Try reversed order
            isNeedReverse = false;
            goto StartCheck;
        }

        toCheck.Reverse(); // Restore original order
        return false;
    }
}
