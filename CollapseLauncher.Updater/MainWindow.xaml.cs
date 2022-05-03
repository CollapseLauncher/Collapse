using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

using Hi3Helper.Data;

using Newtonsoft.Json;

using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Logger;

namespace CollapseLauncher.Updater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    enum UpdaterStatus { Reindex, Update }

    class Prop
    {
        public string ver { get; set; }
        public long time { get; set; }
        public List<fileProp> f { get; set; }
    }

    class fileProp
    {
        public string p { get; set; }
        public string crc { get; set; }
        public long s { get; set; }
    }

    public partial class MainWindow : Window
    {
        static UpdaterStatus status;
        static string[] argument;
        Updater updater;

        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                Hi3Helper.InvokeProp.InitializeConsole();
                LogWriteLine($"This console is for debugging purposes. It will close itself after all tasks on main thread are completed.");
                argument = args;

                if (argument.Length > 0)
                {
                    if (argument[0].ToLower() == "reindex" && argument.Length > 2)
                    {
                        new Reindexer(argument[1], argument[2]).RunReindex();
                        return;
                    }

                    if (argument[0].ToLower() == "update")
                        new Application() { StartupUri = new Uri("MainWindow.xaml", UriKind.Relative) }.Run();

                    if (argument[0].ToLower() == "elevateupdate")
                    {
                        string execPath = Process.GetCurrentProcess().MainModule.FileName;
                        string sourcePath = Path.Combine(Path.GetDirectoryName(execPath), Path.GetFileName(execPath));
                        string elevatedPath = Path.Combine(Path.GetDirectoryName(sourcePath), Path.GetFileNameWithoutExtension(sourcePath) + ".Elevated.exe");

                        if (File.Exists(elevatedPath))
                            File.Delete(elevatedPath);

                        File.Copy(sourcePath, elevatedPath);
                        var elevatedProc = new Process()
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = elevatedPath,
                                Arguments = $"update {argument[1]} {argument[2]}",
                                UseShellExecute = true,
                                Verb = "runas"
                            }
                        };
                        try
                        {
                            elevatedProc.Start();
                            elevatedProc.WaitForExit();
                        }
                        catch { }
                        File.Delete(elevatedPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex}");
                Console.ReadLine();
            }
        }

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                this.Title = "Updating Collapse Launcher...";

                updater = new Updater(argument[1], argument[2]);
                updater.UpdaterProgressChanged += Updater_UpdaterProgressChanged;
                updater.UpdaterStatusChanged += Updater_UpdaterStatusChanged;

                // InitLog(false);

                Task.Run(() =>
                {
                    updater.StartFetch();
                    updater.StartCheck();
                    updater.StartUpdate();
                    updater.FinishUpdate();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex}");
                Console.ReadLine();
            }
        }

        private void Updater_UpdaterStatusChanged(object sender, Updater.UpdaterStatus e)
        {
            Dispatcher.Invoke(() =>
            {
                StageStatus.Text = e.status;
                ActivityStatus.Text = e.message;
            });
        }

        private void Updater_UpdaterProgressChanged(object sender, Updater.UpdaterProgress e)
        {
            Dispatcher.Invoke(() =>
            {
                progressBar.Value = e.ProgressPercentage;
                ActivitySubStatus.Text = $"{SummarizeSizeSimple(e.DownloadedSize)} / {SummarizeSizeSimple(e.TotalSizeToDownload)}";
                SpeedStatus.Text = $"{SummarizeSizeSimple(e.CurrentSpeed)}/s";
                TimeEstimation.Text = string.Format("{0:%h}h{0:%m}m{0:%s}s left", e.TimeLeft);
            });
        }
    }

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

            if (Directory.Exists(TempPath))
                Directory.Delete(TempPath, true);

            Directory.CreateDirectory(TempPath);
        }

        public void StartFetch()
        {
            string data = "";
            MemoryStream _databuf = new MemoryStream();

            Status = new UpdaterStatus
            {
                status = "Fetching Update",
                message = "Connecting to Update Repository..."
            };
            UpdateStatus();
            UpdateStopwatch = Stopwatch.StartNew();

            DownloadFile(ChannelURL + "fileindex.json", _databuf, TokenSource.Token);

            data = Encoding.UTF8.GetString(_databuf.ToArray());
            FileProp = JsonConvert.DeserializeObject<Prop>(data);
        }

        public void StartCheck()
        {
            Status.status = "Verifying:";
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
                        LocalHash = CreateMD5(fs).ToUpper();
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

        public void StartUpdate()
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
                    DownloadFile(URL, Output, 8, TokenSource.Token);
                else
                    DownloadFile(URL, Output, TokenSource.Token);
            }

            DownloadProgress -= Updater_DownloadProgressAdapter;
        }

        public void FinishUpdate()
        {
            if (UpdateFiles.Count > 0)
            {
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
            }
            string newVerTagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "CollapseLauncher", "_NewVer");
            Directory.Delete(TempPath, true);

            Status.status = $"Version has been updated to {FileProp.ver}!";
            Status.message = $"Please re-open the launcher to see what's new.";
            UpdateStatus();

            Progress = new UpdaterProgress(TotalSize, TotalSize, 0, UpdateStopwatch.Elapsed);
            UpdateProgress();
            File.WriteAllText(newVerTagPath, FileProp.ver);
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

        public class UpdaterStatus
        {
            public string status { get; set; }
            public string message { get; set; }
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

    public class Reindexer
    {
        string filePath;
        string clientVer;
        long reindexTime;

        public Reindexer(string filePath, string clientVer)
        {
            Hi3Helper.InvokeProp.InitializeConsole();
            this.filePath = NormalizePath(filePath);
            this.reindexTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            this.clientVer = clientVer;

            if (File.Exists(Path.Combine(this.filePath, "fileindex.json")))
                File.Delete(Path.Combine(this.filePath, "fileindex.json"));
        }

        public void RunReindex()
        {
            int baseLength = filePath.Length + 1;
            string nameRoot, fileCrc;
            Prop Prop = new Prop() { ver = this.clientVer, time = this.reindexTime, f = new List<fileProp>() };
            FileStream fileStream;
            foreach (string file in Directory.GetFiles(filePath, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) != "CollapseLauncher.Updater.exe")
                {
                    nameRoot = file.Substring(baseLength).Replace('\\', '/');
                    fileCrc = CreateMD5(fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
                    Prop.f.Add(new fileProp { p = nameRoot, crc = fileCrc, s = fileStream.Length });
                    LogWriteLine($"{nameRoot} -> {fileCrc}");
                }
            }

            File.WriteAllText(Path.Combine(this.filePath, "fileindex.json"), JsonConvert.SerializeObject(Prop, Formatting.Indented));
        }
    }
}
