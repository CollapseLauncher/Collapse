using CollapseLauncher.GameVersioning;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Preset;
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

            // Try move persistent files to StreamingAssets
            if (_isParsePersistentManifestSuccess) TryMovePersistentToStreamingAssets(assetIndex);

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

        private void TryMovePersistentToStreamingAssets(IEnumerable<PkgVersionProperties> assetIndex)
        {
            if (!Directory.Exists(_gamePersistentPath)) return;
            TryMoveAudioPersistent(assetIndex);
            TryMoveVideoPersistent();
        }

        private void TryMoveAudioPersistent(IEnumerable<PkgVersionProperties> assetIndex)
        {
            // Try get the exclusion list of the audio (language specific) files
            string[] exclusionList = assetIndex
                .Where(x => x.isForceStoreInPersistent && x.remoteName
                    .AsSpan()
                    .EndsWith(".pck"))
                .Select(x => x.remoteNamePersistent
                    .Replace('/', '\\'))
                .ToArray();

            // Get the audio directory paths and create if doesn't exist
            string audioAsbPath = Path.Combine(_gameStreamingAssetsPath, "AudioAssets");
            string audioPersistentPath = Path.Combine(_gamePersistentPath, "AudioAssets");
            if (!Directory.Exists(audioPersistentPath)) return;
            if (!Directory.Exists(audioAsbPath)) Directory.CreateDirectory(audioAsbPath);

            // Get the list of audio language names from _gameVersionManager
            List<string> audioLangList = ((GameTypeGenshinVersion)_gameVersionManager)._audioVoiceLanguageList;

            // Enumerate the content of audio persistent directory
            foreach (string path in Directory.EnumerateDirectories(audioPersistentPath, "*", SearchOption.TopDirectoryOnly))
            {
                // Get the last path section as language name to compare
                string langName = Path.GetFileName(path);

                // If the path section matches the name in language list, then continue
                if (audioLangList.Contains(langName))
                {
                    // Enumerate the files that's being exist in the persistent path of each language
                    // except the one that's included in the exclusion list
                    foreach (string filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                        .Where(x => !exclusionList.Any(y => x.AsSpan().EndsWith(y.AsSpan()))))
                    {
                        // Trim the path name to get the generic languageName/filename form
                        string pathName = filePath.AsSpan().Slice(audioPersistentPath.Length + 1).ToString();
                        // Combine the generic name with audioAsbPath
                        string newPath = Path.Combine(audioAsbPath, pathName);
                        string newPathDir = Path.GetDirectoryName(newPath);

                        // Try move the file to the asb path
                        if (!Directory.Exists(newPathDir)) Directory.CreateDirectory(newPathDir);
                        File.Move(filePath, newPath);
                    }
                }
            }
        }

        private void TryMoveVideoPersistent()
        {
            string videoAsbPath = Path.Combine(_gameStreamingAssetsPath, "VideoAssets");
            string videoPersistentPath = Path.Combine(_gamePersistentPath, "VideoAssets");
            if (!Directory.Exists(videoPersistentPath)) return;
            if (!Directory.Exists(videoAsbPath)) Directory.CreateDirectory(videoAsbPath);
            MoveFolderContent(videoPersistentPath, videoAsbPath);
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

            // Get persistent and streaming paths
            FileInfo fileInfoPersistent = asset.remoteNamePersistent == null ? null : new FileInfo(Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.remoteNamePersistent)));
            FileInfo fileInfoStreaming = new FileInfo(filePath);

            bool UsePersistent = (asset.isForceStoreInPersistent && !asset.isForceStoreInStreaming && fileInfoPersistent != null && !fileInfoPersistent.Exists) || asset.isPatch || (!fileInfoStreaming.Exists && !asset.isForceStoreInStreaming);
            bool IsPersistentExist = fileInfoPersistent != null && fileInfoPersistent.Exists && fileInfoPersistent.Length == asset.fileSize;
            bool IsStreamingExist = fileInfoStreaming.Exists && fileInfoStreaming.Length == asset.fileSize;

            // Update the local path to full persistent or streaming path and add asset for missing/unmatched size file
            asset.remoteName = UsePersistent ? asset.remoteNamePersistent : asset.remoteName;

            // Check if the file exist on both persistent and streaming path, then mark the
            // streaming path as redundant (unused)
            if (IsPersistentExist && IsStreamingExist && !asset.isPatch)
            {
                // Add the count and asset. Mark the type as "RepairAssetType.Unused"
                _progressTotalCountFound++;

                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(fileInfoStreaming.FullName),
                        RepairAssetType.Unused,
                        Path.GetDirectoryName(fileInfoStreaming.FullName),
                        asset.fileSize,
                        null,
                        null
                    )
                ));

                asset.type = "Unused";
                asset.localName = fileInfoStreaming.FullName;
                targetAssetIndex.Add(asset);

                LogWriteLine($"File [T: {asset.type}]: {fileInfoStreaming.FullName} is redundant (exist both in persistent and streaming)", LogType.Warning, true);
            }

            // Check if the persistent or streaming file doesn't exist
            if ((UsePersistent && !IsPersistentExist) || (!IsStreamingExist && !IsPersistentExist))
            {
                // Update the total progress and found counter
                _progressTotalSizeFound += asset.fileSize;
                _progressTotalCountFound++;

                // Set the per size progress
                _progressPerFileSizeCurrent = asset.fileSize;

                // Increment the total current progress
                _progressTotalSizeCurrent += asset.fileSize;

                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(UsePersistent ? asset.remoteNamePersistent : asset.remoteName),
                        RepairAssetType.General,
                        Path.GetDirectoryName(UsePersistent ? asset.remoteNamePersistent : asset.remoteName),
                        asset.fileSize,
                        null,
                        HexTool.HexToBytesUnsafe(asset.md5)
                    )
                ));
                targetAssetIndex.Add(asset);

                string remoteName = UsePersistent ? asset.remoteNamePersistent : asset.remoteName;
                LogWriteLine($"File [T: {RepairAssetType.General}]: {(string.IsNullOrEmpty(remoteName) ? asset.localName : remoteName)} is not found or has unmatched size", LogType.Warning, true);
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
                byte[] remoteCRC = HexTool.HexToBytesUnsafe(asset.md5);
                if (!IsArrayMatch(localCRC, remoteCRC))
                {
                    // Update the total progress and found counter
                    _progressTotalSizeFound += asset.fileSize;
                    _progressTotalCountFound++;

                    // Set the per size progress
                    _progressPerFileSizeCurrent = asset.fileSize;

                    // Increment the total current progress
                    _progressTotalSizeCurrent += asset.fileSize;

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
                    targetAssetIndex.Add(asset);

                    LogWriteLine($"File [T: {asset.type}]: {filefs.Name} is broken! Index CRC: {asset.md5} <--> File CRC: {HexTool.BytesToHexUnsafe(localCRC)}", LogType.Warning, true);
                }
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
    }
}
