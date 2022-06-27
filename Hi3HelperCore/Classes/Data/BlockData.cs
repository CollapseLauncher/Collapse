using InHttp = Hi3Helper.Http;
using Hi3Helper.Preset;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Logger;

namespace Hi3Helper.Data
{
    public class BlockData : XMFDictionaryClasses
    {
        internal protected MemoryStream chunkBuffer;
        internal protected FileStream fileStream;
        internal protected MD5 md5;

        public Dictionary<string, List<XMFBlockList>> BrokenBlocksRegion;
        List<XMFBlockList> BrokenBlocks;
        XMFUtils util;

        public event EventHandler<CheckingBlockProgressChanged> CheckingProgressChanged;
        public event EventHandler<CheckingBlockProgressChangedStatus> CheckingProgressChangedStatus;
        public event EventHandler<RepairingBlockProgressChanged> RepairingProgressChanged;
        public event EventHandler<RepairingBlockProgressChangedStatus> RepairingProgressChangedStatus;

        public void Init(in Stream i, XMFFileFormat format = XMFFileFormat.Dictionary)
        {
            i.Position = 0;
            util = new XMFUtils(i, format);
            util.Read();
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

        public List<string> GetListOfBrokenBlocks(string localPath)
        {
            List<string> list = new List<string>();
            if (BrokenBlocks != null)
                foreach (XMFBlockList block in BrokenBlocks)
                    list.Add(Path.Combine(localPath, $"{block.BlockHash}.wmv"));

            return list;
        }

        protected virtual void OnProgressChanged(CheckingBlockProgressChanged e) => CheckingProgressChanged?.Invoke(this, e);
        protected virtual void OnProgressChanged(CheckingBlockProgressChangedStatus e) => CheckingProgressChangedStatus?.Invoke(this, e);
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
