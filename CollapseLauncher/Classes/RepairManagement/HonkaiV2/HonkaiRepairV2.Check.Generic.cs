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
        byte[] hashBuffer = new byte[SHA1.HashSizeInBytes]; // Use SHA1 size as maximum
        return (await IsHashMatchedAuto(asset,
                                        hashBuffer,
                                        useFastCheck,
                                        addAssetIfBroken,
                                        isUpdateTotalProgressCounter,
                                        hmacKey,
                                        false,
                                        token)).IsHashMatched;
    }

    private async Task<(bool IsHashMatched, int HashSize)> IsHashMatchedAuto(
        FilePropertiesRemote asset,
        byte[]               hashBuffer,
        bool                 useFastCheck,
        bool                 addAssetIfBroken             = true,
        bool                 isUpdateTotalProgressCounter = true,
        byte[]?              hmacKey                      = null,
        bool                 isSkipSizeCheck              = false,
        CancellationToken    token                        = default)
    {
        // Update activity status
        Status.ActivityStatus = string.Format(Locale.Lang._GameRepairPage.Status6, asset.N);

        // Increment current total count
        Interlocked.Increment(ref ProgressAllCountCurrent);

        // Reset per file size counter
        ProgressPerFileSizeTotal   = asset.S;
        ProgressPerFileSizeCurrent = 0;

        int hashSize = asset.CRCArray?.Length ?? 0;

        string   assetFilePath = Path.Combine(GamePath, asset.N);
        FileInfo assetFileInfo = new FileInfo(assetFilePath)
           .EnsureNoReadOnly(out bool isAssetExist);

        if (!isAssetExist || (assetFileInfo.Length != asset.S && !isSkipSizeCheck))
        {
            Interlocked.Add(ref ProgressAllSizeCurrent, asset.S);
            if (addAssetIfBroken)
            {
                this.AddBrokenAssetToList(asset);
            }

            await Logger.LogWriteLineAsync($"Asset with {asset} is missing!", LogType.Warning, true, token: token);
            return (false, hashSize);
        }

        // Fast check only checks for file existence and size match
        if (useFastCheck || asset.CRC == null)
        {
            Interlocked.Add(ref ProgressAllSizeCurrent, asset.S);
            return (true, hashSize);
        }

        /* Perform Hashing Check */
        if (hashBuffer.Length < hashSize)
        {
            throw new ArgumentOutOfRangeException(nameof(hashBuffer), "Buffer has less size than the required hash size");
        }

        Memory<byte> hashBufferSpan = hashBuffer.AsMemory(0, hashSize);

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
                                                  hashBufferSpan,
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
                                                  hashBufferSpan,
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
                                                  hashBufferSpan,
                                                  ImplReadBytesAction,
                                                  hmacKey,
                                                  bufferSize,
                                                  token);
                break;
            }
        }

        if (resultStatus == HashOperationStatus.OperationCancelled)
        {
            throw new OperationCanceledException($"Hash check operation for asset {asset} was cancelled!");
        }

        if (resultStatus != HashOperationStatus.Success)
        {
            throw new InvalidOperationException($"Hash check operation for asset {asset} is unsuccessful! (Status code: {resultStatus})");
        }

        // Check hash equality
        if (IsBytesEqualReversible(hashBufferSpan.Span, asset.CRCArray))
        {
            return (true, hashSize);
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
            await Logger.LogWriteLineAsync($"Asset with {asset} has unmatched hash! {asset.CRC} != {HexTool.BytesToHexUnsafe(hashBufferSpan.Span)}",
                                           LogType.Warning,
                                           true,
                                           token: token);
        }
        return (false, hashSize);

        void ImplReadBytesAction(int read) => UpdateHashReadProgress(read, true, isUpdateTotalProgressCounter);
    }
}
