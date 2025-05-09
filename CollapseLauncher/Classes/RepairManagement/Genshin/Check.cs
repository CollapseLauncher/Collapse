using CollapseLauncher.GameVersioning;
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
            if (IsParsePersistentManifestSuccess) TryMovePersistentToStreamingAssets(assetIndex);

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

        private void TryMovePersistentToStreamingAssets(IEnumerable<PkgVersionProperties> assetIndex)
        {
            if (!Directory.Exists(GamePersistentPath)) return;
            TryMoveAudioPersistent(assetIndex);
            TryMoveVideoPersistent();
        }

        private void TryMoveAudioPersistent(IEnumerable<PkgVersionProperties> assetIndex)
        {
            // Try to get the exclusion list of the audio (language specific) files
            string[] exclusionList = assetIndex
                .Where(x => x.isPatch && x.remoteNamePersistent != null && x.remoteName
                    .AsSpan()
                    .EndsWith(".pck"))
                .Select(x => x.remoteNamePersistent
                    .Replace('/', '\\'))
                .ToArray();

            // Get the audio directory paths and create if it doesn't exist
            string audioAsbPath = Path.Combine(GameStreamingAssetsPath, "AudioAssets");
            string audioPersistentPath = Path.Combine(GamePersistentPath, "AudioAssets");
            if (!Directory.Exists(audioPersistentPath)) return;

            // Create audio streaming asset directory anyway
            Directory.CreateDirectory(audioAsbPath);

            // Get the list of audio language names from _gameVersionManager
            List<string> audioLangList = ((GameTypeGenshinVersion)GameVersionManager).AudioVoiceLanguageList;

            // Enumerate the content of audio persistent directory
            foreach (string path in Directory.EnumerateDirectories(audioPersistentPath, "*", SearchOption.TopDirectoryOnly))
            {
                // Get the last path section as language name to compare
                string langName = Path.GetFileName(path);

                // If the path section matches the name in language list, then continue
                if (!audioLangList.Contains(langName))
                {
                    continue;
                }

                // Enumerate the files that's exist in the persistent path of each language
                // except the one that's included in the exclusion list
                foreach (string filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                                                     .Where(x => !exclusionList.Any(y => x.AsSpan().EndsWith(y.AsSpan()))))
                {
                    // Trim the path name to get the generic languageName/filename form
                    string pathName = filePath.AsSpan()[(audioPersistentPath.Length + 1)..].ToString();
                    // Combine the generic name with audioAsbPath
                    string newPath = EnsureCreationOfDirectory(Path.Combine(audioAsbPath, pathName));

                    // Try to move the file to the asb path
                    File.Move(filePath, newPath, true);
                }
            }
        }

        private void TryMoveVideoPersistent()
        {
            string videoAsbPath = Path.Combine(GameStreamingAssetsPath, "VideoAssets");
            string videoPersistentPath = Path.Combine(GamePersistentPath, "VideoAssets");
            if (!Directory.Exists(videoPersistentPath)) return;
            if (!Directory.Exists(videoAsbPath)) Directory.CreateDirectory(videoAsbPath);
            MoveFolderContent(videoPersistentPath, videoAsbPath);
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
            string filePathStreaming = Path.Combine(GamePath, ConverterTool.NormalizePath(asset.remoteName));
            string filePathPersistent = Path.Combine(GamePath, ConverterTool.NormalizePath(asset.remoteNamePersistent));

            // Get persistent and streaming paths
            FileInfo fileInfoStreaming  = new FileInfo(filePathStreaming).EnsureNoReadOnly();
            FileInfo fileInfoPersistent = new FileInfo(filePathPersistent).EnsureNoReadOnly();

            // Decide file state
            bool isPersistentExist = fileInfoPersistent.Exists;
            bool isStreamingExist  = fileInfoStreaming.Exists;

                // Get the file info to use
                bool isUnmatchedFileFound = !await IsFileMatched(
                                             a =>
                                             {
                                                 asset.isForceStoreInStreaming = a;
                                                 if (a)
                                                 {
                                                     asset.localName = filePathStreaming;
                                                 }
                                             },
                                             b =>
                                             {
                                                 asset.isForceStoreInPersistent = b;
                                                 if (b)
                                                 {
                                                     asset.localName = filePathPersistent;
                                                 }
                                             },
                                             (c, d) =>
                                             {
                                                 // Add the count and asset. Mark the type as "RepairAssetType.Unused"
                                                 ProgressAllCountFound++;

                                                 PkgVersionProperties clonedAsset = asset.Clone();

                                                 Dispatch(() => AssetEntry.Add(new AssetProperty<RepairAssetType>(
                                                                                    Path.GetFileName(c),
                                                                                    RepairAssetType.Unused,
                                                                                    Path.GetDirectoryName(c),
                                                                                    clonedAsset.fileSize,
                                                                                    null,
                                                                                    null
                                                                                   )
                                                                              ));

                                                 clonedAsset.type      = "Unused";
                                                 clonedAsset.localName = d;
                                                 targetAssetIndex.Add(clonedAsset);

                                                 LogWriteLine($"File [T: {clonedAsset.type}]: {c} is redundant (exist both in persistent and streaming)", LogType.Warning, true);
                                             }
                                             );

                if (isUnmatchedFileFound)
                {
                    AddNotFoundOrMismatchAsset(asset);
                }

            return;

            async ValueTask<bool> IsFileMatched(Action<bool> storeAsStreaming, Action<bool> storeAsPersistent, Action<string, string> storeAsUnused)
            {
                bool isUsePersistent = asset.isPatch ||
                                       (!string.IsNullOrEmpty(asset.xxh64hash) &&
                                        !string.IsNullOrEmpty(asset.xxh64hashPersistent) &&
                                        !asset.xxh64hashPersistent.Equals(asset.xxh64hash, StringComparison.OrdinalIgnoreCase));

                // Try to get hash buffer
                byte[] hashBuffer = ArrayPool<byte>.Shared.Rent(16);
                try
                {
                    // Create memory and compare the hash
                    string       streamingHash       = asset.xxh64hash ?? asset.md5;
                    Memory<byte> streamingHashMemory = new(hashBuffer, 0, streamingHash.Length / 2);
                    _ = HexTool.TryHexToBytesUnsafe(streamingHash, streamingHashMemory.Span);

                    if (!isUsePersistent)
                    {
                        if (isPersistentExist)
                        {
                            storeAsUnused(asset.remoteNamePersistent, fileInfoPersistent.FullName);
                        }

                        // ReSharper disable once InvertIf
                        if (!isStreamingExist ||
                            !await IsFileHashMatch(fileInfoStreaming, asset.fileSize, streamingHashMemory, true, token))
                        {
                            storeAsStreaming(true);
                            storeAsPersistent(false);
                            asset.isStreamingNeedDownload = true;
                            return false;
                        }

                        return true;
                    }

                    bool isBrokenFoundOnPersistent = false;
                    bool isHasStreamingInvalid = false;

                    if (!asset.isPatch &&
                        isStreamingExist &&
                        !await IsFileHashMatch(fileInfoStreaming, asset.fileSize, streamingHashMemory, true, token))
                    {
                        storeAsStreaming(true);
                        storeAsPersistent(false);
                        isBrokenFoundOnPersistent = true;
                        isHasStreamingInvalid = true;
                        asset.isStreamingNeedDownload = true;
                    }

                    // Create memory and compare the hash
                    string       persistentHash = asset.xxh64hashPersistent ?? asset.md5Persistent;
                    Memory<byte> persistentHashMemory = new(hashBuffer, 0, persistentHash.Length / 2);
                    _ = HexTool.TryHexToBytesUnsafe(persistentHash, persistentHashMemory.Span);

                    if (isHasStreamingInvalid)
                    {
                        Interlocked.Add(ref ProgressAllSizeCurrent, -fileInfoStreaming.Length);
                    }

                    // ReSharper disable once InvertIf
                    if (!isPersistentExist || !await IsFileHashMatch(fileInfoPersistent, asset.fileSizePersistent, persistentHashMemory, true, token))
                    {
                        storeAsStreaming(false);
                        storeAsPersistent(true);
                        isBrokenFoundOnPersistent = true;
                        asset.isPersistentNeedDownload = true;
                    }

                    return !isBrokenFoundOnPersistent;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(hashBuffer);
                }
            }

            async ValueTask<bool> IsFileHashMatch(FileInfo fileInfo, long fileSizeToCheck, ReadOnlyMemory<byte> hashToCompare, bool isUpdateProgress, CancellationToken cancelToken)
            {
                // Refresh the fileInfo
                fileInfo.Refresh();

                if (fileInfo.Length != fileSizeToCheck)
                    return false; // Skip the hash calculation if the file size is different

                if (UseFastMethod) return true; // Skip the hash calculation if the fast method is enabled

                // Try to get filestream
                await using FileStream fileStream = await fileInfo.NaivelyOpenFileStreamAsync(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                // If pass the check above, then do hash calculation
                // Additional: the total file size progress is disabled and will be incremented after this
                byte[] localHash = hashToCompare.Length == 8 ?
                    await GetHashAsync<XxHash64>(fileStream, isUpdateProgress, isUpdateProgress, cancelToken) :
                    await GetCryptoHashAsync<MD5>(fileStream, null, isUpdateProgress, isUpdateProgress, cancelToken);

                // Check if the hash is equal
                bool isMatch = IsArrayMatch(hashToCompare.Span, localHash);
                return isMatch;
            }

            void AddNotFoundOrMismatchAsset(PkgVersionProperties assetInner)
            {
                // Update the total progress and found counter
                if (assetInner.isStreamingNeedDownload)
                {
                    Interlocked.Increment(ref ProgressAllCountFound);
                    Interlocked.Add(ref ProgressAllSizeFound, assetInner.fileSize);

                    Dispatch(() => AssetEntry.Add(new AssetProperty<RepairAssetType>(
                                                       Path.GetFileName(assetInner.remoteName),
                                                       RepairAssetType.Generic,
                                                       Path.GetDirectoryName(assetInner.remoteName),
                                                       assetInner.fileSize,
                                                       null,
                                                       HexTool.HexToBytesUnsafe(string.IsNullOrEmpty(assetInner.xxh64hash) ? assetInner.md5 : assetInner.xxh64hash)
                                                      )
                                                 ));

                    targetAssetIndex.Add(assetInner);

                    LogWriteLine($"File [T: {RepairAssetType.Generic}-Streaming]: {assetInner.localName} is not found or has unmatched size",
                                 LogType.Warning, true);
                }

                if (!assetInner.isPersistentNeedDownload)
                {
                    return;
                }

                Interlocked.Increment(ref ProgressAllCountFound);
                Interlocked.Add(ref ProgressAllSizeFound, assetInner.fileSizePersistent);

                Dispatch(() => AssetEntry.Add(new AssetProperty<RepairAssetType>(
                                                   Path.GetFileName(assetInner.remoteNamePersistent),
                                                   RepairAssetType.Generic,
                                                   Path.GetDirectoryName(assetInner.remoteNamePersistent),
                                                   assetInner.fileSizePersistent,
                                                   null,
                                                   HexTool.HexToBytesUnsafe(string.IsNullOrEmpty(assetInner.xxh64hashPersistent) ? assetInner.md5Persistent : assetInner.xxh64hashPersistent)
                                                  )
                                             ));

                targetAssetIndex.Add(assetInner.CloneAsPersistent());

                LogWriteLine($"File [T: {RepairAssetType.Generic}-Persistent]: {assetInner.localName} is not found or has unmatched size",
                             LogType.Warning, true);
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
                    var fInfo = new FileInfo(filePath).EnsureNoReadOnly();

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
                        fileSize  = fInfo.Length,
                        type      = RepairAssetType.Unused.ToString()
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
                    fileSize  = fileInfo.Length,
                    type      = RepairAssetType.Unused.ToString()
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
