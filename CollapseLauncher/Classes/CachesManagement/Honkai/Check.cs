using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Interfaces;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.Log;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable CommentTypo

namespace CollapseLauncher
{
    internal partial class HonkaiCache
    {
        private async Task<List<CacheAsset>> Check(List<CacheAsset> assetIndex, CancellationToken token)
        {
            // Initialize asset index for the return
            List<CacheAsset> returnAsset = [];

            // Set Indetermined status as false
            Status.IsProgressAllIndetermined = false;

            // Show the asset entry panel
            Status.IsAssetEntryPanelShow = true;

            try
            {
                // Create the cache directory if it doesn't exist
                if (!Directory.Exists(GamePath))
                {
                    Directory.CreateDirectory(GamePath!);
                }

                HashSet<string> exceptionFromUnusedCategory = [];

                // Do check in parallelization.
                await Parallel.ForEachAsync(assetIndex!, new ParallelOptions
                {
                    MaxDegreeOfParallelism = ThreadCount,
                    CancellationToken = token
                }, async (asset, localToken) =>
                {
                    await CheckAsset(asset, returnAsset, exceptionFromUnusedCategory, localToken);
                });

                // Check for unused files
                CheckUnusedAssets(assetIndex, returnAsset, exceptionFromUnusedCategory);
            }
            catch (AggregateException ex)
            {
                throw ex.Flatten().InnerExceptions.First();
            }

            // Return the asset index
            return returnAsset;
        }

        private void CheckUnusedAssets(List<CacheAsset> assetIndex, List<CacheAsset> returnAsset, HashSet<string> exceptionFromUnusedCategory)
        {
            // Directory info and if the directory doesn't exist, return
            DirectoryInfo directoryInfo = new(GamePath);
            if (!directoryInfo.Exists)
            {
                return;
            }

            SearchValues<string> unusedSearchValues = SearchValues
               .Create(GameVersionManager.GamePreset.GameInstallFileInfo?.CacheUpdateUnusedFilesIgnoreList
                    ?? [
                           "output_log",
                           "Crashes",
                           "Verify.txt",
                           "APM",
                           "FBData",
                           "asb.dat",
                           "MiHoYoSDK.log"
                       ], StringComparison.OrdinalIgnoreCase);

            // Iterate the file contained in the _gamePath
            foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                .EnumerateNoReadOnly())
            {
                bool excludedFromList = exceptionFromUnusedCategory.Contains(fileInfo.FullName);
                if (excludedFromList ||
                    fileInfo.FullName.AsSpan().ContainsAny(unusedSearchValues) ||
                    assetIndex.Exists(x => x.ConcatPath == fileInfo.FullName))
                {
                    continue;
                }

                // Increment the total found count
                ProgressAllCountFound++;

                // Add asset to the returnAsset
                CacheAsset asset = new()
                {
                    BasePath       = Path.GetDirectoryName(fileInfo.FullName),
                    N              = Path.GetFileName(fileInfo.FullName),
                    DataType       = CacheAssetType.Unused,
                    CS             = fileInfo.Length,
                    CRC            = null,
                    Status         = CacheAssetStatus.Unused,
                    IsUseLocalPath = true
                };
                returnAsset!.Add(asset);

                // Add to asset entry display
                Dispatch(() => AssetEntry!.Add(new AssetProperty<CacheAssetType>(
                                                    asset.N,
                                                    asset.DataType,
                                                    asset.BasePath,
                                                    asset.CS,
                                                    null,
                                                    null
                                                   ))
                        );

                Logger.WriteLine($"File: {asset.ConcatPath} is unused!", LogType.Warning, true);
            }
        }

        private async ValueTask CheckAsset(CacheAsset asset, List<CacheAsset> returnAsset, HashSet<string> exceptionFromUnusedCategory, CancellationToken token)
        {
            // Increment the count and update the status
            Interlocked.Add(ref ProgressAllCountCurrent, 1);
            Status.ActivityStatus = string.Format(Locale.Current.Lang?._CachesPage?.CachesStatusChecking ?? "", asset!.DataType, asset.N);
            Status.ActivityAll = string.Format(Locale.Current.Lang?._CachesPage?.CachesTotalStatusChecking ?? "", ProgressAllCountCurrent, ProgressAllCountTotal);

            // Check for patch ifnormation first.
            if (!UseFastMethod && await IsPatchExist(asset, returnAsset, exceptionFromUnusedCategory, token))
            {
                return;
            }

            // Assign the file info.
            FileInfo fileInfo = new FileInfo(asset.ConcatPath).EnsureNoReadOnly(out bool isExist);

            // Check if the file exist. If not, then add it to asset index.
            if (!isExist)
            {
                AddGenericCheckAsset(asset, CacheAssetStatus.New, returnAsset, null, asset.CRCArray);
                return;
            }

            // Check if the file size matches. if not, then add it to asset index.
            if (fileInfo.Length != asset.CS)
            {
                AddGenericCheckAsset(asset, CacheAssetStatus.New, returnAsset, null, asset.CRCArray);
                return;
            }

            // Skip CRC check if fast method is used
            if (UseFastMethod)
            {
                return;
            }

            // If above passes, then run the CRC check
            await using FileStream fs = await NaivelyOpenFileStreamAsync(fileInfo, FileMode.Open, FileAccess.Read, FileShare.Read);
            // Calculate the asset CRC (SHA1)
            byte[] hashArray = await GetCryptoHashAsync<HMACSHA1>(fs, GameSalt, true, true, token);

            // If the asset CRC doesn't match, then add the file to asset index.
            if (!IsArrayMatch(asset.CRCArray, hashArray))
            {
                AddGenericCheckAsset(asset, CacheAssetStatus.Obsolete, returnAsset, hashArray, asset.CRCArray);
            }
        }

        private async ValueTask<bool> IsPatchExist(
            CacheAsset        asset,
            List<CacheAsset>  returnAsset,
            HashSet<string>   exceptionFromUnusedCategory,
            CancellationToken token)
        {
            if (asset.PatchInfo is not { } patchInfoDict)
                return false;

            string assetDir = Path.Combine(GetAssetBasePathByType(asset.DataType), asset.DataType.ToString().ToLowerInvariant());
            byte[] hashArrayBuffer = ArrayPool<byte>.Shared.Rent(20);

            try
            {
                foreach ((string oldFileName, CachePatchInfo patchInfo) in patchInfoDict)
                {
                    string combinedOldPath   = Path.Combine(assetDir, $"{oldFileName}.unity3d");
                    string combinedPatchPath = Path.Combine(assetDir, patchInfo.PatchName);
                    ConverterTool.NormalizePathInplaceNoTrim(combinedOldPath);

                    FileInfo fileInfo = new(combinedOldPath);

                    if (!fileInfo.Exists)
                        continue;

                    // If above passes, then run the CRC check
                    await using FileStream oldFileStream = await NaivelyOpenFileStreamAsync(fileInfo, FileMode.Open, FileAccess.Read, FileShare.Read);
                    // Calculate the asset CRC (SHA1)
                    byte[] hashArray = await GetCryptoHashAsync<HMACSHA1>(oldFileStream, GameSalt, true, true, token);

                    // If the asset CRC doesn't match, then add the file to asset index.
                    ReadOnlySpan<byte> hashSpan = GetHashFromName(hashArrayBuffer, oldFileName);
                    if (!IsArrayMatch(hashSpan, hashArray))
                    {
                        continue;
                    }

                    // Clear patch info and re-add single patch info.
                    patchInfoDict.Clear();
                    patchInfoDict.Add(oldFileName, patchInfo);
                    exceptionFromUnusedCategory.Add(combinedOldPath);
                    exceptionFromUnusedCategory.Add(combinedPatchPath);

                    AddGenericCheckAsset(asset, CacheAssetStatus.Patch, returnAsset, hashArray, asset.CRCArray);
                    return true;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(hashArrayBuffer);
            }

            // Unassign patch info if none is found.
            asset.PatchInfo = null;
            return false;

            static ReadOnlySpan<byte> GetHashFromName(Span<byte> buffer, ReadOnlySpan<char> name)
            {
                ReadOnlySpan<char> hexString = name.GetSplit(1, '_');
                if (hexString.IsEmpty || HexTool.TryHexToBytesUnsafe(hexString, buffer, out _, out int length) != OperationStatus.Done)
                    return ReadOnlySpan<byte>.Empty;

                return buffer[..length];
            }
        }

        private void AddGenericCheckAsset(CacheAsset asset, CacheAssetStatus assetStatus, List<CacheAsset> returnAsset, byte[] localCrc, byte[] remoteCrc)
        {
            long size = asset.PatchInfo?.Values.FirstOrDefault() is { } patchInfo
                ? patchInfo.PatchFileSize
                : asset.CS;

            // Increment the count and total size
            lock (this)
            {
                // Set Indetermined status as false
                Status.IsProgressAllIndetermined = false;
                ProgressAllCountFound++;
                ProgressAllSizeFound += size;
            }

            // Add file into asset index
            lock (returnAsset!)
            {
                asset.Status = assetStatus;
                returnAsset.Add(asset);

                Logger.WriteLine($"[T: {asset.DataType}]: {asset.N} found to be \"{assetStatus}\"", LogType.Warning, true);
            }

            // Add to asset entry display
            Dispatch(() => AssetEntry!.Add(new AssetProperty<CacheAssetType>(
                    Path.GetFileName(asset.N) ?? "",
                    asset.DataType,
                    Path.GetDirectoryName(asset.N),
                    size,
                    localCrc,
                    remoteCrc
                ))
            );
        }
    }
}
