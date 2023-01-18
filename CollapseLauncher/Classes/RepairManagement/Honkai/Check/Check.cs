using Force.Crc32;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.IO;
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
            _status.RepairActivityStatus = string.Format(Lang._GameRepairPage.Status6, asset.N);

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
                _progressPerFileSizeCurrent = asset.S;

                Dispatch(() => RepairAssetEntry.Add(
                    new RepairAssetProperty(
                        asset.N,
                        asset.FT == FileType.Audio ? RepairAssetType.Audio : RepairAssetType.General,
                        Path.GetDirectoryName(asset.N),
                        asset.S,
                        new byte[4] { 0, 0, 0, 0 },
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
                Dispatch(() => RepairAssetEntry.Add(
                    new RepairAssetProperty(
                        asset.N,
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
                _status.RepairActivityPerFile = string.Format(Lang._Misc.Speed, ConverterTool.SummarizeSizeSimple(_progress.ProgressTotalSpeed));

                // Update current activity status
                _status.RepairActivityTotal = string.Format(Lang._GameRepairPage.PerProgressSubtitle2, _progressTotalCountCurrent, _progressTotalCount);

                // Trigger update
                UpdateProgress();
                UpdateStatus();
            }
        }
        #endregion

        #region BlocksCheck
        private async Task CheckAssetTypeBlocks(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex)
        {
            // Increment current total and size for the XMF
            _progressTotalCountCurrent++;
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
            foreach (XMFBlockList block in asset.BlkC)
            {
                await CheckBlockCRC(block, blocks.BlkC);
            }

            // If the block content is not blank, then add it to target asset index
            if (blocks.BlkC.Count != 0) targetAssetIndex.Add(blocks);
        }

        private async Task CheckBlockCRC(XMFBlockList sourceBlock, List<XMFBlockList> targetBlockList)
        {
            // Update activity status
            _status.RepairActivityStatus = string.Format(Lang._GameRepairPage.Status5, sourceBlock.BlockHash);

            // Get block file path
            string blockDirPath = "BH3_Data\\StreamingAssets\\Asb\\pc";
            string blockPath = Path.Combine(_gamePath, blockDirPath, sourceBlock.BlockHash + ".wmv");
            FileInfo file = new FileInfo(blockPath);

            // If file doesn't exist or the length is invalid, then register it.
            if (!file.Exists || file.Length != sourceBlock.BlockSize)
            {
                sourceBlock.BlockMissing = true;
                targetBlockList.Add(sourceBlock);

                Dispatch(() => RepairAssetEntry.Add(
                    new RepairAssetProperty(
                        sourceBlock.BlockHash + ".wmv",
                        RepairAssetType.Block,
                        blockDirPath,
                        sourceBlock.BlockSize,
                        new byte[4] { 0, 0, 0, 0 },
                        new byte[4] { 0, 0, 0, 0 }
                    )
                ));
                return;
            }

            _progressPerFileSizeCurrent = 0;
            _progressPerFileSize = sourceBlock.BlockSize;

            using (Stream fs = file.OpenRead())
            {
                await Task.Run(() =>
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

                    // Iterates chunks and check for the CRC
                    foreach (var chunk in sourceBlock.BlockContent)
                    {
                        // Throw if cancellation was given
                        _token.Token.ThrowIfCancellationRequested();

                        // Initialize buffer and put the chunk into the buffer
                        byte[] buffer = new byte[chunk._filesize];
                        fs.Read(buffer, 0, buffer.Length);

                        // Check the CRC of the chunk buffer
                        byte[] localCRC = CheckCRC(buffer);

                        // If the chunk is unmatch, then add the chunk into temporary block list
                        if (!IsCRCArrayMatch(localCRC, chunk._filecrc32array))
                        {
                            block.BlockContent.Add(chunk);

                            Dispatch(() => RepairAssetEntry.Add(
                                new RepairAssetProperty(
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
                });
            }
        }
        #endregion

        #region Tools
        private bool IsCRCArrayMatch(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target) => source[0] == target[0] && source[1] == target[1]
                                                                                           && source[2] == target[2] && source[3] == target[3];

        private byte[] CheckCRC(ReadOnlySpan<byte> buffer)
        {
            // Increment total size counter
            _progressTotalSizeCurrent += buffer.Length;
            // Increment per file size counter
            _progressPerFileSizeCurrent += buffer.Length;

            // Update status and progress for CRC calculation
            UpdateProgressCRC();

            // Return computed hash byte
            return _crcInstance.ComputeHashByte(buffer);
        }

        private byte[] CheckCRC(Stream stream)
        {
            // Reset CRC instance and assign buffer
            _crcInstance.Initialize();
            byte[] buffer = new byte[_bufferLength];

            using (stream)
            {
                int read;
                while ((read = stream.Read(buffer, 0, _bufferLength)) > 0)
                {
                    _token.Token.ThrowIfCancellationRequested();
                    _crcInstance.Append(buffer, 0, read);
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
        #endregion
    }
}
