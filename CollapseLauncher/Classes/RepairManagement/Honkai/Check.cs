using Force.Crc32;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    internal partial class HonkaiRepair
    {
        private async Task Check(List<FilePropertiesRemote> assetIndex)
        {
            List<FilePropertiesRemote> brokenAssetIndex = new List<FilePropertiesRemote>();

            // Set Indetermined status as false
            _status.IsProgressTotalIndetermined = false;
            _status.IsProgressPerFileIndetermined = false;

            // Show the asset entry panel
            _status.IsAssetEntryPanelShow = true;

            // Reset stopwatch
            RestartStopwatch();

            // Find unused assets
            CheckUnusedAsset(assetIndex, brokenAssetIndex);

            // Await the task for parallel processing
            await Task.Run(() =>
            {
                try
                {
                    // Iterate assetIndex and check it using different method for each type and run it in parallel
                    Parallel.ForEach(assetIndex, new ParallelOptions { MaxDegreeOfParallelism = _threadCount }, (asset) =>
                    {
                        // Assign a task depends on the asset type
                        switch (asset.FT)
                        {
                            case FileType.Blocks:
                                CheckAssetTypeBlocks(asset, brokenAssetIndex);
                                break;
                            case FileType.Audio:
                                CheckAssetTypeAudio(asset, brokenAssetIndex);
                                break;
                            default:
                                CheckAssetTypeGeneric(asset, brokenAssetIndex);
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

        #region AudioCheck
        private void CheckAssetTypeAudio(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex)
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
            localCRC = CheckMD5(file.OpenRead());

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
                        RepairAssetType.Audio,
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
        private void CheckAssetTypeGeneric(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex)
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
            byte[] localCRC = CheckCRC(file.OpenRead());

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

                LogWriteLine($"File [T: {asset.FT}]: {asset.N} is broken! Index CRC: {asset.CRC} <--> File CRC: {localCRC}", LogType.Warning, true);
            }
        }
        #endregion

        #region BlocksCheck
        private void CheckAssetTypeBlocks(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex)
        {
            // Increment current total and size for the XMF
            _progressTotalSizeCurrent += asset.S;

            // Initialize blocks for return into target asset index
            FilePropertiesRemote blocks = new FilePropertiesRemote()
            {
                S = asset.S,
                N = asset.N,
                CRC = asset.CRC,
                FT = asset.FT,
                M = asset.M,
                RN = asset.RN,
                BlkC = new List<XMFBlockList>()
            };

            // Iterate blocks and check it
            try
            {
                Parallel.ForEach(asset.BlkC, new ParallelOptions { MaxDegreeOfParallelism = _threadCount }, (block) =>
                {
                    CheckBlockCRC(block, blocks.BlkC);
                });
            }
            catch (AggregateException ex)
            {
                throw ex.Flatten().InnerExceptions.First();
            }

            // If the block content is not blank, then add it to target asset index
            if (blocks.BlkC.Count != 0) targetAssetIndex.Add(blocks);
        }

        private void CheckBlockCRC(XMFBlockList sourceBlock, List<XMFBlockList> targetBlockList)
        {
            // Update activity status
            _status.ActivityStatus = string.Format(Lang._GameRepairPage.Status5, sourceBlock.BlockHash);

            // Get block file path
            string blockDirPath = "BH3_Data\\StreamingAssets\\Asb\\pc";
            string blockPath = Path.Combine(_gamePath, blockDirPath, sourceBlock.BlockHash + ".wmv");
            FileInfo file = new FileInfo(blockPath);

            // If file doesn't exist or the length is invalid, then register it.
            if (!file.Exists || file.Length != sourceBlock.BlockSize)
            {
                _progressTotalSizeFound += sourceBlock.BlockSize;
                _progressTotalCountFound++;

                _progressTotalCountCurrent += sourceBlock.BlockContent.Count;
                sourceBlock.BlockMissing = true;
                targetBlockList.Add(sourceBlock);

                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        sourceBlock.BlockHash + ".wmv",
                        RepairAssetType.Block,
                        blockDirPath,
                        sourceBlock.BlockSize,
                        null,
                        null
                    )
                ));

                LogWriteLine($"Block [T: {RepairAssetType.Block}]: {sourceBlock.BlockHash} is not found or has unmatched size", LogType.Warning, true);
                return;
            }

            _progressPerFileSizeCurrent = 0;
            _progressPerFileSize = sourceBlock.BlockSize;

            using (Stream fs = file.OpenRead())
            {
                // Initialize new block for assigning temporary list
                XMFBlockList block = new XMFBlockList()
                {
                    BlockHash = sourceBlock.BlockHash,
                    BlockExistingSize = sourceBlock.BlockExistingSize,
                    BlockSize = sourceBlock.BlockSize,
                    BlockMissing = false,
                    BlockUnused = false,
                    BlockContent = new List<XMFFileProperty>()
                };

                foreach (var chunk in sourceBlock.BlockContent)
                {
                    // Increment the count
                    _progressTotalCountCurrent++;

                    // Throw if cancellation was given
                    _token.Token.ThrowIfCancellationRequested();

                    byte[] localCRC;

                    if (chunk._filesize < _bufferBigLength)
                    {
                        // Check the CRC of the chunk buffer using stack
                        localCRC = TryCheckCRCFromStackalloc(fs, (int)chunk._filesize);
                    }
                    else
                    {
                        // Initialize buffer and put the chunk into the buffer
                        Span<byte> buffer = new byte[(int)chunk._filesize];

                        // Read from filesystem
                        fs.Read(buffer);

                        // Check the CRC of the chunk buffer
                        localCRC = CheckCRCThreadChild(buffer);
                    }

                    // If the chunk is unmatch, then add the chunk into temporary block list
                    if (!IsArrayMatch(localCRC, chunk._filecrc32array))
                    {
                        _progressTotalSizeFound += chunk._filesize;
                        _progressTotalCountFound++;

                        block.BlockContent.Add(chunk);

                        Dispatch(() => AssetEntry.Add(
                            new AssetProperty<RepairAssetType>(
                                $"*{chunk._startoffset:x8} -> {chunk._startoffset + chunk._filesize:x8}",
                                RepairAssetType.Chunk,
                                sourceBlock.BlockHash,
                                chunk._filesize,
                                localCRC,
                                chunk._filecrc32array
                                )
                            ));

                        LogWriteLine($"Chunk [T: {RepairAssetType.Chunk}]: *{chunk._startoffset:x8} -> {chunk._startoffset + chunk._filesize:x8} is broken! Index CRC: {chunk._filecrc32} <--> File CRC: {localCRC}", LogType.Warning, true);
                    }
                }

                // If the broken chunk was not 0, then add the temporary block to target block list.
                if (block.BlockContent.Count != 0) targetBlockList.Add(block);
            }
        }

        #endregion

        #region UnusedAssetCheck
        private void CheckUnusedAsset(List<FilePropertiesRemote> assetIndex, List<FilePropertiesRemote> targetAssetIndex)
        {
            List<string> catalog = new List<string>();
            BuildAssetIndexCatalog(catalog, assetIndex);
            BuildVideoIndexCatalog(catalog);
            BuildAudioIndexCatalog(catalog);

            GetUnusedAssetIndexList(catalog, targetAssetIndex);
        }

        private void BuildAssetIndexCatalog(List<string> catalog, List<FilePropertiesRemote> assetIndex)
        {
            foreach (FilePropertiesRemote asset in assetIndex)
            {
                string path = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.N));
                switch (asset.FT)
                {
                    case FileType.Blocks:
                        catalog.Add(Path.Combine(path, "blockVerifiedVersion.txt"));
                        foreach (XMFBlockList block in asset.BlkC)
                        {
                            string blockPath = Path.Combine(path, block.BlockHash + ".wmv");
                            catalog.Add(blockPath);
                        }
                        break;
                    case FileType.Audio:
                    case FileType.Generic:
                        catalog.Add(path);
                        break;

                }
            }
        }

        private void BuildVideoIndexCatalog(List<string> catalog)
        {
            foreach (string video in Directory.EnumerateFiles(Path.Combine(_gamePath, @"BH3_Data\StreamingAssets\Video"), "*", SearchOption.AllDirectories))
            {
                bool isUSM = video.EndsWith(".usm", StringComparison.OrdinalIgnoreCase);
                bool isVersion = video.EndsWith("Version.txt", StringComparison.OrdinalIgnoreCase);
                bool isIncluded = catalog.Contains(video);
                if (isUSM || isVersion || isIncluded)
                {
                    catalog.Add(video);
                }
            }
        }

        private void BuildAudioIndexCatalog(List<string> catalog)
        {
            foreach (string audio in Directory.EnumerateFiles(Path.Combine(_gamePath, @"BH3_Data\StreamingAssets\Audio\GeneratedSoundBanks\Windows"), "*", SearchOption.AllDirectories))
            {
                if ((audio.EndsWith(".pck", StringComparison.OrdinalIgnoreCase)
                  || audio.EndsWith("manifest.m", StringComparison.OrdinalIgnoreCase))
                  && !catalog.Contains(audio))
                {
                    catalog.Add(audio);
                }
            }
        }

        private void GetUnusedAssetIndexList(List<string> catalog, List<FilePropertiesRemote> targetAssetIndex)
        {
            int pathOffset = _gamePath.Length + 1;
            foreach (string asset in Directory.EnumerateFiles(Path.Combine(_gamePath), "*", SearchOption.AllDirectories))
            {
                bool isIncluded = catalog.Contains(asset);
                bool isini = asset.EndsWith(".ini", StringComparison.OrdinalIgnoreCase);
                bool isXMFBlocks = asset.EndsWith($"Blocks_{_gameVersion.Major}_{_gameVersion.Minor}.xmf", StringComparison.OrdinalIgnoreCase);
                bool isXMFMeta = asset.EndsWith("BlockMeta.xmf", StringComparison.OrdinalIgnoreCase);
                bool isVersion = asset.EndsWith("Version.txt", StringComparison.OrdinalIgnoreCase);
                bool isScreenshot = asset.Contains("ScreenShot", StringComparison.OrdinalIgnoreCase);
                bool isWebcaches = asset.Contains("webCaches", StringComparison.OrdinalIgnoreCase);
                bool isSDKcaches = asset.Contains("SDKCaches", StringComparison.OrdinalIgnoreCase);

                if (!isIncluded && !isini && !isXMFBlocks && !isXMFMeta && !isVersion && !isScreenshot && !isWebcaches && !isSDKcaches)
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

                    _progressTotalSizeFound += f.Length;
                    _progressTotalCountFound++;

                    LogWriteLine($"Unused file has been found: {n}", LogType.Warning, true);
                }
            }
        }
        #endregion

        #region Tools
        private bool IsArrayMatch(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target) => source.SequenceEqual(target);

        private byte[] TryCheckCRCFromStackalloc(Stream fs, int bufferSize)
        {
            // Initialize buffer and put the chunk into the buffer using stack
            Span<byte> bufferStackalloc = stackalloc byte[bufferSize];

            // Read from filesystem
            fs.Read(bufferStackalloc);

            // Check the CRC of the chunk buffer
            return CheckCRCThreadChild(bufferStackalloc);
        }

        private byte[] CheckCRCThreadChild(ReadOnlySpan<byte> buffer)
        {
            lock (this)
            {
                // Increment total size counter
                _progressTotalSizeCurrent += buffer.Length;
                // Increment per file size counter
                _progressPerFileSizeCurrent += buffer.Length;
            }

            // Update status and progress for CRC calculation
            UpdateProgressCRC();

            // Return computed hash byte
            Crc32Algorithm _crcInstance = new Crc32Algorithm();
            lock (_crcInstance)
            {
                return _crcInstance.ComputeHashByte(buffer);
            }
        }

        private byte[] CheckCRC(Stream stream)
        {
            // Reset CRC instance and assign buffer
            Crc32Algorithm _crcInstance = new Crc32Algorithm();
            Span<byte> buffer = stackalloc byte[_bufferBigLength];

            using (stream)
            {
                int read;
                while ((read = stream.Read(buffer)) > 0)
                {
                    _token.Token.ThrowIfCancellationRequested();
                    _crcInstance.Append(buffer.Slice(0, read));

                    lock (this)
                    {
                        // Increment total size counter
                        _progressTotalSizeCurrent += read;
                        // Increment per file size counter
                        _progressPerFileSizeCurrent += read;
                    }

                    // Update status and progress for CRC calculation
                    UpdateProgressCRC();
                }
            }

            // Return computed hash byte
            return _crcInstance.Hash;
        }

        private byte[] CheckMD5(Stream stream)
        {
            // Initialize MD5 instance and assign buffer
            MD5 md5Instance = MD5.Create();
            byte[] buffer = new byte[_bufferBigLength];

            using (stream)
            {
                int read;
                while ((read = stream.Read(buffer)) >= _bufferBigLength)
                {
                    _token.Token.ThrowIfCancellationRequested();
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

                // Calculate current speed and update the status and progress speed
                _progress.ProgressTotalSpeed = (long)(_progressTotalSizeCurrent / _stopwatch.Elapsed.TotalSeconds);
                _status.ActivityPerFile = string.Format(Lang._Misc.Speed, ConverterTool.SummarizeSizeSimple(_progress.ProgressTotalSpeed));

                // Update current activity status
                _status.ActivityTotal = string.Format(Lang._GameRepairPage.PerProgressSubtitle2, _progressTotalCountCurrent, _progressTotalCount);

                // Trigger update
                UpdateAll();
            }
        }
        #endregion
    }
}
