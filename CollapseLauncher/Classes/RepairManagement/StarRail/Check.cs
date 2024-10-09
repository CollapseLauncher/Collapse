using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    internal static partial class StarRailRepairExtension
    {
        internal static string ReplaceStreamingToPersistentPath(string inputPath, string execName, FileType type)
        {
            string parentStreamingRelativePath = string.Format(type switch
            {
                FileType.Block => StarRailRepair._assetGameBlocksStreamingPath,
                FileType.Audio => StarRailRepair._assetGameAudioStreamingPath,
                FileType.Video => StarRailRepair._assetGameVideoStreamingPath,
                _ => string.Empty
            }, execName);
            string parentPersistentRelativePath = string.Format(type switch
            {
                FileType.Block => StarRailRepair._assetGameBlocksPersistentPath,
                FileType.Audio => StarRailRepair._assetGameAudioPersistentPath,
                FileType.Video => StarRailRepair._assetGameVideoPersistentPath,
                _ => string.Empty
            }, execName);

            int indexOfStart = inputPath.IndexOf(parentStreamingRelativePath);
            int indexOfEnd = indexOfStart + parentStreamingRelativePath.Length;

            if (indexOfStart == -1) return inputPath;

            ReadOnlySpan<char> startOfPath = inputPath.AsSpan(0, indexOfStart).TrimEnd('\\');
            ReadOnlySpan<char> endOfPath = inputPath.AsSpan(indexOfEnd, inputPath.Length - indexOfEnd).TrimStart('\\');

            string returnPath = Path.Join(startOfPath, parentPersistentRelativePath, endOfPath);
            return returnPath;
        }

        internal static string GetFileRelativePath(string inputPath, string parentPath) => inputPath.AsSpan(parentPath.Length).ToString();
    }

    internal partial class StarRailRepair
    {
        private async Task Check(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            // Try to find "badlist.byte" files in the game folder and delete it
            foreach (string badlistFile in Directory.EnumerateFiles(_gamePath, "*badlist*.byte*", SearchOption.AllDirectories))
            {
                LogWriteLine($"Removing bad list mark at: {badlistFile}", LogType.Warning, true);
                TryDeleteReadOnlyFile(badlistFile);
            }

            // Try to find "verify.fail" files in the game folder and delete it
            foreach (string verifFail in Directory.EnumerateFiles(_gamePath, "*verify*.fail*", SearchOption.AllDirectories))
            {
                LogWriteLine($"Removing verify.fail mark at: {verifFail}", LogType.Warning, true);
                TryDeleteReadOnlyFile(verifFail);
            }

            List<FilePropertiesRemote> brokenAssetIndex = new List<FilePropertiesRemote>();

            // Set Indetermined status as false
            _status.IsProgressAllIndetermined = false;
            _status.IsProgressPerFileIndetermined = false;

            // Show the asset entry panel
            _status.IsAssetEntryPanelShow = true;

            // Await the task for parallel processing
            try
            {
                // Reset stopwatch
                RestartStopwatch();

                // Iterate assetIndex and check it using different method for each type and run it in parallel
                await Parallel.ForEachAsync(assetIndex, new ParallelOptions { MaxDegreeOfParallelism = _threadCount, CancellationToken = token }, async (asset, threadToken) =>
                {
                    // Assign a task depends on the asset type
                    switch (asset.FT)
                    {
                        case FileType.Generic:
                            await CheckGenericAssetType(asset, brokenAssetIndex, threadToken);
                            break;
                        case FileType.Block:
                            await CheckAssetType(asset, brokenAssetIndex, threadToken);
                            break;
                        case FileType.Audio:
                            await CheckAssetType(asset, brokenAssetIndex, threadToken);
                            break;
                        case FileType.Video:
                            await CheckAssetType(asset, brokenAssetIndex, threadToken);
                            break;
                    }
                });
            }
            catch (AggregateException ex)
            {
                throw ex.Flatten().InnerExceptions.First();
            }
            catch (Exception)
            {
                throw;
            }

            // Re-add the asset index with a broken asset index
            assetIndex.Clear();
            assetIndex.AddRange(brokenAssetIndex);
        }

        #region AssetTypeCheck
        private async ValueTask CheckGenericAssetType(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
        {
            // Update activity status
            _status.ActivityStatus = string.Format(Lang._GameRepairPage.Status6, StarRailRepairExtension.GetFileRelativePath(asset.N, _gamePath));

            // Increment current total count
            _progressAllCountCurrent++;

            // Reset per file size counter
            _progressPerFileSizeTotal = asset.S;
            _progressPerFileSizeCurrent = 0;

            // Get the file info
            FileInfo fileInfo = new FileInfo(asset.N);

            // Check if the file exist or has unmatched size
            if (!fileInfo.Exists)
            {
                // Update the total progress and found counter
                _progressAllSizeFound += asset.S;
                _progressAllCountFound++;

                // Set the per size progress
                _progressPerFileSizeCurrent = asset.S;

                // Increment the total current progress
                _progressAllSizeCurrent += asset.S;

                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(asset.N),
                        ConvertRepairAssetTypeEnum(asset.FT),
                        Path.GetDirectoryName(asset.N),
                        asset.S,
                        null,
                        null
                    )
                ));
                targetAssetIndex.Add(asset);

                LogWriteLine($"File [T: {asset.FT}]: {asset.N} is not found", LogType.Warning, true);

                return;
            }

            // Skip CRC check if fast method is used
            if (_useFastMethod)
            {
                return;
            }

            // Open and read fileInfo as FileStream 
            using (FileStream filefs = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // If pass the check above, then do CRC calculation
                // Additional: the total file size progress is disabled and will be incremented after this
                byte[] localCRC = await CheckHashAsync(filefs, MD5.Create(), token);

                // If local and asset CRC doesn't match, then add the asset
                if (!IsArrayMatch(localCRC, asset.CRCArray))
                {
                    _progressAllSizeFound += asset.S;
                    _progressAllCountFound++;

                    Dispatch(() => AssetEntry.Add(
                        new AssetProperty<RepairAssetType>(
                            Path.GetFileName(asset.N),
                            ConvertRepairAssetTypeEnum(asset.FT),
                            Path.GetDirectoryName(asset.N),
                            asset.S,
                            localCRC,
                            asset.CRCArray
                        )
                    ));

                    // Mark the main block as "need to be repaired"
                    asset.IsBlockNeedRepair = true;
                    targetAssetIndex.Add(asset);

                    LogWriteLine($"File [T: {asset.FT}]: {asset.N} is broken! Index CRC: {asset.CRC} <--> File CRC: {HexTool.BytesToHexUnsafe(localCRC)}", LogType.Warning, true);
                }
            }
        }

        private async ValueTask CheckAssetType(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
        {
            // Update activity status
            _status.ActivityStatus = string.Format(Lang._GameRepairPage.Status6, StarRailRepairExtension.GetFileRelativePath(asset.N, _gamePath));

            // Increment current total count
            _progressAllCountCurrent++;

            // Reset per file size counter
            _progressPerFileSizeTotal = asset.S;
            _progressPerFileSizeCurrent = 0;

            // Get persistent and streaming paths
            FileInfo fileInfoPersistent = new FileInfo(StarRailRepairExtension.ReplaceStreamingToPersistentPath(asset.N, _execName, asset.FT));
            FileInfo fileInfoStreaming = new FileInfo(asset.N);

            bool UsePersistent = asset.IsPatchApplicable || !fileInfoStreaming.Exists;
            bool IsHasMark = asset.IsHasHashMark || UsePersistent;
            bool IsPersistentExist = fileInfoPersistent.Exists && fileInfoPersistent.Length == asset.S;
            bool IsStreamingExist = fileInfoStreaming.Exists && fileInfoStreaming.Length == asset.S;

            // Update the local path to full persistent or streaming path and add asset for missing/unmatched size file
            asset.N = UsePersistent ? fileInfoPersistent.FullName : fileInfoStreaming.FullName;

            // Check if the file exist on both persistent and streaming path for non-patch file, then mark the
            // persistent path as redundant (unused)
            if (IsPersistentExist && IsStreamingExist && !asset.IsPatchApplicable)
            {
                // Add the count and asset. Mark the type as "RepairAssetType.Unused"
                _progressAllCountFound++;

                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(fileInfoPersistent.FullName),
                        RepairAssetType.Unused,
                        Path.GetDirectoryName(fileInfoPersistent.FullName),
                        asset.S,
                        null,
                        null
                    )
                ));

                // Fix the asset detected as a used file even though it's actually unused
                asset.FT = FileType.Unused;
                targetAssetIndex.Add(asset);

                // Set the file to be used from Persistent one
                UsePersistent = true;

                LogWriteLine($"File [T: {asset.FT}]: {asset.N} is redundant (exist both on persistent and streaming)", LogType.Warning, true);
            }

            // If the file has Hash Mark or is persistent, then create the hash mark file
            if (IsHasMark) CreateHashMarkFile(asset.N, asset.CRC);

            // Check if both location has the file exist or has the size right
            if ((UsePersistent && !IsPersistentExist && !IsStreamingExist)
             || (UsePersistent && !IsPersistentExist))
            {
                // Update the total progress and found counter
                _progressAllSizeFound += asset.S;
                _progressAllCountFound++;

                // Set the per size progress
                _progressPerFileSizeCurrent = asset.S;

                // Increment the total current progress
                _progressAllSizeCurrent += asset.S;

                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(asset.N),
                        ConvertRepairAssetTypeEnum(asset.FT),
                        Path.GetDirectoryName(asset.N),
                        asset.S,
                        null,
                        null
                    )
                ));
                targetAssetIndex.Add(asset);
                LogWriteLine($"File [T: {asset.FT}]: {asset.N} is not found or has unmatched size", LogType.Warning, true);

                return;
            }

            // Skip CRC check if fast method is used
            if (_useFastMethod)
            {
                return;
            }

            // Open and read fileInfo as FileStream
            string fileNameToOpen = UsePersistent ? fileInfoPersistent.FullName : fileInfoStreaming.FullName;
            try
            {
                await CheckFile(fileNameToOpen, asset, targetAssetIndex, token);
            }
            catch (FileNotFoundException)
            {
                LogWriteLine($"File {fileNameToOpen} is not found while UsePersistent is {UsePersistent}. " +
                             $"Creating hard link and retrying...", LogType.Warning, true);

                var targetFile = File.Exists(fileInfoPersistent.FullName) ? fileInfoPersistent.FullName : 
                    File.Exists(fileInfoStreaming.FullName)           ? fileInfoStreaming.FullName : 
                                                                        throw new FileNotFoundException(fileNameToOpen);
                
                string targetLink     = fileNameToOpen;

                InvokeProp.CreateHardLink(targetLink, targetFile, IntPtr.Zero);
                await CheckFile(fileNameToOpen, asset, targetAssetIndex, token);
            }
        }

        async ValueTask CheckFile(string fileNameToOpen, FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
        {
            await using (FileStream filefs = new FileStream(fileNameToOpen,
                                                      FileMode.Open,
                                                      FileAccess.Read,
                                                      FileShare.Read,
                                                      _bufferBigLength))
            {
                // If pass the check above, then do CRC calculation
                // Additional: the total file size progress is disabled and will be incremented after this
                byte[] localCRC = await CheckHashAsync(filefs, MD5.Create(), token);

                // If local and asset CRC doesn't match, then add the asset
                if (!IsArrayMatch(localCRC, asset.CRCArray))
                {
                    _progressAllSizeFound += asset.S;
                    _progressAllCountFound++;

                    Dispatch(() => AssetEntry.Add(
                                                  new AssetProperty<RepairAssetType>(
                                                       Path.GetFileName(asset.N),
                                                       ConvertRepairAssetTypeEnum(asset.FT),
                                                       Path.GetDirectoryName(asset.N),
                                                       asset.S,
                                                       localCRC,
                                                       asset.CRCArray
                                                      )
                                                 ));

                    // Mark the main block as "need to be repaired"
                    asset.IsBlockNeedRepair = true;
                    targetAssetIndex.Add(asset);

                    LogWriteLine($"File [T: {asset.FT}]: {asset.N} is broken! Index CRC: {asset.CRC} <--> File CRC: {HexTool.BytesToHexUnsafe(localCRC)}", LogType.Warning, true);
                }
            }
        }

        private void CreateHashMarkFile(string filePath, string hash)
        {
            string basePath, baseName;
            RemoveHashMarkFile(filePath, out basePath, out baseName);

            // Create base path if not exist
            if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);

            // Re-create the hash file
            string toName = Path.Combine(basePath, $"{baseName}_{hash}.hash");
            if (File.Exists(toName)) return;
            File.Create(toName).Dispose();
        }

        private static void RemoveHashMarkFile(string filePath, out string basePath, out string baseName)
        {
            // Get the base path and name
            basePath = Path.GetDirectoryName(filePath);
            baseName = Path.GetFileNameWithoutExtension(filePath);

            // Enumerate any possible existing hash path and delete it
            foreach (string existingPath in Directory.EnumerateFiles(basePath, $"{baseName}_*.hash"))
            {
                File.Delete(existingPath);
            }
        }
        #endregion
    }
}
