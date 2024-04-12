using CollapseLauncher.Helper.Update;
using Hi3Helper;
using Hi3Helper.Http;
using Squirrel;
using Squirrel.Sources;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public class Updater : IDisposable
    {
        private string ChannelURL;
        private string ChannelName;
        private Stopwatch UpdateStopwatch;
        private UpdateManager UpdateManager;
        private IFileDownloader UpdateDownloader;
        private GameVersion NewVersionTag;
        private bool IsUseLegacyDownload = false;

        private static string execPath = Process.GetCurrentProcess().MainModule.FileName;
        private static string workingDir = Path.GetDirectoryName(execPath);
        private static string applyElevatedPath = Path.Combine(workingDir, "..\\", $"ApplyUpdate.exe");

        public event EventHandler<UpdaterStatus> UpdaterStatusChanged;
        public event EventHandler<UpdaterProgress> UpdaterProgressChanged;

        private UpdaterStatus Status;
        private UpdaterProgress Progress;

        public Updater(string ChannelName)
        {
            this.ChannelName = ChannelName;
            this.ChannelURL = CombineURLFromString(FallbackCDNUtil.GetPreferredCDN().URLPrefix, "squirrel", this.ChannelName);
            this.UpdateDownloader = new UpdateManagerHttpAdapter();
            this.UpdateManager = new UpdateManager(ChannelURL, null, null, this.UpdateDownloader);
            this.UpdateStopwatch = Stopwatch.StartNew();
            this.Status = new UpdaterStatus();
            this.Progress = new UpdaterProgress(UpdateStopwatch, 0, 100);
        }

        ~Updater() => Dispose();

        public void Dispose()
        {
            UpdateManager?.Dispose();
        }

        public async Task<UpdateInfo> StartCheck() => await UpdateManager.CheckForUpdate();
        public bool IsUpdateAvailable(UpdateInfo info)
        {
            if (!info.ReleasesToApply.Any())
            {
                NewVersionTag = new GameVersion(info.FutureReleaseEntry.Version.Version);
                return DoesLatestVersionExist(NewVersionTag.VersionString);
            }

            return true;
        }

        public async Task<bool> StartUpdate(UpdateInfo UpdateInfo, CancellationToken token = default)
        {
            if (token.IsCancellationRequested)
            {
                return false;
            }

            Status.status = string.Format(Lang._UpdatePage.UpdateStatus3, 1, 1);
            UpdateStatus();
            UpdateProgress();
            UpdateStopwatch = Stopwatch.StartNew();

            try
            {
                if (!UpdateInfo.ReleasesToApply.Any())
                {
                    NewVersionTag = new GameVersion(UpdateInfo.FutureReleaseEntry.Version.Version);
                    if (DoesLatestVersionExist(NewVersionTag.VersionString))
                    {
                        Progress = new UpdaterProgress(UpdateStopwatch, 100, 100);
                        return true;
                    }

                    Status.status = string.Format(Lang._UpdatePage.UpdateStatus4, LauncherUpdateHelper.LauncherCurrentVersionString);
                    Status.message = Lang._UpdatePage.UpdateMessage4;
                    UpdateStatus();

                    await Task.Delay(3000);
                    return false;
                }

                NewVersionTag = new GameVersion(UpdateInfo.ReleasesToApply.FirstOrDefault().Version.Version);

                await UpdateManager.DownloadReleases(UpdateInfo.ReleasesToApply, (progress) =>
                {
                    Progress = new UpdaterProgress(UpdateStopwatch, progress / 2, 100);
                    UpdateProgress();
                });

                await UpdateManager.ApplyReleases(UpdateInfo, (progress) =>
                {
                    Progress = new UpdaterProgress(UpdateStopwatch, progress / 2 + 50, 100);
                    UpdateProgress();
                });
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"Failed while running update via Squirrel. Failback to legacy method...\r\n{ex}", LogType.Error, true);
                IsUseLegacyDownload = true;
                await StartLegacyUpdate();
            }

            return true;
        }

        private async Task StartLegacyUpdate()
        {
            using (Http _httpClient = new Http(true))
            {
                UpdateStopwatch = Stopwatch.StartNew();

                await using (BridgedNetworkStream stream = await FallbackCDNUtil.TryGetCDNFallbackStream($"{this.ChannelName.ToLower()}/fileindex.json", default))
                {
                    AppUpdateVersionProp updateInfo = await stream.DeserializeAsync<AppUpdateVersionProp>(InternalAppJSONContext.Default, default);
                    NewVersionTag = updateInfo.Version.Value;
                    UpdateStatus();
                    UpdateProgress();
                }

                FallbackCDNUtil.DownloadProgress += FallbackCDNUtil_DownloadProgress;
                await FallbackCDNUtil.DownloadCDNFallbackContent(_httpClient, applyElevatedPath, Environment.ProcessorCount > 8 ? 8 : Environment.ProcessorCount, $"{this.ChannelName.ToLower()}/ApplyUpdate.exe", default);
                FallbackCDNUtil.DownloadProgress -= FallbackCDNUtil_DownloadProgress;

                File.WriteAllText(Path.Combine(workingDir, "..\\", "release"), this.ChannelName.ToLower());
            }
        }

        private void FallbackCDNUtil_DownloadProgress(object sender, DownloadEvent e)
        {
            Progress = new UpdaterProgress(UpdateStopwatch, (int)e.ProgressPercentage, 100);
            UpdateProgress();
        }

        private bool DoesLatestVersionExist(string versionString)
        {
            string filePath = Path.Combine(AppFolder, $"..\\app-{versionString}\\{Path.GetFileName(AppExecutablePath)}");

            return File.Exists(filePath);
        }

        public async Task FinishUpdate(bool NoSuicide = false)
        {
            string newVerTagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "CollapseLauncher", "_NewVer");
            string needInnoLogUpdatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "CollapseLauncher", "_NeedInnoLogUpdate");

            Status.status = string.Format(Lang._UpdatePage.UpdateStatus5 + $" {Lang._UpdatePage.UpdateMessage5}", NewVersionTag.VersionString);
            UpdateStatus();

            Progress = new UpdaterProgress(UpdateStopwatch, 100, 100);
            UpdateProgress();

            File.WriteAllText(newVerTagPath, NewVersionTag.VersionString);
            File.WriteAllText(needInnoLogUpdatePath, NewVersionTag.VersionString);

            if (IsUseLegacyDownload)
            {
                SuicideLegacy();
                return;
            }

            if (!NoSuicide)
                await Suicide();
        }

        private async Task Suicide()
        {
            await Task.Delay(3000);
            UpdateManager.RestartApp();
        }

        private void SuicideLegacy()
        {
            Process applyUpdate = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = applyElevatedPath,
                    UseShellExecute = true
                }
            };
            applyUpdate.Start();
            App.Current.Exit();
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
            public UpdaterProgress(Stopwatch currentStopwatch, int counter, int counterGoal)
            {
                if (counter == 0)
                {
                    TimeLeft = TimeSpan.Zero;
                }

                float elapsedMin = ((float)currentStopwatch.ElapsedMilliseconds / 1000) / 60;
                float minLeft = (elapsedMin / counter) * (counterGoal - counter);
                TimeLeft = TimeSpan.FromMinutes(float.IsInfinity(minLeft) || float.IsNaN(minLeft) ? 0 : minLeft);

                ProgressPercentage = counter;
            }
            public long DownloadedSize { get; private set; }
            public long TotalSizeToDownload { get; private set; }
            public double ProgressPercentage { get; private set; }
            public long CurrentRead { get; private set; }
            public long CurrentSpeed => 0;
            public TimeSpan TimeLeft { get; private set; }
        }
    }
}
