using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Update;
using Hi3Helper;
using Hi3Helper.Http;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class UpdaterWindow : Window
    {
        public static string execPath = Process.GetCurrentProcess().MainModule.FileName;
        public static string workingDir = Path.GetDirectoryName(execPath);
        public static string sourcePath = Path.Combine(workingDir, Path.GetFileName(execPath));
        public static string applyPath = Path.Combine(workingDir, $"ApplyUpdate.exe");
        public static string applyElevatedPath = Path.Combine(workingDir, "..\\", $"ApplyUpdate.exe");
        public static string elevatedPath = Path.Combine(workingDir, Path.GetFileNameWithoutExtension(sourcePath) + ".Elevated.exe");
        public static string launcherPath = Path.Combine(workingDir, "CollapseLauncher.exe");

        public UpdaterWindow()
        {
            this.InitializeComponent();
            InitializeWindowSettings();

            string title = $"Collapse Launcher Updater";
            if (IsPreview)
                this.Title = title += "[PREVIEW]";
#if DEBUG
            this.Title = title += "[DEBUG]";
#endif
            UpdateChannelLabel.Text = m_arguments.Updater.UpdateChannel.ToString();
            CurrentVersionLabel.Text = LauncherUpdateHelper.LauncherCurrentVersionString;

            StartAsyncRoutine();
        }

        private async void StartAsyncRoutine()
        {
            try
            {
                string newVerTagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "CollapseLauncher", "_NewVer");
                progressBar.IsIndeterminate = true;
                UpdateChannelLabel.Text = m_arguments.Updater.UpdateChannel.ToString();
                ActivityStatus.Text = Lang._UpdatePage.UpdateMessage1;

                AppUpdateVersionProp updateInfo = new AppUpdateVersionProp();

                await using BridgedNetworkStream metadataStream = await FallbackCDNUtil.TryGetCDNFallbackStream($"{m_arguments.Updater.UpdateChannel.ToString().ToLower()}/fileindex.json", default);
                updateInfo = await metadataStream.DeserializeAsync<AppUpdateVersionProp>(InternalAppJSONContext.Default, default);
                NewVersionLabel.Text = updateInfo.VersionString;

                using (Http _httpClient = new Http(true))
                {
                    FallbackCDNUtil.DownloadProgress += FallbackCDNUtil_DownloadProgress;
                    await FallbackCDNUtil.DownloadCDNFallbackContent(_httpClient, applyElevatedPath, Environment.ProcessorCount > 8 ? 8 : Environment.ProcessorCount, $"{m_arguments.Updater.UpdateChannel.ToString().ToLower()}/ApplyUpdate.exe", default);
                    FallbackCDNUtil.DownloadProgress -= FallbackCDNUtil_DownloadProgress;
                }

                File.WriteAllText(Path.Combine(workingDir, "..\\", "release"), m_arguments.Updater.UpdateChannel.ToString().ToLower());
                GameVersion ver = updateInfo.Version.Value;
                Status.Text = string.Format(Lang._UpdatePage.UpdateStatus5, ver.VersionString);
                ActivityStatus.Text = Lang._UpdatePage.UpdateMessage5;

                File.WriteAllText(newVerTagPath, updateInfo.VersionString);

                await Task.Delay(5000);
                Process applyUpdate = new Process()
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
            }
        }

        private void FallbackCDNUtil_DownloadProgress(object sender, DownloadEvent e)
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                progressBar.IsIndeterminate = false;
                progressBar.Value = e.ProgressPercentage;
                ActivityStatus.Text = string.Format(Lang._UpdatePage.UpdateStatus3, 1, 1);
                ActivitySubStatus.Text = $"{SummarizeSizeSimple(e.SizeDownloaded)} / {SummarizeSizeSimple(e.SizeToBeDownloaded)}";

                SpeedStatus.Text = string.Format(Lang._Misc.SpeedPerSec, SummarizeSizeSimple(e.Speed));
                TimeEstimation.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.TimeLeft);
            });
        }

        private void InitializeAppWindowAndIntPtr()
        {
            this.InitializeComponent();
            this.Activate();
            WindowUtility.RegisterWindow(this);
        }

        public void InitializeWindowSettings()
        {
            InitializeAppWindowAndIntPtr();

            WindowUtility.SetWindowSize(540, 320);
            WindowUtility.CurrentWindowTitlebarExtendContent = true;
            WindowUtility.CurrentWindow.SetTitleBar(DragArea);

            WindowUtility.CurrentWindowIsResizable = false;
            WindowUtility.CurrentWindowIsMaximizable = false;
            WindowUtility.ApplyWindowTitlebarLegacyColor();
        }
    }
}
