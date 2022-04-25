using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Newtonsoft.Json;

using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using static Hi3Helper.Logger;
using static CollapseLauncher.Dialogs.SimpleDialogs;

namespace CollapseLauncher
{
    public enum DownloadType { Update, FirstInstall, PreDownload }
    internal class InstallManagement : HttpClientHelper
    {
        public event EventHandler<InstallManagementStatus> InstallStatusChanged;
        public event EventHandler<InstallManagementProgress> InstallProgressChanged;

        private List<PkgVersionProperties> Entries = new List<PkgVersionProperties>();
        private List<PkgVersionProperties> BrokenFiles = new List<PkgVersionProperties>();

        private InstallManagementStatus InstallStatus;
        private InstallManagementProgress InstallProgress;

        private DownloadType ModeType;
        private CancellationToken Token;
        private int DownloadThread,
                    ExtractionThread;

        private string GameDirPath = string.Empty,
                       DecompressedRemotePath,
                       DispatchKey,
                       GameVersionString,
                       ExecutablePrefix;

        private int DispatchServerID;

        // Debug Parameter
        private bool CanDeleteZip = true;
        private bool CanSkipVerif = false;
        private bool CanSkipExtract = false;

        private Stopwatch DownloadStopwatch;

        public long DownloadLocalSize = 0,
                    DownloadLocalContinueSize = 0,
                    DownloadRemoteSize = 0;

        public int  CountCurrentDownload = 0,
                    CountTotalToDownload = 0;

        public long DownloadLocalPerFileSize = 0,
                    DownloadRemotePerFileSize = 0;

        private List<DownloadAddressProperty> DownloadProperty;
        private GenshinDispatchHelper DispatchReader;

        public InstallManagement(DownloadType downloadType, string GameDirPath, int downloadThread,
            int extractionThread, CancellationToken token, string DecompressedRemotePath = null,
            // These sections are for Genshin only
            string GameVerString = "", string DispatchKey = null, int RegionID = 0,
            string ExecutablePrefix = "BH3")
        {
            this.ModeType = downloadType;
            this.DownloadThread = downloadThread;
            this.ExtractionThread = extractionThread;
            this.Token = token;
            this.DownloadProperty = new List<DownloadAddressProperty>();
            this.GameDirPath = GameDirPath;
            this.DecompressedRemotePath = DecompressedRemotePath;
            this.DispatchKey = DispatchKey;
            this.GameVersionString = GameVerString;
            this.DispatchServerID = RegionID;
            this.ExecutablePrefix = ExecutablePrefix;

            ApplyParameter();
        }

        private void ApplyParameter()
        {
            CanDeleteZip = !File.Exists(Path.Combine(GameDirPath, "@NoDeleteZip"));
            CanSkipVerif = File.Exists(Path.Combine(GameDirPath, "@NoVerification"));
            CanSkipExtract = File.Exists(Path.Combine(GameDirPath, "@NoExtraction"));
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
            long RequiredSpace = DownloadProperty.Sum(x => x.RemoteRequiredSize) - DownloadProperty.Sum(x => GetExistingPartialDownloadLength(x.Output));
            long DiskSpace = new DriveInfo(GameDirPath).TotalFreeSpace;

            if (DiskSpace < RequiredSpace)
            {
                await Dialog_InsufficientDriveSpace(Content, DiskSpace, RequiredSpace, _DriveInfo.Name);
                throw new IOException($"Free Space on {_DriveInfo.Name} is sufficient! (Free space: {DiskSpace}, Req. Space: {RequiredSpace}, Drive: {_DriveInfo.Name}). Cancelling the task!");
            }
        }


        public void StartDownload()
        {
            DownloadStopwatch = Stopwatch.StartNew();
            CountTotalToDownload = DownloadProperty.Count;
            bool IsPerFile = DownloadProperty.Count > 1;

            InstallStatus = new InstallManagementStatus
            {
                IsIndetermined = false,
                IsPerFile = IsPerFile
            };

            DownloadProgress += DownloadStatusAdapter;
            DownloadProgress += DownloadProgressAdapter;

            CountCurrentDownload = 0;
            foreach (DownloadAddressProperty prop in DownloadProperty)
            {
                CountCurrentDownload++;
                if (!File.Exists(prop.Output))
                    DownloadFile(prop.URL, prop.Output, DownloadThread, Token);
            }

            DownloadProgress -= DownloadStatusAdapter;
            DownloadProgress -= DownloadProgressAdapter;
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
                InstallStatus.StatusTitle = $"Verifying: {CountCurrentDownload}/{CountTotalToDownload}";
                UpdateStatus(InstallStatus);
                if ((DownloadProperty[i].LocalHash = GetMD5FromFile(DownloadProperty[i].Output, Token))
                    != DownloadProperty[i].RemoteHash.ToLower())
                    return DownloadProperty[i];
            }

            return null;
        }

        public async Task StartDownloadAsync() => await Task.Run(() => StartDownload());

        public async Task<bool> StartVerificationAsync(UIElement Content)
        {
            DownloadAddressProperty VerificationResult = await Task.Run(() => StartVerification());

            if (VerificationResult != null)
            {
                switch (await Dialog_GameInstallationFileCorrupt(Content, VerificationResult.RemoteHash, VerificationResult.LocalHash))
                {
                    case ContentDialogResult.Primary:
                        new FileInfo(VerificationResult.Output).Delete();
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

        private void DownloadStatusAdapter(object sender, _DownloadProgress e)
        {
            InstallStatus.StatusTitle = $"{e.DownloadState}: {CountCurrentDownload}/{CountTotalToDownload}";
            UpdateStatus(InstallStatus);
        }

        private void DownloadProgressAdapter(object sender, _DownloadProgress e)
        {
            if (e.DownloadState == State.Downloading)
                DownloadLocalSize += e.CurrentRead;

            DownloadLocalPerFileSize = e.DownloadedSize;
            DownloadRemotePerFileSize = e.TotalSizeToDownload;
            InstallProgress = new InstallManagementProgress(DownloadLocalSize, DownloadRemoteSize,
                DownloadLocalPerFileSize, DownloadRemotePerFileSize, DownloadStopwatch.Elapsed.TotalSeconds, true, e.CurrentSpeed);

            UpdateProgress(InstallProgress);
        }

        /*
         * Return true if download is completed
         * Return false if download is uncompleted
         */
        public bool CheckExistingDownload()
        {
            DownloadLocalSize = 0;
            DownloadRemoteSize = 0;

            for (int i = 0; i < DownloadProperty.Count; i++)
            {
                DownloadLocalSize += (DownloadProperty[i].LocalSize = GetExistingPartialDownloadLength(DownloadProperty[i].Output));
                DownloadRemoteSize += (DownloadProperty[i].RemoteSize = GetContentLength(DownloadProperty[i].URL) ?? 0);
            }

            return DownloadLocalSize == 0 ? true : DownloadLocalSize == DownloadRemoteSize;
        }

        public async Task CheckExistingDownloadAsync(UIElement Content)
        {
            if (!CheckExistingDownload())
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
                if ((fileInfo = new FileInfo(DownloadProperty[i].Output)).Exists)
                    fileInfo.Delete();
                RemoveExistingPartialDownload(DownloadProperty[i].Output);
            }
        }

        long GetExistingPartialDownloadLength(string fileOutput)
        {
            FileInfo fileInfo;

            if ((fileInfo = new FileInfo(fileOutput)).Exists)
                return fileInfo.Length;

            List<string> partPaths = Directory.GetFiles(Path.GetDirectoryName(fileOutput), $"{Path.GetFileName(fileOutput)}.0*").ToList();
            
            if (partPaths.Count == 0)
                return 0;

            return partPaths
                   .Sum(x => (fileInfo = new FileInfo(x)).Exists ? fileInfo.Length : 0);
        }

        public void RemoveExistingPartialDownload(string fileOutput)
        {
            FileInfo fileInfo;

            foreach (string file in Directory.GetFiles(Path.GetDirectoryName(fileOutput), $"{Path.GetFileName(fileOutput)}.0*"))
                if ((fileInfo = new FileInfo(file)).Exists)
                    fileInfo.Delete();
        }

        public void StartInstall()
        {
            DownloadStopwatch = Stopwatch.StartNew();
            DownloadLocalSize = 0;
            DownloadRemoteSize = DownloadProperty.Sum(x => CalculateUncompressedSize(ref x));

            CountCurrentDownload = 0;
            foreach (DownloadAddressProperty prop in
                CanSkipExtract ? new List<DownloadAddressProperty>() : DownloadProperty)
            {
                SevenZipTool ExtractTool = new SevenZipTool();
                CountCurrentDownload++;
                InstallStatus.StatusTitle = $"Extracting: {CountCurrentDownload}/{CountTotalToDownload}";
                UpdateStatus(InstallStatus);

                ExtractTool.AutoLoad(prop.Output);

                ExtractTool.ExtractProgressChanged += ExtractProgressAdapter;
                ExtractTool.ExtractToDirectory(GameDirPath, ExtractionThread, Token);
                ExtractTool.ExtractProgressChanged -= ExtractProgressAdapter;

                ExtractTool.Dispose();
                if (CanDeleteZip)
                    File.Delete(prop.Output);
            }
        
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

        public async Task StartInstallAsync() => await Task.Run(() => StartInstall());

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

            await PostInstallVerification(Content);
        }

        private long GetHdiffSize(in IEnumerable<string> List)
        {
            long outSize = 0;
            PkgVersionProperties _Entry;
            FileInfo file;
            string path;
            foreach (string entry in List)
            {
                _Entry = JsonConvert.DeserializeObject<PkgVersionProperties>(entry);
                path = Path.Combine(GameDirPath, ConverterTool.NormalizePath(_Entry.remoteName) + ".hdiff");
                file = new FileInfo(path);
                outSize += file.Exists ? file.Length : 0;
            }

            return outSize;
        }

        private void ApplyHdiffPatch()
        {
            DownloadStopwatch = Stopwatch.StartNew();
            string PatchListPath = Path.Combine(GameDirPath, "hdifffiles.txt");
            if (!File.Exists(PatchListPath)) return;

            string FileSource,
                   FilePatch,
                   FileOutput;

            IEnumerable<string> HPatchList = File.ReadAllLines(PatchListPath);

            DownloadLocalSize = 0;
            DownloadRemoteSize = GetHdiffSize(HPatchList);

            HPatchUtil Patcher = new HPatchUtil();
            PkgVersionProperties Entry;

            InstallStatus.IsPerFile = false;

            int i = 0;
            foreach (string _Entry in HPatchList)
            {
                i++;
                Entry = JsonConvert.DeserializeObject<PkgVersionProperties>(_Entry);
                FileSource = Path.Combine(GameDirPath, ConverterTool.NormalizePath(Entry.remoteName));
                FilePatch = FileSource + ".hdiff";
                FileOutput = FileSource + "_tmp";

                InstallStatus.StatusTitle = $"Patching: {i}/{HPatchList.Count()}";
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
                            Patcher.HPatchFile(FileSource, FilePatch, FileOutput);
                            File.Delete(FilePatch);
                            File.Delete(FileSource);
                            File.Move(FileOutput, FileSource);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogWriteLine($"Error while patching file: {Entry.remoteName}. Skipping!\r\n{ex}", LogType.Warning, true);
                }

                InstallProgress = new InstallManagementProgress(DownloadLocalSize, DownloadRemoteSize,
                    DownloadLocalPerFileSize, DownloadRemotePerFileSize, DownloadStopwatch.Elapsed.TotalSeconds);
                UpdateProgress(InstallProgress);
            }

            File.Delete(PatchListPath);
        }

        private void CleanUpUnusedAssets()
        {
            try
            {
                string AssetsListPath = Path.Combine(GameDirPath, "deletefiles.txt"),
                   FilePath;
                if (File.Exists(AssetsListPath))
                {
                    foreach (string _Entry in File.ReadAllLines(AssetsListPath))
                    {
                        FilePath = Path.Combine(GameDirPath, ConverterTool.NormalizePath(_Entry));
                        if (File.Exists(FilePath)) File.Delete(FilePath);
                    }
                }

                // Remove any _tmp or .diff files
                IEnumerable<string> UnusedFiles = Directory.GetFiles(GameDirPath, "*.*", SearchOption.AllDirectories)
                        .Where(x => x.EndsWith(".diff", StringComparison.OrdinalIgnoreCase)
                                 || x.EndsWith("_tmp", StringComparison.OrdinalIgnoreCase)
                                 || x.EndsWith(".hdiff", StringComparison.OrdinalIgnoreCase));

                foreach (string _Entry in UnusedFiles) File.Delete(_Entry);
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

            List<string> UnusedFiles = new List<string>();
            BlockData Util = new BlockData();

            Util.Init(new FileStream(XmfPath, FileMode.Open, FileAccess.Read, FileShare.Read), XMFFileFormat.XMF);
            Util.CheckForUnusedBlocks(XmfDir);
            UnusedFiles = Util.GetListOfBrokenBlocks(XmfDir);
            UnusedFiles.AddRange(Directory.EnumerateFiles(XmfDir, "Blocks_*.xmf"));

            foreach (string _Entry in UnusedFiles) File.Delete(_Entry);
        }

        public async Task PostInstallVerification(UIElement Content)
        {
            if (DecompressedRemotePath == null) return;

            InstallStatus = new InstallManagementStatus
            {
                IsPerFile = false,
                IsIndetermined = false,
                StatusTitle = "Integrity Check"
            };
            UpdateStatus(InstallStatus);

            Dictionary<string, PkgVersionProperties> HashtableManifest = new Dictionary<string, PkgVersionProperties>();

            Entries = new List<PkgVersionProperties>();
            BrokenFiles = new List<PkgVersionProperties>();

            await Task.Run(() =>
            {
                // Build primary manifest list
                BuildPrimaryManifest(Entries, ref HashtableManifest);

                // Build persistent manifest list
                BuildPersistentManifest(Entries, ref HashtableManifest);

                CheckFileIntegrity(Entries, ref BrokenFiles);
            });

            long Size = BrokenFiles.Sum(x => x.fileSize);
            if (BrokenFiles.Count > 0)
            {
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

        private void BuildPrimaryManifest(in List<PkgVersionProperties> Entries,
            ref Dictionary<string, PkgVersionProperties> HashtableManifest)
        {
            // Build basic file entry.
            string ManifestPath = Path.Combine(GameDirPath, "pkg_version");
            if (!File.Exists(ManifestPath))
                new HttpClientHelper(false).DownloadFile(DecompressedRemotePath + "/pkg_version", ManifestPath, Token);
            BuildManifestList(ManifestPath, Entries, ref HashtableManifest, "", "", DecompressedRemotePath);

            // Build local audio entry.
            foreach (string _Entry in Directory.GetFiles(GameDirPath, "Audio_*_pkg_version"))
                BuildManifestList(_Entry, Entries, ref HashtableManifest, "", "", DecompressedRemotePath);

            // Build additional blks entry.
            foreach (string _Entry in Directory.GetFiles(
                Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\StreamingAssets"), "data_versions_*"))
                BuildManifestList(_Entry, Entries, ref HashtableManifest, $"{ExecutablePrefix}_Data\\StreamingAssets\\AssetBundles", "", DecompressedRemotePath);
            foreach (string _Entry in Directory.GetFiles(
                Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\StreamingAssets"), "silence_versions_*"))
                BuildManifestList(_Entry, Entries, ref HashtableManifest, $"{ExecutablePrefix}_Data\\StreamingAssets\\AssetBundles", "", DecompressedRemotePath);
            foreach (string _Entry in Directory.GetFiles(
                Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\StreamingAssets"), "res_versions_*"))
                BuildManifestList(_Entry, Entries, ref HashtableManifest, $"{ExecutablePrefix}_Data\\StreamingAssets\\AssetBundles", ".blk", DecompressedRemotePath);

            // Build cutscenes entry.
            foreach (string _Entry in Directory.GetFiles(
                Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\StreamingAssets\\VideoAssets"), "*_versions_*"))
                BuildManifestList(_Entry, Entries, ref HashtableManifest, $"{ExecutablePrefix}_Data\\StreamingAssets\\VideoAssets", "", DecompressedRemotePath);
        }

        private void BuildPersistentManifest(in List<PkgVersionProperties> Entries,
            ref Dictionary<string, PkgVersionProperties> HashtableManifest)
        {
            // Load Dispatcher Data
            DispatchReader = new GenshinDispatchHelper(DispatchServerID, DispatchKey, GameVersionString);
            DispatchReader.LoadDispatch();
            GenshinDispatchHelper.QueryProperty QueryProperty = DispatchReader.GetResult();

            string ManifestPath, ParentURL, ParentAudioURL;

            if (!Directory.Exists(Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\Persistent")))
                Directory.CreateDirectory(Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\Persistent"));

            // Build data_versions (silence)
            ManifestPath = Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\Persistent\\silence_data_versions");
            ParentURL = $"{QueryProperty.ClientDesignDataSilURL}/AssetBundles";
            // Remove read-only and system attribute from silence_data_version that was set by game.
            try
            {
                if (File.Exists(ManifestPath + "_persist"))
                {
                    FileInfo _file = new FileInfo(ManifestPath + "_persist");
                    _file.IsReadOnly = false;
                }
                using (FileStream fs = new FileStream(ManifestPath + "_persist", FileMode.Create, FileAccess.Write))
                    new HttpClientHelper(false).DownloadFile(ParentURL + "/data_versions", fs, Token);

                BuildManifestPersistentList(ManifestPath + "_persist", Entries, ref HashtableManifest, $"{ExecutablePrefix}_Data\\Persistent\\AssetBundles", "", ParentURL);
            }
            catch { }

            // Build data_versions
            ManifestPath = Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\Persistent\\data_versions");
            ParentURL = $"{QueryProperty.ClientDesignDataURL}/AssetBundles";
            if (File.Exists(ManifestPath + "_persist")) File.Delete(ManifestPath + "_persist");
            using (FileStream fs = new FileStream(ManifestPath + "_persist", FileMode.Create, FileAccess.Write))
                new HttpClientHelper(false).DownloadFile(ParentURL + "/data_versions", fs, Token);

            BuildManifestPersistentList(ManifestPath + "_persist", Entries, ref HashtableManifest, $"{ExecutablePrefix}_Data\\Persistent\\AssetBundles", "", ParentURL);

            // Build release_res_versions_external
            ManifestPath = Path.Combine(GameDirPath, $"{ExecutablePrefix}_Data\\Persistent\\res_versions");
            ParentURL = $"{QueryProperty.ClientGameResURL}/StandaloneWindows64";
            ParentAudioURL = $"{QueryProperty.ClientAudioAssetsURL}/StandaloneWindows64";
            if (File.Exists(ManifestPath + "_persist")) File.Delete(ManifestPath + "_persist");
            using (FileStream fs = new FileStream(ManifestPath + "_persist", FileMode.Create, FileAccess.Write))
                new HttpClientHelper(false).DownloadFile(ParentURL + "/release_res_versions_external", fs, Token);

            BuildManifestPersistentList(ManifestPath + "_persist", Entries, ref HashtableManifest, $"{ExecutablePrefix}_Data\\Persistent", "", ParentURL, true, ParentAudioURL);

            // Save Persistent Revision
            SavePersistentRevision(QueryProperty);
        }

        private void SavePersistentRevision(in GenshinDispatchHelper.QueryProperty dispatchQuery)
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

            foreach (string data in File.ReadAllLines(manifestPath)
                .Where(x => x.EndsWith(onlyAcceptExt, StringComparison.OrdinalIgnoreCase)))
            {
                Entry = JsonConvert.DeserializeObject<PkgVersionProperties>(data);

                IsHashHasValue = hashtable.ContainsKey(Entry.remoteName);
                if (!IsHashHasValue)
                {
                    if (IsResVersion)
                    {
                        switch (Path.GetExtension(Entry.remoteName).ToLower())
                        {
                            case ".pck":
                                if (Entry.remoteName.Contains("English(US)"))
                                {
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
                Entry = JsonConvert.DeserializeObject<PkgVersionProperties>(data);
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

        private void CheckFileIntegrity(
            in List<PkgVersionProperties> EntryIn,
            ref List<PkgVersionProperties> EntryOut)
        {
            DownloadStopwatch = Stopwatch.StartNew();
            DownloadLocalSize = 0;
            DownloadRemoteSize = EntryIn.Sum(x => x.fileSize);

            PostInstallCheck Util = new PostInstallCheck(GameDirPath, EntryIn, ExtractionThread, Token);
            Util.PostInstallCheckChanged += PostInstallCheckProgressAdapter;
            EntryOut = Util.StartCheck();
            Util.PostInstallCheckChanged -= PostInstallCheckProgressAdapter;
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

            DownloadProgress += DownloadProgressAdapter;
            foreach (PkgVersionProperties Entry in EntryIn)
            {
                FilesRead++;
                InstallStatus.StatusTitle = $"Addt. Download: {FilesRead}/{BrokenFilesCount}";

                LocalPath = Path.Combine(GameDirPath, ConverterTool.NormalizePath(Entry.remoteName));
                RemotePath = Entry.remoteURL;

                if (!Directory.Exists(Path.GetDirectoryName(LocalPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(LocalPath));

                UpdateStatus(InstallStatus);
                await Task.Run(() =>
                {
                    // Use Parallel Download if file size >= 40 MiB
                    // Else, use Serial Download
                    if (Entry.fileSize >= 10 << 20)
                        DownloadFile(RemotePath, LocalPath, DownloadThread, Token);
                    else
                        DownloadFile(RemotePath, new FileStream(LocalPath, FileMode.Create, FileAccess.Write, FileShare.Write), Token);
                });
            }
            DownloadProgress -= DownloadProgressAdapter;

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
