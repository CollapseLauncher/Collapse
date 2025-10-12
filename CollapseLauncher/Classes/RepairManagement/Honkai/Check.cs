using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Hashes;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher
{
    internal partial class HonkaiRepair
    {
        private async Task Check(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            List<FilePropertiesRemote> brokenAssetIndex = [];

            // Set Indetermined status as false
            Status.IsProgressAllIndetermined     = false;
            Status.IsProgressPerFileIndetermined = false;

            // Show the asset entry panel
            Status.IsAssetEntryPanelShow = true;

            // Find unused assets
            CheckUnusedAsset(assetIndex, brokenAssetIndex);

            // Await the task for parallel processing
            try
            {
                // Check for skippable assets to skip the check
                RemoveSkipableAssets(assetIndex);

                // Iterate assetIndex and check it using different method for each type and run it in parallel
                await Parallel.ForEachAsync(assetIndex, new ParallelOptions { MaxDegreeOfParallelism = ThreadCount, CancellationToken = token }, async (asset, threadToken) =>
                {
                    if (asset.IsUsed)
                    {
                        return;
                    }
                    asset.IsUsed = true;

                    // Assign a task depends on the asset type
                    switch (asset.FT)
                    {
                        case FileType.Block:
                            await CheckAssetTypeBlocks(asset, brokenAssetIndex, threadToken);
                            break;
                        case FileType.Audio:
                            await CheckAssetTypeAudio(asset, brokenAssetIndex, threadToken);
                            break;
                        case FileType.Video:
                            CheckAssetTypeVideo(asset, brokenAssetIndex);
                            break;
                        case FileType.Generic:
                        case FileType.Unused:
                        default:
                            await CheckAssetTypeGeneric(asset, brokenAssetIndex, threadToken);
                            break;
                    }
                });
            }
            catch (AggregateException ex)
            {
                var innerExceptionsFirst = ex.Flatten().InnerExceptions.First();
                await SentryHelper.ExceptionHandlerAsync(innerExceptionsFirst, SentryHelper.ExceptionType.UnhandledOther);
                throw innerExceptionsFirst;
            }

            // Re-check unused assets for blocks
            CheckUnusedOldBlocks(assetIndex, brokenAssetIndex);

            // Re-add the asset index with a broken asset index
            assetIndex.Clear();
            assetIndex.AddRange(brokenAssetIndex);
        }

        #region Additional Old Block Removal Check
        private void CheckUnusedOldBlocks(List<FilePropertiesRemote> origAssetIndex, List<FilePropertiesRemote> targetAssetIndex)
        {
            HashSet<string> listedAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string filePath in origAssetIndex
                .Select(x => Path.Combine(GamePath, x.N.NormalizePath()))
                .Where(x => x.EndsWith(".wmv")))
            {
                _ = listedAssets.Add(filePath);
            }

            HashSet<string> listedAssetsTarget = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string filePath in targetAssetIndex
                .Select(x => Path.Combine(GamePath, x.N.NormalizePath()))
                .Where(x => x.EndsWith(".wmv")))
            {
                _ = listedAssetsTarget.Add(filePath);
            }

            string gamePath = GamePath.NormalizePath();
            foreach (string blockPath in Directory.EnumerateFiles(gamePath, "*.wmv", SearchOption.AllDirectories))
            {
                if (!listedAssets.Contains(blockPath) &&
                    !listedAssetsTarget.Contains(blockPath))
                {
                    FileInfo fileInfo = new FileInfo(blockPath)
                        .EnsureNoReadOnly(out bool isExist);

                    if (isExist)
                    {
                        string nameBase = blockPath.Substring(gamePath.Length).TrimStart(['/', '\\']);
                        targetAssetIndex.Add(new FilePropertiesRemote
                        {
                            N = nameBase,
                            S = fileInfo.Length,
                            FT = FileType.Unused
                        });
                        Dispatch(() => AssetEntry.Add(new AssetProperty<RepairAssetType>(
                                                           Path.GetFileName(nameBase),
                                                           RepairAssetType.Unused,
                                                           Path.GetDirectoryName(nameBase),
                                                           fileInfo.Length,
                                                           null,
                                                           null
                                                          )
                                                     ));
                    }
                }
            }
        }
        #endregion

        #region VideoCheck
        private void CheckAssetTypeVideo(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex)
        {
            // Increment current total count
            // _progressAllCountCurrent++;

            // Increment current Total Size
            // _progressAllSizeCurrent += asset.S;

            // Get file path
            string filePath = Path.Combine(GamePath, ConverterTool.NormalizePath(asset.N));
            FileInfo file = new FileInfo(filePath);

            // If file doesn't exist
            if (file.Exists)
            {
                return;
            }

            // Increment progress count and size
            ProgressAllSizeFound += asset.S;
            ProgressAllCountFound++;

            Dispatch(() => AssetEntry.Add(new AssetProperty<RepairAssetType>(Path.GetFileName(asset.N),
                                                                             RepairAssetType.Video,
                                                                             Path.GetDirectoryName(asset.N),
                                                                             asset.S,
                                                                             null,
                                                                             asset.CRCArray)
                                         ));

            // Add asset for missing/unmatched size file
            targetAssetIndex.Add(asset);

            LogWriteLine($"File [T: {asset.FT}]: {asset.N} is not found", LogType.Warning, true);
        }
        #endregion

        #region AudioCheck
        private async Task CheckAssetTypeAudio(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
        {
            // Update activity status
            Status.ActivityStatus = string.Format(Lang._GameRepairPage.Status6, asset.N);

            // Increment current total count
            ProgressAllCountCurrent++;

            // Reset per file size counter
            ProgressPerFileSizeTotal = asset.S;
            ProgressPerFileSizeCurrent = 0;

            // Get file path
            string filePath = Path.Combine(GamePath, asset.N);
            FileInfo file = new FileInfo(filePath);

            // If file doesn't exist or the file asset length isn't the same as the actual length
            // and doesn't have Patch Info, then add it.
            if (!file.Exists || (file.Exists && file.Length != asset.S && asset.AudioPatchInfo == null))
            {
                // Increment progress count and size
                ProgressAllSizeFound += asset.S;
                ProgressAllCountFound++;

                ProgressPerFileSizeCurrent = asset.S;

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
                switch (file.Exists)
                {
                    case false:
                        LogWriteLine($"File [T: {asset.FT}]: {asset.N} is not found locally", LogType.Warning, true);
                        break;
                    case true when file.Length != asset.S && asset.AudioPatchInfo == null:
                        LogWriteLine($"File [T: {asset.FT}]: {asset.N} has unmatched size", LogType.Warning, true); // length mismatch
                        break;
                }

                // Increment current Total Size
                ProgressAllSizeCurrent += asset.S;
                return;
            }

            // If fast method is used, check the patch appliance based on its length
            if (UseFastMethod)
            {
                // If the patch info has a value and the length is similar, then flag it as patch applicable
                if (asset.AudioPatchInfo != null && file.Length == asset.S)
                {
                    asset.IsPatchApplicable = true;
                }

                // Skip CRC check
                return;
            }

            // Open and read fileInfo as FileStream 
            await using FileStream fileStream = await NaivelyOpenFileStreamAsync(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            // If pass the check above, then do MD5 Hash calculation
            byte[] localCrc = asset.CRCArray.Length > 8
                ? await base.GetCryptoHashAsync<MD5>(fileStream, null, true, true, token)
                : await GetCryptoHashAsync<MD5>(fileStream, null, true, true, token);

            if (asset.CRCArray.Length == 8) // Reverse the hash if the game version is >= 8.2.0
                Array.Reverse(localCrc);

            // Get size difference for summarize the _progressAllSizeCurrent
            long sizeDifference = asset.S - file.Length;

            // If the asset has patch info and the hash is matching with the old hash,
            // then flag it as Patch Applicable
            if (asset.AudioPatchInfo != null && IsArrayMatch(localCrc, asset.AudioPatchInfo.OldAudioMD5Array))
            {
                asset.IsPatchApplicable = true;
            }

            // If local and asset CRC doesn't match, then add the asset
            if (!IsArrayMatch(localCrc, asset.CRCArray))
            {
                // Increment/decrement the size of the file based on size differences
                ProgressAllSizeCurrent += sizeDifference;
                // ReSharper disable PossibleInvalidOperationException
                // Increment progress count and size
                ProgressAllSizeFound += asset.IsPatchApplicable ? asset.AudioPatchInfo!.PatchFileSize : asset.S;
                ProgressAllCountFound++;

                // Add asset to Display
                Dispatch(() => AssetEntry.Add(
                     new AssetProperty<RepairAssetType>(
                          Path.GetFileName(asset.N),
                          asset.IsPatchApplicable
                              ? RepairAssetType.AudioUpdate
                              : RepairAssetType.Audio,
                          Path.GetDirectoryName(asset.N),
                          asset.IsPatchApplicable
                              ? asset.AudioPatchInfo!.PatchFileSize
                              : asset.S,
                          localCrc,
                          asset.IsPatchApplicable
                              ? asset.AudioPatchInfo!.NewAudioMD5Array
                              : asset.CRCArray
                         )
                    ));
                // ReSharper restore PossibleInvalidOperationException
                // Add asset into targetAssetIndex
                targetAssetIndex.Add(asset);

                LogWriteLine($"File [T: {asset.FT}]: {asset.N} " + (asset.IsPatchApplicable ? "has an update and patch applicable" : $"is broken! Index CRC: {asset.CRC} <--> File CRC: {HexTool.BytesToHexUnsafe(localCrc)}"), LogType.Warning, true);
            }
        }
        #endregion

        #region GenericCheck
        private async Task CheckAssetTypeGeneric(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
        {
            // Update activity status
            Status.ActivityStatus = string.Format(Lang._GameRepairPage.Status6, asset.N);

            // Increment current total count
            ProgressAllCountCurrent++;

            // Reset per file size counter
            ProgressPerFileSizeTotal = asset.S;
            ProgressPerFileSizeCurrent = 0;

            // Get file path
            string filePath = Path.Combine(GamePath, asset.N);
            FileInfo file = new FileInfo(filePath);

            // If file doesn't exist or the file size doesn't match, then skip and update the progress
            if (!file.Exists || (file.Exists && file.Length != asset.S))
            {
                ProgressAllSizeCurrent += asset.S;
                ProgressAllSizeFound += asset.S;
                ProgressAllCountFound++;

                ProgressPerFileSizeCurrent = asset.S;

                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(asset.N),
                        RepairAssetType.Generic,
                        Path.GetDirectoryName(asset.N),
                        asset.S,
                        null,
                        asset.CRCArray
                    )
                ));

                // Add asset for missing/unmatched size file
                targetAssetIndex.Add(asset);

                switch (file.Exists)
                {
                    case false:
                        LogWriteLine($"File [T: {asset.FT}]: {asset.N} is not found", LogType.Warning, true);
                        break;
                    case true when file.Length != asset.S:
                        LogWriteLine($"File [T: {asset.FT}]: {asset.N} has unmatched size", LogType.Warning, true);
                        break;
                }
                return;
            }

            // Skip CRC check if fast method is used
            if (UseFastMethod)
            {
                return;
            }

            // Open and read fileInfo as FileStream 
            await using FileStream fileStream = await NaivelyOpenFileStreamAsync(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            // If pass the check above, then do CRC calculation
            byte[] localCrc = await base.GetCryptoHashAsync<MD5>(fileStream, null, true, true, token);

            // If local and asset CRC doesn't match, then add the asset
            if (!IsArrayMatch(localCrc, asset.CRCArray))
            {
                ProgressAllSizeFound += asset.S;
                ProgressAllCountFound++;

                Dispatch(() => AssetEntry.Add(
                                              new AssetProperty<RepairAssetType>(
                                                   Path.GetFileName(asset.N),
                                                   asset.FT == FileType.Audio ? RepairAssetType.Audio : RepairAssetType.Generic,
                                                   Path.GetDirectoryName(asset.N),
                                                   asset.S,
                                                   localCrc,
                                                   asset.CRCArray
                                                  )
                                             ));

                // Add asset for unmatched CRC
                targetAssetIndex.Add(asset);

                LogWriteLine($"File [T: {asset.FT}]: {asset.N} is broken! Index CRC: {asset.CRC} <--> File CRC: {HexTool.BytesToHexUnsafe(localCrc)}", LogType.Warning, true);
            }
        }

        private void RemoveSkipableAssets(List<FilePropertiesRemote> assetIndex)
        {
            // Skip if _isOnlyRecoverMain set to true
            if (IsOnlyRecoverMain) return;

            List<FilePropertiesRemote> removableAssets = [];
            Span<Range>                ranges          = stackalloc Range[2];

            // Iterate the skippable asset and do LINQ check
            foreach (string skippableAsset in _skippableAssets)
            {
                // Try to get the filename and the enum type
                ReadOnlySpan<char> skippableNameSpan = skippableAsset.AsSpan();
                _ = skippableNameSpan.Split(ranges, '$');
                ReadOnlySpan<char> skippableName = skippableNameSpan[ranges[0]];
                ReadOnlySpan<char> skippableType = skippableNameSpan[ranges[1]];

                if (!Enum.TryParse(skippableType, true, out FileType skippableFt))
                {
                    continue;
                }

                // Try to get the IEnumerable to iterate the asset
                foreach (FilePropertiesRemote asset in assetIndex)
                {
                    // If the asset name and type is equal, then add as removable
                    if (asset.N.AsSpan().EndsWith(skippableName, StringComparison.OrdinalIgnoreCase)
                        && skippableFt == asset.FT)
                    {
                        // If there's any, then add it to removable assets list
                        removableAssets.Add(asset);
                    }
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
        private static BlockPatchInfo TryGetPossibleOldBlockLinkedPatch(string directory, FilePropertiesRemote block)
        {
            BlockOldPatchInfo existingOldBlockPair = block.BlockPatchInfo?.PatchPairs?
               .Where(x => File.Exists(Path.Combine(directory, x.OldName)))
               .FirstOrDefault();

            if (string.IsNullOrEmpty(existingOldBlockPair?.PatchName)) return null;

            BlockOldPatchInfo oldBlockPairCopy = existingOldBlockPair;

            block.BlockPatchInfo?.PatchPairs.Clear();
            block.BlockPatchInfo?.PatchPairs.Add(oldBlockPairCopy);

            return block.BlockPatchInfo;
        }

        private async Task CheckAssetTypeBlocks(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
        {
            // Update activity status
            Status.ActivityStatus = string.Format(Lang._GameRepairPage.Status5, asset.CRC);

            // Increment current total count
            ProgressAllCountCurrent++;

            // Reset per file size counter
            ProgressPerFileSizeTotal = asset.S;
            ProgressPerFileSizeCurrent = 0;

            // Get original and old path (for patching)
            string blockPath = Path.Combine(GamePath, ConverterTool.NormalizePath(BlockBasePath));
            string filePath = Path.Combine(GamePath, asset.N);
            FileInfo file = new FileInfo(filePath);

            BlockPatchInfo patchInfo = TryGetPossibleOldBlockLinkedPatch(blockPath, asset);
            string filePathOld = patchInfo != null ? Path.Combine(GamePath, ConverterTool.NormalizePath(BlockBasePath), asset.BlockPatchInfo?.PatchPairs[0].OldName!) : null;
            FileInfo fileOld = patchInfo != null ? new FileInfo(filePathOld) : null;

            // If old block exist but current block doesn't, check if the hash of the old block matches and patchable
            if ((fileOld?.Exists ?? false) && !file.Exists)
            {
                // Open and read fileInfo as FileStream 
                await using FileStream fileOldFs = await NaivelyOpenFileStreamAsync(fileOld, FileMode.Open, FileAccess.Read, FileShare.Read);
                // If pass the check above, then do CRC calculation
                byte[] localOldCrc = fileHashToCheck.Length > 8
                    ? await base.GetCryptoHashAsync<MD5>(fileOldFs, null, true, false, token)
                    : await GetCryptoHashAsync<MD5>(fileOldFs, null, true, false, token);

                if (fileHashToCheck.Length == 8) // Reverse the hash if the game version is >= 8.2.0
                    Array.Reverse(localOldCrc);

                // If the hash matches, then add the patch
                if (IsArrayMatch(localOldCrc, patchInfo.PatchPairs[0].OldHash))
                {
                    // Update the total progress and found counter
                    ProgressAllSizeFound += patchInfo.PatchPairs[0].PatchSize;
                    ProgressAllCountFound++;

                    // Set the per size progress
                    ProgressPerFileSizeCurrent = asset.S;

                    // Increment the total current progress
                    ProgressAllSizeCurrent += asset.S;

                    Dispatch(() => AssetEntry.Add(new AssetProperty<RepairAssetType>(
                                                       Path.GetFileName(asset.N),
                                                       RepairAssetType.BlockUpdate,
                                                       Path.GetDirectoryName(asset.N) + $" (MetaVer: {string.Join('.', patchInfo.PatchPairs[0].OldVersion)})",
                                                       patchInfo.PatchPairs[0].PatchSize,
                                                       localOldCrc,
                                                       asset.CRCArray
                                                      )
                                                 ));

                    // Mark the block to be patchable
                    asset.IsPatchApplicable = true;

                    // Add asset for missing/unmatched size file
                    targetAssetIndex.Add(asset);

                    LogWriteLine($"File [T: {asset.FT}]: {HexTool.BytesToHexUnsafe(localOldCrc)} has an update! Orig CRC: {HexTool.BytesToHexUnsafe(localOldCrc)} <--> New CRC: {HexTool.BytesToHexUnsafe(asset.CRCArray)}", LogType.Warning, true);

                    return;
                }
            }

            // Check if the file exist or doesn't have proper size, then mark it.
            bool isFileNotExistOrHasImproperSize = !file.Exists || (file.Exists && file.Length != asset.S);
            bool isFileImproperSize              = file.Exists && file.Length != asset.S;
            bool isFileExist                     = !file.Exists; // invert operator to match logic below

            if (isFileNotExistOrHasImproperSize)
            {
                // Update the total progress and found counter
                ProgressAllSizeFound += asset.S;
                ProgressAllCountFound++;

                // Set the per size progress
                ProgressPerFileSizeCurrent = asset.S;

                // Increment the total current progress
                ProgressAllSizeCurrent += asset.S;

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

                if (isFileImproperSize)
                {
                    LogWriteLine($"File [T: {asset.FT}]: {asset.N} has unmatched size", LogType.Warning, true);
                } else if (isFileExist)
                {
                    LogWriteLine($"File [T: {asset.FT}]: {asset.N} is not found", LogType.Warning, true);
                }

                return;
            }

            // Skip CRC check if fast method is used
            if (UseFastMethod)
            {
                return;
            }

            // Open and read fileInfo as FileStream 
            await using FileStream filefs = await NaivelyOpenFileStreamAsync(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            // If pass the check above, then do CRC calculation
            // Additional: the total file size progress is disabled and will be incremented after this
            byte[] localCrc = fileHashToCheck.Length > 8
                ? await base.GetCryptoHashAsync<MD5>(filefs, null, true, true, token)
                : await GetCryptoHashAsync<MD5>(filefs, null, true, true, token);
            if (_isGame820PostVersion) // Reverse the hash if the game version is >= 8.2.0
                Array.Reverse(localCrc);

            // If local and asset CRC doesn't match, then add the asset
            if (!IsArrayMatch(localCrc, asset.CRCArray))
            {
                ProgressAllSizeFound += asset.S;
                ProgressAllCountFound++;

                Dispatch(() => AssetEntry.Add(
                                              new AssetProperty<RepairAssetType>(
                                                   Path.GetFileName(asset.N),
                                                   RepairAssetType.Block,
                                                   Path.GetDirectoryName(asset.N),
                                                   asset.S,
                                                   localCrc,
                                                   asset.CRCArray
                                                  )
                                             ));

                // Mark the main block as "need to be repaired"
                asset.IsBlockNeedRepair = true;

                // Add asset for unmatched CRC
                targetAssetIndex.Add(asset);

                LogWriteLine($"File [T: {asset.FT}]: {asset.N} is broken! Index CRC: {asset.CRC} <--> File CRC: {HexTool.BytesToHexUnsafe(localCrc)}", LogType.Warning, true);
            }
        }
        #endregion

        #region UnusedAssetCheck
        private void CheckUnusedAsset(List<FilePropertiesRemote> assetIndex, List<FilePropertiesRemote> targetAssetIndex)
        {
            // Build the list of existing files inside the game folder
            // for comparison with asset index into catalog list
            HashSet<string> catalog = new(StringComparer.OrdinalIgnoreCase);
            BuildAssetIndexCatalog(catalog, assetIndex);

            // Compare the catalog list with asset index and add it to target asset index
            // As per update on April 16th 2023, the method below won't be executed while _isOnlyRecoverMain was set to true.
            if (!IsOnlyRecoverMain)
            {
                GetUnusedAssetIndexList(catalog, targetAssetIndex);
            }
        }

        private void BuildAssetIndexCatalog(HashSet<string> catalog, List<FilePropertiesRemote> assetIndex)
        {
            // Iterate the asset index
            foreach (FilePropertiesRemote asset in assetIndex)
            {
                // Get the asset path
                string path = Path.Combine(GamePath, ConverterTool.NormalizePath(asset.N));

                // Determine the type of the asset
                switch (asset.FT)
                {
                    case FileType.Block:
                        catalog.Add(path);
                        if (asset.BlockPatchInfo != null)
                        {
                            string oldBlockPath = Path.Combine(GamePath, ConverterTool.NormalizePath(BlockBasePath), asset.BlockPatchInfo?.PatchPairs[0].OldName!);
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

        private void GetUnusedAssetIndexList(HashSet<string> catalog, List<FilePropertiesRemote> targetAssetIndex)
        {
            SearchValues<string> searchValuesContains = SearchValues.Create([
                "\\ScreenShot\\",
                "webCaches",
                "SDKCaches",
                "Patch",
                ".zip",
                ".7z"
                ], StringComparison.OrdinalIgnoreCase);

            SearchValues<string> searchValuesStartsWith = SearchValues.Create([
                "@",
                "d3d",
                "dxgi.dll",
                GameVersionManager.GamePreset.ProfileName
                ], StringComparison.OrdinalIgnoreCase);

            SearchValues<string> searchValuesEndsWith = SearchValues.Create([
                ".log",
                ".sys",
                "Version.txt",
                ".ini",
                "manifest.m",
                "Wwise_IDs.h",
                "Blocks.xmf",
                $"Blocks_{GameVersion.Major}_{GameVersion.Minor}.xmf",
                "BlockMeta.xmf",
                ".wmv",
                ".patch",
                ".dll"
                ], StringComparison.OrdinalIgnoreCase);

            List<Regex> matchIgnoredFilesRegex = [];
            string      ignoredFilesPath       = Path.Combine(GamePath, "@IgnoredFiles");
            if (File.Exists(ignoredFilesPath))
            {
                try
                {
                    string[] ignoredFiles = File.ReadAllLines(ignoredFilesPath);
                    matchIgnoredFilesRegex.AddRange(
                        ignoredFiles.Select(
                            regex => new Regex(regex,
                                RegexOptions.IgnoreCase |
                                RegexOptions.NonBacktracking |
                                RegexOptions.Compiled)));
                    LogWriteLine("Found ignore file settings!");
                }
                catch (Exception ex)
                {
                    SentryHelper.ExceptionHandler(ex);
                    LogWriteLine($"Failed when reading ignore file setting! Ignoring...\r\n{ex}", LogType.Error, true);
                }
            }

            int pathOffset = GamePath.Length + 1;
            foreach (string asset in Directory.EnumerateFiles(Path.Combine(GamePath), "*", SearchOption.AllDirectories))
            {
                ReadOnlySpan<char> filename = Path.GetFileName(asset);
                ReadOnlySpan<char> assetSpan = asset;

                // Universal
                bool isIncluded = catalog.Contains(asset);
                if (isIncluded) // If it's included, then return
                {
                    continue;
                }

                bool isContainsAnyAsset = assetSpan.ContainsAny(searchValuesContains);
                if (isContainsAnyAsset)
                {
                    continue;
                }

                bool isStartsWithAnyFileName = filename.IndexOfAny(searchValuesStartsWith) == 0;
                int isEndsWithAnyFileNameIndex = filename.IndexOfAny(searchValuesEndsWith);
                bool isEndsWithAnyFileName = isEndsWithAnyFileNameIndex > -1 && isEndsWithAnyFileNameIndex < filename.Length;
                if (isStartsWithAnyFileName && isEndsWithAnyFileName)
                {
                    continue;
                }

                if (isStartsWithAnyFileName)
                {
                    continue;
                }

                if (isEndsWithAnyFileName)
                {
                    continue;
                }

                bool isBlockPatch = asset.EndsWith(".wmv", StringComparison.OrdinalIgnoreCase) && asset.Contains("Patch", StringComparison.OrdinalIgnoreCase);
                if (isBlockPatch)
                {
                    continue;
                }

                // Is file ignored
                bool isFileIgnored = matchIgnoredFilesRegex.Any(x => x.IsMatch(asset));
                if (isFileIgnored)
                {
                    continue;
                }

                string   n = asset.AsSpan()[pathOffset..].ToString();
                FileInfo f = new FileInfo(asset);
                targetAssetIndex.Add(new FilePropertiesRemote
                {
                    N  = n,
                    S  = f.Length,
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

                ProgressAllCountFound++;

                LogWriteLine($"Unused file has been found: {n}", LogType.Warning, true);
            }
        }
        #endregion

        #region Override Methods
#nullable enable
        protected override ConfiguredTaskAwaitable<byte[]> GetCryptoHashAsync<T>(
            Stream stream,
            byte[]? hmacKey = null,
            bool updateProgress = true,
            bool updateTotalProgress = true,
            CancellationToken token = default)
        {
            // If the game version is >= 8.2.0 and the T is MD5, switch to MhyMurmurHash2 implementation.
            if (_isGame820PostVersion && typeof(T) == typeof(MD5))
            {
                // Create the Hasher provider by explicitly specify the length of the stream.
                MhyMurmurHash264B murmurHashProvider = MhyMurmurHash264B.CreateForStream(stream, 0, stream.Length);

                // Pass the provider and return the task
                return Hash.GetHashAsync(stream,
                                         murmurHashProvider,
                                         read => UpdateHashReadProgress(read, updateProgress, updateTotalProgress),
                                         stream is { Length: > 1024 << 20 },
                                         token);
            }

            // Otherwise, fallback to the default implementation.
            return base.GetCryptoHashAsync<T>(stream,
                                              hmacKey,
                                              updateProgress,
                                              updateTotalProgress,
                                              token);
        }
#nullable restore
        #endregion
    }
}
