using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Reflection;
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

        public Dictionary<string, List<_XMFBlockList>> BrokenBlocksRegion;
        List<_XMFBlockList> BrokenBlocks;
        List<_XMFFileProperty> BrokenChunkProp;
        XMFUtils util;
        readonly HttpClientTool httpUtil = new HttpClientTool();

        public event EventHandler<CheckingBlockProgressChanged> CheckingProgressChanged;
        public event EventHandler<CheckingBlockProgressChangedStatus> CheckingProgressChangedStatus;
        public event EventHandler<RepairingBlockProgressChanged> RepairingProgressChanged;
        public event EventHandler<RepairingBlockProgressChangedStatus> RepairingProgressChangedStatus;

#if DEBUG
        // 4 KiB buffer
        byte[] buffer = new byte[4096];
#else
        // 512 KiB buffer
        byte[] buffer = new byte[524288];
#endif

        public void Init(in MemoryStream i, PresetConfigClasses j)
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
            foreach (_XMFBlockList i in util.XMFBook)
            {
                z++;
                BrokenChunkProp = new List<_XMFFileProperty>();
                string localFile = Path.Combine(localPath, $"{i.BlockHash.ToLowerInvariant()}.wmv");
                fileInfo = new FileInfo(localFile);

                if (fileInfo.Exists && fileInfo.Length == i.BlockSize)
                {
                    using (fileStream = fileInfo.Open(
                       FileMode.Open,
                       FileAccess.Read))
                    {
                        foreach (_XMFFileProperty j in i.BlockContent)
                        {
                            chunkSize = (int)j._filesize;
                            chunkBuffer = new MemoryStream();

                            OnProgressChanged(new CheckingBlockProgressChangedStatus()
                            {
                                BlockHash = i.BlockHash,
                                ChunkHash = j._filecrc32,
                                ChunkName = j._filename,
                                CurrentBlockPos = z,
                                BlockCount = y
                            });

                            while (chunkSize > 0)
                            {
                                token.ThrowIfCancellationRequested();
                                byteSize = chunkSize > buffer.Length ? buffer.Length : chunkSize;
                                _ = fileStream.Read(buffer, 0, byteSize);
                                chunkBuffer.Write(buffer, 0, byteSize);
                                chunkSize -= byteSize;
                            }

                            totalRead += j._filesize;

                            OnProgressChanged(new CheckingBlockProgressChanged()
                            {
                                BlockSize = i.BlockSize,
                                ChunkSize = j._filesize,
                                BytesRead = totalRead,
                                TotalBlockSize = totalFileSize
                            });

                            chunkBuffer.Position = 0;

                            if (j._filecrc32 != (chunkHash = BytesToCRC32Simple(chunkBuffer)))
                            {
                                j._fileactualcrc32 = chunkHash;
                                BrokenChunkProp.Add(j);
                                LogWriteLine($"Block: {i.BlockHash} CRC: {j._filecrc32} != {chunkHash} Offset: {NumberToHexString(j._startoffset)} Size: {NumberToHexString(j._filesize)} is broken", LogType.Warning, true);
                            }
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
                    foreach (_XMFBlockList b in BrokenBlocksRegion[a.ZoneName])
                    {
                        currentChunkPos = 0;
                        currentBlockPos++;
                        chunkCount = b.BlockContent.Count;
                        downloadSize = b.BlockSize < b.BlockExistingSize ? b.BlockSize : (b.BlockSize - b.BlockExistingSize);
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
            in _XMFBlockList b, in FileInfo j, in CancellationToken k,
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

        void RepairCorruptedBlock(in _XMFBlockList blockProp, in FileInfo blockInfo, in CancellationToken token, in string remotePath)
        {
            using (fileStream = blockInfo.Open(FileMode.Open, FileAccess.Write, FileShare.Write))
            {
                foreach (_XMFFileProperty chunkProp in blockProp.BlockContent)
                {
                    currentChunkPos++;
                    chunkBuffer = new MemoryStream();
                    fileStream.Position = chunkProp._startoffset;
                    DownloadContent($"{remotePath}.c/{chunkProp._filename}", chunkBuffer, chunkProp, -1, -1, token,
                        $"Down: {blockProp.BlockHash} Offset {NumberToHexString(chunkProp._startoffset)} Size {NumberToHexString(chunkProp._filesize)}");

                    RepairingProgressChangedStatus(this, new()
                    {
                        BlockHash = blockProp.BlockHash,
                        ZoneName = zoneName,
                        ChunkOffset = chunkProp._startoffset,
                        ChunkSize = chunkProp._filesize,
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

        void DownloadContent(string url, in MemoryStream destination, in _XMFFileProperty chunkProp, long startOffset, long endOffset, CancellationToken token, string message)
        {
            httpUtil.ProgressChanged += DownloadEventConverterForStream;
            RepairingProgressChangedStatus(this, new()
            {
                BlockHash = blockHash,
                Downloading = true,
                DownloadingBlock = false,
                ZoneName = zoneName,
                DownloadTotalSize = downloadSize,
                ChunkOffset = chunkProp._startoffset,
                ChunkSize = chunkProp._filesize,
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
            RepairingProgressChangedStatus(this, new()
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
            RepairingProgressChanged(this, new()
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
            RepairingProgressChanged(this, new()
            {
                DownloadReceivedBytes = e.BytesReceived,
                DownloadTotalSize = downloadSize,
                DownloadSpeed = e.CurrentSpeed,
                TotalBytesRead = totalBytesRead,
                TotalRepairableSize = totalRepairableSize,
            });

            LogWrite($"{e.Message} {(byte)e.ProgressPercentage}% {SummarizeSizeSimple(e.BytesReceived)} {SummarizeSizeSimple(e.CurrentSpeed)}/s", LogType.NoTag, false, true);
        }

        protected virtual void OnProgressChanged(CheckingBlockProgressChanged e) => CheckingProgressChanged(this, e);
        protected virtual void OnProgressChanged(CheckingBlockProgressChangedStatus e) => CheckingProgressChangedStatus(this, e);
        protected virtual void OnProgressChanged(RepairingBlockProgressChanged e) => RepairingProgressChanged(this, e);
        protected virtual void OnProgressChanged(RepairingBlockProgressChangedStatus e) => RepairingProgressChangedStatus(this, e);

    }

    public class CheckingBlockProgressChangedStatus : EventArgs
    {
        public string BlockHash { get; set; }
        public string ChunkName { get; set; }
        public string ChunkHash { get; set; }
        public int CurrentBlockPos { get; set; }
        public int BlockCount { get; set; }
    }

    public class CheckingBlockProgressChanged : EventArgs
    {
        public long BlockSize { get; set; }
        public long ChunkSize { get; set; }
        public long BytesRead { get; set; }
        public long TotalBlockSize { get; set; }
        public float ProgressPercentage { get => ((float)BytesRead / (float)TotalBlockSize) * 100; }
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
