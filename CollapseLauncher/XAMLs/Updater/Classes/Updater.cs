using Hi3Helper.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.UpdaterWindow;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;

namespace CollapseLauncher
{
    public class Updater : Http
    {
        string ChannelURL;
        string TargetPath;
        string TempPath;
        string RepoURL = "https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/raw/main/{0}/";
        Prop FileProp = new Prop();
        List<fileProp> UpdateFiles = new List<fileProp>();
        CancellationTokenSource TokenSource = new CancellationTokenSource();
        Stopwatch UpdateStopwatch;

        public event EventHandler<UpdaterStatus> UpdaterStatusChanged;
        public event EventHandler<UpdaterProgress> UpdaterProgressChanged;

        private UpdaterStatus Status;
        private UpdaterProgress Progress;

        private long Read = 0,
                     TotalSize = 0;

        private byte DownloadThread;

        public Updater(string TargetFolder, string ChannelName, byte DownloadThread)
        {
            this.ChannelURL = string.Format(RepoURL, ChannelName);
            this.TargetPath = NormalizePath(TargetFolder);
            this.TempPath = Path.Combine(TargetPath, "_Temp");
            this.DownloadThread = DownloadThread;
        }

        public async Task StartFetch()
        {
            using (MemoryStream _databuf = new MemoryStream())
            {

                Status = new UpdaterStatus
                {
                    status = Lang._UpdatePage.UpdateStatus1,
                    message = Lang._UpdatePage.UpdateMessage1
                };
                UpdateStatus();
                UpdateStopwatch = Stopwatch.StartNew();

                await Download(ChannelURL + "fileindex.json", _databuf, null, null, TokenSource.Token);
                _databuf.Position = 0;
                FileProp = (Prop)JsonSerializer.Deserialize(_databuf, typeof(Prop), PropContext.Default);
            }
        }

        public async Task StartCheck()
        {
            if (Directory.Exists(TempPath))
                Directory.Delete(TempPath, true);

            Directory.CreateDirectory(TempPath);

            Status.status = Lang._UpdatePage.UpdateStatus2;
            Status.newver = FileProp.ver;
            TotalSize = FileProp.f.Sum(x => x.s);
            Progress = new UpdaterProgress(Read, TotalSize, 0, UpdateStopwatch.Elapsed);
            string LocalHash, RemoteHash, FilePath;

            foreach (fileProp _entry in FileProp.f)
            {
                Status.message = _entry.p;
                UpdateStatus();

                FilePath = Path.Combine(TargetPath, NormalizePath(_entry.p));
                RemoteHash = _entry.crc.ToUpper();

                if (File.Exists(FilePath))
                {
                    using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
                    {
                        LocalHash = (await CreateMD5Async(fs)).ToUpper();
                        if (LocalHash != RemoteHash)
                            UpdateFiles.Add(_entry);
                    }
                }
                else
                    UpdateFiles.Add(_entry);

                Read += _entry.s;
                Progress = new UpdaterProgress(Read, TotalSize, (int)_entry.s, UpdateStopwatch.Elapsed);
                UpdateProgress();
            }
        }

        public async Task StartUpdate()
        {
            if (UpdateFiles.Count == 0) return;
            Read = 0;
            TotalSize = UpdateFiles.Sum(x => x.s);

            string URL;
            string Output;
            int FilesCount = UpdateFiles.Count;

            DownloadProgress += Updater_DownloadProgressAdapter;

            int i = 1;
            foreach (fileProp _entry in UpdateFiles)
            {
                Status.message = _entry.p;
                URL = ChannelURL + _entry.p;
                Output = Path.Combine(TempPath, NormalizePath(_entry.p));

                Status.status = string.Format(Lang._UpdatePage.UpdateStatus3, i, FilesCount);
                UpdateStatus();

                if (!Directory.Exists(Path.GetDirectoryName(Output)))
                    Directory.CreateDirectory(Path.GetDirectoryName(Output));

                if (_entry.s >= (20 << 20))
                {
                    await Download(URL, Output, DownloadThread, true, TokenSource.Token);
                    await Merge();
                }
                else
                    await Download(URL, Output, true, null, null, TokenSource.Token);

                i++;
            }

            DownloadProgress -= Updater_DownloadProgressAdapter;
        }

        public async Task FinishUpdate(bool NoSuicide = false)
        {
            if (UpdateFiles.Count == 0)
            {
                Status.status = string.Format(Lang._UpdatePage.UpdateStatus4, FileProp.ver);
                Status.message = Lang._UpdatePage.UpdateMessage4;
                Console.WriteLine(UpdateFiles.Count);
                UpdateStatus();
                await Suicide();
                return;
            }

            string newVerTagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "CollapseLauncher", "_NewVer");

            Status.status = string.Format(Lang._UpdatePage.UpdateStatus5, FileProp.ver);
            Status.message = Lang._UpdatePage.UpdateMessage5;
            UpdateStatus();

            Progress = new UpdaterProgress(TotalSize, TotalSize, 0, UpdateStopwatch.Elapsed);
            UpdateProgress();
            File.WriteAllText(newVerTagPath, FileProp.ver);
            if (!NoSuicide)
                await Suicide();
        }

        private async Task Suicide()
        {
            await Task.Delay(3000);

            string prefix = Path.GetFileNameWithoutExtension(applyPath);

            try
            {
                foreach (string file in Directory.EnumerateFiles(TempPath, "*", SearchOption.TopDirectoryOnly))
                    if (file.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                        File.Move(file, Path.Combine(TargetPath, Path.GetFileName(file)), true);
            }
            catch { }

            new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = applyPath,
                    UseShellExecute = true,
                    Verb = "runas",
                    Arguments = $"\"{TargetPath}\"",
                    WorkingDirectory = workingDir
                }
            }.Start();
            Environment.Exit(0);
        }

        private void Updater_DownloadProgressAdapter(object sender, DownloadEvent e)
        {
            if (e.State != MultisessionState.Merging)
                Read += e.Read;

            Progress = new UpdaterProgress(Read, TotalSize, e.Read, UpdateStopwatch.Elapsed);
            UpdateProgress();
        }

        public void UpdateStatus() => UpdaterStatusChanged?.Invoke(this, Status);
        public void UpdateProgress() => UpdaterProgressChanged?.Invoke(this, Progress);

        public class UpdaterStatus
        {
            public string status { get; set; }
            public string message { get; set; }
            public string newver { get; set; }
        }

        public class UpdaterProgress
        {
            public UpdaterProgress(long DownloadedSize, long TotalSizeToDownload, long CurrentRead, TimeSpan TimeSpan)
            {
                this.DownloadedSize = DownloadedSize;
                this.TotalSizeToDownload = TotalSizeToDownload;
                this._TotalSecond = TimeSpan.TotalSeconds;
                this.CurrentRead = CurrentRead;
            }
            private double _TotalSecond = 0;

            public long DownloadedSize { get; private set; }
            public long TotalSizeToDownload { get; private set; }
            public double ProgressPercentage => Math.Round((DownloadedSize / (double)TotalSizeToDownload) * 100, 2);
            public long CurrentRead { get; private set; }
            public long CurrentSpeed => (long)(DownloadedSize / _TotalSecond);
            public TimeSpan TimeLeft => checked(TimeSpan.FromSeconds((TotalSizeToDownload - DownloadedSize) / CurrentSpeed));
        }
    }
}
