﻿using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public class GameConversionManagement : IDisposable
    {
        public event EventHandler<ConvertProgress> ProgressChanged;

        private PresetConfigV2 SourceProfile, TargetProfile;
        private List<FileProperties> SourceFileManifest;
        private List<FileProperties> TargetFileManifest;
        private Http _http;

        string BaseURL;
        string GameVersion;
        string CookbookPath;
        Stopwatch ConvertSw;
        CancellationToken Token = new CancellationToken();
        private void ResetSw() => ConvertSw = Stopwatch.StartNew();
        string ConvertStatus, ConvertDetail;
        byte DownloadThread;

        public GameConversionManagement(PresetConfigV2 SourceProfile, PresetConfigV2 TargetProfile,
            string BaseURL, string GameVersion, string CookbookPath, CancellationToken Token = new CancellationToken())
        {
            this._http = new Http();
            this.SourceProfile = SourceProfile;
            this.TargetProfile = TargetProfile;
            this.BaseURL = BaseURL;
            this.GameVersion = GameVersion;
            this.DownloadThread = (byte)GetAppConfigValue("DownloadThread").ToInt();
            this.CookbookPath = CookbookPath;
            this.Token = Token;
        }

        ~GameConversionManagement() => Dispose();

        public void Dispose() => this._http?.Dispose();

        public async Task StartPreparation()
        {
            List<FilePropertiesRemote> SourceFileRemote;
            List<FilePropertiesRemote> TargetFileRemote;
            ConvertSw = Stopwatch.StartNew();
            ConvertStatus = Lang._InstallConvert.Step3Title;

            string IngredientsPath = TargetProfile.ActualGameDataLocation + "_Ingredients";
            string URL = "";

            using (MemoryStream buffer = new MemoryStream())
            {
                string.Format(AppGameRepairIndexURLPrefix, SourceProfile.ProfileName, this.GameVersion);
                ConvertDetail = Lang._InstallConvert.Step2Subtitle;
                this._http.DownloadProgress += FetchIngredientsAPI_Progress;
                await this._http.Download(URL, buffer, null, null, Token);
                this._http.DownloadProgress -= FetchIngredientsAPI_Progress;
                buffer.Position = 0;
                SourceFileRemote = (List<FilePropertiesRemote>)JsonSerializer.Deserialize(buffer, typeof(List<FilePropertiesRemote>), L_FilePropertiesRemoteContext.Default);
            }
            using (MemoryStream buffer = new MemoryStream())
            {
                URL = string.Format(AppGameRepairIndexURLPrefix, TargetProfile.ProfileName, this.GameVersion);
                ConvertDetail = Lang._InstallConvert.Step2Subtitle;
                this._http.DownloadProgress += FetchIngredientsAPI_Progress;
                await this._http.Download(URL, buffer, null, null, Token);
                this._http.DownloadProgress -= FetchIngredientsAPI_Progress;
                buffer.Position = 0;
                TargetFileRemote = (List<FilePropertiesRemote>)JsonSerializer.Deserialize(buffer, typeof(List<FilePropertiesRemote>), L_FilePropertiesRemoteContext.Default);
            }

            SourceFileManifest = BuildManifest(SourceFileRemote);
            TargetFileManifest = BuildManifest(TargetFileRemote);
            await Task.Run(() => PrepareIngredients(SourceFileManifest));
            await RepairIngredients(await VerifyIngredients(SourceFileManifest, IngredientsPath), IngredientsPath);
        }

        long MakeIngredientsRead = 0;
        long MakeIngredientsTotalSize = 0;
        private void PrepareIngredients(List<FileProperties> FileManifest)
        {
            ResetSw();
            MakeIngredientsRead = 0;
            MakeIngredientsTotalSize = FileManifest.Sum(x => x.FileSize);

            string InputPath;
            string OutputPath;

            ConvertStatus = Lang._InstallConvert.Step3Title;

            foreach (FileProperties Entry in FileManifest)
            {
                InputPath = Path.Combine(SourceProfile.ActualGameDataLocation, Entry.FileName);
                OutputPath = Path.Combine(TargetProfile.ActualGameDataLocation + "_Ingredients", Entry.FileName);

                if (!Directory.Exists(Path.GetDirectoryName(OutputPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

                if (File.Exists(InputPath))
                {
                    ConvertDetail = $"Moving: {Entry.FileName} ({Entry.FileSizeStr})";
                    MakeIngredientsRead += Entry.FileSize;
                    File.Move(InputPath, OutputPath, true);
                    UpdateProgress(MakeIngredientsRead, MakeIngredientsTotalSize, 1, 1, ConvertSw.Elapsed, ConvertStatus, ConvertDetail);
                }
                else
                {
                    MakeIngredientsRead += Entry.FileSize;
                    UpdateProgress(MakeIngredientsRead, MakeIngredientsTotalSize, 1, 1, ConvertSw.Elapsed, ConvertStatus, ConvertDetail);
                }
            }
        }

        public async Task PostConversionVerify()
        {
            string TargetPath = TargetProfile.ActualGameDataLocation;
            await RepairIngredients(await VerifyIngredients(TargetFileManifest, TargetPath), TargetPath);
        }

        private async Task<List<FileProperties>> VerifyIngredients(List<FileProperties> FileManifest, string GamePath)
        {
            ResetSw();
            List<FileProperties> BrokenManifest = new List<FileProperties>();
            long CurRead = 0;
            long TotalSize = FileManifest.Sum(x => x.FileSize);
            string LocalHash;
            string OutputPath;

            ConvertStatus = Lang._InstallConvert.Step3Title2;
            foreach (FileProperties Entry in FileManifest)
            {
                OutputPath = Path.Combine(GamePath, Entry.FileName);
                ConvertDetail = string.Format("{0}: {1}", Lang._Misc.CheckingFile, string.Format(Lang._Misc.PerFromTo, Entry.FileName, Entry.FileSizeStr));
                UpdateProgress(CurRead, TotalSize, 1, 1, ConvertSw.Elapsed, ConvertStatus, ConvertDetail);
                if (File.Exists(OutputPath))
                {
                    using (FileStream fs = new FileStream(OutputPath, FileMode.Open, FileAccess.Read))
                    {
                        await Task.Run(() =>
                        {
                            LocalHash = Entry.DataType != FileType.Blocks ?
                                BytesToCRC32Simple(fs) :
                                CreateMD5(fs);

                            Token.ThrowIfCancellationRequested();
                            if (LocalHash.ToLower() != Entry.CurrCRC)
                                BrokenManifest.Add(Entry);
                        }, Token);
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

        private List<FileProperties> BuildBlockManifest(List<XMFBlockList> BlockC, string BaseName)
        {
            string Name;
            List<FileProperties> _out = new List<FileProperties>();

            foreach (XMFBlockList Block in BlockC)
            {
                Name = BaseName + '/' + Block.BlockHash + ".wmv";
                _out.Add(new FileProperties
                {
                    FileName = Name,
                    FileSize = Block.BlockSize,
                    CurrCRC = Block.BlockHash
                });
            }

            return _out;
        }

        public string CleanUpPreviousChunkFiles(string CookbookPath)
        {
            foreach (string Path in Directory.EnumerateFiles(Path.GetDirectoryName(CookbookPath),
                string.Format("{0}*", Path.GetFileNameWithoutExtension(Path.GetDirectoryName(CookbookPath)))))
            {
                try
                {
                    FileInfo a = new FileInfo(Path);
                    a.IsReadOnly = false;
                    a.Delete();
                }
                catch { }
            }

            return CookbookPath;
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

            ConvertStatus = Lang._InstallConvert.Step3Title1;
            foreach (FileProperties Entry in BrokenFile)
            {
                Token.ThrowIfCancellationRequested();
                OutputPath = Path.Combine(GamePath, Entry.FileName);
                InputURL = BaseURL + Entry.FileName;

                ConvertDetail = string.Format("{0}: {1}", Lang._Misc.Downloading, string.Format(Lang._Misc.PerFromTo, Entry.FileName, Entry.FileSizeStr));
                if (!Directory.Exists(Path.GetDirectoryName(OutputPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

                if (File.Exists(OutputPath))
                    File.Delete(OutputPath);

                _http.DownloadProgress += RepairIngredients_Progress;
                if (Entry.FileSize >= 20 << 20)
                {
                    await _http.Download(InputURL, OutputPath, DownloadThread, true, Token);
                    await _http.Merge();
                }
                else
                    await _http.Download(InputURL, new FileStream(OutputPath, FileMode.Create, FileAccess.Write), null, null, Token);
                _http.DownloadProgress -= RepairIngredients_Progress;
            }
        }

        private void RepairIngredients_Progress(object sender, DownloadEvent e)
        {
            if (_http.DownloadState != DownloadState.Merging)
                RepairRead += e.Read;

            UpdateProgress(RepairRead, RepairTotalSize, 1, 1, ConvertSw.Elapsed,
                ConvertStatus, ConvertDetail);
        }

        private void FetchIngredientsAPI_Progress(object sender, DownloadEvent e)
        {
            UpdateProgress(e.SizeDownloaded, e.SizeToBeDownloaded, 1, 1, ConvertSw.Elapsed,
                ConvertStatus, ConvertDetail);
        }

        FileSystemWatcher ConvertFsWatcher;

        long ConvertRead = 0;
        long ConvertTotalSize = 0;
        public async Task StartConversion()
        {
            ResetSw();
            string IngredientsPath = TargetProfile.ActualGameDataLocation + "_Ingredients";
            string OutputPath = TargetProfile.ActualGameDataLocation;

            try
            {
                if (Directory.Exists(OutputPath))
                    TryDirectoryDelete(OutputPath, true);

                Directory.CreateDirectory(OutputPath);
                ConvertTotalSize = TargetFileManifest.Sum(x => x.FileSize);

                ConvertFsWatcher = new FileSystemWatcher()
                {
                    Path = OutputPath,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                                 | NotifyFilters.Size,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                ConvertFsWatcher.Created += ConvertFsWatcher_Created;

                await Task.Run(() => new HPatchUtil().HPatchDir(IngredientsPath, CookbookPath, OutputPath), Token);
                TryDirectoryDelete(IngredientsPath, true);
                TryFileDelete(CookbookPath);
                MoveMiscSourceFiles(SourceProfile.ActualGameDataLocation, OutputPath);
                TryDirectoryDelete(SourceProfile.ActualGameDataLocation, true);

                ConvertFsWatcher.Created -= ConvertFsWatcher_Created;
            }
            catch (Exception ex)
            {
                try
                {
                    RevertBackIngredients(SourceFileManifest, IngredientsPath, OutputPath);
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

        private void RevertBackIngredients(List<FileProperties> FileManifest, string IngredientPath, string Output)
        {
            string InputPath, OutputPath;
            foreach (FileProperties Entry in FileManifest)
            {
                OutputPath = Path.Combine(SourceProfile.ActualGameDataLocation, Entry.FileName);
                InputPath = Path.Combine(TargetProfile.ActualGameDataLocation + "_Ingredients", Entry.FileName);

                if (!Directory.Exists(Path.GetDirectoryName(OutputPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

                if (File.Exists(InputPath))
                    File.Move(InputPath, OutputPath, true);
            }

            Directory.Delete(IngredientPath, true);
            Directory.Delete(Output, true);
        }

        private void MoveMiscSourceFiles(string InputPath, string OutputPath)
        {
            string OutputFile;
            IEnumerable<string> Files = Directory.EnumerateFiles(InputPath, "*", SearchOption.AllDirectories);
            foreach (string _Entry in Files)
            {
                OutputFile = Path.Combine(OutputPath, _Entry.Substring(InputPath.Length + 1));
                if (!File.Exists(OutputFile))
                {
                    if (!Directory.Exists(Path.GetDirectoryName(OutputFile)))
                        Directory.CreateDirectory(Path.GetDirectoryName(OutputFile));
                    File.Move(_Entry, OutputFile);
                }
            }
        }

        string lastName = null;
        private void ConvertFsWatcher_Created(object sender, FileSystemEventArgs e)
        {
            if (!Directory.Exists(e.FullPath))
            {
                ConvertDetail = string.Format(Lang._Misc.Converting, e.Name);
                UpdateProgress(ConvertRead, ConvertTotalSize, 1, 1, ConvertSw.Elapsed, ConvertStatus, ConvertDetail);
                if (lastName != null)
                    ConvertRead += new FileInfo(lastName).Length;
                lastName = e.FullPath;
            }
        }

        public void UpdateProgress(long StartSize, long EndSize, int StartCount, int EndCount,
                TimeSpan TimeSpan, string StatusMsg = "", string DetailMsg = "", bool UseCountUnit = false)
        {
            ProgressChanged?.Invoke(this, new ConvertProgress(StartSize, EndSize, StartCount, EndCount,
                TimeSpan, StatusMsg, DetailMsg, UseCountUnit));
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
