using Force.Crc32;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Http;
using static CollapseLauncher.Pages.RepairData;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public static class RepairData
    {
        public static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        public static List<FilePropertiesRemote> FileIndexesProperty = new List<FilePropertiesRemote>();
        public static List<FilePropertiesRemote> BrokenFileIndexesProperty = new List<FilePropertiesRemote>();
    }

    public sealed partial class RepairPage : Page
    {
        public ObservableCollection<FileProperties> NeedRepairListUI = new ObservableCollection<FileProperties>();

        Http http = new Http();
        MemoryStream memBuffer;
        byte[] buffer = new byte[0x400000];

        long TotalIndexedSize,
             TotalCurrentReadSize,
             SingleSize,
             SingleCurrentReadSize,
             BlockSize,
             BlockCurrentReadSize,
             BrokenFilesSize;

        int TotalIndexedCount,
            TotalCurrentReadCount;

        string CurrentCheckName = "";

        private async void StartGameCheck(object sender, RoutedEventArgs e)
        {
            RepairStatus.Text = Lang._GameRepairPage.Status2;
            NeedRepairListUI.Clear();
            cancellationTokenSource = new CancellationTokenSource();
            FileIndexesProperty = new List<FilePropertiesRemote>();
            BrokenFileIndexesProperty = new List<FilePropertiesRemote>();
            CheckFilesBtn.IsEnabled = false;
            CancelBtn.IsEnabled = true;

            DispatcherQueue.TryEnqueue(() =>
            {
                RepairPerFileStatus.Text = Lang._GameRepairPage.StatusNone;
                RepairTotalStatus.Text = Lang._GameRepairPage.StatusNone;
                RepairPerFileProgressBar.Value = 0;
                RepairTotalProgressBar.Value = 0;
            });

            try
            {
                string indexURL = string.Format(CurrentRegion.ZipFileURL + "index.json", Path.GetFileNameWithoutExtension(regionResourceProp.data.game.latest.path));
                using (memBuffer = new MemoryStream())
                {
                    http.DownloadProgress += DataFetchingProgress;
                    await http.DownloadStream(indexURL, memBuffer, cancellationTokenSource.Token);
                    http.DownloadProgress -= DataFetchingProgress;
                    FileIndexesProperty = JsonConvert.DeserializeObject<List<FilePropertiesRemote>>(Encoding.UTF8.GetString(memBuffer.ToArray()));
                }

                await Task.Run(() => CheckGameFiles());
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Game Check Cancelled!");
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
            sw.Stop();
        }

        Stopwatch sw = new Stopwatch();
        string FileCRC, FilePath, FileURL, GameBasePath, GameBaseURL;
        FileInfo FileInfoProp;
        int BrokenFilesCount;
        private void CheckGameFiles()
        {
            sw = Stopwatch.StartNew();
            TotalCurrentReadSize = 0;
            TotalCurrentReadCount = 0;
            BlockCurrentReadSize = 0;
            BrokenFilesCount = 0;
            BlockSize = FileIndexesProperty.Where(x => x.BlkC != null).Sum(x => x.BlkC.Sum(x => x.BlockSize));
            TotalIndexedSize = FileIndexesProperty.Sum(x => x.S) + BlockSize;
            TotalIndexedCount = FileIndexesProperty.Count + FileIndexesProperty.Where(x => x.BlkC != null).Sum(y => y.BlkC.Sum(z => z.BlockContent.Count));

            DispatcherQueue.TryEnqueue(() => RepairDataTableGrid.Visibility = Visibility.Visible);

            foreach (FilePropertiesRemote Index in FileIndexesProperty)
            {
                TotalCurrentReadCount++;
                FilePath = Path.Combine(GameBasePath, NormalizePath(Index.N));
                FileInfoProp = new FileInfo(FilePath);

                CurrentCheckName = Index.N;

                switch (Index.FT)
                {
                    case FileType.Generic:
                    case FileType.Audio:
                        if (Index.S > 0)
                        {
                            if (FileInfoProp.Exists)
                                CheckGenericAudioCRC(Index);
                            else
                            {
                                DispatcherQueue.TryEnqueue(() => NeedRepairListUI.Add(new FileProperties
                                {
                                    FileName = Path.GetFileName(Index.N),
                                    DataType = Index.FT,
                                    FileSource = Path.GetDirectoryName(Index.N),
                                    FileSize = Index.S,
                                    ExpctCRC = Index.CRC,
                                    CurrCRC = "Missing"
                                }));

                                BrokenFileIndexesProperty.Add(Index);

                                BrokenFilesSize += Index.S;
                                BrokenFilesCount++;
                            }
                        }
                        break;
                    case FileType.Blocks:
                        CheckChunksCRC(Index);
                        break;
                }
            }

            sw.Stop();
            SummarizeResult();
        }

        FileInfo BlockFileInfo;
        FileStream BlockFileStream;
        byte[] ChunkBuffer;
        private void CheckChunksCRC(FilePropertiesRemote input)
        {
            List<XMFDictionaryClasses.XMFBlockList> BrokenBlock = new List<XMFDictionaryClasses.XMFBlockList>();
            List<XMFDictionaryClasses.XMFFileProperty> BrokenChunk = new List<XMFDictionaryClasses.XMFFileProperty>();
            string BlockBasePath = FilePath;
            foreach (var block in input.BlkC)
            {
                FilePath = Path.Combine(BlockBasePath, block.BlockHash + ".wmv");
                BlockFileInfo = new FileInfo(FilePath);

                if (!BlockFileInfo.Exists)
                {
                    TotalCurrentReadCount += block.BlockContent.Count;
                    DispatcherQueue.TryEnqueue(() => NeedRepairListUI.Add(new FileProperties
                    {
                        FileName = block.BlockHash,
                        DataType = input.FT,
                        FileSource = Path.GetDirectoryName(input.N),
                        FileSize = block.BlockSize,
                        ExpctCRC = "",
                        CurrCRC = "Missing"
                    }));
                    BrokenBlock.Add(new XMFDictionaryClasses.XMFBlockList
                    {
                        BlockHash = block.BlockHash,
                        BlockSize = block.BlockSize,
                        BlockExistingSize = 0,
                        BlockMissing = true,
                        BlockContent = BrokenChunk
                    });

                    BrokenFilesSize += block.BlockSize;

                    BrokenFilesCount++;

                    BlockCurrentReadSize += block.BlockSize;

                    SingleCurrentReadSize = block.BlockSize;
                    TotalCurrentReadSize += block.BlockSize;
                }
                else if (BlockFileInfo.Length != block.BlockSize)
                {
                    TotalCurrentReadCount += block.BlockContent.Count;
                    DispatcherQueue.TryEnqueue(() => NeedRepairListUI.Add(new FileProperties
                    {
                        FileName = block.BlockHash,
                        DataType = input.FT,
                        FileSource = Path.GetDirectoryName(input.N),
                        FileSize = block.BlockSize,
                        ExpctCRC = "",
                        CurrCRC = "Size < Actual"
                    }));
                    BrokenBlock.Add(new XMFDictionaryClasses.XMFBlockList
                    {
                        BlockHash = block.BlockHash,
                        BlockSize = block.BlockSize,
                        BlockExistingSize = BlockFileInfo.Length,
                        BlockContent = BrokenChunk
                    });

                    BrokenFilesCount++;

                    BrokenFilesSize += block.BlockSize;

                    BlockCurrentReadSize += block.BlockSize;

                    SingleCurrentReadSize = block.BlockSize;
                    TotalCurrentReadSize += block.BlockSize;
                }
                else
                {
                    using (BlockFileStream = BlockFileInfo.OpenRead())
                    {
                        SingleCurrentReadSize = 0;
                        SingleSize = block.BlockSize;
                        BrokenChunk = new List<XMFDictionaryClasses.XMFFileProperty>();
                        foreach (var chunk in block.BlockContent)
                        {
                            cancellationTokenSource.Token.ThrowIfCancellationRequested();
                            TotalCurrentReadCount++;

                            CurrentCheckName = block.BlockHash;

                            BlockFileStream.Position = chunk._startoffset;
                            BlockFileStream.Read(ChunkBuffer = new byte[chunk._filesize], 0, (int)chunk._filesize);

                            FileCRC = GenerateCRC(ChunkBuffer);

                            if (!string.Equals(FileCRC, chunk._filecrc32))
                            {
                                DispatcherQueue.TryEnqueue(() => NeedRepairListUI.Add(new FileProperties
                                {
                                    FileName = $"Offset: 0x{chunk._startoffset.ToString("x8")} - Size: 0x{chunk._filesize.ToString("x8")}",
                                    DataType = input.FT,
                                    FileSource = block.BlockHash,
                                    FileSize = chunk._filesize,
                                    ExpctCRC = chunk._filecrc32,
                                    CurrCRC = FileCRC
                                }));

                                BrokenChunk.Add(chunk);

                                BrokenFilesSize += chunk._filesize;
                                BrokenFilesCount++;
                            }

                            SingleCurrentReadSize += chunk._filesize;
                            TotalCurrentReadSize += chunk._filesize;
                        }

                        if (BrokenChunk.Count > 0)
                        {
                            BrokenBlock.Add(new XMFDictionaryClasses.XMFBlockList
                            {
                                BlockHash = block.BlockHash,
                                BlockSize = block.BlockSize,
                                BlockContent = BrokenChunk
                            });
                        }

                        BlockCurrentReadSize += block.BlockSize;

                        GetComputeBlockStatus();
                    }
                }
            }

            if (BrokenBlock.Count > 0)
            {
                BrokenFileIndexesProperty.Add(new FilePropertiesRemote
                {
                    N = input.N,
                    RN = input.RN,
                    CRC = input.CRC,
                    FT = input.FT,
                    S = input.S,
                    M = input.M,
                    BlkC = BrokenBlock
                });
            }
        }

        private void SummarizeResult()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (BrokenFilesCount > 0)
                {
                    RepairStatus.Text = string.Format(Lang._GameRepairPage.Status3, BrokenFilesCount, SummarizeSizeSimple(BrokenFilesSize));
                    RepairFilesBtn.Visibility = Visibility.Visible;
                    CheckFilesBtn.Visibility = Visibility.Collapsed;
                    CancelBtn.IsEnabled = false;
                    RepairFilesBtn.IsEnabled = true;
                }
                else
                {
                    RepairStatus.Text = Lang._GameRepairPage.Status4;
                    CheckFilesBtn.IsEnabled = true;
                    CancelBtn.IsEnabled = false;
                }
                RepairPerFileStatus.Text = Lang._GameRepairPage.StatusNone;
                RepairTotalStatus.Text = Lang._GameRepairPage.StatusNone;
                RepairPerFileProgressBar.Value = 0;
                RepairTotalProgressBar.Value = 0;
            });
        }

        private void CheckGenericAudioCRC(FilePropertiesRemote input)
        {
            FileCRC = GenerateCRC(new FileStream(FilePath, FileMode.Open, FileAccess.Read));
            if (!string.Equals(FileCRC, input.CRC))
            {
                DispatcherQueue.TryEnqueue(() => NeedRepairListUI.Add(new FileProperties
                {
                    FileName = Path.GetFileName(input.N),
                    DataType = input.FT,
                    FileSource = Path.GetDirectoryName(input.N),
                    FileSize = input.S,
                    ExpctCRC = input.CRC,
                    CurrCRC = FileCRC
                }));

                BrokenFileIndexesProperty.Add(input);
                BrokenFilesSize += input.S;

                BrokenFilesCount++;
            }
        }

        Crc32Algorithm crcTool;
        private string GenerateCRC(Stream input)
        {
            crcTool = new Crc32Algorithm();
            int read = 0;

            if (input.Length == 0) return "00000000";

            using (input)
            {
                SingleCurrentReadSize = 0;
                SingleSize = input.Length;
                while ((read = input.Read(buffer)) >= buffer.Length)
                {
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    crcTool.TransformBlock(buffer, 0, buffer.Length, buffer, 0);
                    TotalCurrentReadSize += read;
                    SingleCurrentReadSize += read;
                    GetComputeStatus();
                }

                crcTool.TransformFinalBlock(buffer, 0, read);
                TotalCurrentReadSize += read;
                SingleCurrentReadSize += read;
                GetComputeStatus();
            }

            return BytesToHex(crcTool.Hash).ToLower();
        }

        private string GenerateCRC(in byte[] input) => BytesToHex(new Crc32Algorithm().ComputeHash(input)).ToLower();

        Stopwatch swRefresh = Stopwatch.StartNew();
        private void GetComputeBlockStatus()
        {
            if (swRefresh.ElapsedMilliseconds >= 50)
            {
                long Speed = (long)(BlockCurrentReadSize / sw.Elapsed.TotalSeconds);
                DispatcherQueue.TryEnqueue(() =>
                {
                    RepairStatus.Text = string.Format(Lang._GameRepairPage.Status5, CurrentCheckName);
                    RepairTotalStatus.Text = string.Format(Lang._GameRepairPage.PerProgressSubtitle2, TotalCurrentReadCount, TotalIndexedCount);
                    RepairPerFileStatus.Text = string.Format(Lang._Misc.Speed, SummarizeSizeSimple(Speed));
                    RepairPerFileProgressBar.Value = GetPercentageNumber(BlockCurrentReadSize, BlockSize);
                    RepairTotalProgressBar.Value = GetPercentageNumber(TotalCurrentReadCount, TotalIndexedCount);
                });
                swRefresh = Stopwatch.StartNew();
            }
        }

        private void GetComputeStatus()
        {
            if (swRefresh.ElapsedMilliseconds >= 50)
            {
                long Speed = (long)(SingleCurrentReadSize / sw.Elapsed.TotalSeconds);
                DispatcherQueue.TryEnqueue(() =>
                {
                    RepairStatus.Text = string.Format(Lang._GameRepairPage.Status6, CurrentCheckName);
                    RepairTotalStatus.Text = string.Format(Lang._GameRepairPage.PerProgressSubtitle2, TotalCurrentReadCount, TotalIndexedCount);
                    RepairPerFileStatus.Text = string.Format(Lang._Misc.Speed, SummarizeSizeSimple(Speed));
                    RepairPerFileProgressBar.Value = GetPercentageNumber(SingleCurrentReadSize, SingleSize);
                    RepairTotalProgressBar.Value = GetPercentageNumber(TotalCurrentReadCount, TotalIndexedCount);
                });
                swRefresh = Stopwatch.StartNew();
            }
        }

        private void DataFetchingProgress(object sender, DownloadEvent e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RepairPerFileProgressBar.Value = UnInfinity(e.ProgressPercentage);
                RepairPerFileStatus.Text = string.Format(Lang._GameRepairPage.PerProgressSubtitle3, SummarizeSizeSimple(e.Speed));
            });
        }
    }
}
