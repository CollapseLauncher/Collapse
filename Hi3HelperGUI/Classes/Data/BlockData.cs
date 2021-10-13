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
        XMFUtils util;
        internal protected MemoryStream chunkBuffer;
        internal protected FileStream fileStream;

        public Dictionary<string, List<_XMFBlockList>> BrokenBlocksRegion = new();
        List<_XMFBlockList> BrokenBlocks = new();
        List<_XMFFileProperty> BrokenChunkProp;

        public event EventHandler<ReadingBlockProgressChanged> ProgressChanged;
        public event EventHandler<ReadingBlockProgressCompleted> Completed;

        public void Init(in MemoryStream i, PresetConfigClasses j)
        {
            i.Position = 0;
            util = new XMFUtils(i, XMFFileFormat.Dictionary);
            util.Read();
        }

        public void CheckIntegrity(PresetConfigClasses input, CancellationToken token)
        {
            int byteSize;
            int chunkSize;
            long totalRead = 0;
            long totalFileSize = util.XMFBook.Sum(item => item.BlockSize);
            byte[] buffer = new byte[1024];
            string chunkHash = string.Empty;
            FileInfo fileInfo;

            string localPath = Path.Combine(input.ActualGameDataLocation, @"BH3_Data\StreamingAssets\Asb\pc");

            foreach (_XMFBlockList i in util.XMFBook)
            {
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

                            while (chunkSize > 0)
                            {
                                token.ThrowIfCancellationRequested();
                                byteSize = chunkSize > buffer.Length ? buffer.Length : chunkSize;
                                _ = fileStream.Read(buffer, 0, byteSize);
                                chunkBuffer.Write(buffer, 0, byteSize);
                                chunkSize -= byteSize;
                            }

                            totalRead += j._filesize;

                            OnProgressChanged(new ReadingBlockProgressChanged()
                            {
                                BlockHash = i.BlockHash,
                                BlockSize = i.BlockSize,
                                ChunkHash = j._filecrc32,
                                ChunkName = j._filename,
                                ChunkSize = j._filesize,
                                BytesRead = totalRead,
                                TotalBlockSize = totalFileSize
                            });

                            chunkBuffer.Position = 0;

                            if (j._filecrc32 != (chunkHash = BytesToCRC32Simple(chunkBuffer)))
                            {
                                j._fileactualcrc32 = chunkHash;
                                BrokenChunkProp.Add(j);
                                LogWriteLine($"Block: {i.BlockHash} CRC: {j._filecrc32} != {chunkHash} Offset: {j._startoffset} Size: {j._filesize} is broken", LogType.Warning, true);
                            }
                        }
                    }
                }
                else
                {
                    totalRead += i.BlockSize;

                    OnProgressChanged(new ReadingBlockProgressChanged()
                    {
                        BlockHash = i.BlockHash,
                        BlockSize = i.BlockSize,
                        BytesRead = totalRead,
                        TotalBlockSize = totalFileSize
                    });

                    i.BlockIncompleted = true;
                    i.BlockExistingSize = fileInfo.Exists ? fileInfo.Length : 0;
                    i.BlockContent.Clear();
                    if (!fileInfo.Exists)
                        LogWriteLine($"Block: {i.BlockHash} doesn't exist!", LogType.Warning, true);
                    else
                        LogWriteLine($"Block: {i.BlockHash} is not completed! Size: {i.BlockSize} != {i.BlockExistingSize}", LogType.Warning, true);
                    BrokenBlocks.Add(i);
                }

                if (BrokenChunkProp.Count > 0)
                {
                    i.BlockIncompleted = false;
                    i.BlockContent = BrokenChunkProp;
                    BrokenBlocks.Add(i);
                }
            }
            if (BrokenBlocks.Count > 0)
                BrokenBlocksRegion.Add(input.ZoneName, BrokenBlocks);
        }

        protected virtual void OnProgressChanged(ReadingBlockProgressChanged e) => ProgressChanged?.Invoke(this, e);

        protected virtual void OnCompleted(ReadingBlockProgressCompleted e) => Completed?.Invoke(this, e);

    }

    public class ReadingBlockProgressChanged : EventArgs
    {
        public string BlockHash { get; set; }
        public long BlockSize { get; set; }
        public string ChunkName { get; set; }
        public long ChunkSize { get; set; }
        public string ChunkHash { get; set; }
        public long BytesRead { get; set; }
        public long TotalBlockSize { get; set; }
        public float ProgressPercentage { get => ((float)BytesRead / (float)TotalBlockSize) * 100; }
    }

    public class ReadingBlockProgressCompleted : EventArgs
    {
        public bool ReadCompleted { get; set; }
    }
}
