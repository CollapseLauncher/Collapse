using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.Native.ManagedTools;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher
{
    internal partial class GenshinRepair
    {
        private async Task Check(List<PkgVersionProperties> assetIndex, CancellationToken token)
        {
            List<PkgVersionProperties> brokenAssetIndex = [];

            // Set Indetermined status as false
            Status.IsProgressAllIndetermined = false;
            Status.IsProgressPerFileIndetermined = false;

            // Show the asset entry panel
            Status.IsAssetEntryPanelShow = true;

            // Ensure to delete the DXSetup files
            EnsureDeleteDxSetupFiles();

            // Try to move persistent files to StreamingAssets
            if (IsParsePersistentManifestSuccess)
                TryMovePersistentToStreamingAssets(assetIndex);

            // Check for any redundant files
            await CheckRedundantFiles(brokenAssetIndex, token);

            // Await the task for parallel processing
            try
            {
                var threadCount = ThreadCount;
                var isSsd = DriveTypeChecker.IsDriveSsd(GameStreamingAssetsPath, ILoggerHelper.GetILogger());
                if (!isSsd)
                {
                    threadCount = 1;
                    LogWriteLine($"The drive is not SSD, the repair process will be slower!.\r\n\t" +
                                 $"Thread count set to {threadCount}.", LogType.Warning, true);
                }
                
                // Await the task for parallel processing
                // and iterate assetIndex and check it using different method for each type and run it in parallel
                await Parallel.ForEachAsync(assetIndex, new ParallelOptions { MaxDegreeOfParallelism = threadCount, CancellationToken = token }, async (asset, threadToken) =>
                {
                    await CheckAssetAllType(asset, brokenAssetIndex, threadToken);
                });
            }
            catch (AggregateException ex)
            {
                var innerExceptionsFirst = ex.Flatten().InnerExceptions.First();
                await SentryHelper.ExceptionHandlerAsync(innerExceptionsFirst, SentryHelper.ExceptionType.UnhandledOther);
                throw innerExceptionsFirst;
            }

            // Re-add the asset index with a broken asset index
            assetIndex.Clear();
            assetIndex.AddRange(brokenAssetIndex);
        }

        private void EnsureDeleteDxSetupFiles()
        {
            // Check if the DXSETUP file is exist, then delete it.
            // The DXSETUP files causes some false positive detection of data modification
            // for some games (like Genshin, which causes 4302-x error for some reason)
            string dxSetupDir = Path.Combine(GamePath, "DXSETUP");
            TryDeleteReadOnlyDir(dxSetupDir);
        }

        private void TryMovePersistentToStreamingAssets(List<PkgVersionProperties> assetIndex)
        {
            if (!Directory.Exists(GamePersistentPath)) return;
            TryMoveNonPatchFilesFromPersistent(assetIndex, "AudioAssets", assetName => assetName.EndsWith(".pck", StringComparison.OrdinalIgnoreCase));
            TryMoveNonPatchFilesFromPersistent(assetIndex, "VideoAssets", assetName => assetName.EndsWith(".usm", StringComparison.OrdinalIgnoreCase) ||
                                                                                                      assetName.EndsWith(".cuepoint", StringComparison.OrdinalIgnoreCase));
        }

        private void TryMoveNonPatchFilesFromPersistent(List<PkgVersionProperties> assetIndex, string assetRelativePath, Func<string, bool> assetTypeSelector)
        {
            // Try to get the exclusion list of the patch files
            string[] exclusionList = assetIndex
                                    .Where(x => x.isPatch &&
                                                !string.IsNullOrEmpty(x.remoteName) &&
                                                assetTypeSelector(x.remoteName))
                                    .Select(x => x.remoteName.Replace('/', '\\'))
                                    .ToArray();

            SearchValues<string> exclusionListSearch = SearchValues.Create(exclusionList, StringComparison.OrdinalIgnoreCase);

            // Get the directory paths and create if it doesn't exist
            string assetAsbPath        = Path.Combine(GameStreamingAssetsPath, assetRelativePath);
            string assetPersistentPath = Path.Combine(GamePersistentPath,      assetRelativePath);
            if (!Directory.Exists(assetPersistentPath)) return;

            // Create streaming asset directory anyway
            Directory.CreateDirectory(assetAsbPath);

            // Enumerate all contents of files in persistent directory
            IEnumerable<string> enumerateFilesExcept = Directory
                                                      .EnumerateFiles(assetPersistentPath, "*", SearchOption.AllDirectories)
                                                      .Where(x => !x.AsSpan().ContainsAny(exclusionListSearch));
            foreach (string filePath in enumerateFilesExcept)
            {
                string relativePath = filePath
                                     .AsSpan(assetPersistentPath.Length)
                                     .TrimStart('\\')
                                     .ToString();
                string newPath = Path.Combine(assetAsbPath, relativePath);
                FileInfo newFileInfo = new FileInfo(newPath)
                                      .EnsureCreationOfDirectory()
                                      .StripAlternateDataStream()
                                      .EnsureNoReadOnly();

                FileInfo oldFileInfo = new FileInfo(filePath)
                                      .StripAlternateDataStream()
                                      .EnsureNoReadOnly();

#if DEBUG
                oldFileInfo.TryMoveTo(newFileInfo, true, true);
#else
                oldFileInfo.TryMoveTo(newFileInfo);
#endif
            }
        }

#nullable enable
        private async ValueTask CheckAssetAllType(PkgVersionProperties asset, List<PkgVersionProperties> targetAssetIndex, CancellationToken token)
        {
            // Update activity status
            Status.ActivityStatus = string.Format(Lang._GameRepairPage.Status6, asset.remoteName);

            // Increment current total count
            ProgressAllCountCurrent++;

            // Reset per file size counter
            ProgressPerFileSizeTotal = asset.fileSize;
            ProgressPerFileSizeCurrent = 0;

            // Get file path
            string filePath = Path.Combine(GamePath, asset.remoteName.NormalizePath());

            // Get persistent and streaming paths
            FileInfo fileInfo = new FileInfo(filePath)
                               .EnsureCreationOfDirectory()
                               .StripAlternateDataStream()
                               .EnsureNoReadOnly();

            // Get remote hash to compare
            bool   isUseXxh64       = !string.IsNullOrEmpty(asset.xxh64hash);
            byte[] remoteHashBuffer = new byte[isUseXxh64 ? 8 : 16];
            HexTool.TryHexToBytesUnsafe(isUseXxh64 ? asset.xxh64hash : asset.md5, remoteHashBuffer);

            // Check if the file doesn't exist or has unmatching size, then mark it as broken.
            if (!fileInfo.Exists || fileInfo.Length != asset.fileSize)
            {
                RepairAssetType type = AddToBrokenEntry(asset, null, remoteHashBuffer);
                LogWriteLine($"File [T: {type}]: {asset.remoteName} is missing or has unmatched size",
                             LogType.Warning,
                             true);
                return;
            }

            // If it uses fast method, then ignore hash check
            if (UseFastMethod)
            {
                return;
            }

            // Open the file as Stream and get the current hash
            await using FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] localHashBuffer = isUseXxh64 ?
                await GetHashAsync<XxHash64>(fileStream, true, true, token) :
                await GetCryptoHashAsync<MD5>(fileStream, null, true, true, token);

            // If the hash is unmatched, mark as broken.
            // ReSharper disable once InvertIf
            if (!localHashBuffer.SequenceEqual(remoteHashBuffer))
            {
                RepairAssetType type = AddToBrokenEntry(asset, localHashBuffer, remoteHashBuffer);
                LogWriteLine($"File [T: {type}]: {asset.remoteName} has unmatched hash",
                             LogType.Warning,
                             true);
            }

            return;

            RepairAssetType AddToBrokenEntry(PkgVersionProperties currentAsset, byte[]? localHash, byte[]? remoteHash)
            {
                _ = GetRelativePathByRemoteName(currentAsset.remoteName, out RepairAssetType assetType);

                Interlocked.Increment(ref ProgressAllCountFound);
                Interlocked.Add(ref ProgressAllSizeFound, currentAsset.fileSize);

                Dispatch(() => AssetEntry.Add(new AssetProperty<RepairAssetType>(
                                                   Path.GetFileName(currentAsset.remoteName),
                                                   assetType,
                                                   Path.GetDirectoryName(currentAsset.remoteName),
                                                   currentAsset.fileSize,
                                                   localHash,
                                                   remoteHash
                                                  )
                                             ));

                targetAssetIndex.Add(currentAsset);

                return assetType;
            }
        }
#nullable restore

        #region UnusedFiles
        private async Task CheckRedundantFiles(List<PkgVersionProperties> targetAssetIndex, CancellationToken token)
        {
            // Iterate the available deletefiles files
            DirectoryInfo directoryInfo = new DirectoryInfo(GamePath);
            foreach (FileInfo listFile in directoryInfo
                .EnumerateFiles("*deletefiles*", SearchOption.TopDirectoryOnly)
                .EnumerateNoReadOnly())
            {
                LogWriteLine($"deletefiles file list path: {listFile}", LogType.Default, true);

                // Use deletefiles files to get the list of the redundant file
                await using Stream fs         = await listFile.NaivelyOpenFileStreamAsync(FileMode.Open, FileAccess.Read, FileShare.None, FileOptions.DeleteOnClose);
                using StreamReader listReader = new StreamReader(fs);
                while (!listReader.EndOfStream)
                {
                    // Get the File name and FileInfo
                    var filePath = Path.Combine(GamePath, ConverterTool.NormalizePath(await listReader.ReadLineAsync(token)));
                    var fInfo = new FileInfo(filePath).StripAlternateDataStream().EnsureNoReadOnly();

                    // If the file doesn't exist, then continue
                    if (!fInfo.Exists)
                        continue;

                    // Update total found progress
                    ProgressAllCountFound++;

                    // Get the stripped relative name
                    string strippedName = fInfo.FullName.AsSpan()[(GamePath.Length + 1)..].ToString();

                    // Assign the asset before adding to targetAssetIndex
                    PkgVersionProperties asset = new PkgVersionProperties
                    {
                        localName = strippedName,
                        fileSize  = fInfo.Length
                    };
                    Dispatch(() => AssetEntry.Add(
                                                  new AssetProperty<RepairAssetType>(
                                                       Path.GetFileName(asset.localName),
                                                       RepairAssetType.Unused,
                                                       Path.GetDirectoryName(asset.localName),
                                                       asset.fileSize,
                                                       null,
                                                       null
                                                      )
                                                 ));

                    // Add the asset into targetAssetIndex
                    targetAssetIndex.Add(asset);
                    LogWriteLine($"Redundant file has been found: {strippedName}", LogType.Default, true);
                }
            }

            // Iterate redundant diff and temporary files
            foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles("*.*", SearchOption.AllDirectories)
                .EnumerateNoReadOnly()
                .Where(x => x.Name.EndsWith(".diff",  StringComparison.OrdinalIgnoreCase)
                                 || x.Name.EndsWith("_tmp",   StringComparison.OrdinalIgnoreCase)
                                 || x.Name.EndsWith(".hdiff", StringComparison.OrdinalIgnoreCase)))
            {
                // Update total found progress
                ProgressAllCountFound++;

                // Get the stripped relative name
                string strippedName = fileInfo.FullName.AsSpan()[(GamePath.Length + 1)..].ToString();

                // Assign the asset before adding to targetAssetIndex
                PkgVersionProperties asset = new PkgVersionProperties
                {
                    localName = strippedName,
                    fileSize  = fileInfo.Length
                };
                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(asset.localName),
                        RepairAssetType.Unused,
                        Path.GetDirectoryName(asset.localName),
                        asset.fileSize,
                        null,
                        null
                    )
                ));

                // Add the asset into targetAssetIndex
                targetAssetIndex.Add(asset);
                LogWriteLine($"Redundant file has been found: {strippedName}", LogType.Default, true);
            }
        }
        #endregion
    }
}
