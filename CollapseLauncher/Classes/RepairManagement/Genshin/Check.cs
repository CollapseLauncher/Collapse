using Hi3Helper;
using Hi3Helper.Preset;
using Hi3Helper.Data;
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
    internal partial class GenshinRepair
    {
        private async Task Check(List<PkgVersionProperties> assetIndex, CancellationToken token)
        {
            List<PkgVersionProperties> brokenAssetIndex = new List<PkgVersionProperties>();

            // Set Indetermined status as false
            _status.IsProgressTotalIndetermined = false;
            _status.IsProgressPerFileIndetermined = false;

            // Show the asset entry panel
            _status.IsAssetEntryPanelShow = true;

            // Reset stopwatch
            RestartStopwatch();

            // Check for any redundant files
            CheckRedundantFiles(brokenAssetIndex);

            // Await the task for parallel processing
            await Task.Run(() =>
            {
                try
                {
                    // Await the task for parallel processing
                    // and iterate assetIndex and check it using different method for each type and run it in parallel
                    Parallel.ForEach(assetIndex, new ParallelOptions { MaxDegreeOfParallelism = _threadCount }, (asset) =>
                    {
                        CheckAssetAllType(asset, brokenAssetIndex, token);
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

        private void CheckAssetAllType(PkgVersionProperties asset, List<PkgVersionProperties> targetAssetIndex, CancellationToken token)
        {
            // Update activity status
            _status.ActivityStatus = string.Format(Lang._GameRepairPage.Status6, asset.remoteName);

            // Increment current total count
            _progressTotalCountCurrent++;

            // Reset per file size counter
            _progressPerFileSize = asset.fileSize;
            _progressPerFileSizeCurrent = 0;

            // Get file path
            string filePath = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.remoteName));
            FileInfo file = new FileInfo(filePath);

            // If file doesn't exist or the file size doesn't match, then skip and update the progress
            if (!file.Exists || (file.Exists && file.Length != asset.fileSize))
            {
                _progressTotalSizeCurrent += asset.fileSize;
                _progressTotalSizeFound += asset.fileSize;
                _progressTotalCountFound++;

                _progressPerFileSizeCurrent = asset.fileSize;

                asset.type = RepairAssetType.General.ToString();

                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(asset.remoteName),
                        RepairAssetType.General,
                        Path.GetDirectoryName(asset.remoteName),
                        asset.fileSize,
                        null,
                        HexTool.HexToBytesUnsafe(asset.md5)
                    )
                ));

                // Add asset for missing/unmatched size file
                targetAssetIndex.Add(asset);

                LogWriteLine($"File [T: {asset.type}]: {asset.remoteName} is not found or has unmatched size", LogType.Warning, true);
                return;
            }

            // If pass the check above, then do CRC calculation
            byte[] localCRC = CheckMD5(file.OpenRead(), token);

            // If local and asset CRC doesn't match, then add the asset
            byte[] remoteCRC = HexTool.HexToBytesUnsafe(asset.md5);
            if (!IsArrayMatch(localCRC, remoteCRC))
            {
                _progressTotalSizeFound += asset.fileSize;
                _progressTotalCountFound++;

                asset.type = RepairAssetType.General.ToString();

                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(asset.remoteName),
                        RepairAssetType.General,
                        Path.GetDirectoryName(asset.remoteName),
                        asset.fileSize,
                        localCRC,
                        remoteCRC
                    )
                ));

                // Add asset for unmatched CRC
                targetAssetIndex.Add(asset);

                LogWriteLine($"File [T: {asset.type}]: {asset.remoteName} is broken! Index CRC: {asset.md5} <--> File CRC: {HexTool.BytesToHexUnsafe(localCRC)}", LogType.Warning, true);
            }
        }

        #region UnusedFiles
        private void CheckRedundantFiles(List<PkgVersionProperties> targetAssetIndex)
        {
            // Initialize FilePath and FileInfo
            string FilePath;
            FileInfo fInfo;

            // Iterate the available deletefiles files
            foreach (string listFile in Directory.EnumerateFiles(_gamePath, "*deletefiles*", SearchOption.TopDirectoryOnly))
            {
                LogWriteLine($"deletefiles file list path: {listFile}", LogType.Default, true);

                // Use deletefiles files to get the list of the redundant file
                using (Stream fs = new FileStream(listFile, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.DeleteOnClose))
                using (StreamReader listReader = new StreamReader(fs))
                {
                    while (!listReader.EndOfStream)
                    {
                        // Get the File name and FileInfo
                        FilePath = Path.Combine(_gamePath, ConverterTool.NormalizePath(listReader.ReadLine()));
                        fInfo = new FileInfo(FilePath);

                        // If the file exist, then add to targetAssetIndex
                        if (fInfo.Exists)
                        {
                            // Update total found progress
                            _progressTotalCountFound++;

                            // Get the stripped relative name
                            string strippedName = fInfo.FullName.AsSpan().Slice(_gamePath.Length + 1).ToString();

                            // Assign the asset before adding to targetAssetIndex
                            PkgVersionProperties asset = new PkgVersionProperties
                            {
                                localName = strippedName,
                                fileSize = fInfo.Length,
                                type = RepairAssetType.Unused.ToString()
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
                }
            }

            // Iterate redundant diff and temporary files
            foreach (string _Entry in Directory.EnumerateFiles(_gamePath, "*.*", SearchOption.AllDirectories)
                                               .Where(x => x.EndsWith(".diff", StringComparison.OrdinalIgnoreCase)
                                                        || x.EndsWith("_tmp", StringComparison.OrdinalIgnoreCase)
                                                        || x.EndsWith(".hdiff", StringComparison.OrdinalIgnoreCase)))
            {
                // Assign the FileInfo
                fInfo = new FileInfo(_Entry);

                // Update total found progress
                _progressTotalCountFound++;

                // Get the stripped relative name
                string strippedName = fInfo.FullName.AsSpan().Slice(_gamePath.Length + 1).ToString();

                // Assign the asset before adding to targetAssetIndex
                PkgVersionProperties asset = new PkgVersionProperties
                {
                    localName = strippedName,
                    fileSize = fInfo.Length,
                    type = RepairAssetType.Unused.ToString()
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

        #region Tools
        private bool IsArrayMatch(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target) => source.SequenceEqual(target);

        private byte[] CheckMD5(Stream stream, CancellationToken token)
        {
            // Initialize MD5 instance and assign buffer
            MD5 md5Instance = MD5.Create();
            byte[] buffer = new byte[_bufferBigLength];

            using (stream)
            {
                int read;
                while ((read = stream.Read(buffer)) >= _bufferBigLength)
                {
                    token.ThrowIfCancellationRequested();
                    // Append buffer into hash block
                    md5Instance.TransformBlock(buffer, 0, buffer.Length, buffer, 0);

                    lock (this)
                    {
                        // Increment total size counter
                        _progressTotalSizeCurrent += read;
                        // Increment per file size counter
                        _progressPerFileSizeCurrent += read;
                    }

                    // Update status and progress for MD5 calculation
                    UpdateProgressCRC();
                }

                // Finalize the hash calculation
                md5Instance.TransformFinalBlock(buffer, 0, read);
            }

            // Return computed hash byte
            return md5Instance.Hash;
        }

        private void TryUnassignReadOnlyFiles()
        {
            // Iterate every files and set the read-only flag to false
            foreach (string file in Directory.EnumerateFiles(_gamePath, "*", SearchOption.AllDirectories))
            {
                FileInfo fileInfo = new FileInfo(file);
                if (fileInfo.IsReadOnly)
                    fileInfo.IsReadOnly = false;
            }
        }

        private async void UpdateProgressCRC()
        {
            if (await CheckIfNeedRefreshStopwatch())
            {
                // Update current progress percentages
                _progress.ProgressPerFilePercentage = _progressPerFileSizeCurrent != 0 ?
                    ConverterTool.GetPercentageNumber(_progressPerFileSizeCurrent, _progressPerFileSize) :
                    0;
                _progress.ProgressTotalPercentage = _progressTotalSizeCurrent != 0 ?
                    ConverterTool.GetPercentageNumber(_progressTotalSizeCurrent, _progressTotalSize) :
                    0;

                // Calculate speed
                long speed = (long)(_progressTotalSizeCurrent / _stopwatch.Elapsed.TotalSeconds);

                // Calculate current speed and update the status and progress speed
                _progress.ProgressTotalSpeed = (long)(_progressTotalSizeCurrent / _stopwatch.Elapsed.TotalSeconds);
                _status.ActivityPerFile = string.Format(Lang._Misc.Speed, ConverterTool.SummarizeSizeSimple(_progress.ProgressTotalSpeed));

                // Set time estimation string
                string timeLeftString = string.Format(Lang._Misc.TimeRemainHMSFormat, TimeSpan.FromSeconds((_progressTotalSizeCurrent - _progressTotalSize) / ConverterTool.Unzeroed(speed)));

                // Update current activity status
                _status.ActivityTotal = string.Format(Lang._GameRepairPage.PerProgressSubtitle2, _progressTotalCountCurrent, _progressTotalCount) + $" | {timeLeftString}";

                // Trigger update
                UpdateAll();
            }
        }
        #endregion
    }
}
