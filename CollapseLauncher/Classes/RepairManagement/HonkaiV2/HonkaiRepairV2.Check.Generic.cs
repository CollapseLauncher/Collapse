using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.RepairManagement;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Hashes;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher;

internal partial class HonkaiRepairV2
{
    private async ValueTask CheckAssetGenericType(
        FilePropertiesRemote asset,
        bool                 useFastCheck,
        CancellationToken    token)
    {
        _ = await IsHashMatchedAuto(asset,
                                useFastCheck,
                                token: token)
           .ConfigureAwait(false);
    }

    private async Task<bool> IsHashMatchedAuto(
        FilePropertiesRemote asset,
        bool                 useFastCheck,
        bool                 addAssetIfBroken             = true,
        bool                 isUpdateTotalProgressCounter = true,
        byte[]?              hmacKey                      = null,
        CancellationToken    token                        = default)
    {
        // Update activity status
        Status.ActivityStatus = string.Format(Locale.Lang._GameRepairPage.Status6, asset.N);

        // Increment current total count
        Interlocked.Increment(ref ProgressAllCountCurrent);

        // Reset per file size counter
        ProgressPerFileSizeTotal   = asset.S;
        ProgressPerFileSizeCurrent = 0;

        string   assetFilePath = Path.Combine(GamePath, asset.N);
        FileInfo assetFileInfo = new FileInfo(assetFilePath)
           .EnsureNoReadOnly(out bool isAssetExist);

        if (!isAssetExist || assetFileInfo.Length != asset.S)
        {
            Interlocked.Add(ref ProgressAllSizeCurrent, asset.S);
            if (addAssetIfBroken)
            {
                this.AddBrokenAssetToList(asset);
            }

            await Logger.LogWriteLineAsync($"Asset with {asset} is missing!", LogType.Warning, true, token: token);
            return false;
        }

        // Fast check only checks for file existence and size match
        if (useFastCheck || asset.CRC == null)
        {
            Interlocked.Add(ref ProgressAllSizeCurrent, asset.S);
            return true;
        }
        
        // Open file stream.
        int bufferSize = asset.S.GetFileStreamBufferSize();
        await using FileStream assetFileStream = assetFileInfo
           .Open(new FileStreamOptions
                 {
                     Mode = FileMode.Open,
                     Access = FileAccess.Read,
                     Share = FileShare.Read,
                     BufferSize = bufferSize
                 });

        /* Perform Hashing Check */
        int    hashSize   = asset.CRC.Length / 2;
        byte[] hashBuffer = new byte[hashSize];

        // Override hash if HMAC key is null and asset's AssociatedObject is CacheAssetInfo
        hmacKey ??= (asset.AssociatedObject as CacheAssetInfo)?.HmacSha1Salt;

        HashOperationStatus resultStatus = HashOperationStatus.InvalidOperation;
        switch (hashSize)
        {
            case 8:
            {
                MhyMurmurHash264B              hasher   = new((uint)asset.S);
                HashUtility<MhyMurmurHash264B> hashUtil = HashUtility<MhyMurmurHash264B>.ThreadSafe;

                (resultStatus, _) =
                    await hashUtil
                       .TryGetHashFromStreamAsync(hasher,
                                                  assetFileStream,
                                                  hashBuffer,
                                                  ImplReadBytesAction,
                                                  bufferSize,
                                                  token);
                break;
            }
            case MD5.HashSizeInBytes:
            {
                CryptoHashUtility<MD5> hashUtil = CryptoHashUtility<MD5>.ThreadSafe;
                (resultStatus, _) =
                    await hashUtil
                       .TryGetHashFromStreamAsync(assetFileStream,
                                                  hashBuffer,
                                                  ImplReadBytesAction,
                                                  hmacKey,
                                                  bufferSize,
                                                  token);
                break;
            }
            case SHA1.HashSizeInBytes:
            {
                CryptoHashUtility<SHA1> hashUtil = CryptoHashUtility<SHA1>.ThreadSafe;
                (resultStatus, _) =
                    await hashUtil
                       .TryGetHashFromStreamAsync(assetFileStream,
                                                  hashBuffer,
                                                  ImplReadBytesAction,
                                                  hmacKey,
                                                  bufferSize,
                                                  token);
                break;
            }
        }

        if (resultStatus != HashOperationStatus.Success)
        {
            throw new InvalidOperationException($"Hash check operation for asset {asset} is unsuccessful! (Status code: {resultStatus})");
        }

        // Check hash equality
        bool isNeedReverse = true;
    HashCheck:
        if (hashBuffer.SequenceEqual(asset.CRCArray))
        {
            return true;
        }

        if (isNeedReverse)
        {
            Array.Reverse(hashBuffer);
            isNeedReverse = false;
            goto HashCheck;
        }

        // If we reach this point, it means the hash check failed
        if (addAssetIfBroken)
        {
            this.AddBrokenAssetToList(asset);
        }

        // Reverse the hash back for logging
        // ReSharper disable once InvertIf
        if (addAssetIfBroken)
        {
            Array.Reverse(hashBuffer);
            await Logger.LogWriteLineAsync($"Asset with {asset} has unmatched hash! {asset.CRC} != {HexTool.BytesToHexUnsafe(hashBuffer)}",
                                           LogType.Warning,
                                           true,
                                           token: token);
        }
        return false;

        void ImplReadBytesAction(int read) => UpdateHashReadProgress(read, true, true);
    }
}
