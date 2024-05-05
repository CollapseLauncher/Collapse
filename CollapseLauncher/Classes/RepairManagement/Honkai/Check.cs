﻿using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata;
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
            try
            {
                // Check for skippable assets to skip the check
                RemoveSkippableAssets(assetIndex);

                // Reset stopwatch
                RestartStopwatch();

                // Iterate assetIndex and check it using different method for each type and run it in parallel
                await Parallel.ForEachAsync(assetIndex, new ParallelOptions { MaxDegreeOfParallelism = _threadCount, CancellationToken = token }, async (asset, threadToken) =>
                {
                    // Assign a task depends on the asset type
                    switch (asset.FT)
                    {
                        case FileType.Blocks:
                            await CheckAssetTypeBlocks(asset, brokenAssetIndex, threadToken);
                            break;
                        case FileType.Audio:
                            await CheckAssetTypeAudio(asset, brokenAssetIndex, threadToken);
                            break;
                        case FileType.Video:
                            CheckAssetTypeVideo(asset, brokenAssetIndex);
                            break;
                        default:
                            await CheckAssetTypeGeneric(asset, brokenAssetIndex, threadToken);
                            break;
                    }
                }).ConfigureAwait(false);
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

        #region VideoCheck
        private void CheckAssetTypeVideo(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex)
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
        private async ValueTask CheckAssetTypeAudio(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
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

            // If fast method is used, check the patch appliance based on its length
            if (_useFastMethod)
            {
                // If the patch info has a value and the length is similar, then flag it as patch applicable
                if (asset.AudioPatchInfo.HasValue && file.Length == asset.S)
                {
                    asset.IsPatchApplicable = true;
                }

                // Skip CRC check
                return;
            }

            // Open and read fileInfo as FileStream 
            using (FileStream filefs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None, _bufferBigLength))
            {
                // If pass the check above, then do MD5 Hash calculation
                localCRC = await CheckHashAsync(filefs, MD5.Create(), token);

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

                    LogWriteLine($"File [T: {asset.FT}]: {asset.N} " + (asset.IsPatchApplicable ? "has an update and patch applicable" : $"is broken! Index CRC: {asset.CRC} <--> File CRC: {HexTool.BytesToHexUnsafe(localCRC)}"), LogType.Warning, true);
                }
            }
        }
        #endregion

        #region GenericCheck
        private async ValueTask CheckAssetTypeGeneric(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
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

            // Skip CRC check if fast method is used
            if (_useFastMethod)
            {
                return;
            }

            // Open and read fileInfo as FileStream 
            using (FileStream filefs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None, _bufferBigLength))
            {
                // If pass the check above, then do CRC calculation
                byte[] localCRC = await CheckHashAsync(filefs, MD5.Create(), token);

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
        }

        private void RemoveSkippableAssets(List<FilePropertiesRemote> assetIndex)
        {
            // Skip if _isOnlyRecoverMain set to true
            if (_isOnlyRecoverMain) return;

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
        private BlockPatchInfo? TryGetPossibleOldBlockLinkedPatch(string directory, FilePropertiesRemote block)
        {
            if (!block.BlockPatchInfo.HasValue) return null;

            BlockOldPatchInfo? existingOldBlockPair = block.BlockPatchInfo
                .Value.PatchPairs?
                .Where(x => File.Exists(
                    Path.Combine(directory, x.OldHashStr) + ".wmv"
                    ))?.FirstOrDefault();

            if (!existingOldBlockPair.HasValue || string.IsNullOrEmpty(existingOldBlockPair.Value.PatchHashStr)) return null;

            BlockOldPatchInfo oldBlockPairCopy = existingOldBlockPair.Value;

            block.BlockPatchInfo?.PatchPairs.Clear();
            block.BlockPatchInfo?.PatchPairs.Add(oldBlockPairCopy);

            return block.BlockPatchInfo;
        }

        private async ValueTask CheckAssetTypeBlocks(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
        {
            // Update activity status
            _status.ActivityStatus = string.Format(Lang._GameRepairPage.Status5, asset.CRC);

            // Increment current total count
            _progressTotalCountCurrent++;

            // Reset per file size counter
            _progressPerFileSize = asset.S;
            _progressPerFileSizeCurrent = 0;

            // Get original and old path (for patching)
            string blockPath = Path.Combine(_gamePath, ConverterTool.NormalizePath(_blockBasePath));
            string filePath = Path.Combine(_gamePath, asset.N);
            FileInfo file = new FileInfo(filePath);

            BlockPatchInfo? patchInfo = TryGetPossibleOldBlockLinkedPatch(blockPath, asset);
            string filePathOld = patchInfo.HasValue ? Path.Combine(_gamePath, ConverterTool.NormalizePath(_blockBasePath), asset.BlockPatchInfo?.PatchPairs[0].OldHashStr + ".wmv") : null;
            FileInfo fileOld = patchInfo.HasValue ? new FileInfo(filePathOld) : null;

            // If old block exist but current block doesn't, check if the hash of the old block matches and patchable
            if ((fileOld?.Exists ?? false) && !file.Exists)
            {
                // Open and read fileInfo as FileStream 
                using (FileStream fileOldfs = new FileStream(filePathOld, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, _bufferBigLength))
                {
                    // If pass the check above, then do CRC calculation
                    byte[] localOldCRC = await CheckHashAsync(fileOldfs, MD5.Create(), token, false);

                    // If the hash matches, then add the patch
                    if (IsArrayMatch(localOldCRC, patchInfo?.PatchPairs[0].OldHash))
                    {
                        // Update the total progress and found counter
                        _progressTotalSizeFound += (long)patchInfo?.PatchPairs[0].PatchSize;
                        _progressTotalCountFound++;

                        // Set the per size progress
                        _progressPerFileSizeCurrent = asset.S;

                        // Increment the total current progress
                        _progressTotalSizeCurrent += asset.S;

                        Dispatch(() => AssetEntry.Add(
                            new AssetProperty<RepairAssetType>(
                                Path.GetFileName(asset.N),
                                RepairAssetType.BlockUpdate,
                                Path.GetDirectoryName(asset.N) + $" (MetaVer: {string.Join('.', patchInfo?.PatchPairs[0].OldVersion)})",
                                (long)patchInfo?.PatchPairs[0].PatchSize,
                                localOldCRC,
                                asset.CRCArray
                            )
                        ));

                        // Mark the block to be patchable
                        asset.IsPatchApplicable = true;

                        // Add asset for missing/unmatched size file
                        targetAssetIndex.Add(asset);

                        LogWriteLine($"File [T: {asset.FT}]: {HexTool.BytesToHexUnsafe(localOldCRC)} has an update! Orig CRC: {HexTool.BytesToHexUnsafe(localOldCRC)} <--> New CRC: {HexTool.BytesToHexUnsafe(asset.CRCArray)}", LogType.Warning, true);

                        return;
                    }
                }
            }

            // Check if the file exist or doesn't have proper size, then mark it.
            bool isFileNotExistOrHasInproperSize = !file.Exists || (file.Exists && file.Length != asset.S);

            if (isFileNotExistOrHasInproperSize)
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
                        RepairAssetType.Block,
                        Path.GetDirectoryName(asset.N),
                        asset.S,
                        null,
                        null
                    )
                ));

                // Add asset for missing/unmatched size file
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
            using (FileStream filefs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, _bufferBigLength))
            {
                // If pass the check above, then do CRC calculation
                // Additional: the total file size progress is disabled and will be incremented after this
                byte[] localCRC = await CheckHashAsync(filefs, MD5.Create(), token);

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
            // As per update on April 16th 2023, the method below won't be executed while _isOnlyRecoverMain was set to true.
            if (!_isOnlyRecoverMain)
            {
                GetUnusedAssetIndexList(catalog, targetAssetIndex);
            }
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
                        if (asset.BlockPatchInfo.HasValue)
                        {
                            string oldBlockPath = Path.Combine(_gamePath, ConverterTool.NormalizePath(_blockBasePath), asset.BlockPatchInfo?.PatchPairs[0].OldHashStr + ".wmv");
                            catalog.Add(oldBlockPath);
                        }
                        break;
                    case FileType.Audio:
                    case FileType.Generic:
                    case FileType.Video:
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
                string filename = Path.GetFileName(asset);

                // Universal
                bool isIncluded = catalog.Any(x => x.Equals(asset, StringComparison.OrdinalIgnoreCase));
                bool isScreenshot = asset.Contains("ScreenShot", StringComparison.OrdinalIgnoreCase);
                bool isLog = asset.EndsWith(".log", StringComparison.OrdinalIgnoreCase);
                bool isDriver = asset.EndsWith(".sys", StringComparison.OrdinalIgnoreCase);

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

                // Flags related
                bool isFlags = filename.StartsWith('@');

                // Archive file related
                bool isZip = filename.Contains(".zip", StringComparison.OrdinalIgnoreCase) || filename.Contains(".7z", StringComparison.OrdinalIgnoreCase);

                // Delta-patch related
                bool isDeltaPatch = filename.StartsWith(_gameVersionManager.GamePreset.ProfileName) && asset.EndsWith(".patch");

                // Direct X related
                bool isDirectX = (filename.StartsWith("d3d", StringComparison.OrdinalIgnoreCase) && asset.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    || filename.StartsWith("dxgi.dll", StringComparison.OrdinalIgnoreCase);

                // Is file ignored
                bool isFileIgnored = _ignoredUnusedFileList.Contains(asset, StringComparer.OrdinalIgnoreCase);

                if (!isIncluded && !isFileIgnored && !isIni && !isDriver && !isXMFBlocks && !isXMFBlocksVer && !isXMFMeta
                    && !isVersion && !isScreenshot && !isWebcaches && !isSDKcaches && !isLog
                    && !isUSM && !isWwiseHeader && !isAudioManifest && !isBlockPatch
                    && !isDeltaPatch && !isFlags && !isZip && !isDirectX)
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
