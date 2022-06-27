using Force.Crc32;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;

namespace Hi3Helper.Shared.GameConversion
{
    public class CheckIntegrity
    {
        private string targetPath;
        private string endpointURL;
        private Http.Http http;
        private Stream stream;
        private CancellationTokenSource tokenSource;
        private Stopwatch sw;

        private List<FilePropertiesRemote> FileIndexesProperty = new List<FilePropertiesRemote>();
        private List<FilePropertiesRemote> BrokenFileIndexesProperty = new List<FilePropertiesRemote>();

        public event EventHandler<CheckIntegrityChanged> ProgressChanged;

        private string CheckStatus;
        private long TotalSizeToRead, TotalRead;
        private int TotalCountToRead, TotalCount;

        private string FilePath, FileCRC;
        private FileInfo FileInfo;
        private FilePropertiesRemote FileIndex;
        private Crc32Algorithm FileCRCTool;
        private byte[] buffer = new byte[0x400000];

        public CheckIntegrity(string targetPath, string endpointURL, CancellationTokenSource tokenSource)
        {
            this.sw = Stopwatch.StartNew();
            this.targetPath = targetPath;
            this.endpointURL = endpointURL;
            this.tokenSource = tokenSource;
            this.http = new Http.Http();
        }

        public async Task StartCheckIntegrity()
        {
            File.Create(Path.Combine(targetPath, "_conversion_unfinished")).Close();
            await FetchAPI();
            await Task.Run(() => CheckGameFiles());
            stream.Dispose();
            FileIndexesProperty.Clear();
        }

        private async Task FetchAPI()
        {
            CheckStatus = Lang._InstallMigrateSteam.Step3Subtitle;

            using (stream = new MemoryStream())
            {
                http.DownloadProgress += HttpAdapter;
                await http.DownloadStream(endpointURL, stream, tokenSource.Token);
                http.DownloadProgress -= HttpAdapter;

                FileIndexesProperty = JsonConvert.DeserializeObject<List<FilePropertiesRemote>>
                    (Encoding.UTF8.GetString((stream as MemoryStream)
                    .ToArray()));
            }
        }

        public List<FilePropertiesRemote> GetNecessaryFileList() => BrokenFileIndexesProperty;

        private void CheckGameFiles()
        {
            sw = Stopwatch.StartNew();

            TotalCount = 0;
            TotalRead = 0;
            TotalSizeToRead = FileIndexesProperty.Sum(x => x.S)
                            + FileIndexesProperty.Where(x => x.BlkC != null).Sum(x => x.BlkC.Sum(x => x.BlockSize));

            TotalCountToRead = FileIndexesProperty.Count
                             + FileIndexesProperty.Where(x => x.BlkC != null).Sum(y => y.BlkC.Sum(z => z.BlockContent.Count));

            foreach (FilePropertiesRemote Index in FileIndexesProperty)
            {
                FilePath = Path.Combine(targetPath, NormalizePath(Index.N));
                FileInfo = new FileInfo(FilePath);
                FileIndex = Index;

                switch (Index.FT)
                {
                    case FileType.Generic:
                    case FileType.Audio:
                        CheckGenericAudioFile();
                        break;
                    case FileType.Blocks:
                        CheckBlockFile();
                        break;
                }
            }
        }

        private void CheckGenericAudioFile()
        {
            TotalCount++;
            CheckStatus = string.Format(Lang._InstallMigrateSteam.InnerCheckFile, FileIndex.FT, TotalCount, TotalCountToRead, FileIndex.N);
            if (!FileInfo.Exists)
            {
                TotalRead += FileIndex.S;
                BrokenFileIndexesProperty.Add(FileIndex);
                return;
            }

            if ((FileCRC = GenerateCRC(stream = FileInfo.OpenRead())) != FileIndex.CRC)
                BrokenFileIndexesProperty.Add(FileIndex);
        }

        private void CheckBlockFile()
        {
            List<XMFDictionaryClasses.XMFBlockList> BrokenBlock = new List<XMFDictionaryClasses.XMFBlockList>();
            List<XMFDictionaryClasses.XMFFileProperty> BrokenChunk = new List<XMFDictionaryClasses.XMFFileProperty>();
            string BlockBasePath = FilePath;
            byte[] ChunkBuffer;

            foreach (var block in FileIndex.BlkC)
            {
                FilePath = Path.Combine(BlockBasePath, block.BlockHash + ".wmv");
                FileInfo = new FileInfo(FilePath);

                if (!FileInfo.Exists || FileInfo.Length != block.BlockSize)
                {
                    TotalCount++;
                    CheckStatus = string.Format(Lang._InstallMigrateSteam.InnerCheckBlock1, FileIndex.FT, block.BlockHash);
                    TotalRead += block.BlockSize;
                    TotalCount += block.BlockContent.Count;

                    BrokenBlock.Add(new XMFDictionaryClasses.XMFBlockList
                    {
                        BlockHash = block.BlockHash,
                        BlockSize = block.BlockSize,
                        BlockExistingSize = 0,
                        BlockMissing = true
                    });

                    OnProgressChanged(new CheckIntegrityChanged(TotalRead, TotalSizeToRead, sw.Elapsed.TotalSeconds)
                    {
                        Message = CheckStatus
                    });
                }
                else
                {
                    using (stream = FileInfo.OpenRead())
                    {
                        BrokenChunk = new List<XMFDictionaryClasses.XMFFileProperty>();
                        foreach (var chunk in block.BlockContent)
                        {
                            CheckStatus = string.Format(Lang._InstallMigrateSteam.InnerCheckBlock2, FileIndex.FT, TotalCount, TotalCountToRead, block.BlockHash, chunk._startoffset.ToString("x8"), chunk._filesize.ToString("x8"));
                            TotalCount++;
                            tokenSource.Token.ThrowIfCancellationRequested();

                            stream.Position = chunk._startoffset;
                            stream.Read(ChunkBuffer = new byte[chunk._filesize], 0, (int)chunk._filesize);

                            TotalRead += chunk._filesize;

                            FileCRC = GenerateCRC(ChunkBuffer);

                            OnProgressChanged(new CheckIntegrityChanged(TotalRead, TotalSizeToRead, sw.Elapsed.TotalSeconds)
                            {
                                Message = CheckStatus
                            });

                            if (!string.Equals(FileCRC, chunk._filecrc32))
                                BrokenChunk.Add(chunk);
                        }
                    }

                    if (BrokenChunk.Count > 0)
                        BrokenBlock.Add(new XMFDictionaryClasses.XMFBlockList
                        {
                            BlockHash = block.BlockHash,
                            BlockSize = block.BlockSize,
                            BlockContent = BrokenChunk
                        });
                }
            }

            if (BrokenBlock.Count > 0)
            {
                BrokenFileIndexesProperty.Add(new FilePropertiesRemote
                {
                    N = FileIndex.N,
                    RN = FileIndex.RN,
                    CRC = FileIndex.CRC,
                    FT = FileIndex.FT,
                    S = FileIndex.S,
                    M = FileIndex.M,
                    BlkC = BrokenBlock
                });
            }
        }

        private string GenerateCRC(Stream input)
        {
            FileCRCTool = new Crc32Algorithm();
            int read = 0;

            if (input.Length == 0) return "00000000";

            using (input)
            {
                while ((read = input.Read(buffer)) >= buffer.Length)
                {
                    tokenSource.Token.ThrowIfCancellationRequested();
                    FileCRCTool.TransformBlock(buffer, 0, buffer.Length, buffer, 0);
                    TotalRead += read;
                    OnProgressChanged(new CheckIntegrityChanged(TotalRead, TotalSizeToRead, sw.Elapsed.TotalSeconds)
                    {
                        Message = CheckStatus
                    });
                }

                FileCRCTool.TransformFinalBlock(buffer, 0, read);
                TotalRead += read;

                OnProgressChanged(new CheckIntegrityChanged(TotalRead, TotalSizeToRead, sw.Elapsed.TotalSeconds)
                {
                    Message = CheckStatus
                });
            }

            return BytesToHex(FileCRCTool.Hash).ToLower();
        }

        private string GenerateCRC(in byte[] input) => BytesToHex(new Crc32Algorithm().ComputeHash(input)).ToLower();

        private void HttpAdapter(object sender, Http.DownloadEvent e)
        {
            OnProgressChanged(new CheckIntegrityChanged(e.SizeDownloaded, e.SizeToBeDownloaded, sw.Elapsed.TotalSeconds)
            {
                Message = CheckStatus
            });
        }

        protected virtual void OnProgressChanged(CheckIntegrityChanged e) => ProgressChanged?.Invoke(this, e);
    }

    public class CheckIntegrityChanged : EventArgs
    {
        public CheckIntegrityChanged(long totalReceived, long fileSize, double totalSecond)
        {
            BytesReceived = totalReceived;
            TotalBytesToReceive = fileSize;
            CurrentSpeed = (long)(totalReceived / totalSecond);
        }
        public string Message { get; set; }
        public long CurrentReceived { get; set; }
        public long BytesReceived { get; private set; }
        public long TotalBytesToReceive { get; private set; }
        public float ProgressPercentage => ((float)BytesReceived / (float)TotalBytesToReceive) * 100;
        public long CurrentSpeed { get; private set; }
        public TimeSpan TimeLeft => TimeSpan.FromSeconds((TotalBytesToReceive - BytesReceived) / CurrentSpeed);
    }
}
