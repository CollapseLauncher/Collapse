using Hi3Helper.Data;
using Hi3Helper.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.Locale;

namespace CollapseLauncher.Pages
{
    public sealed partial class UpdatePage : Page
    {
        public UpdatePage()
        {
            this.InitializeComponent();
            RunAsyncTasks();
        }

        public async void RunAsyncTasks()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                string ChannelName = IsPreview ? Lang._Misc.BuildChannelPreview : Lang._Misc.BuildChannelStable;
                if (IsPortable)
                    ChannelName += "-Portable";
                CurrentVersionLabel.Text = $"{AppCurrentVersion}";
                NewVersionLabel.Text = LauncherUpdateWatcher.UpdateProperty.ver;
                UpdateChannelLabel.Text = ChannelName;
                AskUpdateCheckbox.IsChecked = GetAppConfigValue("DontAskUpdate").ToBoolNullable() ?? false;
                BuildTimestampLabel.Text = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
                                            .AddSeconds(LauncherUpdateWatcher.UpdateProperty.time)
                                            .ToLocalTime().ToString("f");
            });

            await GetReleaseNote();
        }

        public async Task GetReleaseNote()
        {
            DispatcherQueue.TryEnqueue(() => ReleaseNotesBox.Text = Lang._UpdatePage.LoadingRelease);

            MemoryStream ResponseStream = new MemoryStream();
            string ReleaseNoteURL = string.Format(UpdateRepoChannel + "changelog_{0}", IsPreview ? "preview" : "stable");

            try
            {
                await new Http().DownloadStream(ReleaseNoteURL, ResponseStream, new CancellationToken());
                string Content = Encoding.UTF8.GetString(ResponseStream.ToArray());

                DispatcherQueue.TryEnqueue(() => ReleaseNotesBox.Text = Content);
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() => ReleaseNotesBox.Text = string.Format(Lang._UpdatePage.LoadingReleaseFailed, ex));
            }
        }

        private void AskUpdateToggle(object sender, RoutedEventArgs e)
        {
            bool AskForUpdateLater = (sender as CheckBox).IsChecked ?? false;
            SetAndSaveConfigValue("DontAskUpdate", AskForUpdateLater);
        }

        private void RemindMeClick(object sender, RoutedEventArgs e)
        {
            ForceInvokeUpdate = true;
            LauncherUpdateWatcher.GetStatus(new LauncherUpdateProperty { QuitFromUpdateMenu = true });
        }

        private void DoUpdateClick(object sender, RoutedEventArgs e)
        {
            UpdateBtnBox.Visibility = Visibility.Collapsed;
            AskUpdateCheckbox.Visibility = Visibility.Collapsed;
            UpdateProgressBox.Visibility = Visibility.Visible;

            StartUpdateRoutine();
        }

        private async void StartUpdateRoutine()
        {
            string ChannelName = (IsPreview ? "Preview" : "Stable");
            if (IsPortable)
                ChannelName += "Portable";

            string ExecutableLocation = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            Updater updater = new Updater(ExecutableLocation.Replace('\\', '/'), ChannelName.ToLower(), (byte)GetAppConfigValue("DownloadThread").ToInt());
            updater.UpdaterProgressChanged += Updater_UpdaterProgressChanged;
            updater.UpdaterStatusChanged += Updater_UpdaterStatusChanged;
            
            await updater.StartFetch();
            await updater.StartCheck();
            await updater.StartUpdate();
            await updater.FinishUpdate();
        }

        private void Updater_UpdaterStatusChanged(object sender, Updater.UpdaterStatus e)
        {
            Status.Text = e.status;
            ActivityStatus.Text = e.message;
            NewVersionLabel.Text = e.newver;
        }

        private void Updater_UpdaterProgressChanged(object sender, Updater.UpdaterProgress e)
        {
            progressBar.IsIndeterminate = false;
            progressBar.Value = e.ProgressPercentage;
            ActivitySubStatus.Text = string.Format(Lang._Misc.PerFromTo, SummarizeSizeSimple(e.DownloadedSize), SummarizeSizeSimple(e.TotalSizeToDownload));
            SpeedStatus.Text = string.Format(Lang._Misc.SpeedPerSec, SummarizeSizeSimple(e.CurrentSpeed));
            TimeEstimation.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.TimeLeft);
        }
    }
}
