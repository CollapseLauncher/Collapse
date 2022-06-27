using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hi3Helper.Http;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    internal partial class InstallManagement : Http
    {
        public event EventHandler<ConvertProgress> ProgressChanged;

        List<FileProperties> SourceFileManifest;

        Stopwatch ConvertSw;

        string IngredientPath;
        string SourceBaseURL;
        private void ResetSw() => ConvertSw = Stopwatch.StartNew();
        string ConvertStatus;

        public async Task StartPreparation()
        {
            List<FilePropertiesRemote> SourceFileRemote;
            ResetSw();
            ConvertStatus = Lang._InstallMgmt.PreparePatchTitle;
            IngredientPath = SourceProfile.ActualGameDataLocation + "_Ingredients";
            SourceBaseURL = string.Format(DecompressedRemotePath, $"BH3_v{PatchProp.SourceVer}_{PatchProp.ZipHash}");
            DecompressedRemotePath = null;

            InstallStatus = new InstallManagementStatus
            {
                IsIndetermined = false,
                IsPerFile = false,
                StatusTitle = ConvertStatus
            };

            UpdateStatus(InstallStatus);

            using (MemoryStream buffer = new MemoryStream())
            {
                DownloadProgress += FetchIngredientsAPI_Progress;
                await DownloadStream(SourceBaseURL + "index.json", buffer, Token);
                DownloadProgress -= FetchIngredientsAPI_Progress;
                SourceFileRemote = JsonConvert.DeserializeObject<List<FilePropertiesRemote>>(Encoding.UTF8.GetString(buffer.ToArray()));
            }

            SourceFileManifest = BuildManifest(SourceFileRemote);
            PrepareIngredients(SourceFileManifest);
        }

        long MakeIngredientsRead = 0;
        long MakeIngredientsTotalSize = 0;
        private void PrepareIngredients(List<FileProperties> FileManifest)
        {
            ResetSw();
            MakeIngredientsRead = 0;
            MakeIngredientsTotalSize = FileManifest.Sum(x => x.FileSize);

            int i = 0;
            int j = FileManifest.Count;

            string InputPath;
            string OutputPath;

            foreach (FileProperties Entry in FileManifest)
            {
                i++;
                InstallStatus.StatusTitle = string.Format("{0}: {1}", Lang._Misc.MovingFile, string.Format(Lang._Misc.PerFromTo, i, j));
                UpdateStatus(InstallStatus);
                DownloadLocalSize = MakeIngredientsRead;
                DownloadRemoteSize = MakeIngredientsTotalSize;
                UpdateProgress(InstallProgress = new InstallManagementProgress(
                    DownloadLocalSize, DownloadRemoteSize,
                    DownloadLocalPerFileSize, DownloadRemotePerFileSize,
                    DownloadStopwatch.Elapsed.TotalSeconds, false));
                InputPath = Path.Combine(SourceProfile.ActualGameDataLocation, Entry.FileName);
                OutputPath = Path.Combine(IngredientPath, Entry.FileName);

                if (!Directory.Exists(Path.GetDirectoryName(OutputPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

                Token.ThrowIfCancellationRequested();

                if (File.Exists(InputPath))
                {
                    File.Move(InputPath, OutputPath, true);
                    MakeIngredientsRead += Entry.FileSize;
                }
                else
                {
                    MakeIngredientsRead += Entry.FileSize;
                }
            }
        }

        private async Task<List<FileProperties>> VerifyIngredients(List<FileProperties> FileManifest, string GamePath)
        {
            ResetSw();
            List<FileProperties> BrokenManifest = new List<FileProperties>();
            long CurRead = 0;
            long TotalSize = FileManifest.Sum(x => x.FileSize);
            string LocalHash;
            string OutputPath;

            int i = 0;
            int j = FileManifest.Count;

            foreach (FileProperties Entry in FileManifest)
            {
                i++;
                OutputPath = Path.Combine(GamePath, Entry.FileName);

                InstallStatus.StatusTitle = string.Format("{0}: {1}", Lang._Misc.CheckingFile, string.Format(Lang._Misc.PerFromTo, i, j));
                UpdateStatus(InstallStatus);
                DownloadLocalSize = CurRead;
                DownloadRemoteSize = TotalSize;
                UpdateProgress(InstallProgress = new InstallManagementProgress(
                    DownloadLocalSize, DownloadRemoteSize,
                    DownloadLocalPerFileSize, DownloadRemotePerFileSize,
                    DownloadStopwatch.Elapsed.TotalSeconds, false));

                if (File.Exists(OutputPath))
                {
                    using (FileStream fs = new FileStream(OutputPath, FileMode.Open, FileAccess.Read))
                    {
                        Token.ThrowIfCancellationRequested();
                        LocalHash = Entry.DataType != FileType.Blocks ?
                            await BytesToCRC32SimpleAsync(fs) :
                            await CreateMD5Async(fs);

                        if (LocalHash.ToLower() != Entry.CurrCRC)
                            BrokenManifest.Add(Entry);
                    }
                }
                else
                    BrokenManifest.Add(Entry);
                CurRead += Entry.FileSize;
            }

            return BrokenManifest;
        }

        private List<FileProperties> BuildManifest(List<FilePropertiesRemote> FileRemote)
        {
            List<FileProperties> _out = new List<FileProperties>();

            foreach (FilePropertiesRemote Entry in FileRemote)
            {
                switch (Entry.FT)
                {
                    case FileType.Generic:
                        {
                            _out.Add(new FileProperties
                            {
                                FileName = Entry.N,
                                FileSize = Entry.S,
                                CurrCRC = Entry.CRC,
                                DataType = FileType.Generic
                            });
                        }
                        break;
                    case FileType.Blocks:
                        {
                            _out.AddRange(BuildBlockManifest(Entry.BlkC, Entry.N));
                        }
                        break;
                }
            }

            return _out;
        }

        private List<FileProperties> BuildBlockManifest(List<XMFDictionaryClasses.XMFBlockList> BlockC, string BaseName)
        {
            string Name;
            List<FileProperties> _out = new List<FileProperties>();

            foreach (XMFDictionaryClasses.XMFBlockList Block in BlockC)
            {
                Name = BaseName + "/" + Block.BlockHash + ".wmv";
                _out.Add(new FileProperties
                {
                    FileName = Name,
                    FileSize = Block.BlockSize,
                    CurrCRC = Block.BlockHash
                });
            }

            return _out;
        }

        long RepairRead = 0;
        long RepairTotalSize = 0;
        private async Task RepairIngredients(List<FileProperties> BrokenFile, string GamePath)
        {
            if (BrokenFile.Count == 0) return;

            ResetSw();
            string OutputPath;
            string InputURL;
            RepairTotalSize = BrokenFile.Sum(x => x.FileSize);
            int i = 0;
            int j = BrokenFile.Count;

            foreach (FileProperties Entry in BrokenFile)
            {
                i++;
                InstallStatus.StatusTitle = string.Format("{0}: {1}", Lang._Misc.RepairingFile, string.Format(Lang._Misc.PerFromTo, i, j));
                InstallStatus.IsPerFile = true;
                UpdateStatus(InstallStatus);

                Token.ThrowIfCancellationRequested();
                OutputPath = Path.Combine(GamePath, Entry.FileName);
                InputURL = SourceBaseURL + Entry.FileName;

                if (!Directory.Exists(Path.GetDirectoryName(OutputPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

                if (File.Exists(OutputPath))
                    File.Delete(OutputPath);

                DownloadProgress += RepairIngredients_Progress;
                if (Entry.FileSize >= 20 << 20)
                {
                    await DownloadMultisession(InputURL, OutputPath, true, this.DownloadThread, Token);
                    await MergeMultisession(OutputPath, this.DownloadThread, Token);
                }
                else
                    await DownloadStream(InputURL, new FileStream(OutputPath, FileMode.Create, FileAccess.Write), Token);
                DownloadProgress -= RepairIngredients_Progress;
            }
        }

        private void RepairIngredients_Progress(object sender, DownloadEvent e)
        {
            if (e.State != MultisessionState.Merging)
                RepairRead += e.Read;

            DownloadLocalSize = RepairRead;
            DownloadRemoteSize = RepairTotalSize;
            UpdateProgress(InstallProgress = new InstallManagementProgress(
                DownloadLocalSize, DownloadRemoteSize,
                e.SizeDownloaded, e.SizeToBeDownloaded,
                DownloadStopwatch.Elapsed.TotalSeconds, true));
        }

        private void FetchIngredientsAPI_Progress(object sender, DownloadEvent e)
        {
            DownloadLocalSize = e.SizeDownloaded;
            DownloadRemoteSize = e.SizeToBeDownloaded;
            UpdateProgress(InstallProgress = new InstallManagementProgress(
                DownloadLocalSize, DownloadRemoteSize,
                DownloadLocalPerFileSize, DownloadRemotePerFileSize,
                DownloadStopwatch.Elapsed.TotalSeconds, false));
        }

        FileSystemWatcher ConvertFsWatcher;

        long ConvertRead = 0;
        long ConvertTotalSize = 0;
        public void StartConversion()
        {
            ResetSw();

            try
            {
                string OutputPath = SourceProfile.ActualGameDataLocation;

                ConvertTotalSize = SourceFileManifest.Sum(x => x.FileSize);

                ConvertFsWatcher = new FileSystemWatcher()
                {
                    Path = OutputPath,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                                 | NotifyFilters.Size,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };
                InstallStatus.StatusTitle = Lang._Misc.ApplyingPatch;
                InstallStatus.IsPerFile = false;
                UpdateStatus(InstallStatus);

                ConvertFsWatcher.Created += ConvertFsWatcher_Created;

                new HPatchUtil().HPatchDir(IngredientPath, PatchProp.PatchPath, OutputPath);
                TryDirectoryDelete(IngredientPath, true);
                TryFileDelete(PatchProp.PatchPath);

                ConvertFsWatcher.Created -= ConvertFsWatcher_Created;
            }
            catch (Exception ex)
            {
                try
                {
                    RevertBackIngredients(SourceFileManifest);
                }
                catch (Exception exf)
                {
                    LogWriteLine($"Conversion process has failed and sorry, the files can't be reverted back :(\r\nInner Exception: {ex}\r\nReverting Exception: {exf}", LogType.Error, true);
                    throw new Exception($"Conversion process has failed and sorry, the files can't be reverted back :(\r\nInner Exception: {ex}\r\nReverting Exception: {exf}", new Exception($"Inner exception: {ex}", ex));
                }
                LogWriteLine($"Conversion process has failed! But don't worry, the files have been reverted :D\r\n{ex}", LogType.Error, true);
                throw new Exception($"Conversion process has failed! But don't worry, the file have been reverted :D\r\n{ex}", ex);
            }
        }

        private void TryFileDelete(string Input)
        {
            try
            {
                File.Delete(Input);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while trying to delete file \"{Input}\"\r\n{ex}");
            }
        }

        private void TryDirectoryDelete(string Input, bool Recursive)
        {
            try
            {
                Directory.Delete(Input, Recursive);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while trying to delete directory \"{Input}\"{(Recursive ? " recursively!" : "!")}\r\n{ex}");
            }
        }

        private void RevertBackIngredients(List<FileProperties> FileManifest)
        {
            string InputPath, OutputPath;
            foreach (FileProperties Entry in FileManifest)
            {
                InputPath = Path.Combine(SourceProfile.ActualGameDataLocation, Entry.FileName);
                OutputPath = Path.Combine(SourceProfile.ActualGameDataLocation + "_Ingredients", Entry.FileName);

                if (!Directory.Exists(Path.GetDirectoryName(InputPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(InputPath));

                if (File.Exists(InputPath))
                    File.Move(OutputPath, InputPath, true);
            }
        }

        string lastName = null;
        private void ConvertFsWatcher_Created(object sender, FileSystemEventArgs e)
        {
            if (!Directory.Exists(e.FullPath))
            {
                DownloadLocalSize = ConvertRead;
                DownloadRemoteSize = ConvertTotalSize;
                UpdateProgress(InstallProgress = new InstallManagementProgress(
                    DownloadLocalSize, DownloadRemoteSize,
                    DownloadLocalPerFileSize, DownloadRemotePerFileSize,
                    DownloadStopwatch.Elapsed.TotalSeconds, false));
                if (lastName != null)
                    ConvertRead += new FileInfo(lastName).Length;
                lastName = e.FullPath;
            }
        }

        private Stopwatch InnerRefreshSw = Stopwatch.StartNew();
        private void ResetInnerRefreshSw() => InnerRefreshSw = Stopwatch.StartNew();
        public void UpdateProgress(long StartSize, long EndSize, int StartCount, int EndCount,
                TimeSpan TimeSpan, string StatusMsg = "", string DetailMsg = "", bool UseCountUnit = false)
        {
            double Lastms = InnerRefreshSw.Elapsed.TotalMilliseconds;
            if (Lastms >= 33)
            {
                ProgressChanged?.Invoke(this, new ConvertProgress(StartSize, EndSize, StartCount, EndCount,
                    TimeSpan, StatusMsg, DetailMsg, UseCountUnit));
                ResetInnerRefreshSw();
            }
        }

        public class ConvertProgress
        {
            public ConvertProgress(long StartSize, long EndSize, int StartCount, int EndCount,
                TimeSpan TimeSpan, string StatusMsg = "", string DetailMsg = "", bool UseCountUnit = false)
            {
                this.StartSize = StartSize;
                this.EndSize = EndSize;
                this.StartCount = StartCount;
                this.EndCount = EndCount;
                this.UseCountUnit = UseCountUnit;
                this._TimeSecond = TimeSpan.TotalSeconds;
                this._StatusMsg = StatusMsg;
                this._DetailMsg = DetailMsg;
            }

            private double _TimeSecond = 0f;
            private string _StatusMsg = "";
            private string _DetailMsg = "";
            public bool UseCountUnit { get; private set; }
            public long StartSize { get; private set; }
            public long EndSize { get; private set; }
            public int StartCount { get; private set; }
            public int EndCount { get; private set; }
            public double Percentage => UseCountUnit ? Math.Round((StartCount / (double)EndCount) * 100, 2) :
                                                       Math.Round((StartSize / (double)EndSize) * 100, 2);
            public long ProgressSpeed => (long)(StartSize / _TimeSecond);
            public TimeSpan RemainingTime => UseCountUnit ? TimeSpan.FromSeconds(0f) :
                                                            TimeSpan.FromSeconds((EndSize - StartSize) / Unzeroed(ProgressSpeed));
            private double Unzeroed(double i) => i == 0 ? 1 : i;
            public string ProgressStatus => _StatusMsg;
            public string ProgressDetail => string.Format(
                            "[{0}] ({1})\r\n{2}...",
                            UseCountUnit ? string.Format(Lang._Misc.PerFromTo, StartCount, EndCount) :
                                           string.Format(Lang._Misc.PerFromTo, SummarizeSizeSimple(StartSize), SummarizeSizeSimple(EndSize)),
                            UseCountUnit ? $"{Percentage}%" :
                                           string.Format("{0}% {1} - {2}",
                                                         Percentage,
                                                         string.Format(Lang._Misc.SpeedPerSec, SummarizeSizeSimple(ProgressSpeed)),
                                                         string.Format(Lang._Misc.TimeRemainHMSFormat, RemainingTime)),
                            _DetailMsg
                            );
        }
    }
}
