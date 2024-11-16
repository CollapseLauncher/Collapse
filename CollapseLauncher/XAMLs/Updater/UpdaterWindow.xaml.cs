using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Update;
using Hi3Helper;
using Hi3Helper.Http;
using Hi3Helper.Http.Legacy;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Hi3Helper.SentryHelper;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher;

public sealed partial class UpdaterWindow
{
    private static string execPath   = Process.GetCurrentProcess().MainModule!.FileName;
    
    public static  string workingDir = Path.GetDirectoryName(execPath);
    public static  string sourcePath = Path.Combine(workingDir, Path.GetFileName(execPath)!);
    public static  string applyPath  = Path.Combine(workingDir, $"ApplyUpdate.exe");
    
    private static string applyElevatedPath = Path.Combine(workingDir, "..\\", $"ApplyUpdate.exe");

    public static string elevatedPath =
        Path.Combine(workingDir, Path.GetFileNameWithoutExtension(sourcePath) + ".Elevated.exe");
    public static string launcherPath = Path.Combine(workingDir, "CollapseLauncher.exe");

    private static Stopwatch CurrentStopwatch = Stopwatch.StartNew();

    public UpdaterWindow()
    {
        InitializeComponent();
        InitializeWindowSettings();
// ReSharper disable RedundantAssignment
        var title = $"Collapse Launcher Updater";
        if (IsPreview)
            Title = title += "[PREVIEW]";
    #if DEBUG
        Title = title += "[DEBUG]";
    #endif
        UpdateChannelLabel.Text  = m_arguments.Updater.UpdateChannel.ToString();
        CurrentVersionLabel.Text = LauncherUpdateHelper.LauncherCurrentVersionString;
// ReSharper restore RedundantAssignment

        StartAsyncRoutine();
    }

    private async void StartAsyncRoutine()
    {
        try
        {
            var newVerTagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                             "AppData", "LocalLow", "CollapseLauncher", "_NewVer");
            progressBar.IsIndeterminate = true;
            UpdateChannelLabel.Text = m_arguments.Updater.UpdateChannel.ToString();
            ActivityStatus.Text = Lang._UpdatePage.UpdateMessage1;

            await using var metadataStream =
                await
                    FallbackCDNUtil
                       .TryGetCDNFallbackStream($"{m_arguments.Updater.UpdateChannel.ToString().ToLower()}/fileindex.json",
                                                default);
            var updateInfo =
                await metadataStream.DeserializeAsync(InternalAppJSONContext.Default.AppUpdateVersionProp, default);
            NewVersionLabel.Text = updateInfo!.VersionString;

            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder()
                .UseLauncherConfig()
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            DownloadClient downloadClient = DownloadClient.CreateInstance(client);

            CurrentStopwatch.Restart();

            FallbackCDNUtil.DownloadProgress += FallbackCDNUtil_DownloadProgress;
            await FallbackCDNUtil.DownloadCDNFallbackContent(downloadClient, applyElevatedPath,
                                                             Environment.ProcessorCount > 8
                                                                 ? 8
                                                                 : Environment.ProcessorCount,
                                                             $"{m_arguments.Updater.UpdateChannel.ToString().ToLower()}/ApplyUpdate.exe",
                                                             default);
            FallbackCDNUtil.DownloadProgress -= FallbackCDNUtil_DownloadProgress;

            await File.WriteAllTextAsync(Path.Combine(workingDir, "..\\", "release"),
                                         m_arguments.Updater.UpdateChannel.ToString().ToLower());
            if (updateInfo.Version != null)
            {
                var ver = updateInfo.Version.Value;
                Status.Text = string.Format(Lang._UpdatePage.UpdateStatus5, ver.VersionString);
            }

            ActivityStatus.Text = Lang._UpdatePage.UpdateMessage5;

            await File.WriteAllTextAsync(newVerTagPath, updateInfo.VersionString);

            await Task.Delay(5000);
            var applyUpdate = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = applyElevatedPath,
                    UseShellExecute = true
                }
            };
            applyUpdate.Start();

            Application.Current.Exit();
        }
        catch (Exception ex)
        {
            LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
            await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
        }
    }

    private void FallbackCDNUtil_DownloadProgress(object sender, DownloadEvent e)
    {
        double speed = e.SizeDownloaded / CurrentStopwatch.Elapsed.TotalSeconds;
        TimeSpan timeLeft = ((e.SizeToBeDownloaded - e.SizeDownloaded) / speed).ToTimeSpanNormalized();

        DispatcherQueue?.TryEnqueue(() =>
                                    {
                                        progressBar.IsIndeterminate = false;
                                        progressBar.Value = e.ProgressPercentage;
                                        ActivityStatus.Text = string.Format(Lang._UpdatePage.UpdateStatus3, 1, 1);
                                        ActivitySubStatus.Text =
                                            $"{SummarizeSizeSimple(e.SizeDownloaded)} / {SummarizeSizeSimple(e.SizeToBeDownloaded)}";

                                        SpeedStatus.Text =
                                            string.Format(Lang._Misc.SpeedPerSec, SummarizeSizeSimple(speed));
                                        TimeEstimation.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, timeLeft);
                                    });
    }

    private void InitializeAppWindowAndIntPtr()
    {
        InitializeComponent();
        Activate();
        WindowUtility.RegisterWindow(this);
    }

    private void InitializeWindowSettings()
    {
        InitializeAppWindowAndIntPtr();

        WindowUtility.SetWindowSize(540, 320);
        WindowUtility.CurrentWindowTitlebarExtendContent = true;
        WindowUtility.CurrentWindow!.SetTitleBar(DragArea);

        WindowUtility.CurrentWindowIsResizable   = false;
        WindowUtility.CurrentWindowIsMaximizable = false;
        WindowUtility.ApplyWindowTitlebarLegacyColor();
    }
}