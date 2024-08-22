using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Update;
using Hi3Helper;
using Hi3Helper.Http;
using Squirrel;
using Squirrel.Sources;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher;

public class Updater : IDisposable
{
    // ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
    private string          ChannelURL;
    private string          ChannelName;
    private Stopwatch       UpdateStopwatch;
    private UpdateManager   UpdateManager;
    private IFileDownloader UpdateDownloader;
    private GameVersion     NewVersionTag;

    // ReSharper disable once RedundantDefaultMemberInitializer
    private bool IsUseLegacyDownload = false;
    // ReSharper restore PrivateFieldCanBeConvertedToLocalVariable

    private static readonly string execPath          = Process.GetCurrentProcess().MainModule!.FileName;
    private static readonly string workingDir        = Path.GetDirectoryName(execPath);
    private static readonly string applyElevatedPath = Path.Combine(workingDir, "..\\", $"ApplyUpdate.exe");

    public event EventHandler<UpdaterStatus>   UpdaterStatusChanged;
    public event EventHandler<UpdaterProgress> UpdaterProgressChanged;

    private UpdaterStatus   Status;
    private UpdaterProgress Progress;

    public Updater(string ChannelName)
    {
        this.ChannelName = ChannelName;
        ChannelURL = CombineURLFromString(FallbackCDNUtil.GetPreferredCDN().URLPrefix, "squirrel", this.ChannelName);
        UpdateDownloader = new UpdateManagerHttpAdapter();
        UpdateManager = new UpdateManager(ChannelURL, null, null, UpdateDownloader);
        UpdateStopwatch = Stopwatch.StartNew();
        Status = new UpdaterStatus();
        Progress = new UpdaterProgress(UpdateStopwatch, 0, 100);
    }

    ~Updater()
    {
        Dispose();
    }

    public void Dispose()
    {
        UpdateManager?.Dispose();
    }

    public async Task<UpdateInfo> StartCheck()
    {
        return await UpdateManager.CheckForUpdate();
    }

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
        if (token.IsCancellationRequested) return false;

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

                Status.status = string.Format(Lang._UpdatePage.UpdateStatus4,
                                              LauncherUpdateHelper.LauncherCurrentVersionString);
                Status.message = Lang._UpdatePage.UpdateMessage4;
                UpdateStatus();

                await Task.Delay(3000);
                return false;
            }

            NewVersionTag = new GameVersion(UpdateInfo.ReleasesToApply.FirstOrDefault()!.Version.Version);

            await UpdateManager.DownloadReleases(UpdateInfo.ReleasesToApply, (progress) =>
                                                                             {
                                                                                 Progress =
                                                                                     new
                                                                                         UpdaterProgress(UpdateStopwatch,
                                                                                             progress / 2, 100);
                                                                                 UpdateProgress();
                                                                             });

            await UpdateManager.ApplyReleases(UpdateInfo, (progress) =>
                                                          {
                                                              Progress = new UpdaterProgress(UpdateStopwatch,
                                                                  progress / 2 + 50, 100);
                                                              UpdateProgress();
                                                          });
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"Failed while running update via Squirrel. Failback to legacy method...\r\n{ex}",
                                LogType.Error, true);
            IsUseLegacyDownload = true;
            await StartLegacyUpdate();
        }

        return true;
    }

    private async Task StartLegacyUpdate()
    {
        // Initialize new proxy-aware HttpClient
        using HttpClient client = new HttpClientBuilder()
            .UseLauncherConfig()
            .SetAllowedDecompression(DecompressionMethods.None)
            .Create();

        using (var _httpClient = new Http(true, customHttpClient: client))
        {
            UpdateStopwatch = Stopwatch.StartNew();

            await using (var stream =
                         await FallbackCDNUtil.TryGetCDNFallbackStream($"{ChannelName.ToLower()}/fileindex.json"))
            {
                var updateInfo =
                    await stream.DeserializeAsync<AppUpdateVersionProp>(InternalAppJSONContext.Default);
                var gameVersion = updateInfo!.Version;

                if (gameVersion != null) NewVersionTag = gameVersion.Value!;
                UpdateStatus();
                UpdateProgress();
            }

            FallbackCDNUtil.DownloadProgress += FallbackCDNUtil_DownloadProgress;
            await FallbackCDNUtil.DownloadCDNFallbackContent(_httpClient, applyElevatedPath,
                                                             Environment.ProcessorCount > 8
                                                                 ? 8
                                                                 : Environment.ProcessorCount,
                                                             $"{ChannelName.ToLower()}/ApplyUpdate.exe", default);
            FallbackCDNUtil.DownloadProgress -= FallbackCDNUtil_DownloadProgress;

            await File.WriteAllTextAsync(Path.Combine(workingDir, "..\\", "release"), ChannelName.ToLower());
        }
    }

    private void FallbackCDNUtil_DownloadProgress(object sender, DownloadEvent e)
    {
        Progress = new UpdaterProgress(UpdateStopwatch, (int)e.ProgressPercentage, 100);
        UpdateProgress();
    }

    private bool DoesLatestVersionExist(string versionString)
    {
        var filePath = Path.Combine(AppFolder, $"..\\app-{versionString}\\{Path.GetFileName(AppExecutablePath)}");

        return File.Exists(filePath);
    }

    public async Task FinishUpdate(bool NoSuicide = false)
    {
        var newVerTagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData",
                                         "LocalLow", "CollapseLauncher", "_NewVer");
        var needInnoLogUpdatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                                 "AppData", "LocalLow", "CollapseLauncher", "_NeedInnoLogUpdate");

        Status.status = string.Format(Lang._UpdatePage.UpdateStatus5 + $" {Lang._UpdatePage.UpdateMessage5}",
                                      NewVersionTag.VersionString);
        UpdateStatus();

        Progress = new UpdaterProgress(UpdateStopwatch, 100, 100);
        UpdateProgress();

        File.WriteAllText(newVerTagPath,         NewVersionTag.VersionString);
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
        var applyUpdate = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName        = applyElevatedPath,
                UseShellExecute = true
            }
        };
        applyUpdate.Start();
        (WindowUtility.CurrentWindow as MainWindow)?.CloseApp();
    }

    public void UpdateStatus()
    {
        UpdaterStatusChanged?.Invoke(this, Status);
    }

    public void UpdateProgress()
    {
        UpdaterProgressChanged?.Invoke(this, Progress);
    }

    public class UpdaterStatus
    {
        public string status { get; set; }

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public string message { get; set; }

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public string newver { get; set; }
    }

    public class UpdaterProgress
    {
        public UpdaterProgress(Stopwatch currentStopwatch, int counter, int counterGoal)
        {
            if (counter == 0) TimeLeft = TimeSpan.Zero;

            var elapsedMin = (float)currentStopwatch.ElapsedMilliseconds / 1000 / 60;
            var minLeft    = elapsedMin / counter * (counterGoal - counter);
            TimeLeft = TimeSpan.FromMinutes(minLeft.UnNaNInfinity());

            ProgressPercentage = counter;
        }

        // ReSharper disable UnusedMember.Global
        // ReSharper disable UnusedAutoPropertyAccessor.Local
        public long DownloadedSize { get; private set; }

        public long TotalSizeToDownload { get; private set; }

        public double ProgressPercentage { get; private set; }

        public long CurrentRead { get; private set; }

        public long CurrentSpeed => 0;

        public TimeSpan TimeLeft { get; private set; }
        // ReSharper restore UnusedMember.Global
        // ReSharper restore UnusedAutoPropertyAccessor.Local
    }
}