using Force.Crc32;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static Hi3Helper.Locale;

namespace CollapseLauncher
{
    internal partial class HonkaiRepair
    {
        private Crc32Algorithm _crcInstance = new Crc32Algorithm();
        private async Task<List<FilePropertiesRemote>> Check(List<FilePropertiesRemote> assetIndex)
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

            // Iterate assets and check it using different method for each type
            foreach (FilePropertiesRemote asset in assetIndex)
            {
                // Assign a task depends on the asset type
                ConfiguredTaskAwaitable assetTask = (asset.FT switch
                {
                    FileType.Blocks => CheckAssetTypeBlocks(asset, brokenAssetIndex),
                    _ => CheckAssetTypeGenericAudio(asset, brokenAssetIndex)
                }).ConfigureAwait(false);

                // Await the task
                await assetTask;
            }

            return brokenAssetIndex;
        }

        #region GenericAudioCheck
        private async Task CheckAssetTypeGenericAudio(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex)
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
                        asset.FT == FileType.Audio ? RepairAssetType.Audio : RepairAssetType.General,
                        Path.GetDirectoryName(asset.N),
                        asset.S,
                        null,
                        asset.CRCArray
                    )
                ));

                // Add asset for missing/unmatched size file
                targetAssetIndex.Add(asset);
                return;
            }

            // If pass the check above, then do CRC calculation
            byte[] localCRC = await Task.Run(() => CheckCRC(file.OpenRead()));

            // If local and asset CRC doesn't match, then add the asset
            if (!IsCRCArrayMatch(localCRC, asset.CRCArray))
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
            }
        }
        #endregion

        #region BlocksCheck
        private async Task CheckAssetTypeBlocks(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex)
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
            await Task.Run(() =>
            {
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
            });

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
                        localCRC = CheckCRC(buffer);
                    }

                    // If the chunk is unmatch, then add the chunk into temporary block list
                    if (!IsCRCArrayMatch(localCRC, chunk._filecrc32array))
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
            List<string> Assets = new List<string>();
            BuildAssetIndexList(Assets, assetIndex);
            BuildVideoIndexList(Assets);
            BuildAudioIndexList(Assets);
            BuildUnusedAssetIndexList(Assets, targetAssetIndex);
        }

        private void BuildAssetIndexList(List<string> assets, List<FilePropertiesRemote> assetIndex)
        {
            foreach (FilePropertiesRemote asset in assetIndex)
            {
                string path = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.N));
                if (asset.FT == FileType.Blocks)
                {
                    assets.Add(Path.Combine(path, "blockVerifiedVersion.txt"));
                    foreach (XMFBlockList block in asset.BlkC)
                    {
                        string blockPath = Path.Combine(path, block.BlockHash + ".wmv");
                        assets.Add(blockPath);
                    }
                }
                else
                {
                    assets.Add(path);
                }
            }
        }

        private void BuildVideoIndexList(List<string> assets)
        {
            foreach (string video in Directory.EnumerateFiles(Path.Combine(_gamePath, @"BH3_Data\StreamingAssets\Video"), "*", SearchOption.AllDirectories))
            {
                if ((video.EndsWith(".usm", StringComparison.OrdinalIgnoreCase))
                 && !assets.Contains(video)) assets.Add(video);
            }
        }

        private void BuildAudioIndexList(List<string> assets)
        {
            foreach (string audio in Directory.EnumerateFiles(Path.Combine(_gamePath, @"BH3_Data\StreamingAssets\Audio\GeneratedSoundBanks\Windows"), "*", SearchOption.AllDirectories))
            {
                if ((audio.EndsWith(".pck", StringComparison.OrdinalIgnoreCase)
                  || audio.EndsWith("manifest.m", StringComparison.OrdinalIgnoreCase))
                  && !assets.Contains(audio)) assets.Add(audio);
            }
        }

        private void BuildUnusedAssetIndexList(List<string> assets, List<FilePropertiesRemote> targetAssetIndex)
        {
            int pathOffset = _gamePath.Length + 1;
            foreach (string asset in Directory.EnumerateFiles(Path.Combine(_gamePath), "*", SearchOption.AllDirectories))
            {
                if (!assets.Contains(asset)
                 && !asset.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)
                 && !asset.EndsWith($"Blocks_{_gameVersion.Major}_{_gameVersion.Minor}.xmf", StringComparison.OrdinalIgnoreCase)
                 && !asset.EndsWith("BlockMeta.xmf", StringComparison.OrdinalIgnoreCase)
                 && !asset.EndsWith("Version.txt", StringComparison.OrdinalIgnoreCase)
                 && !asset.Contains("ScreenShot")
                 && !asset.Contains("webCaches")
                 && !asset.Contains("SDKCaches"))
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
                }
            }
        }
        #endregion

        #region Tools
        private bool IsCRCArrayMatch(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target) => source[0] == target[0] && source[1] == target[1]
                                                                                           && source[2] == target[2] && source[3] == target[3];

        private byte[] TryCheckCRCFromStackalloc(Stream fs, int bufferSize)
        {
            // Initialize buffer and put the chunk into the buffer using stack
            Span<byte> bufferStackalloc = stackalloc byte[bufferSize];

            // Read from filesystem
            fs.Read(bufferStackalloc);

            // Check the CRC of the chunk buffer
            return CheckCRC(bufferStackalloc);
        }

        private byte[] CheckCRC(ReadOnlySpan<byte> buffer)
        {
            // Increment total size counter
            _progressTotalSizeCurrent += buffer.Length;
            // Increment per file size counter
            _progressPerFileSizeCurrent += buffer.Length;

            // Update status and progress for CRC calculation
            UpdateProgressCRC();

            // Return computed hash byte
            lock (_crcInstance)
            {
                return _crcInstance.ComputeHashByte(buffer);
            }
        }

        private byte[] CheckCRC(Stream stream)
        {
            // Reset CRC instance and assign buffer
            _crcInstance.Initialize();
            Span<byte> buffer = stackalloc byte[_bufferBigLength];

            using (stream)
            {
                int read;
                while ((read = stream.Read(buffer)) > 0)
                {
                    _token.Token.ThrowIfCancellationRequested();
                    _crcInstance.Append(buffer.Slice(0, read));
                    // Increment total size counter
                    _progressTotalSizeCurrent += read;
                    // Increment per file size counter
                    _progressPerFileSizeCurrent += read;
                    // Update status and progress for CRC calculation
                    UpdateProgressCRC();
                }
            }

            // Return computed hash byte
            return _crcInstance.Hash;
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
