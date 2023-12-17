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
    internal partial class StarRailRepair
    {
        private async Task Check(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            List<FilePropertiesRemote> brokenAssetIndex = new List<FilePropertiesRemote>();

            // Set Indetermined status as false
            _status.IsProgressTotalIndetermined = false;
            _status.IsProgressPerFileIndetermined = false;

            // Show the asset entry panel
            _status.IsAssetEntryPanelShow = true;

            // Await the task for parallel processing
            await Task.Run(() =>
            {
                try
                {
                    // Reset stopwatch
                    RestartStopwatch();

                    // Get persistent and streaming paths
                    string execName = Path.GetFileNameWithoutExtension(_innerGameVersionManager.GamePreset.GameExecutableName);

                    string baseBlocksPathPersistent = Path.Combine(_gamePath, @$"{execName}_Data\Persistent\Asb\Windows");
                    string baseBlocksPathStreaming = Path.Combine(_gamePath, @$"{execName}_Data\StreamingAssets\Asb\Windows");

                    string baseAudioPathPersistent = Path.Combine(_gamePath, @$"{execName}_Data\Persistent\Audio\AudioPackage\Windows");
                    string baseAudioPathStreaming = Path.Combine(_gamePath, @$"{execName}_Data\StreamingAssets\Audio\AudioPackage\Windows");

                    string baseVideoPathPersistent = Path.Combine(_gamePath, @$"{execName}_Data\Persistent\Video\Windows");
                    string baseVideoPathStreaming = Path.Combine(_gamePath, @$"{execName}_Data\StreamingAssets\Video\Windows");

                    // Iterate assetIndex and check it using different method for each type and run it in parallel
                    Parallel.ForEach(assetIndex, new ParallelOptions { MaxDegreeOfParallelism = _threadCount }, (asset) =>
                    {
                        // Assign a task depends on the asset type
                        switch (asset.FT)
                        {
                            case FileType.Generic:
                                CheckGenericAssetType(asset, brokenAssetIndex, token);
                                break;
                            case FileType.Blocks:
                                CheckAssetType(asset, brokenAssetIndex, baseBlocksPathPersistent, baseBlocksPathStreaming, token);
                                break;
                            case FileType.Audio:
                                CheckAssetType(asset, brokenAssetIndex, baseAudioPathPersistent, baseAudioPathStreaming, token);
                                break;
                            case FileType.Video:
                                CheckAssetType(asset, brokenAssetIndex, baseVideoPathPersistent, baseVideoPathStreaming, token);
                                break;
                        }
                    });
                }
                catch (AggregateException ex)
                {
                    throw ex.Flatten().InnerExceptions.First();
                }
            }).ConfigureAwait(false);

            // Re-add the asset index with a broken asset index
            assetIndex.Clear();
            assetIndex.AddRange(brokenAssetIndex);
        }

        #region AssetTypeCheck
        private void CheckGenericAssetType(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
        {
            // Update activity status
            _status.ActivityStatus = string.Format(Lang._GameRepairPage.Status6, asset.N);

            // Increment current total count
            _progressTotalCountCurrent++;

            // Reset per file size counter
            _progressPerFileSize = asset.S;
            _progressPerFileSizeCurrent = 0;

            // Override the path
            asset.N = Path.Combine(_gamePath, asset.N);

            // Get the file info
            FileInfo fileInfo = new FileInfo(asset.N);

            // Check if the file exist or has unmached size
            if (!fileInfo.Exists || (fileInfo.Exists && fileInfo.Length != asset.S))
            {
                // Update the total progress and found counter
                _progressTotalSizeFound += asset.S;
                _progressTotalCountFound++;

                // Set the per size progress
                _progressPerFileSizeCurrent = asset.S;

                // Increment the total current progress
                _progressTotalSizeCurrent += asset.S;

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
            using (FileStream filefs = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // If pass the check above, then do CRC calculation
                // Additional: the total file size progress is disabled and will be incremented after this
                byte[] localCRC = CheckHash(filefs, MD5.Create(), token);

                // If local and asset CRC doesn't match, then add the asset
                if (!IsArrayMatch(localCRC, asset.CRCArray))
                {
                    _progressTotalSizeFound += asset.S;
                    _progressTotalCountFound++;

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

        private void CheckAssetType(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, string basePersistentPath, string baseStreamingPath, CancellationToken token)
        {
            // Update activity status
            _status.ActivityStatus = string.Format(Lang._GameRepairPage.Status5, asset.CRC);

            // Increment current total count
            _progressTotalCountCurrent++;

            // Reset per file size counter
            _progressPerFileSize = asset.S;
            _progressPerFileSizeCurrent = 0;

            // Get persistent and streaming paths
            FileInfo fileInfoPersistent = new FileInfo(Path.Combine(basePersistentPath, asset.N));
            FileInfo fileInfoStreaming = new FileInfo(Path.Combine(baseStreamingPath, asset.N));

            bool UsePersistent = !fileInfoStreaming.Exists;
            bool IsPersistentExist = fileInfoPersistent.Exists && fileInfoPersistent.Length == asset.S;
            bool IsStreamingExist = fileInfoStreaming.Exists && fileInfoStreaming.Length == asset.S;

            // Update the local path to full persistent or streaming path and add asset for missing/unmatched size file
            asset.N = UsePersistent ? fileInfoPersistent.FullName : fileInfoStreaming.FullName;

            // If the file has Hash Mark, then create the hash mark file
            if (asset.IsHasHashMark && UsePersistent) CreateHashMarkFile(asset.N, asset.CRC);

            // Check if the file exist on both persistent and streaming path, then mark the
            // streaming path as redundant (unused)
            if (IsPersistentExist && IsStreamingExist)
            {
                // Add the count and asset. Mark the type as "RepairAssetType.Unused"
                _progressTotalCountFound++;

                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(fileInfoStreaming.FullName),
                        RepairAssetType.Unused,
                        Path.GetDirectoryName(fileInfoStreaming.FullName),
                        asset.S,
                        null,
                        null
                    )
                ));
                targetAssetIndex.Add(asset);

                LogWriteLine($"File [T: {asset.FT}]: {asset.N} is redundant (exist both on persistent and streaming)", LogType.Warning, true);
            }

            // Check if both location has the file exist or has the size right
            if (UsePersistent && !IsPersistentExist && !IsStreamingExist)
            {
                // Update the total progress and found counter
                _progressTotalSizeFound += asset.S;
                _progressTotalCountFound++;

                // Set the per size progress
                _progressPerFileSizeCurrent = asset.S;

                // Increment the total current progress
                _progressTotalSizeCurrent += asset.S;

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
            using (FileStream filefs = new FileStream(UsePersistent ? fileInfoPersistent.FullName : fileInfoStreaming.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None,
                _bufferBigLength))
            {
                // If pass the check above, then do CRC calculation
                // Additional: the total file size progress is disabled and will be incremented after this
                byte[] localCRC = CheckHash(filefs, MD5.Create(), token);

                // If local and asset CRC doesn't match, then add the asset
                if (!IsArrayMatch(localCRC, asset.CRCArray))
                {
                    _progressTotalSizeFound += asset.S;
                    _progressTotalCountFound++;

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
            // Get the base path and name
            string basePath = Path.GetDirectoryName(filePath);
            string baseName = Path.GetFileNameWithoutExtension(filePath);

            // Create base path if not exist
            if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);

            // Enumerate any possible existing hash path and delete it
            foreach (string existingPath in Directory.EnumerateFiles(basePath, $"{baseName}_*.hash"))
            {
                File.Delete(existingPath);
            }

            // Re-create the hash file
            string toName = Path.Combine(basePath, $"{baseName}_{hash}.hash");
            File.Create(toName).Dispose();
        }
        #endregion
    }
}
