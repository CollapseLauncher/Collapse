using Hi3Helper;
using Hi3Helper.Data;
// Load YSDispatch from this namespace
using Hi3Helper.EncTool;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Preset.ConfigV2Store;
using static Hi3Helper.Shared.Region.LauncherConfig;
// Load CurrentConfig from here

namespace CollapseLauncher
{
    public enum DownloadType { Update, FirstInstall, PreDownload }

    internal partial class InstallManagement : Http
    {
        public event EventHandler<InstallManagementStatus> InstallStatusChanged;
        public event EventHandler<InstallManagementProgress> InstallProgressChanged;

        private List<PkgVersionProperties> Entries = new List<PkgVersionProperties>();
        private List<PkgVersionProperties> BrokenFiles = new List<PkgVersionProperties>();

        private InstallManagementStatus InstallStatus;
        private InstallManagementProgress InstallProgress;
        private DeltaPatchProperty PatchProp;
        private PresetConfigV2 SourceProfile;
        private UIElement Content;

        private DownloadType ModeType;
        private CancellationToken Token;
        private byte DownloadThread,
                     ExtractionThread;

        private string GameDirPath = string.Empty,
                       IndexRemoteURL,
                       RepoRemoteURL,
                       DispatchKey,
                       DispatchURLPrefix,
                       GameVersionString,
                       ExecutablePrefix;

        private int DispatchServerID;

        // Debug Parameter
        private bool CanDeleteZip = true;
        private bool CanSkipVerif = false;
        private bool CanSkipExtract = false;
        private bool CanDeltaPatch = false;

        private Stopwatch DownloadStopwatch;

        public long DownloadLocalSize = 0,
                    DownloadLocalContinueSize = 0,
                    DownloadRemoteSize = 0;

        public int CountCurrentDownload = 0,
                    CountTotalToDownload = 0;

        public long DownloadLocalPerFileSize = 0,
                    DownloadRemotePerFileSize = 0;

        private List<DownloadAddressProperty> DownloadProperty;
        private GenshinDispatchHelper DispatchReader;

        public InstallManagement(UIElement Content, DownloadType downloadType, PresetConfigV2 SourceProfile,
            string GameDirPath, int downloadThread,
            int extractionThread, CancellationToken token, string DecompressedRemotePath = null,
            // These sections are for Genshin only
            string GameVerString = "", string DispatchKey = null, string DispatchURLPrefix = null, int RegionID = 0,
            string ExecutablePrefix = "BH3") : base(true, 10)
        {
            this.Content = Content;
            this.SourceProfile = SourceProfile;
            this.ModeType = downloadType;
            this.DownloadThread = (byte)downloadThread;
            this.ExtractionThread = (byte)extractionThread;
            this.Token = token;
            this.DownloadProperty = new List<DownloadAddressProperty>();
            this.GameDirPath = GameDirPath;
            this.IndexRemoteURL = string.Format(AppGameRepairIndexURLPrefix, SourceProfile.ProfileName, GameVerString);
            this.RepoRemoteURL = DecompressedRemotePath;
            this.DispatchKey = DispatchKey;
            this.GameVersionString = GameVerString;
            this.DispatchServerID = RegionID;
            this.DispatchURLPrefix = DispatchURLPrefix;
            this.ExecutablePrefix = ExecutablePrefix;

            this.SourceProfile.ActualGameDataLocation = GameDirPath;

            ApplyParameter();
        }

        private void ApplyParameter()
        {
            CanDeleteZip = !File.Exists(Path.Combine(GameDirPath, "@NoDeleteZip"));
            CanSkipVerif = File.Exists(Path.Combine(GameDirPath, "@NoVerification"));
            CanSkipExtract = File.Exists(Path.Combine(GameDirPath, "@NoExtraction"));
            CanDeltaPatch = (PatchProp = CheckDeltaPatchUpdate(GameDirPath, SourceProfile.ProfileName, GameVersionString, ModeType)) != null;
        }

        public static DeltaPatchProperty CheckDeltaPatchUpdate(string GamePath, string ProfileName, string GameVersion, DownloadType ModType)
        {
            string[] GamePaths = Directory.GetFiles(GamePath, "*.patch", SearchOption.TopDirectoryOnly);
            if (GamePaths.Length == 0) return null;

            DeltaPatchProperty Prop;

            try
            {
                Prop = new DeltaPatchProperty(GamePaths.First());
                if (Prop.ProfileName != ProfileName) return null;
                if (!(ModType == DownloadType.PreDownload || ModType == DownloadType.Update)) return null;
                if (Prop.TargetVer != GameVersion) return null;
            }
            catch (IndexOutOfRangeException) { return null; }

            return Prop;
        }

        public void AddDownloadProperty(string URL, string OutputPath, string OutputDir, string RemoteHash, long RemoteRequiredSize) => DownloadProperty.Add(new DownloadAddressProperty
        {
            URL = URL,
            Output = OutputPath,
            DirectoryOutput = OutputDir,
            RemoteHash = RemoteHash,
            RemoteRequiredSize = RemoteRequiredSize
        });

        public async Task CheckDriveFreeSpace(UIElement Content)
        {
            DriveInfo _DriveInfo = new DriveInfo(GameDirPath);
            long RequiredSpace = DownloadProperty.Sum(x =>
            {
                LogWriteLine($"Package: {Path.GetFileName(x.URL)} {ConverterTool.SummarizeSizeSimple(x.RemoteRequiredSize)} ({x.RemoteRequiredSize} bytes) free space required.\r\nHash: {x.RemoteHash}", LogType.Default, true);
                return x.RemoteRequiredSize;
            });
            long DiskSpace = _DriveInfo.TotalFreeSpace;
            LogWriteLine($"Total free space required: {ConverterTool.SummarizeSizeSimple(RequiredSpace)} with {_DriveInfo.Name} remained free space: {ConverterTool.SummarizeSizeSimple(DiskSpace)}", LogType.Default, true);

            if (DiskSpace < (RequiredSpace - DownloadProperty.Sum(x => GetExistingPartialDownloadLength(x.Output))))
            {
                LogWriteLine($"DISK SPACE ON {_DriveInfo.Name} IS INSUFFICIENT!", LogType.Error, true);
                await Dialog_InsufficientDriveSpace(Content, DiskSpace, RequiredSpace, _DriveInfo.Name);
                throw new IOException($"Free Space on {_DriveInfo.Name} is sufficient! (Free space: {DiskSpace}, Req. Space: {RequiredSpace}, Drive: {_DriveInfo.Name}). Cancelling the task!");
            }
        }

        public async Task StartDownloadAsync()
        {
            DownloadStopwatch = Stopwatch.StartNew();
            CountTotalToDownload = DownloadProperty.Count;
            bool IsPerFile = true;

            InstallStatus = new InstallManagementStatus
            {
                IsIndetermined = false,
                IsPerFile = IsPerFile
            };

            CountCurrentDownload = 0;

            DownloadProgress += DownloadStatusAdapter;
            DownloadProgress += DownloadProgressAdapter;
            DownloadLog += DownloadLogAdapter;

            foreach (DownloadAddressProperty prop in DownloadProperty)
            {
                FileInfo file = new FileInfo(prop.Output);
                CountCurrentDownload++;
                InstallStatus.StatusTitle = string.Format("{0}: {1}", Lang._Misc.Downloading, string.Format(Lang._Misc.PerFromTo, CountCurrentDownload, CountTotalToDownload));
                LogWriteLine($"Download URL {CountCurrentDownload}/{DownloadProperty.Count}:\r\n{prop.URL}");
                if (!file.Exists || file.Length < prop.LocalSize)
                {
                    await Download(prop.URL, prop.Output, DownloadThread, false, Token);
                    await Merge();
                }
            }

            DownloadProgress -= DownloadStatusAdapter;
            DownloadProgress -= DownloadProgressAdapter;
            DownloadLog -= DownloadLogAdapter;
        }

        public DownloadAddressProperty StartVerification()
        {
            bool IsPerFile = DownloadProperty.Count > 1;
            CountCurrentDownload = 0;

            InstallStatus = new InstallManagementStatus
            {
                IsIndetermined = false,
                IsPerFile = IsPerFile
            };

            DownloadStopwatch = Stopwatch.StartNew();
            DownloadLocalSize = 0;

            for (int i = 0; i < (CanSkipVerif ? 0 : DownloadProperty.Count); i++)
            {
                CountCurrentDownload++;
                DownloadLocalPerFileSize = 0;
                DownloadRemotePerFileSize = DownloadProperty[i].RemoteSize;
                InstallStatus.StatusTitle = string.Format("{0}: {1}", Lang._Misc.Verifying, string.Format(Lang._Misc.PerFromTo, CountCurrentDownload, CountTotalToDownload));
                UpdateStatus(InstallStatus);
                if ((DownloadProperty[i].LocalHash = GetMD5FromFile(DownloadProperty[i].Output, Token))
                    != DownloadProperty[i].RemoteHash.ToLower())
                    return DownloadProperty[i];
            }

            return null;
        }

        public async Task<bool> StartVerificationAsync(UIElement Content)
        {
            if (CanDeltaPatch) return false;
            DownloadAddressProperty VerificationResult = await Task.Run(StartVerification);

            if (VerificationResult != null)
            {
                switch (await Dialog_GameInstallationFileCorrupt(Content, VerificationResult.RemoteHash, VerificationResult.LocalHash))
                {
                    case ContentDialogResult.Primary:
                        DeleteDownloadedFile(VerificationResult.Output, DownloadThread);
                        return true;
                    case ContentDialogResult.None:
                        throw new OperationCanceledException();
                }
            }

            return false;
        }

        private string GetMD5FromFile(string fileOutput, CancellationToken token)
        {
            MD5 md5 = MD5.Create();
            FileStream stream;
            // Buffer is 4 MiB
            byte[] buffer = new byte[4 << 20];

            int read = 0;

            using (stream = new FileStream(fileOutput, FileMode.Open, FileAccess.Read))
            {
                while ((read = stream.Read(buffer, 0, buffer.Length)) >= buffer.Length)
                {
                    token.ThrowIfCancellationRequested();
                    md5.TransformBlock(buffer, 0, buffer.Length, buffer, 0);

                    DownloadLocalSize += read;
                    DownloadLocalPerFileSize += read;
                    InstallProgress = new InstallManagementProgress(DownloadLocalSize, DownloadRemoteSize,
                        DownloadLocalPerFileSize, DownloadRemotePerFileSize,
                        DownloadStopwatch.Elapsed.TotalSeconds, false);

                    UpdateProgress(InstallProgress);
                }
            }

            md5.TransformFinalBlock(buffer, 0, read);

            return ConverterTool.BytesToHex(md5.Hash).ToLower();
        }

        string DownloadStateStr = "";
        private void DownloadStatusAdapter(object sender, DownloadEvent e)
        {
            switch (e.State)
            {
                case MultisessionState.Downloading:
                    DownloadStateStr = Lang._Misc.Downloading;
                    break;
                case MultisessionState.Merging:
                    DownloadStateStr = Lang._Misc.Merging;
                    break;
            }

            InstallStatus.StatusTitle = string.Format("{0}: {1}", DownloadStateStr, string.Format(Lang._Misc.PerFromTo, CountCurrentDownload, CountTotalToDownload));
            UpdateStatus(InstallStatus);
        }

        private void DownloadProgressAdapter(object sender, DownloadEvent e)
        {
            if (e.State != MultisessionState.Merging) DownloadLocalSize += e.Read;

            DownloadLocalPerFileSize = e.SizeDownloaded;
            DownloadRemotePerFileSize = e.SizeToBeDownloaded;
            InstallProgress = new InstallManagementProgress(DownloadLocalSize, DownloadRemoteSize,
                DownloadLocalPerFileSize, DownloadRemotePerFileSize, DownloadStopwatch.Elapsed.TotalSeconds, true, e.Speed);

            UpdateProgress(InstallProgress);
        }

        private void DownloadLogAdapter(object sender, DownloadLogEvent e) => LogWriteLine(e.Message, LogSeverity2LogType(e.Severity), true);

        private LogType LogSeverity2LogType(LogSeverity e)
        {
            switch (e)
            {
                case LogSeverity.Error:
                    return LogType.Error;
                case LogSeverity.Warning:
                    return LogType.Warning;
                default:
                    return LogType.Default;
            }
        }

        /*
         * Return true if download is completed
         * Return false if download is uncompleted
         */
        public async Task<bool> CheckExistingDownload()
        {
            DownloadLocalSize = 0;
            DownloadRemoteSize = 0;

            for (int i = 0; i < DownloadProperty.Count; i++)
            {
                DownloadRemoteSize += DownloadProperty[i].RemoteSize = await Task.Run(() => TryGetContentLength(DownloadProperty[i].URL, Token) ?? 0);
                DownloadLocalSize += DownloadProperty[i].LocalSize = CalculateExistingMultisessionFilesWithExpctdSize(DownloadProperty[i].Output, DownloadThread, DownloadProperty[i].RemoteSize);
            }

            return DownloadLocalSize == 0 ? true : DownloadLocalSize == DownloadRemoteSize;
        }

        public async Task CheckExistingDownloadAsync(UIElement Content)
        {
            if (!await CheckExistingDownload())
            {
                switch (await Dialog_ExistingDownload(Content, DownloadLocalSize, DownloadRemoteSize))
                {
                    case ContentDialogResult.Primary:
                        break;
                    case ContentDialogResult.Secondary:
                        ResetDownload();
                        break;
                }
            }
        }

        public void ResetDownload()
        {
            DownloadLocalSize = 0;
            FileInfo fileInfo;

            for (int i = 0; i < DownloadProperty.Count; i++)
            {
                DeleteDownloadedFile(DownloadProperty[i].Output, DownloadThread);
            }
        }

        private void DeleteDownloadedFile(string FileOutput, byte Thread)
        {
            FileInfo fileInfo = new FileInfo(FileOutput);
            if (fileInfo.Exists)
                fileInfo.Delete();
            DeleteMultisessionFiles(FileOutput, Thread);
        }

        long GetExistingPartialDownloadLength(string fileOutput)
        {
            FileInfo fileInfo;

            if ((fileInfo = new FileInfo(fileOutput)).Exists && fileInfo.Length > 0)
                return fileInfo.Length;

            string[] partPaths = Directory.GetFiles(Path.GetDirectoryName(fileOutput), $"{Path.GetFileName(fileOutput)}.0*");

            if (partPaths.Length == 0)
                return 0;

            return partPaths.Sum(x => (fileInfo = new FileInfo(x)).Exists ? fileInfo.Length : 0);
        }

        public void StartInstall()
        {
            DownloadStopwatch = Stopwatch.StartNew();
            DownloadLocalSize = 0;

            CountCurrentDownload = 0;

            TryUnassignReadOnlyFiles();

            if (CanSkipExtract) return;

            DownloadRemoteSize = DownloadProperty.Sum(x => CalculateUncompressedSize(ref x));

            foreach (DownloadAddressProperty prop in DownloadProperty)
            {
                SevenZipTool ExtractTool = new SevenZipTool();
                CountCurrentDownload++;
                InstallStatus.StatusTitle = string.Format("{0}: {1}", Lang._Misc.Extracting, string.Format(Lang._Misc.PerFromTo, CountCurrentDownload, CountTotalToDownload));
                UpdateStatus(InstallStatus);

                ExtractTool.AutoLoad(prop.Output);

                ExtractTool.ExtractProgressChanged += ExtractProgressAdapter;
                ExtractTool.ExtractToDirectory(GameDirPath, ExtractionThread, Token);
                ExtractTool.ExtractProgressChanged -= ExtractProgressAdapter;

                ExtractTool.Dispose();
                if (CanDeleteZip)
                    File.Delete(prop.Output);

                FileInfo hdiffList = new FileInfo(Path.Combine(GameDirPath, "hdifffiles.txt"));
                FileInfo deleteList = new FileInfo(Path.Combine(GameDirPath, "deletefiles.txt"));

                if (hdiffList.Exists)
                {
                    hdiffList.MoveTo(Path.Combine(GameDirPath, $"hdifffiles_{Path.GetFileNameWithoutExtension(prop.Output)}.txt"), true);
                }

                if (deleteList.Exists)
                {
                    deleteList.MoveTo(Path.Combine(GameDirPath, $"deletefiles_{Path.GetFileNameWithoutExtension(prop.Output)}.txt"), true);
                }
            }
        }

        private async Task TryUnassignReadOnlyFilesAsync() => await Task.Run(TryUnassignReadOnlyFiles);

        private void TryUnassignReadOnlyFiles()
        {
            foreach (string File in Directory.EnumerateFiles(GameDirPath, "*", SearchOption.AllDirectories))
            {
                FileInfo fileInfo = new FileInfo(File);
                if (fileInfo.IsReadOnly)
                    fileInfo.IsReadOnly = false;
            }
        }

        private async Task RunPatch()
        {
            CountCurrentDownload = 1;
            CountTotalToDownload = 1;

            await StartPreparation();
            await RepairIngredients(await VerifyIngredients(SourceFileManifest, IngredientPath), IngredientPath);
            await Task.Run(StartConversion);
        }

        long LastSize = 0;

        private long GetLastSize(long input)
        {
            if (LastSize > input)
                LastSize = input;

            long a = input - LastSize;
            LastSize = input;
            return a;
        }

        private void ExtractProgressAdapter(object sender, ExtractProgress e)
        {
            DownloadLocalSize += GetLastSize(e.totalExtractedSize);

            DownloadLocalPerFileSize = e.totalExtractedSize;
            DownloadRemotePerFileSize = e.totalUncompressedSize;
            InstallProgress = new InstallManagementProgress(DownloadLocalSize, DownloadRemoteSize,
                DownloadLocalPerFileSize, DownloadRemotePerFileSize, DownloadStopwatch.Elapsed.TotalSeconds, true, e.CurrentSpeed);

            UpdateProgress(InstallProgress);
        }

        public async Task<bool> StartIfDeltaPatchAvailable()
        {
            if (CanDeltaPatch)
            {
                switch (await Dialog_DeltaPatchFileDetected(Content, PatchProp.SourceVer, PatchProp.TargetVer))
                {
                    case ContentDialogResult.Secondary:
                        this.CanDeltaPatch = false;
                        return true;
                    case ContentDialogResult.None:
                        throw new TaskCanceledException();
                }

                await RunPatch();
                return false;
            }

            return true;
        }

        public long CalculateUncompressedSize(ref DownloadAddressProperty Input) =>
            Input.LocalUncompressedSize = new SevenZipTool().GetUncompressedSize(Input.Output);

        public async Task FinalizeInstallation(UIElement Content)
        {
            InstallStatus.IsPerFile = false;
            await Task.Run(() =>
            {
                UpdateStatus(InstallStatus);
                ApplyHdiffPatch();
                CleanUpUnusedAssets();

                // This part is only for Honkai Block files Cleanup.
                StashOldBlock();
            });

            if (!CanDeltaPatch)
                await PostInstallVerification(Content);
        }

        private List<PkgVersionProperties> TryGetHDiffList()
        {
            List<PkgVersionProperties> _out = new List<PkgVersionProperties>();
            PkgVersionProperties prop;
            foreach (string listFile in Directory.EnumerateFiles(GameDirPath, "*hdifffiles*", SearchOption.TopDirectoryOnly))
            {
                LogWriteLine($"hdiff File list path: {listFile}", LogType.Default, true);

                try
                {
                    using (Stream fs = new FileStream(listFile, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.DeleteOnClose))
                    using (StreamReader listReader = new StreamReader(fs))
                    {
                        while (!listReader.EndOfStream)
                        {
                            _out.Add(prop = (PkgVersionProperties)JsonSerializer
                                .Deserialize(
                                    listReader.ReadLine(),
                                    typeof(PkgVersionProperties),
                                    PkgVersionPropertiesContext.Default));
                            LogWriteLine($"hdiff entry: {prop.remoteName}", LogType.Default, true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogWriteLine($"Failed while trying to read hdiff file list: {listFile}\r\n{ex}", LogType.Warning, true);
                }
            }

            return _out;
        }

        private void ApplyHdiffPatch()
        {
            DownloadStopwatch = Stopwatch.StartNew();

            string FileSource,
                   FilePatch,
                   FileOutput;

            HPatchUtil Patcher = new HPatchUtil();
            List<PkgVersionProperties> Entry = TryGetHDiffList();

            if (Entry.Count == 0) return;

            FileInfo _fDiff;

            DownloadLocalSize = 0;
            DownloadRemoteSize = Entry.Sum(x =>
            {
                _fDiff = new FileInfo(Path.Combine(GameDirPath, ConverterTool.NormalizePath(x.remoteName) + ".hdiff"));
                return _fDiff.Exists ? _fDiff.Length : 0;
            });

            InstallStatus.IsPerFile = false;

            int i = 0;
            int listCount = Entry.Count;
            foreach (PkgVersionProperties _Entry in Entry)
            {
                i++;
                FileSource = Path.Combine(GameDirPath, ConverterTool.NormalizePath(_Entry.remoteName));
                FilePatch = FileSource + ".hdiff";
                FileOutput = FileSource + "_tmp";

                InstallStatus.StatusTitle = string.Format("{0}: {1}", Lang._Misc.Patching, string.Format(Lang._Misc.PerFromTo, i, listCount));
                UpdateStatus(InstallStatus);
                InstallProgress = new InstallManagementProgress(DownloadLocalSize, DownloadRemoteSize,
                    DownloadLocalPerFileSize, DownloadRemotePerFileSize, DownloadStopwatch.Elapsed.TotalSeconds);
                UpdateProgress(InstallProgress);

                DownloadLocalSize += new FileInfo(FilePatch).Length;

                try
                {
                    if (File.Exists(FileSource) && File.Exists(FileOutput) || File.Exists(FileSource))
                    {
                        if (File.Exists(FilePatch))
                        {
                            LogWriteLine($"Patching file {_Entry.remoteName}...", LogType.Default, true);
                            Patcher.HPatchFile(FileSource, FilePatch, FileOutput);
                            File.Delete(FilePatch);
                            File.Delete(FileSource);
                            File.Move(FileOutput, FileSource);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogWriteLine($"Error while patching file: {_Entry.remoteName}. Skipping!\r\n{ex}", LogType.Warning, true);
                }

                InstallProgress = new InstallManagementProgress(DownloadLocalSize, DownloadRemoteSize,
                    DownloadLocalPerFileSize, DownloadRemotePerFileSize, DownloadStopwatch.Elapsed.TotalSeconds);
                UpdateProgress(InstallProgress);
            }
        }

        private void TryRemoveDeleteFilesList()
        {
            string FilePath;
            FileInfo fInfo;
            foreach (string listFile in Directory.EnumerateFiles(GameDirPath, "*deletefiles*", SearchOption.TopDirectoryOnly))
            {
                LogWriteLine($"deletefiles File list path: {listFile}", LogType.Default, true);
                using (Stream fs = new FileStream(listFile, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.DeleteOnClose))
                using (StreamReader listReader = new StreamReader(fs))
                {
                    while (!listReader.EndOfStream)
                    {
                        FilePath = Path.Combine(GameDirPath, ConverterTool.NormalizePath(listReader.ReadLine()));
                        fInfo = new FileInfo(FilePath);
                        if (fInfo.Exists)
                        {
                            fInfo.Delete();
                            LogWriteLine($"Deleting redundant file: {FilePath}", LogType.Default, true);
                        }
                    }
                }
            }
        }

        private void CleanUpUnusedAssets()
        {
            try
            {
                TryRemoveDeleteFilesList();

                // Remove any _tmp or .diff files
                foreach (string _Entry in Directory.EnumerateFiles(GameDirPath, "*.*", SearchOption.AllDirectories)
                                                   .Where(x => x.EndsWith(".diff", StringComparison.OrdinalIgnoreCase)
                                                            || x.EndsWith("_tmp", StringComparison.OrdinalIgnoreCase)
                                                            || x.EndsWith(".hdiff", StringComparison.OrdinalIgnoreCase)))
                {
                    File.Delete(_Entry);
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while doing cleanup! Ignoring...\r\n{ex}", LogType.Warning, true);
            }
        }

        private void StashOldBlock()
        {
            string XmfPath = Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\StreamingAssets\\Asb\\pc\\Blocks.xmf");
            string XmfDir = Path.GetDirectoryName(XmfPath);

            if (!File.Exists(XmfPath)) return;

            BlockData Util = new BlockData();

            Util.Init(new FileStream(XmfPath, FileMode.Open, FileAccess.Read, FileShare.Read), XMFFileFormat.XMF);
            Util.CheckForUnusedBlocks(XmfDir);
            List<string> UnusedFiles = Util.GetListOfBrokenBlocks(XmfDir);
            UnusedFiles.AddRange(Directory.GetFiles(XmfDir, "Blocks_*.xmf"));

            foreach (string _Entry in UnusedFiles) File.Delete(_Entry);
        }

        public async Task PostInstallVerification(UIElement Content)
        {
            if (!(this.SourceProfile.IsGenshin ?? false)) return;

            InstallStatus = new InstallManagementStatus
            {
                IsPerFile = false,
                IsIndetermined = false,
                StatusTitle = Lang._InstallMgmt.IntegrityCheckTitle
            };
            UpdateStatus(InstallStatus);

            await TryUnassignReadOnlyFilesAsync();

            Dictionary<string, PkgVersionProperties> HashtableManifest = new Dictionary<string, PkgVersionProperties>();

            Entries = new List<PkgVersionProperties>();
            BrokenFiles = new List<PkgVersionProperties>();

            // Build primary manifest list
            await BuildPrimaryManifest(Entries, HashtableManifest);

            try
            {
                // Initialize Dispatch
                await InitializeNewGenshinDispatch();

                // Build persistent manifest list
                await BuildPersistentManifest(Entries, HashtableManifest);
            }
            catch (Exception ex)
            {
                LogWriteLine($"There's a problem while fetching the list of the persistent manifest via dispatcher. Skipping!\r\n{ex}", LogType.Warning, true);
            }

            BrokenFiles = await CheckFileIntegrity(Entries);

            long Size = BrokenFiles.Sum(x => x.fileSize);
            if (BrokenFiles.Count > 0)
            {
                LogWriteLine($"Total File: {BrokenFiles.Count} with size {ConverterTool.SummarizeSizeSimple(Size)}", LogType.Default, true);
                foreach (PkgVersionProperties Entry in BrokenFiles)
                    LogWriteLine($"{Entry.remoteName};{Entry.fileSize}", LogType.Default, true);

                switch (await Dialog_AdditionalDownloadNeeded(Content, Size))
                {
                    case ContentDialogResult.None:
                        BrokenFiles.Clear();
                        return;
                    case ContentDialogResult.Primary:
                        break;
                }
            }

            await RepairFileIntegrity(Content, BrokenFiles);
        }

        private async Task BuildPrimaryManifest(List<PkgVersionProperties> Entries,
            Dictionary<string, PkgVersionProperties> HashtableManifest)
        {
            // Build basic file entry.
            string ManifestPath = Path.Combine(GameDirPath, "pkg_version");
            if (!File.Exists(ManifestPath))
                await Download(RepoRemoteURL + "/pkg_version", ManifestPath, true, null, null, Token);
            BuildManifestList(ManifestPath, Entries, ref HashtableManifest, "", "", RepoRemoteURL);

            // Build local audio entry.
            foreach (string _Entry in Directory.EnumerateFiles(GameDirPath, "Audio_*_pkg_version"))
                BuildManifestList(_Entry, Entries, ref HashtableManifest, "", "", RepoRemoteURL);

            // Build additional blks entry.
            foreach (string _Entry in Directory.EnumerateFiles(
                Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\StreamingAssets"), "data_versions_*"))
                BuildManifestList(_Entry, Entries, ref HashtableManifest, $"{ExecutablePrefix}_Data\\StreamingAssets\\AssetBundles", "", RepoRemoteURL);
            foreach (string _Entry in Directory.EnumerateFiles(
                Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\StreamingAssets"), "silence_versions_*"))
                BuildManifestList(_Entry, Entries, ref HashtableManifest, $"{ExecutablePrefix}_Data\\StreamingAssets\\AssetBundles", "", RepoRemoteURL);
            foreach (string _Entry in Directory.EnumerateFiles(
                Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\StreamingAssets"), "res_versions_*"))
                BuildManifestList(_Entry, Entries, ref HashtableManifest, $"{ExecutablePrefix}_Data\\StreamingAssets\\AssetBundles", ".blk", RepoRemoteURL);

            // Build cutscenes entry.
            foreach (string _Entry in Directory.EnumerateFiles(
                Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\StreamingAssets\\VideoAssets"), "*_versions_*"))
                BuildManifestList(_Entry, Entries, ref HashtableManifest, $"{ExecutablePrefix}_Data\\StreamingAssets\\VideoAssets", "", RepoRemoteURL);
        }

        private async Task InitializeNewGenshinDispatch()
        {
            try
            {
                // Load Dispatcher Data
                DispatchReader = new GenshinDispatchHelper(DispatchServerID, DispatchKey, DispatchURLPrefix, GameVersionString, Token);

                // As per 2.8 update, the dispatch must be decrypted first.
                // Grab Genshin and Master Key to decrypt first
                string MasterKey = ConfigV2.MasterKey;
                int MasterKeyBitLength = ConfigV2.MasterKeyBitLength;
                string GenshinKey = CurrentConfigV2.DispatcherKey;
                int GenshinKeyBitLength = CurrentConfigV2.DispatcherKeyBitLength ?? 0;

                YSDispatchDec Decryptor = new YSDispatchDec();
                Decryptor.InitMasterKey(MasterKey, MasterKeyBitLength, RSAEncryptionPadding.Pkcs1);
                Decryptor.DecryptStringWithMasterKey(ref GenshinKey);
                Decryptor.InitYSDecoder(GenshinKey, RSAEncryptionPadding.Pkcs1, GenshinKeyBitLength);

                // Init Genshin Key to Inner RSA class
                Decryptor.InitRSA();

                // Load Dispatch Info in JSON
                YSDispatchInfo DispatcherData = await DispatchReader.LoadDispatchInfo();

                // DEBUG ONLY: Show encrypted Proto as JSON+Base64 format
                string dFormat = string.Format("Query Response (from server in encrypted form):\r\n{0}", DispatcherData.content);
#if DEBUG
                LogWriteLine(dFormat);
#endif
                WriteLog(dFormat, LogType.Default);

                // Get Protobuf Encrypted content and decrypt it. Then load it to LoadDispatch.
                byte[] ProtoDecrypted = Decryptor.DecryptYSDispatch(DispatcherData.content);

                // DEBUG ONLY: Show the decrypted Proto as Base64 format
                dFormat = string.Format("Proto Response (from server after decryption process):\r\n{0}", Convert.ToBase64String(ProtoDecrypted));
#if DEBUG
                LogWriteLine(dFormat);
#endif
                WriteLog(dFormat, LogType.Default);

                await DispatchReader.LoadDispatch(ProtoDecrypted);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while loading the dispatcher! :(\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private async Task BuildPersistentManifest(List<PkgVersionProperties> Entries,
            Dictionary<string, PkgVersionProperties> HashtableManifest)
        {
            // Load dispatch info as QueryProperty
            QueryProperty QueryProperty = DispatchReader.GetResult();

            string ManifestPath, ParentURL, ParentAudioURL;

            if (!Directory.Exists(Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\Persistent")))
                Directory.CreateDirectory(Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\Persistent"));

            // Build data_versions (silence)
            ManifestPath = Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\Persistent\\silence_data_versions");
            ParentURL = $"{QueryProperty.ClientDesignDataSilURL}/AssetBundles";
            // Remove read-only and system attribute from silence_data_version that was set by game.
            try
            {
                if (File.Exists(ManifestPath + "_persist")) TryUnassignDeleteROPersistFile(ManifestPath + "_persist");
                using (FileStream fs = new FileStream(ManifestPath + "_persist", FileMode.Create, FileAccess.Write))
                    await Download(ParentURL + "/data_versions", fs, null, null, Token);

                LogWriteLine($"data_versions (silence) path: {ParentURL + "/data_versions"}", LogType.Default, true);

                BuildManifestPersistentList(ManifestPath + "_persist", Entries, ref HashtableManifest, $"{ExecutablePrefix}_Data\\Persistent\\AssetBundles", "", ParentURL);
            }
            catch { }

            // Build data_versions
            ManifestPath = Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\Persistent\\data_versions");
            ParentURL = $"{QueryProperty.ClientDesignDataURL}/AssetBundles";
            if (File.Exists(ManifestPath + "_persist")) TryUnassignDeleteROPersistFile(ManifestPath + "_persist");
            using (FileStream fs = new FileStream(ManifestPath + "_persist", FileMode.Create, FileAccess.Write))
                await Download(ParentURL + "/data_versions", fs, null, null, Token);

            LogWriteLine($"data_versions path: {ParentURL + "/data_versions"}", LogType.Default, true);

            BuildManifestPersistentList(ManifestPath + "_persist", Entries, ref HashtableManifest, $"{ExecutablePrefix}_Data\\Persistent\\AssetBundles", "", ParentURL);

            // Build release_res_versions_external
            ManifestPath = Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\Persistent\\res_versions");
            ParentURL = $"{QueryProperty.ClientGameResURL}/StandaloneWindows64";
            ParentAudioURL = $"{QueryProperty.ClientAudioAssetsURL}/StandaloneWindows64";
            if (File.Exists(ManifestPath + "_persist")) TryUnassignDeleteROPersistFile(ManifestPath + "_persist");
            using (FileStream fs = new FileStream(ManifestPath + "_persist", FileMode.Create, FileAccess.Write))
                await Download(ParentURL + "/release_res_versions_external", fs, null, null, Token);

            LogWriteLine($"release_res_versions_external path: {ParentURL + "/release_res_versions_external"}", LogType.Default, true);


            BuildManifestPersistentList(ManifestPath + "_persist", Entries, ref HashtableManifest, $"{ExecutablePrefix}_Data\\Persistent", "", ParentURL, true, ParentAudioURL);

            // Save Persistent Revision
            SavePersistentRevision(QueryProperty);
        }

        private void TryUnassignDeleteROPersistFile(string path)
        {
            try
            {
                FileInfo file = new FileInfo(path);
                file.IsReadOnly = false;
                file.Delete();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to delete file: {path}\r\n{ex}", LogType.Error, true);
            }
        }

        private void SavePersistentRevision(in QueryProperty dispatchQuery)
        {
            string PersistentPath = Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\Persistent");

            // Get base_res_version_hash content
            string FilePath = Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\StreamingAssets\\res_versions_streaming");
            string Hash = ConverterTool.CreateMD5(new FileStream(FilePath, FileMode.Open, FileAccess.Read)).ToLower();
            File.WriteAllText(PersistentPath + "\\base_res_version_hash", Hash);
            // Get data_revision content
            File.WriteAllText(PersistentPath + "\\data_revision", $"{dispatchQuery.DataRevisionNum}");
            // Get res_revision content
            File.WriteAllText(PersistentPath + "\\res_revision", $"{dispatchQuery.ResRevisionNum}");
            // Get silence_revision content
            File.WriteAllText(PersistentPath + "\\silence_revision", $"{dispatchQuery.SilenceRevisionNum}");
            // Get audio_revision content
            File.WriteAllText(PersistentPath + "\\audio_revision", $"{dispatchQuery.AudioRevisionNum}");
            // Get ChannelName content
            File.WriteAllText(PersistentPath + "\\ChannelName", $"{dispatchQuery.ChannelName}");
            // Get ScriptVersion content
            File.WriteAllText(PersistentPath + "\\ScriptVersion", $"{dispatchQuery.GameVersion}");
        }

        private void BuildManifestPersistentList(string manifestPath, in List<PkgVersionProperties> listInput,
            ref Dictionary<string, PkgVersionProperties> hashtable, string parentPath = "",
            string onlyAcceptExt = "", string parentURL = "", bool IsResVersion = false, string parentAudioURL = "")
        {
            PkgVersionProperties Entry;
            bool IsHashHasValue = false;
            int GameVoiceLanguageID = SourceProfile.GetVoiceLanguageID();

            foreach (string data in File.ReadAllLines(manifestPath)
                .Where(x => x.EndsWith(onlyAcceptExt, StringComparison.OrdinalIgnoreCase)))
            {
                Entry = (PkgVersionProperties)JsonSerializer.Deserialize(data, typeof(PkgVersionProperties), PkgVersionPropertiesContext.Default);

                IsHashHasValue = hashtable.ContainsKey(Entry.remoteName);
                if (!IsHashHasValue)
                {
                    if (IsResVersion)
                    {
                        switch (Path.GetExtension(Entry.remoteName).ToLower())
                        {
                            case ".pck":
                                // Only add if GameVoiceLanguageID == 1 (en-us)
                                if (Entry.remoteName.Contains("English(US)") && GameVoiceLanguageID == 1)
                                {
                                    if (Entry.isPatch)
                                        Entry.remoteURL = $"{parentURL}/AudioAssets/{Entry.remoteName}";
                                    else
                                        Entry.remoteURL = $"{parentAudioURL}/AudioAssets/{Entry.remoteName}";

                                    if (parentPath != "")
                                        Entry.remoteName = $"{parentPath.Replace('\\', '/')}/AudioAssets/{Entry.remoteName}";

                                    hashtable.Add(Entry.remoteName, Entry);
                                    listInput.Add(Entry);
                                }
                                break;
                            case ".blk":
                                if (Entry.isPatch)
                                {
                                    Entry.remoteURL = $"{parentURL}/AssetBundles/{Entry.remoteName}";
                                    if (parentPath != "")
                                        Entry.remoteName = $"{parentPath.Replace('\\', '/')}/AssetBundles/{Entry.remoteName}";

                                    Entry.remoteName = Entry.localName != null ? $"{parentPath.Replace('\\', '/')}/AssetBundles/{Entry.localName}" : Entry.remoteName;
                                    hashtable.Add(Entry.remoteName, Entry);
                                    listInput.Add(Entry);
                                }
                                break;
                            case ".usm":
                            case ".cuepoint":
                            case ".json":
                                break;
                            default:
                                switch (Path.GetFileName(Entry.remoteName))
                                {
                                    case "svc_catalog":
                                        break;
                                    case "ctable.dat":
                                        Entry.remoteURL = $"{parentAudioURL}/{Entry.remoteName}";
                                        if (parentPath != "")
                                            Entry.remoteName = $"{parentPath.Replace('\\', '/')}/{Entry.remoteName}";

                                        hashtable.Add(Entry.remoteName, Entry);
                                        listInput.Add(Entry);
                                        break;
                                    default:
                                        Entry.remoteURL = $"{parentURL}/{Entry.remoteName}";
                                        if (parentPath != "")
                                            Entry.remoteName = $"{parentPath.Replace('\\', '/')}/{Entry.remoteName}";

                                        hashtable.Add(Entry.remoteName, Entry);
                                        listInput.Add(Entry);
                                        break;
                                }
                                break;
                        }
                    }
                    else
                    {
                        Entry.remoteURL = $"{parentURL}/{Entry.remoteName}";
                        if (parentPath != "")
                            Entry.remoteName = $"{parentPath.Replace('\\', '/')}/{Entry.remoteName}";
                        hashtable.Add(Entry.remoteName, Entry);
                        listInput.Add(Entry);
                    }
                }
            }
        }

        private void BuildManifestList(string manifestPath, in List<PkgVersionProperties> listInput,
            ref Dictionary<string, PkgVersionProperties> hashtable, string parentPath = "",
            string onlyAcceptExt = "", string parentURL = "")
        {
            PkgVersionProperties Entry;
            bool IsHashHasValue = false;

            foreach (string data in File.ReadAllLines(manifestPath)
                .Where(x => x.EndsWith(onlyAcceptExt, StringComparison.OrdinalIgnoreCase)))
            {
                Entry = (PkgVersionProperties)JsonSerializer.Deserialize(data, typeof(PkgVersionProperties), PkgVersionPropertiesContext.Default);
                if (parentPath != "")
                    Entry.remoteName = $"{parentPath.Replace('\\', '/')}/{Entry.remoteName}";
                Entry.remoteURL = $"{parentURL}/{Entry.remoteName}";

                IsHashHasValue = hashtable.ContainsKey(Entry.remoteName);
                if (!IsHashHasValue)
                {
                    hashtable.Add(Entry.remoteName, Entry);
                    listInput.Add(Entry);
                }
            }
        }

        private async Task<List<PkgVersionProperties>> CheckFileIntegrity(List<PkgVersionProperties> EntryIn)
        {
            DownloadStopwatch = Stopwatch.StartNew();
            DownloadLocalSize = 0;
            DownloadRemoteSize = EntryIn.Sum(x => x.fileSize);

            PostInstallCheck Util = new PostInstallCheck(GameDirPath, EntryIn, ExtractionThread, Token);
            Util.PostInstallCheckChanged += PostInstallCheckProgressAdapter;
            List<PkgVersionProperties> EntryOut = await Util.StartCheck();
            Util.PostInstallCheckChanged -= PostInstallCheckProgressAdapter;
            return EntryOut;
        }

        public int GetBrokenFilesCount() => BrokenFiles.Count;

        private async Task RepairFileIntegrity(UIElement Content, List<PkgVersionProperties> EntryIn)
        {
            if (EntryIn == null || EntryIn.Count == 0) return;

            DownloadStopwatch = Stopwatch.StartNew();
            DownloadLocalSize = 0;
            DownloadRemoteSize = EntryIn.Sum(x => x.fileSize);

            string LocalPath, RemotePath;

            int BrokenFilesCount = EntryIn.Count;
            int FilesRead = 0;

            DownloadLog += DownloadLogAdapter;
            DownloadProgress += DownloadProgressAdapter;
            foreach (PkgVersionProperties Entry in EntryIn)
            {
                FilesRead++;
                InstallStatus.StatusTitle = string.Format(Lang._InstallMgmt.AddtDownloadTitle, FilesRead, BrokenFilesCount);

                LocalPath = Path.Combine(GameDirPath, ConverterTool.NormalizePath(Entry.remoteName));
                RemotePath = Entry.remoteURL;

                if (!Directory.Exists(Path.GetDirectoryName(LocalPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(LocalPath));

                UpdateStatus(InstallStatus);
                // Use Parallel Download if file size >= 40 MiB
                // Else, use Serial Download
                try
                {
                    if (File.Exists(LocalPath))
                        TryUnassignDeleteROPersistFile(LocalPath);

                    LogWriteLine($"Downloading: {RemotePath}...", LogType.Default, true);
                    if (Entry.fileSize >= 10 << 20)
                    {
                        await Download(RemotePath, LocalPath, DownloadThread, true, Token);
                        await Merge();
                    }
                    else
                        await Download(RemotePath, LocalPath, true, null, null, Token);
                }
                catch (TaskCanceledException ex)
                {
                    LogWriteLine($"Download on {Entry.remoteURL} has been cancelled!\r\n{ex}", LogType.Warning, true);
                    throw new OperationCanceledException($"Download on {Entry.remoteURL} has been cancelled!", ex);
                }
                catch (OperationCanceledException ex)
                {
                    LogWriteLine($"Download on {Entry.remoteURL} has been cancelled!\r\n{ex}", LogType.Warning, true);
                    throw new OperationCanceledException($"Download on {Entry.remoteURL} has been cancelled!", ex);
                }
                catch (Exception ex)
                {
                    LogWriteLine($"{ex}", LogType.Error, true);
                }
            }
            DownloadProgress -= DownloadProgressAdapter;
            DownloadLog -= DownloadLogAdapter;

            await Dialog_AdditionalDownloadCompleted(Content);
        }

        private void PostInstallCheckProgressAdapter(object sender, PostInstallCheckProp e)
        {
            DownloadLocalSize = e.TotalReadSize;
            DownloadRemoteSize = e.TotalCheckSize;

            UpdateProgress(InstallProgress = new InstallManagementProgress(
                DownloadLocalSize, DownloadRemoteSize,
                DownloadLocalPerFileSize, DownloadRemotePerFileSize,
                DownloadStopwatch.Elapsed.TotalSeconds, false));
        }

        public async Task FinalizeInstallationAsync(UIElement Content) => await FinalizeInstallation(Content);

        public class DownloadAddressProperty
        {
            public string URL;
            public string Output;
            public string DirectoryOutput;
            public long RemoteSize;
            public long RemoteRequiredSize;
            public long LocalSize;
            public long LocalUncompressedSize;
            public bool IsCorrupted = false;
            public string RemoteHash;
            public string LocalHash;
        }

        private void UpdateProgress(InstallManagementProgress e) => InstallProgressChanged?.Invoke(this, e);
        private void UpdateStatus(InstallManagementStatus e) => InstallStatusChanged?.Invoke(this, e);

    }

    public class DeltaPatchProperty
    {
        public DeltaPatchProperty(string PatchFile)
        {
            ReadOnlySpan<string> strings = Path.GetFileNameWithoutExtension(PatchFile).Split('_');
            this.MD5hash = strings[5];
            this.ZipHash = strings[4];
            this.ProfileName = strings[0];
            this.SourceVer = strings[1];
            this.TargetVer = strings[2];
            this.PatchCompr = strings[3];
            this.PatchPath = PatchFile;
        }

        public string ZipHash { get; set; }
        public string MD5hash { get; set; }
        public string ProfileName { get; set; }
        public string SourceVer { get; set; }
        public string TargetVer { get; set; }
        public string PatchCompr { get; set; }
        public string PatchPath { get; set; }
    }

    public class InstallManagementStatus
    {
        public string StatusTitle { get; set; } = "-";
        public bool IsIndetermined { get; set; } = true;
        public bool IsPerFile { get; set; } = false;
    }

    public class InstallManagementProgress
    {
        public InstallManagementProgress(
            long ProgressDownloadedSize, long ProgressTotalSizeToDownload,
            long ProgressDownloadedPerFileSize, long ProgressTotalSizePerFileToDownload,
            double totalSecond, bool UsePerFileSpeed = false, long OverrideSpeed = -1)
        {
            this.ProgressDownloadedSize = ProgressDownloadedSize;
            this.ProgressTotalSizeToDownload = ProgressTotalSizeToDownload;
            this.ProgressDownloadedPerFileSize = ProgressDownloadedPerFileSize;
            this.ProgressTotalSizePerFileToDownload = ProgressTotalSizePerFileToDownload;
            this.ProgressSpeed = OverrideSpeed < 0 ? (long)(Math.Max(UsePerFileSpeed ? ProgressDownloadedPerFileSize : ProgressDownloadedSize, 1) / totalSecond) : OverrideSpeed;
        }
        public long ProgressDownloadedSize { get; set; }
        public long ProgressTotalSizeToDownload { get; set; }
        public long ProgressDownloadedPerFileSize { get; set; }
        public long ProgressTotalSizePerFileToDownload { get; set; }
        public double ProgressPercentage => Math.Round((ProgressDownloadedSize / (float)ProgressTotalSizeToDownload) * 100, 2);
        public double ProgressPercentagePerFile => Math.Round((ProgressDownloadedPerFileSize / (float)ProgressTotalSizePerFileToDownload) * 100, 2);
        public long ProgressSpeed { get; private set; }
        public TimeSpan TimeLeft => TimeSpan.FromSeconds((ProgressTotalSizeToDownload - ProgressDownloadedSize) / Math.Max(ProgressSpeed, 1));
    }
}
