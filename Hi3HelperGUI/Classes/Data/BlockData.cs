using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.IO;
using Hi3HelperGUI.Preset;

using static Hi3HelperGUI.Logger;
using static Hi3HelperGUI.Data.ConverterTool;

namespace Hi3HelperGUI.Data
{
    public class BlockData : XMFDictionaryClasses
    {
        internal protected MemoryStream chunkBuffer;
        internal protected FileStream fileStream;

        public Dictionary<string, List<XMFBlockList>> BrokenBlocksRegion;
        List<XMFBlockList> BrokenBlocks;
        List<XMFFileProperty> BrokenChunkProp;
        XMFUtils util;
        readonly HttpClientTool httpUtil = new();

        public event EventHandler<CheckingBlockProgressChanged> CheckingProgressChanged;
        public event EventHandler<CheckingBlockProgressChangedStatus> CheckingProgressChangedStatus;
        public event EventHandler<RepairingBlockProgressChanged> RepairingProgressChanged;
        public event EventHandler<RepairingBlockProgressChangedStatus> RepairingProgressChangedStatus;

#if DEBUG
        // 4 KiB buffer
        byte[] buffer = new byte[4096];
#else
        // 512 KiB buffer
        readonly byte[] buffer = new byte[524288];
#endif

        public void Init(in MemoryStream i)
        {
            i.Position = 0;
            util = new XMFUtils(i, XMFFileFormat.Dictionary);
            util.Read();
        }

        public void FlushProp()
        {
            BrokenBlocks = new();
            BrokenBlocksRegion = new();
        }

        public void FlushTemp()
        {
            BrokenBlocks = new();
            BrokenChunkProp = new();
        }

        public void DisposeAssets()
        {
            chunkBuffer?.Dispose();
            fileStream?.Dispose();
        }

        public void DisposeAll()
        {
            FlushProp();
            FlushTemp();
            DisposeAssets();
        }

        public void CheckIntegrity(PresetConfigClasses input, CancellationToken token)
        {
            int byteSize;
            int chunkSize;
            long totalRead = 0;
            long totalFileSize = util.XMFBook.Sum(item => item.BlockSize);
            string chunkHash = string.Empty;
            FileInfo fileInfo;

            FlushTemp();

            string localPath = Path.Combine(input.ActualGameDataLocation, @"BH3_Data\StreamingAssets\Asb\pc");

            int z = 0;
            int y = util.XMFBook.Count;
            foreach (XMFBlockList i in util.XMFBook)
            {
                z++;
                BrokenChunkProp = new List<XMFFileProperty>();
                string localFile = Path.Combine(localPath, $"{i.BlockHash.ToLowerInvariant()}.wmv");
                fileInfo = new FileInfo(localFile);

                if (fileInfo.Exists && fileInfo.Length == i.BlockSize)
                {
                    using (fileStream = fileInfo.Open(
                       FileMode.Open,
                       FileAccess.Read))
                    {
                        OnProgressChanged(new CheckingBlockProgressChangedStatus()
                        {
                            BlockHash = i.BlockHash,
                            CurrentBlockPos = z,
                            BlockCount = y
                        });

                        foreach (XMFFileProperty j in i.BlockContent)
                        {
                            chunkSize = (int)j.FileSize;
                            chunkBuffer = new MemoryStream();

                            while (chunkSize > 0)
                            {
                                token.ThrowIfCancellationRequested();
                                byteSize = chunkSize > buffer.Length ? buffer.Length : chunkSize;
                                _ = fileStream.Read(buffer, 0, byteSize);
                                chunkBuffer.Write(buffer, 0, byteSize);
                                chunkSize -= byteSize;
                            }

                            /*
                                token.ThrowIfCancellationRequested();
                                _ = fileStream.Read(buffer = new byte[chunkSize], 0, chunkSize);
                            */

                            totalRead += j.FileSize;
                            // totalRead += chunkSize;

                            chunkBuffer.Position = 0;

                            if (j.FileHash != (chunkHash = BytesToCRC32Simple(chunkBuffer)))
                            {
                                j.FileActualHash = chunkHash;
                                BrokenChunkProp.Add(j);
                                LogWriteLine($"Block: {i.BlockHash} CRC: {j.FileHash} != {chunkHash} Offset: {NumberToHexString(j.StartOffset)} Size: {NumberToHexString(j.FileSize)} is broken", LogType.Warning, true);
                            }

                            OnProgressChanged(new CheckingBlockProgressChanged()
                            {
                                BlockSize = i.BlockSize,
                                ChunkSize = j.FileSize,
                                BytesRead = totalRead,
                                TotalBlockSize = totalFileSize
                            });
                        }
                    }
                }
                else
                {
                    totalRead += i.BlockSize;

                    OnProgressChanged(new CheckingBlockProgressChangedStatus()
                    {
                        BlockHash = i.BlockHash,
                        CurrentBlockPos = z,
                        BlockCount = y
                    });

                    OnProgressChanged(new CheckingBlockProgressChanged()
                    {
                        BlockSize = i.BlockSize,
                        BytesRead = totalRead,
                        TotalBlockSize = totalFileSize,
                    });

                    i.BlockExistingSize = fileInfo.Exists ? fileInfo.Length : 0;
                    i.BlockContent.Clear();
                    if (!fileInfo.Exists)
                    {
                        i.BlockMissing = true;
                        LogWriteLine($"Block: {i.BlockHash} is missing!", LogType.Warning, true);
                    }
                    else
                    {
                        LogWriteLine($"Block: {i.BlockHash} size is not expected! Existing Size: {NumberToHexString(i.BlockExistingSize)} Size: {NumberToHexString(i.BlockSize)}", LogType.Warning, true);
                    }
                    BrokenBlocks.Add(i);
                }

                if (BrokenChunkProp.Count > 0)
                {
                    i.BlockMissing = false;
                    i.BlockContent = BrokenChunkProp;
                    BrokenBlocks.Add(i);
                }
            }

            if (BrokenBlocks.Count > 0)
                BrokenBlocksRegion.Add(input.ZoneName, BrokenBlocks);
        }


        long totalBytesRead = 0;
        long totalRepairableSize = 0;
        long downloadSize = 0;
        string blockHash;
        string zoneName;
        int currentBlockPos;
        int blockCount;
        int currentChunkPos;
        int chunkCount;

        public void BlockRepair(List<PresetConfigClasses> i, CancellationToken token)
        {
            totalBytesRead = 0;
            totalRepairableSize = BrokenBlocksRegion.Sum(item => item.Value.Sum(child => child.SumDownloadableContent()));
            string remoteAddress;
            string remotePath;
            string localPath;
            FileInfo fileInfo;

            blockCount = BrokenBlocksRegion.Sum(block => block.Value.Count);

            LogWriteLine($"Downloadable Content for Repairing: {SummarizeSizeSimple(totalRepairableSize)}");

            foreach (PresetConfigClasses a in i)
            {
                zoneName = a.ZoneName;
                remoteAddress = ConfigStore.GetMirrorAddressByIndex(a, ConfigStore.DataType.Bigfile);
                try
                {
                    foreach (XMFBlockList b in BrokenBlocksRegion[a.ZoneName])
                    {
                        currentChunkPos = 0;
                        currentBlockPos++;
                        chunkCount = b.BlockContent.Count;
                        downloadSize = b.BlockSize;
                        blockHash = b.BlockHash;
                        remotePath = $"{remoteAddress}{blockHash.ToLowerInvariant()}";
                        localPath = Path.Combine(a.ActualGameDataLocation,
                            @"BH3_Data\StreamingAssets\Asb\pc",
                            $"{b.BlockHash.ToLowerInvariant()}.wmv");

                        fileInfo = new FileInfo(localPath);
                        RunRepairAction(b, fileInfo, token, remotePath);
                    }
                }
                catch (KeyNotFoundException) { }
            }
        }

        void RunRepairAction(
            in XMFBlockList b, in FileInfo j, in CancellationToken k,
            in string remotePath)
        {
            if (b.BlockMissing ||
               (b.BlockExistingSize < b.BlockSize && b.BlockContent.Count == 0))
                DownloadContent(remotePath + ".wmv", j.FullName, -1, -1, k);
            else if (b.BlockExistingSize > b.BlockSize)
            {
                j.Delete();
                DownloadContent(remotePath + ".wmv", j.FullName, -1, -1, k);
            }
            else
                RepairCorruptedBlock(b, j, k, remotePath);
        }

        void RepairCorruptedBlock(in XMFBlockList blockProp, in FileInfo blockInfo, in CancellationToken token, in string remotePath)
        {
            using (fileStream = blockInfo.Open(FileMode.Open, FileAccess.Write, FileShare.Write))
            {
                foreach (XMFFileProperty chunkProp in blockProp.BlockContent)
                {
                    currentChunkPos++;
                    chunkBuffer = new MemoryStream();
                    fileStream.Position = chunkProp.StartOffset;
                    DownloadContent($"{remotePath}.wmv", chunkBuffer, chunkProp, chunkProp.StartOffset, chunkProp.StartOffset + chunkProp.FileSize, token,
                        $"Down: {blockProp.BlockHash} Offset {NumberToHexString(chunkProp.StartOffset)} Size {NumberToHexString(chunkProp.FileSize)}");

                    OnProgressChanged(new RepairingBlockProgressChangedStatus()
                    {
                        BlockHash = blockProp.BlockHash,
                        ZoneName = zoneName,
                        ChunkOffset = chunkProp.StartOffset,
                        ChunkSize = chunkProp.FileSize,
                        Downloading = false,
                        DownloadingBlock = false,
                        BlockCount = blockCount,
                        CurrentBlockPos = currentBlockPos,
                        ChunkCount = chunkCount,
                        CurrentChunkPos = currentChunkPos
                    });

                    int byteSize = 0;

                    using (chunkBuffer)
                    {
                        chunkBuffer.Position = 0;
                        long chunkSize = chunkBuffer.Length;
                        while ((byteSize = chunkBuffer.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            token.ThrowIfCancellationRequested();
                            fileStream.Write(buffer, 0, byteSize);
                            totalBytesRead += byteSize;
                        }
                    }
                }
            }
        }

        void DownloadContent(string url, in MemoryStream destination, in XMFFileProperty chunkProp, long startOffset, long endOffset, CancellationToken token, string message)
        {
            httpUtil.ProgressChanged += DownloadEventConverterForStream;
            OnProgressChanged(new RepairingBlockProgressChangedStatus()
            {
                BlockHash = blockHash,
                Downloading = true,
                DownloadingBlock = false,
                ZoneName = zoneName,
                DownloadTotalSize = downloadSize,
                ChunkOffset = chunkProp.StartOffset,
                ChunkSize = chunkProp.FileSize,
                BlockCount = blockCount,
                CurrentBlockPos = currentBlockPos,
                ChunkCount = chunkCount,
                CurrentChunkPos = currentChunkPos
            });

            while (!httpUtil.DownloadStream(url, destination, token, startOffset, endOffset, message))
                LogWriteLine($"Retrying...");

            httpUtil.ProgressChanged -= DownloadEventConverterForStream;
            GC.Collect();
            LogWriteLine();
        }

        void DownloadContent(string url, string destination, long startOffset, long endOffset, CancellationToken token)
        {
            httpUtil.ProgressChanged += DownloadEventConverter;
            OnProgressChanged(new RepairingBlockProgressChangedStatus()
            {
                BlockHash = blockHash,
                Downloading = true,
                ZoneName = zoneName,
                DownloadTotalSize = downloadSize,
                BlockCount = blockCount,
                CurrentBlockPos = currentBlockPos
            });

            while (!httpUtil.DownloadFile(url, destination, "", startOffset, endOffset, token))
                LogWriteLine($"Retrying...");

            httpUtil.ProgressChanged -= DownloadEventConverter;
            GC.Collect();
            LogWriteLine();
        }

        private void DownloadEventConverterForStream(object sender, DownloadProgressChanged e)
        {
            OnProgressChanged(new RepairingBlockProgressChanged()
            {
                DownloadReceivedBytes = e.BytesReceived,
                DownloadTotalSize = e.TotalBytesToReceive,
                DownloadSpeed = e.CurrentSpeed,
                TotalBytesRead = totalBytesRead,
                TotalRepairableSize = totalRepairableSize,
            });

            LogWrite($"{e.Message} {(byte)e.ProgressPercentage}% {SummarizeSizeSimple(e.BytesReceived)} {SummarizeSizeSimple(e.CurrentSpeed)}/s", LogType.NoTag, false, true);
        }

        private void DownloadEventConverter(object sender, DownloadProgressChanged e)
        {
            totalBytesRead += e.CurrentReceived;
            OnProgressChanged(new RepairingBlockProgressChanged()
            {
                DownloadReceivedBytes = e.BytesReceived,
                DownloadTotalSize = downloadSize,
                DownloadSpeed = e.CurrentSpeed,
                TotalBytesRead = totalBytesRead,
                TotalRepairableSize = totalRepairableSize,
            });

            LogWrite($"{e.Message} {(byte)e.ProgressPercentage}% {SummarizeSizeSimple(e.BytesReceived)} {SummarizeSizeSimple(e.CurrentSpeed)}/s", LogType.NoTag, false, true);
        }

        protected virtual void OnProgressChanged(CheckingBlockProgressChanged e) => CheckingProgressChanged?.Invoke(this, e);
        protected virtual void OnProgressChanged(CheckingBlockProgressChangedStatus e) => CheckingProgressChangedStatus?.Invoke(this, e);
        protected virtual void OnProgressChanged(RepairingBlockProgressChanged e) => RepairingProgressChanged?.Invoke(this, e);
        protected virtual void OnProgressChanged(RepairingBlockProgressChangedStatus e) => RepairingProgressChangedStatus?.Invoke(this, e);

    }

    public class CheckingBlockProgressChangedStatus : EventArgs
    {
        public string BlockHash { get; set; }
        public int CurrentBlockPos { get; set; }
        public int BlockCount { get; set; }
    }

    public class CheckingBlockProgressChanged : EventArgs
    {
        public long BlockSize { get; set; }
        public long ChunkSize { get; set; }
        public long BytesRead { get; set; }
        public long TotalBlockSize { get; set; }
    }

    public class RepairingBlockProgressChangedStatus : EventArgs
    {
        public string BlockHash { get; set; }
        public string ZoneName { get; set; }
        public uint ChunkSize { get; set; }
        public uint ChunkOffset { get; set; }
        public bool Downloading { get; set; } = false;
        public bool DownloadingBlock { get; set; } = true;
        public long DownloadTotalSize { get; set; } = 0;
        public int CurrentBlockPos { get; set; }
        public int BlockCount { get; set; }
        public int CurrentChunkPos { get; set; }
        public int ChunkCount { get; set; }
    }

    public class RepairingBlockProgressChanged : EventArgs
    {
        public long DownloadReceivedBytes { get; set; } = 0;
        public long DownloadTotalSize { get; set; } = 0;
        public long DownloadSpeed { get; set; } = 0;
        public float DownloadProgressPercentage { get => ((float)DownloadReceivedBytes / (float)DownloadTotalSize) * 100; }
        public long TotalBytesRead { get; set; }
        public long TotalRepairableSize { get; set; }
        public float ProgressPercentage { get => ((float)TotalBytesRead / (float)TotalRepairableSize) * 100; }
    }
}
