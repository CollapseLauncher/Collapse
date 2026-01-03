using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.SentryHelper;
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
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault

#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher
{
    internal partial class StarRailRepairV2
    {
        private async Task Check(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            // Try to find "badlist.byte" files in the game folder and delete it
            foreach (string badListFile in Directory.EnumerateFiles(GamePath, "*badlist*.byte*", SearchOption.AllDirectories))
            {
                LogWriteLine($"Removing bad list mark at: {badListFile}", LogType.Warning, true);
                TryDeleteReadOnlyFile(badListFile);
            }

            // Try to find "verify.fail" files in the game folder and delete it
            foreach (string verifyFail in Directory.EnumerateFiles(GamePath, "*verify*.fail*", SearchOption.AllDirectories))
            {
                LogWriteLine($"Removing verify.fail mark at: {verifyFail}", LogType.Warning, true);
                TryDeleteReadOnlyFile(verifyFail);
            }

            List<FilePropertiesRemote> brokenAssetIndex = [];

            // Set Indetermined status as false
            Status.IsProgressAllIndetermined     = false;
            Status.IsProgressPerFileIndetermined = false;

            // Show the asset entry panel
            Status.IsAssetEntryPanelShow = true;

            // Await the task for parallel processing
            try
            {
                // Iterate assetIndex and check it using different method for each type and run it in parallel
                await Parallel.ForEachAsync(assetIndex, new ParallelOptions { MaxDegreeOfParallelism = ThreadCount, CancellationToken = token }, async (asset, threadToken) =>
                {
                    await CheckGenericAssetType(asset, brokenAssetIndex, threadToken);
                });
            }
            catch (AggregateException ex)
            {
                Exception innerExceptionsFirst = ex.Flatten().InnerExceptions.First();
                await SentryHelper.ExceptionHandlerAsync(innerExceptionsFirst, SentryHelper.ExceptionType.UnhandledOther);
                throw innerExceptionsFirst;
            }

            // Re-add the asset index with a broken asset index
            assetIndex.Clear();
            assetIndex.AddRange(brokenAssetIndex);
        }

        #region AssetTypeCheck

        private async Task CheckGenericAssetType(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
        {
            // Update activity status
            Status.ActivityStatus = string.Format(Lang._GameRepairPage.Status6, asset.N);

            // Increment current total count
            ProgressAllCountCurrent++;

            // Reset per file size counter
            ProgressPerFileSizeTotal   = asset.S;
            ProgressPerFileSizeCurrent = 0;

            string gamePath = GamePath;
            string filePath = Path.Combine(gamePath, asset.N);

            AddUnusedHashMarkFile(filePath, gamePath, asset, targetAssetIndex);
            if (asset.FT == FileType.Unused)
            {
                AddIndex(asset, targetAssetIndex);
                LogWriteLine($"File: {asset.N} is unused", LogType.Warning, true);
                return;
            }

            // Get the file info
            FileInfo fileInfo = new(filePath);

            // Check if the file exist or has unmatched size
            if (!fileInfo.Exists)
            {
                AddIndex(asset, targetAssetIndex);
                LogWriteLine($"File [T: {asset.FT}]: {asset.N} is not found", LogType.Warning, true);
                return;
            }

            if (fileInfo.Length != asset.S)
            {
                if (fileInfo.Name.Contains("pkg_version")) return;
                AddIndex(asset, targetAssetIndex);
                LogWriteLine($"File [T: {asset.FT}]: {asset.N} has unmatched size " +
                             $"(Local: {fileInfo.Length} <=> Remote: {asset.S}",
                             LogType.Warning, true);
                return;
            }

            // Skip CRC check if fast method is used
            if (UseFastMethod)
            {
                return;
            }

            // Open and read fileInfo as FileStream 
            await using FileStream fileStream = await NaivelyOpenFileStreamAsync(fileInfo, FileMode.Open, FileAccess.Read, FileShare.Read);
            // If pass the check above, then do CRC calculation
            // Additional: the total file size progress is disabled and will be incremented after this
            byte[] localCrc = await GetCryptoHashAsync<MD5>(fileStream, null, true, true, token);

            // If local and asset CRC doesn't match, then add the asset
            if (IsArrayMatch(localCrc, asset.CRCArray))
            {
                return;
            }

            AddIndex(asset, targetAssetIndex);
            LogWriteLine($"File [T: {asset.FT}]: {asset.N} is broken! Index CRC: {asset.CRC} <--> File CRC: {HexTool.BytesToHexUnsafe(localCrc)}", LogType.Warning, true);
        }

        private void AddIndex(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex)
        {
            // Update the total progress and found counter
            ProgressAllSizeFound += asset.S;
            ProgressAllCountFound++;

            // Set the per size progress
            ProgressPerFileSizeCurrent = asset.S;

            // Increment the total current progress
            ProgressAllSizeCurrent += asset.S;

            var prop = new AssetProperty<RepairAssetType>(Path.GetFileName(asset.N),
                                                          ConvertRepairAssetTypeEnum(asset.FT),
                                                          Path.GetDirectoryName(asset.N),
                                                          asset.S,
                                                          null,
                                                          null);

            Dispatch(() => AssetEntry.Add(prop));
            asset.AssociatedAssetProperty = prop;
            targetAssetIndex.Add(asset);
        }

        private void AddUnusedHashMarkFile(string                     filePath,
                                           string                     gamePath,
                                           FilePropertiesRemote       asset,
                                           List<FilePropertiesRemote> brokenFileList)
        {
            if (asset.CRCArray?.Length == 0 ||
                (!asset.IsHasHashMark && asset.FT != FileType.Unused))
            {
                return;
            }

            string dir           = Path.GetDirectoryName(filePath)!;
            string fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);

            if (!Directory.Exists(dir))
            {
                return;
            }

            foreach (string markFile in Directory.EnumerateFiles(dir, $"{fileNameNoExt}_*",
                                                                 SearchOption.TopDirectoryOnly))
            {
                ReadOnlySpan<char> markFilename = Path.GetFileName(markFile);
                ReadOnlySpan<char> hashSpan = ConverterTool.GetSplit(markFilename, ^2, "_.");
                if (!HexTool.IsHexString(hashSpan))
                {
                    continue;
                }

                if (!asset.CRC?.Equals(hashSpan, StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    AddAssetInner(markFile);
                }

                // Add equal hash mark if the file is marked as unused.
                if (asset.FT != FileType.Unused)
                {
                    continue;
                }

                AddAssetInner(markFile);
            }

            return;

            void AddAssetInner(string thisFilePath)
            {
                string relPath = thisFilePath[gamePath.Length..].Trim('\\');
                AddIndex(new FilePropertiesRemote
                {
                    FT = FileType.Unused,
                    N  = relPath
                }, brokenFileList);
            }
        }
        #endregion
    }
}
