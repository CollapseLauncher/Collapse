using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;


using Hi3Helper.Data;

using Newtonsoft.Json;

using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Logger;

using static CollapseLauncher.UpdaterWindow;

namespace CollapseLauncher
{
    public class Updater : HttpClientHelper
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

        public Updater(string TargetFolder, string ChannelName)
        {
            this.ChannelURL = string.Format(RepoURL, ChannelName);
            this.TargetPath = NormalizePath(TargetFolder);
            this.TempPath = Path.Combine(TargetPath, "_Temp");
        }

        public async Task StartFetch()
        {
            MemoryStream _databuf = new MemoryStream();

            Status = new UpdaterStatus
            {
                status = "Fetching Update",
                message = "Connecting to Update Repository..."
            };
            UpdateStatus();
            UpdateStopwatch = Stopwatch.StartNew();

            await DownloadFileAsync(ChannelURL + "fileindex.json", _databuf, TokenSource.Token);

            FileProp = JsonConvert.DeserializeObject<Prop>(Encoding.UTF8.GetString(_databuf.ToArray()));
        }

        public async Task StartCheck()
        {
            if (Directory.Exists(TempPath))
                Directory.Delete(TempPath, true);

            Directory.CreateDirectory(TempPath);

            Status.status = "Verifying:";
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
            Status.status = "Downloading Update:";

            string URL;
            string Output;

            DownloadProgress += Updater_DownloadProgressAdapter;

            foreach (fileProp _entry in UpdateFiles)
            {
                Status.message = _entry.p;
                URL = ChannelURL + _entry.p;
                Output = Path.Combine(TempPath, NormalizePath(_entry.p));

                UpdateStatus();

                if (!Directory.Exists(Path.GetDirectoryName(Output)))
                    Directory.CreateDirectory(Path.GetDirectoryName(Output));

                if (_entry.s >= (20 << 20))
                    await DownloadFileAsync(URL, Output, 8, TokenSource.Token);
                else
                    await DownloadFileAsync(URL, Output, TokenSource.Token);
            }

            DownloadProgress -= Updater_DownloadProgressAdapter;
        }

        public async Task FinishUpdate()
        {
            if (UpdateFiles.Count == 0)
            {
                Status.status = $"You're using the latest version ({FileProp.ver}) now!";
                Status.message = $"Back to the launcher shortly...";
                Console.WriteLine(UpdateFiles.Count);
                UpdateStatus();
                await Suicide();
                return;
            }

            /*
            string Output;
            string Temp;
            foreach (fileProp _entry in UpdateFiles)
            {
                Temp = Path.Combine(TempPath, NormalizePath(_entry.p));
                Output = Path.Combine(TargetPath, NormalizePath(_entry.p));

                if (!Directory.Exists(Path.GetDirectoryName(Output)))
                    Directory.CreateDirectory(Path.GetDirectoryName(Output));

                if (File.Exists(Output))
                    File.Delete(Output);

                File.Move(Temp, Output);
            }
            Directory.Delete(TempPath, true);
            */

            string newVerTagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "CollapseLauncher", "_NewVer");

            Status.status = $"Version has been updated to {FileProp.ver}!";
            Status.message = $"The launcher will re-open your launcher shortly...";
            UpdateStatus();

            Progress = new UpdaterProgress(TotalSize, TotalSize, 0, UpdateStopwatch.Elapsed);
            UpdateProgress();
            File.WriteAllText(newVerTagPath, FileProp.ver);
            await Suicide();
        }

        private async Task Suicide()
        {
            await Task.Delay(3000);
            new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = applyPath,
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = workingDir
                }
            }.Start();
            Environment.Exit(0);
        }

        private void Updater_DownloadProgressAdapter(object sender, _DownloadProgress e)
        {
            if (e.DownloadState == State.Downloading)
                Read += e.CurrentRead;

            Progress = new UpdaterProgress(Read, TotalSize, e.CurrentRead, UpdateStopwatch.Elapsed);
            UpdateProgress();
        }

        public void UpdateStatus() => UpdaterStatusChanged?.Invoke(this, Status);
        public void UpdateProgress() => UpdaterProgressChanged?.Invoke(this, Progress);
        public class Prop
        {
            public string ver { get; set; }
            public long time { get; set; }
            public List<fileProp> f { get; set; }
        }

        public class fileProp
        {
            public string p { get; set; }
            public string crc { get; set; }
            public long s { get; set; }
        }

        public class UpdaterStatus
        {
            public string status { get; set; }
            public string message { get; set; }
            public string newver { get; set; }
        }

        public class UpdaterProgress
        {
            public UpdaterProgress(long DownloadedSize, long TotalSizeToDownload, int CurrentRead, TimeSpan TimeSpan)
            {
                this.DownloadedSize = DownloadedSize;
                this.TotalSizeToDownload = TotalSizeToDownload;
                this._TotalSecond = TimeSpan.TotalSeconds;
                this.CurrentRead = CurrentRead;
            }

            private long _LastContinuedSize = 0;
            private double _TotalSecond = 0;

            public long DownloadedSize { get; private set; }
            public long TotalSizeToDownload { get; private set; }
            public double ProgressPercentage => Math.Round((DownloadedSize / (double)TotalSizeToDownload) * 100, 2);
            public int CurrentRead { get; private set; }
            public long CurrentSpeed => (long)(DownloadedSize / _TotalSecond);
            public TimeSpan TimeLeft => checked(TimeSpan.FromSeconds((TotalSizeToDownload - DownloadedSize) / CurrentSpeed));
        }
    }
}
