using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    internal partial class HonkaiRepair
    {
        private async Task Check(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            List<FilePropertiesRemote> brokenAssetIndex = new List<FilePropertiesRemote>();

            // Set Indetermined status as false
            _status.IsProgressTotalIndetermined = false;
            _status.IsProgressPerFileIndetermined = false;

            // Show the asset entry panel
            _status.IsAssetEntryPanelShow = true;

            // Find unused assets
            CheckUnusedAsset(assetIndex, brokenAssetIndex);

            // Await the task for parallel processing
            await Task.Run(() =>
            {
                try
                {
                    // Check for skippable assets to skip the check
                    RemoveSkippableAssets(assetIndex);

                    // Reset stopwatch
                    RestartStopwatch();

                    // Iterate assetIndex and check it using different method for each type and run it in parallel
                    Parallel.ForEach(assetIndex, new ParallelOptions { MaxDegreeOfParallelism = _threadCount }, (asset) =>
                    {
                        // Assign a task depends on the asset type
                        switch (asset.FT)
                        {
                            case FileType.Blocks:
                                CheckAssetTypeBlocks(asset, brokenAssetIndex, token);
                                break;
                            case FileType.Audio:
                                CheckAssetTypeAudio(asset, brokenAssetIndex, token);
                                break;
                            case FileType.Video:
                                CheckAssetTypeVideo(asset, brokenAssetIndex, token);
                                break;
                            default:
                                CheckAssetTypeGeneric(asset, brokenAssetIndex, token);
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

        #region VideoCheck
        private void CheckAssetTypeVideo(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
        {
            // Increment current total count
            // _progressTotalCountCurrent++;

            // Increment current Total Size
            // _progressTotalSizeCurrent += asset.S;

            // Get file path
            string filePath = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.N));
            FileInfo file = new FileInfo(filePath);

            // If file doesn't exist
            if (!file.Exists)
            {
                // Increment progress count and size
                _progressTotalSizeFound += asset.S;
                _progressTotalCountFound++;

                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(asset.N),
                        RepairAssetType.Video,
                        Path.GetDirectoryName(asset.N),
                        asset.S,
                        null,
                        asset.CRCArray
                    )
                ));

                // Add asset for missing/unmatched size file
                targetAssetIndex.Add(asset);

                LogWriteLine($"File [T: {asset.FT}]: {asset.N} is not found", LogType.Warning, true);
            }
        }
        #endregion

        #region AudioCheck
        private void CheckAssetTypeAudio(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
        {
            // Update activity status
            _status.ActivityStatus = string.Format(Lang._GameRepairPage.Status6, asset.N);

            // Increment current total count
            _progressTotalCountCurrent++;

            // Reset per file size counter
            _progressPerFileSize = asset.S;
            _progressPerFileSizeCurrent = 0;

            // Get file path
            string filePath = Path.Combine(_gamePath, asset.N);
            FileInfo file = new FileInfo(filePath);
            byte[] localCRC;

            // If file doesn't exist or the file asset length isn't the same as the actual length
            // and doesn't have Patch Info, then add it.
            if (!file.Exists || (file.Exists && file.Length != asset.S && !asset.AudioPatchInfo.HasValue))
            {
                // Increment progress count and size
                _progressTotalSizeFound += asset.S;
                _progressTotalCountFound++;

                _progressPerFileSizeCurrent = asset.S;

                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(asset.N),
                        RepairAssetType.Audio,
                        Path.GetDirectoryName(asset.N),
                        asset.S,
                        null,
                        asset.CRCArray
                    )
                ));

                // Add asset for missing/unmatched size file
                targetAssetIndex.Add(asset);

                LogWriteLine($"File [T: {asset.FT}]: {asset.N} is not found or has unmatched size", LogType.Warning, true);

                // Increment current Total Size
                _progressTotalSizeCurrent += asset.S;
                return;
            }

            // If pass the check above, then do MD5 Hash calculation
            localCRC = CheckMD5(file.OpenRead(), token);

            // Get size difference for summarize the _progressTotalSizeCurrent
            long sizeDifference = asset.S - file.Length;

            // If the asset has patch info and the hash is matching with the old hash,
            // then flag it as Patch Applicable
            if (asset.AudioPatchInfo.HasValue && IsArrayMatch(localCRC, asset.AudioPatchInfo.Value.OldAudioMD5Array))
            {
                asset.IsPatchApplicable = true;
            }

            // If local and asset CRC doesn't match, then add the asset
            if (!IsArrayMatch(localCRC, asset.CRCArray))
            {
                // Increment/decrement the size of the file based on size differences
                _progressTotalSizeCurrent += sizeDifference;
                // Increment progress count and size
                _progressTotalSizeFound += asset.IsPatchApplicable ? asset.AudioPatchInfo.Value.PatchFileSize : asset.S;
                _progressTotalCountFound++;

                // Add asset to Display
                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(asset.N),
                        asset.IsPatchApplicable ? RepairAssetType.AudioUpdate : RepairAssetType.Audio,
                        Path.GetDirectoryName(asset.N),
                        asset.IsPatchApplicable ? asset.AudioPatchInfo.Value.PatchFileSize : asset.S,
                        localCRC,
                        asset.IsPatchApplicable ? asset.AudioPatchInfo.Value.NewAudioMD5Array : asset.CRCArray
                    )
                ));

                // Add asset into targetAssetIndex
                targetAssetIndex.Add(asset);

                LogWriteLine($"File [T: {asset.FT}]: {asset.N} " + (asset.IsPatchApplicable ? "has an update and patch applicable" : $"is broken! Index CRC: {asset.CRC} <--> File CRC: {localCRC}"), LogType.Warning, true);
            }
        }
        #endregion

        #region GenericCheck
        private void CheckAssetTypeGeneric(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
        {
            // Update activity status
            _status.ActivityStatus = string.Format(Lang._GameRepairPage.Status6, asset.N);

            // Increment current total count
            _progressTotalCountCurrent++;

            // Reset per file size counter
            _progressPerFileSize = asset.S;
            _progressPerFileSizeCurrent = 0;

            // Get file path
            string filePath = Path.Combine(_gamePath, asset.N);
            FileInfo file = new FileInfo(filePath);

            // If file doesn't exist or the file size doesn't match, then skip and update the progress
            if (!file.Exists || (file.Exists && file.Length != asset.S))
            {
                _progressTotalSizeCurrent += asset.S;
                _progressTotalSizeFound += asset.S;
                _progressTotalCountFound++;

                _progressPerFileSizeCurrent = asset.S;

                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(asset.N),
                        RepairAssetType.General,
                        Path.GetDirectoryName(asset.N),
                        asset.S,
                        null,
                        asset.CRCArray
                    )
                ));

                // Add asset for missing/unmatched size file
                targetAssetIndex.Add(asset);

                LogWriteLine($"File [T: {asset.FT}]: {asset.N} is not found or has unmatched size", LogType.Warning, true);
                return;
            }

            // If pass the check above, then do CRC calculation
            byte[] localCRC = CheckMD5(file.OpenRead(), token);

            // If local and asset CRC doesn't match, then add the asset
            if (!IsArrayMatch(localCRC, asset.CRCArray))
            {
                _progressTotalSizeFound += asset.S;
                _progressTotalCountFound++;

                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(asset.N),
                        asset.FT == FileType.Audio ? RepairAssetType.Audio : RepairAssetType.General,
                        Path.GetDirectoryName(asset.N),
                        asset.S,
                        localCRC,
                        asset.CRCArray
                    )
                ));

                // Add asset for unmatched CRC
                targetAssetIndex.Add(asset);

                LogWriteLine($"File [T: {asset.FT}]: {asset.N} is broken! Index CRC: {asset.CRC} <--> File CRC: {HexTool.BytesToHexUnsafe(localCRC)}", LogType.Warning, true);
            }
        }

        private void RemoveSkippableAssets(List<FilePropertiesRemote> assetIndex)
        {
            List<FilePropertiesRemote> removableAssets = new List<FilePropertiesRemote>();

            // Iterate the skippable asset and do LINQ check
            foreach (string skippableAsset in _skippableAssets)
            {
                // Try get the IEnumerable to iterate the asset
                foreach (FilePropertiesRemote asset in assetIndex.Where(x => x.N.Contains(skippableAsset)))
                {
                    // If there's any, then add it to removable assets list
                    removableAssets.Add(asset);
                }
            }

            // Remove all the removable assets in asset index
            foreach (FilePropertiesRemote removableAsset in removableAssets)
            {
                assetIndex.Remove(removableAsset);
            }
        }
        #endregion

        #region BlocksCheck
        private void CheckAssetTypeBlocks(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
        {
            // Update activity status
            _status.ActivityStatus = string.Format(Lang._GameRepairPage.Status5, asset.CRC);

            // Increment current total count
            _progressTotalCountCurrent++;

            // Reset per file size counter
            _progressPerFileSize = asset.S;
            _progressPerFileSizeCurrent = 0;

            bool isAlterAvailable = asset.AlterN != null;

            // Get original and alternative path
            string filePath = Path.Combine(_gamePath, asset.N);
            FileInfo file = new FileInfo(filePath);
            string filePathAlter = isAlterAvailable ? Path.Combine(_gamePath, asset.AlterN) : null;
            FileInfo fileAlter = isAlterAvailable ? new FileInfo(filePathAlter) : null;

            bool isFileNotExistOrHasInproperSize = !file.Exists || (file.Exists && file.Length != asset.S);

            // Main block phase
            // If file doesn't exist or the file size doesn't match, then skip and update the progress
            if (isFileNotExistOrHasInproperSize)
            {
                // Update the total progress and found counter
                _progressTotalSizeFound += asset.S;
                _progressTotalCountFound++;

                // Set the per size progress
                _progressPerFileSizeCurrent = asset.S;

                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(asset.N),
                        RepairAssetType.Block,
                        Path.GetDirectoryName(asset.N),
                        asset.S,
                        null,
                        asset.CRCArray
                    )
                ));

                // Mark the main block as "need to be repaired"
                asset.IsBlockNeedRepair = true;

                // Add asset for missing/unmatched size file
                targetAssetIndex.Add(asset);

                LogWriteLine($"File [T: {asset.FT}]: {asset.N} is not found or has unmatched size", LogType.Warning, true);
            }

            // Main block phase
            // If the main block does have proper size and exist, then do check
            if (!isFileNotExistOrHasInproperSize)
            {
                // If pass the check above, then do CRC calculation
                // Additional: the total file size progress is disabled and will be incremented after this
                byte[] localCRC = CheckMD5(file.OpenRead(), token, false);

                // If local and asset CRC doesn't match, then add the asset
                if (!IsArrayMatch(localCRC, asset.CRCArray))
                {
                    _progressTotalSizeFound += asset.S;
                    _progressTotalCountFound++;

                    Dispatch(() => AssetEntry.Add(
                        new AssetProperty<RepairAssetType>(
                            Path.GetFileName(asset.N),
                            RepairAssetType.Block,
                            Path.GetDirectoryName(asset.N),
                            asset.S,
                            localCRC,
                            asset.CRCArray
                        )
                    ));

                    // Mark the main block as "need to be repaired"
                    asset.IsBlockNeedRepair = true;

                    // Add asset for unmatched CRC
                    targetAssetIndex.Add(asset);

                    LogWriteLine($"File [T: {asset.FT}]: {asset.N} is broken! Index CRC: {asset.CRC} <--> File CRC: {HexTool.BytesToHexUnsafe(localCRC)}", LogType.Warning, true);
                }
            }

            // If the block doesn't have alter, then skip it
            if (!isAlterAvailable)
            {
                // Increment the total current progress
                _progressTotalSizeCurrent += asset.S;
                return;
            }

            // Alter block phase
            // If the alter block exist, then proceed
            if (fileAlter?.Exists ?? false)
            {
                // Assign the PerFileSize progress from existing fileAlter size
                _progressPerFileSizeCurrent = fileAlter.Length;

                // If pass the check above, then do CRC calculation
                // Additional: the total file size progress is disabled and will be incremented after this
                byte[] alterCRC = CheckMD5(fileAlter.OpenRead(), token, false);

                // If local and alter asset CRC doesn't match, then add the alter asset
                if (!IsArrayMatch(alterCRC, asset.BlockPatchInfo.Value.NewHash))
                {
                    // Update the progress
                    _progressTotalSizeFound += asset.S;
                    _progressTotalCountFound++;

                    Dispatch(() => AssetEntry.Add(
                        new AssetProperty<RepairAssetType>(
                            Path.GetFileName(asset.AlterN),
                            RepairAssetType.Block,
                            Path.GetDirectoryName(asset.AlterN),
                            asset.S,
                            alterCRC,
                            asset.BlockPatchInfo.Value.NewHash
                        )
                    ));

                    // Mark asset to use alter name
                    asset.IsUseAlterName = true;

                    // Mark the alter block as "need to be repaired"
                    asset.IsBlockNeedRepair = true;

                    // Add asset for unmatched CRC
                    targetAssetIndex.Add(asset);

                    LogWriteLine($"File [T: {asset.FT}]: {asset.AlterN} is broken! Index CRC: {asset.BlockPatchInfo.Value.NewHash} <--> File CRC: {HexTool.BytesToHexUnsafe(alterCRC)}", LogType.Warning, true);
                }
            }
            // Alter block phase
            // Else, add the asset index as the block is marked to be patchable
            else
            {
                // Update the total progress and found counter
                _progressTotalSizeFound += asset.BlockPatchInfo.Value.PatchSize;
                _progressTotalCountFound++;

                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(asset.N),
                        RepairAssetType.BlockUpdate,
                        Path.GetDirectoryName(asset.N),
                        asset.BlockPatchInfo.Value.PatchSize,
                        asset.CRCArray,
                        asset.BlockPatchInfo.Value.NewHash
                    )
                ));

                // Mark asset to be patchable
                asset.IsPatchApplicable = true;

                // Add asset for unmatched CRC
                targetAssetIndex.Add(asset);

                LogWriteLine($"File [T: {asset.FT}]: {asset.N} has an update! Orig CRC: {asset.CRC} <--> New CRC: {HexTool.BytesToHexUnsafe(asset.BlockPatchInfo.Value.NewHash)}", LogType.Warning, true);
            }

            // Increment the total current progress
            _progressTotalSizeCurrent += asset.S;
        }
        #endregion

        #region UnusedAssetCheck
        private void CheckUnusedAsset(List<FilePropertiesRemote> assetIndex, List<FilePropertiesRemote> targetAssetIndex)
        {
            // Build the list of existing files inside of the game folder
            // for comparison with asset index into catalog list
            List<string> catalog = new List<string>();
            BuildAssetIndexCatalog(catalog, assetIndex);

            // Compare the catalog list with asset index and add it to target asset index
            GetUnusedAssetIndexList(catalog, targetAssetIndex);
        }

        private void BuildAssetIndexCatalog(List<string> catalog, List<FilePropertiesRemote> assetIndex)
        {
            // Iterate the asset index
            foreach (FilePropertiesRemote asset in assetIndex)
            {
                // Get the asset path
                string path = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.N));

                // Determine the type of the asset
                switch (asset.FT)
                {
                    case FileType.Blocks:
                        catalog.Add(path);
                        if (asset.AlterN != null)
                        {
                            catalog.Add(Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.AlterN)));
                        }
                        break;
                    case FileType.Audio:
                    case FileType.Generic:
                        catalog.Add(path);
                        break;

                }
            }
        }

        private void GetUnusedAssetIndexList(List<string> catalog, List<FilePropertiesRemote> targetAssetIndex)
        {
            int pathOffset = _gamePath.Length + 1;
            foreach (string asset in Directory.EnumerateFiles(Path.Combine(_gamePath), "*", SearchOption.AllDirectories))
            {
                // Universal
                bool isIncluded = catalog.Contains(asset);
                bool isScreenshot = asset.Contains("ScreenShot", StringComparison.OrdinalIgnoreCase);
                bool isLog = asset.EndsWith(".log", StringComparison.OrdinalIgnoreCase);

                // Configuration related
                bool isWebcaches = asset.Contains("webCaches", StringComparison.OrdinalIgnoreCase);
                bool isSDKcaches = asset.Contains("SDKCaches", StringComparison.OrdinalIgnoreCase);
                bool isVersion = asset.EndsWith("Version.txt", StringComparison.OrdinalIgnoreCase);
                bool isIni = asset.EndsWith(".ini", StringComparison.OrdinalIgnoreCase);

                // Audio related
                bool isAudioManifest = asset.EndsWith("manifest.m", StringComparison.OrdinalIgnoreCase);
                bool isWwiseHeader = asset.EndsWith("Wwise_IDs.h", StringComparison.OrdinalIgnoreCase);

                // Video related
                bool isUSM = asset.EndsWith(".usm", StringComparison.OrdinalIgnoreCase);

                // Blocks related
                bool isXMFBlocks = asset.EndsWith($"Blocks.xmf", StringComparison.OrdinalIgnoreCase);
                bool isXMFBlocksVer = asset.EndsWith($"Blocks_{_gameVersion.Major}_{_gameVersion.Minor}.xmf", StringComparison.OrdinalIgnoreCase);
                bool isXMFMeta = asset.EndsWith("BlockMeta.xmf", StringComparison.OrdinalIgnoreCase);
                bool isBlockPatch = asset.EndsWith(".wmv", StringComparison.OrdinalIgnoreCase) && asset.Contains("Patch", StringComparison.OrdinalIgnoreCase);

                if (!isIncluded && !isIni && !isXMFBlocks && !isXMFBlocksVer && !isXMFMeta
                    && !isVersion && !isScreenshot && !isWebcaches && !isSDKcaches && !isLog
                    && !isUSM && !isWwiseHeader && !isAudioManifest && !isBlockPatch)
                {
                    string n = asset.AsSpan().Slice(pathOffset).ToString();
                    FileInfo f = new FileInfo(asset);
                    targetAssetIndex.Add(new FilePropertiesRemote()
                    {
                        N = n,
                        S = f.Length,
                        FT = FileType.Unused
                    });
                    Dispatch(() => AssetEntry.Add(
                            new AssetProperty<RepairAssetType>(
                                Path.GetFileName(n),
                                RepairAssetType.Unused,
                                Path.GetDirectoryName(n),
                                f.Length,
                                null,
                                null
                            )
                        ));

                    _progressTotalCountFound++;

                    LogWriteLine($"Unused file has been found: {n}", LogType.Warning, true);
                }
            }
        }
        #endregion
    }
}
