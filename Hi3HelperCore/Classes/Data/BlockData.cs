using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO;
using Hi3Helper.Preset;

using static Hi3Helper.Logger;
using static Hi3Helper.Data.ConverterTool;

namespace Hi3Helper.Data
{
    public class BlockData : XMFDictionaryClasses
    {
        internal protected MemoryStream chunkBuffer;
        internal protected FileStream fileStream;
        internal protected MD5 md5;

        public Dictionary<string, List<XMFBlockList>> BrokenBlocksRegion;
        List<XMFBlockList> BrokenBlocks;
        List<XMFFileProperty> BrokenChunkProp;
        XMFUtils util;
        readonly HttpClientTool httpUtil = new HttpClientTool();

        public event EventHandler<CheckingBlockProgressChanged> CheckingProgressChanged;
        public event EventHandler<CheckingBlockProgressChangedStatus> CheckingProgressChangedStatus;
        public event EventHandler<RepairingBlockProgressChanged> RepairingProgressChanged;
        public event EventHandler<RepairingBlockProgressChangedStatus> RepairingProgressChangedStatus;

#if DEBUG
        // 4 KiB buffer
        readonly byte[] buffer = new byte[4096];
#else
        // 4 MB buffer
        readonly byte[] buffer = new byte[4194304];
#endif

        public void Init(in Stream i, XMFFileFormat format = XMFFileFormat.Dictionary)
        {
            i.Position = 0;
            util = new XMFUtils(i, format);
            util.Read();
        }

        public void FlushProp()
        {
            BrokenBlocks = new List<XMFBlockList>();
            BrokenBlocksRegion = new Dictionary<string, List<XMFBlockList>>();
        }

        public void FlushTemp()
        {
            BrokenBlocks = new List<XMFBlockList>();
            BrokenChunkProp = new List<XMFFileProperty>();
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

        public void PrintBlocks()
        {
            foreach (XMFBlockList block in util.XMFBook)
            {
                LogWriteLine($"{block.BlockHash} Size: {SummarizeSizeSimple(block.BlockSize)}");
            }
        }

        int chunkSize;
        long totalRead = 0;
        long totalFileSize = 0;
        public void CheckIntegrity(PresetConfigClasses input, CancellationToken token)
        {
            int byteSize;
            totalRead = 0;
            totalFileSize = util.XMFBook.Sum(item => item.BlockSize);
            FileInfo fileInfo;

            FlushTemp();

            string localPath = Path.Combine(input.ActualGameDataLocation, @"BH3_Data\StreamingAssets\Asb\pc");

            int z = 0;
            int y = util.XMFBook.Count;

            CheckForUnusedBlocks(localPath);

            foreach (XMFBlockList i in util.XMFBook)
            {
                try
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

                            if (!CheckMD5Integrity(i, fileStream, token))
                            {
                                fileStream.Position = 0;
                                foreach (XMFFileProperty j in i.BlockContent)
                                {
                                    token.ThrowIfCancellationRequested();
                                    chunkBuffer = new MemoryStream();

                                    chunkSize = (int)j.FileSize;

                                    while (chunkSize > 0)
                                    {
                                        byteSize = chunkSize > buffer.Length ? buffer.Length : chunkSize;
                                        fileStream.Read(buffer, 0, byteSize);
                                        chunkBuffer.Write(buffer, 0, byteSize);
                                        chunkSize -= byteSize;
                                    }

                                    chunkBuffer.Position = 0;

                                    if (j.FileHashArray != (j.FileActualHashArray = BytesToCRC32Int(chunkBuffer)))
                                    {
                                        BrokenChunkProp.Add(j);
                                        LogWriteLine($"Blk: {i.BlockHash} CRC: {NumberToHexString(j.FileHashArray)} != {NumberToHexString(j.FileActualHashArray)} Start: {NumberToHexString(j.StartOffset)} Size: {NumberToHexString(j.FileSize)} (Virtual chunkname: {j.FileName}) is broken.", LogType.Warning, true);
                                    }

                                    OnProgressChanged(new CheckingBlockProgressChanged()
                                    {
                                        BlockSize = i.BlockSize,
                                        ChunkSize = j.FileSize,
                                        BytesRead = (totalRead += j.FileSize),
                                        TotalBlockSize = totalFileSize
                                    });
                                }
                            }
                            else
                            {
                                OnProgressChanged(new CheckingBlockProgressChanged()
                                {
                                    BlockSize = i.BlockSize,
                                    BytesRead = (totalRead += i.BlockSize),
                                    TotalBlockSize = totalFileSize,
                                });
                            }
                        }
                    }
                    else
                    {
                        OnProgressChanged(new CheckingBlockProgressChangedStatus()
                        {
                            BlockHash = i.BlockHash,
                            CurrentBlockPos = z,
                            BlockCount = y
                        });

                        OnProgressChanged(new CheckingBlockProgressChanged()
                        {
                            BlockSize = i.BlockSize,
                            BytesRead = (totalRead += i.BlockSize),
                            TotalBlockSize = totalFileSize,
                        });

                        i.BlockExistingSize = fileInfo.Exists ? fileInfo.Length : 0;
                        i.BlockContent.Clear();
                        if (!fileInfo.Exists)
                        {
                            i.BlockMissing = true;
                            LogWriteLine($"Blk: {i.BlockHash} is missing!", LogType.Warning, true);
                        }
                        else
                            LogWriteLine($"Blk: {i.BlockHash} size is not expected! Existing Size: {NumberToHexString(i.BlockExistingSize)} Size: {NumberToHexString(i.BlockSize)}", LogType.Warning, true);

                        BrokenBlocks.Add(i);
                    }

                    if (BrokenChunkProp.Count > 0)
                    {
                        i.BlockMissing = false;
                        i.BlockContent = BrokenChunkProp;
                        BrokenBlocks.Add(i);
                    }
                }
                catch (IOException e)
                {
                    OnProgressChanged(new CheckingBlockProgressChangedStatus()
                    {
                        BlockHash = i.BlockHash,
                        CurrentBlockPos = z,
                        BlockCount = y
                    });

                    OnProgressChanged(new CheckingBlockProgressChanged()
                    {
                        BlockSize = i.BlockSize,
                        BytesRead = (totalRead += i.BlockSize),
                        TotalBlockSize = totalFileSize,
                    });

                    LogWriteLine($"Hi3Helper cannot read Blk: {i.BlockHash} (Size: {NumberToHexString(i.BlockSize)}). This Blk will be ignored.\r\nTraceback: {e.Message}", LogType.Error, true);
                }
            }

            if (BrokenBlocks.Count > 0)
                BrokenBlocksRegion.Add(input.ZoneName, BrokenBlocks);
        }

        bool CheckMD5Integrity(XMFBlockList i, Stream stream, CancellationToken token) => i.BlockHash == GenerateMD5(stream, token);

        string GenerateMD5(Stream stream, CancellationToken token)
        {
            md5 = MD5.Create();
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) >= buffer.Length)
            {
                token.ThrowIfCancellationRequested();
                md5.TransformBlock(buffer, 0, buffer.Length, buffer, 0);
            }

            md5.TransformFinalBlock(buffer, 0, read);

            return BytesToHex(md5.Hash);
        }

        public void CheckForUnusedBlocks(string localPath)
        {
            if (BrokenBlocks == null)
                BrokenBlocks = new List<XMFBlockList>();
            List<string> fileList = Directory.EnumerateFiles(localPath, "*.wmv").Where(file => file.ToLowerInvariant().EndsWith("wmv")).ToList();
            string blockLocal;
            long blockLocalSize;
            foreach (string file in fileList)
            {
                blockLocal = Path.GetFileNameWithoutExtension(file).ToUpperInvariant();
                blockLocalSize = new FileInfo(file).Length;
                if (!util.XMFBook.Any(item => item.BlockHash == blockLocal))
                {
                    LogWriteLine($"Block: {blockLocal} ({SummarizeSizeSimple(blockLocalSize)}) might be unused. This will be deleted.", LogType.Warning, true);
                    BrokenBlocks.Add(new XMFBlockList() { BlockHash = blockLocal, BlockSize = blockLocalSize, BlockExistingSize = 0, BlockMissing = false, BlockUnused = true });
                }
            }
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
            currentBlockPos = 0;
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
               (b.BlockExistingSize < b.BlockSize && (b.BlockContent.Count == 0 && !b.BlockUnused)))
                DownloadContent(remotePath + ".wmv", j.FullName, -1, -1, k);
            else if (b.BlockExistingSize > b.BlockSize)
            {
                j.Delete();
                DownloadContent(remotePath + ".wmv", j.FullName, -1, -1, k);
            }
            else if (b.BlockUnused)
            {
                j.Delete();
                totalBytesRead += b.BlockSize;
                OnProgressChanged(new RepairingBlockProgressChanged()
                {
                    DownloadReceivedBytes = 0,
                    DownloadTotalSize = b.BlockSize,
                    TotalBytesRead = totalBytesRead,
                    TotalRepairableSize = totalRepairableSize,
                });
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

                    OnProgressChanged(new RepairingBlockProgressChangedStatus()
                    {
                        BlockHash = blockProp.BlockHash,
                        ZoneName = zoneName,
                        Downloading = false,
                        DownloadingBlock = false,
                        BlockCount = blockCount,
                        CurrentBlockPos = currentBlockPos,
                        ChunkCount = chunkCount,
                        CurrentChunkPos = currentChunkPos,
                        ChunkOffset = chunkProp.StartOffset
                    });

                    chunkBuffer = new MemoryStream();
                    fileStream.Position = chunkProp.StartOffset;
                    DownloadContent($"{remotePath}.wmv", chunkBuffer, chunkProp, chunkProp.StartOffset, chunkProp.StartOffset + chunkProp.FileSize, token,
                        $"Down: {blockProp.BlockHash} Offset {NumberToHexString(chunkProp.StartOffset)} Size {NumberToHexString(chunkProp.FileSize)}");

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

        public List<string> GetListOfBrokenBlocks(string localPath)
        {
            List<string> list = new List<string>();
            if (BrokenBlocks != null)
                foreach (XMFBlockList block in BrokenBlocks)
                    list.Add(Path.Combine(localPath, $"{block.BlockHash}.wmv"));

            return list;
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
                BlockCount = blockCount,
                CurrentBlockPos = currentBlockPos,
                ChunkCount = chunkCount,
                CurrentChunkPos = currentChunkPos,
                ChunkOffset = chunkProp.StartOffset
            });

            httpUtil.DownloadStream(url, destination, token, startOffset, endOffset, message);

            httpUtil.ProgressChanged -= DownloadEventConverterForStream;
#if (DEBUG)
            LogWriteLine();
#endif
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

            httpUtil.DownloadFile(url, destination, $"Down: {Path.GetFileNameWithoutExtension(destination).ToUpperInvariant()}", startOffset, endOffset, token);

            httpUtil.ProgressChanged -= DownloadEventConverter;
#if (DEBUG)
            LogWriteLine();
#endif
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

#if (DEBUG)
            LogWrite($"{e.Message} {(byte)e.ProgressPercentage}% {SummarizeSizeSimple(e.BytesReceived)} {SummarizeSizeSimple(e.CurrentSpeed)}/s", LogType.NoTag, false, true);
#endif
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

#if (DEBUG)
            LogWrite($"{e.Message} {(byte)e.ProgressPercentage}% {SummarizeSizeSimple(e.BytesReceived)} {SummarizeSizeSimple(e.CurrentSpeed)}/s", LogType.NoTag, false, true);
#endif
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
        public float DownloadProgressPercentage => ((float)DownloadReceivedBytes / (float)DownloadTotalSize) * 100;
        public long TotalBytesRead { get; set; }
        public long TotalRepairableSize { get; set; }
        public float ProgressPercentage => ((float)TotalBytesRead / (float)TotalRepairableSize) * 100;
    }
}
