using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    internal partial class ZenlessRepair
    {
        private async Task Check(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            List<FilePropertiesRemote> brokenAssetIndex = new List<FilePropertiesRemote>();

            // Set Indetermined status as false
            _status.IsProgressAllIndetermined = false;
            _status.IsProgressPerFileIndetermined = false;

            // Show the asset entry panel
            _status.IsAssetEntryPanelShow = true;

            // Await the task for parallel processing
            try
            {
                // Iterate assetIndex and check it using different method for each type and run it in parallel
                await Parallel.ForEachAsync(assetIndex, new ParallelOptions { MaxDegreeOfParallelism = _threadCount, CancellationToken = token }, async (asset, threadToken) =>
                {
                    // Assign a task depends on the asset type
                    switch (asset.FT)
                    {
                        case FileType.Generic:
                        case FileType.Block:
                        case FileType.Audio:
                        case FileType.Video:
                            await CheckGenericAssetType(asset, brokenAssetIndex, threadToken);
                            break;
                    }
                });
            }
            catch (AggregateException ex)
            {
                throw ex.Flatten().InnerExceptions.First();
            }

            // Re-add the asset index with a broken asset index
            assetIndex.Clear();
            assetIndex.AddRange(brokenAssetIndex);
        }

        private async ValueTask CheckGenericAssetType(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
        {
            // Update activity status
            _status.ActivityStatus = string.Format(Locale.Lang._GameRepairPage.Status6, StarRailRepairExtension.GetFileRelativePath(asset.N, _gamePath));

            // Increment current total count
            _progressAllCountCurrent++;

            // Reset per file size counter
            _progressPerFileSizeTotal = asset.S;
            _progressPerFileSizeCurrent = 0;

            // Get the file info
            FileInfo fileInfo = new FileInfo(asset.N);

            // Check if the file exist
            if (!fileInfo.Exists)
            {
                AddIndex();
                Logger.LogWriteLine($"File [T: {asset.FT}]: {asset.N} is not found", LogType.Warning, true);
                return;
            }

            if (fileInfo.Length != asset.S)
            {
                if (fileInfo.Name.Contains("pkg_version")) return;
                AddIndex();
                Logger.LogWriteLine($"File [T: {asset.FT}]: {asset.N} has unmatched size " +
                             $"(Local: {fileInfo.Length} <=> Remote: {asset.S}",
                             LogType.Warning, true);
                return;
            }

            // Skip CRC check if fast method is used
            if (_useFastMethod)
            {
                return;
            }

            // Open and read fileInfo as FileStream 
            await using FileStream filefs = await NaivelyOpenFileStreamAsync(fileInfo, FileMode.Open, FileAccess.Read, FileShare.Read);
            // If pass the check above, then do CRC calculation
            // Additional: the total file size progress is disabled and will be incremented after this
            byte[] localCRC = asset.CRCArray.Length > 8 ? await CheckHashAsync(filefs, MD5.Create(), token) : await CheckNonCryptoHashAsync(filefs, new XxHash64(), token);

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

                Logger.LogWriteLine($"File [T: {asset.FT}]: {asset.N} is broken! Index CRC: {asset.CRC} <--> File CRC: {HexTool.BytesToHexUnsafe(localCRC)}", LogType.Warning, true);
            }

            return;

            void AddIndex()
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
            }
        }

        private RepairAssetType ConvertRepairAssetTypeEnum(FileType assetType) => assetType switch
        {
            FileType.Block => RepairAssetType.Block,
            FileType.Audio => RepairAssetType.Audio,
            FileType.Video => RepairAssetType.Video,
            _ => RepairAssetType.Generic
        };
    }
}
