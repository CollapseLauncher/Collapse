using CommunityToolkit.WinUI.UI.Controls;
using Hi3Helper;
using Hi3Helper.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Squirrel;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class UpdatePage : Page
    {
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();

        public UpdatePage()
        {
            this.InitializeComponent();
            this.Loaded += LoadedAsyncRoutine;
            this.Unloaded += UpdatePage_Unloaded;
        }

        private void UpdatePage_Unloaded(object sender, RoutedEventArgs e) => ChangeTitleDragArea.Change(DragAreaTemplate.Default);

        private async void LoadedAsyncRoutine(object sender, RoutedEventArgs e)
        {
            ChangeTitleDragArea.Change(DragAreaTemplate.Full);

            string ChannelName = IsPreview ? Lang._Misc.BuildChannelPreview : Lang._Misc.BuildChannelStable;
            if (IsPortable)
                ChannelName += "-Portable";
            CurrentVersionLabel.Text = $"{AppCurrentVersion.VersionString}";

            GameVersion NewUpdateVersion = new GameVersion(LauncherUpdateWatcher.UpdateProperty.ver);

            NewVersionLabel.Text = NewUpdateVersion.VersionString;
            UpdateChannelLabel.Text = ChannelName;
            AskUpdateCheckbox.IsChecked = GetAppConfigValue("DontAskUpdate").ToBoolNullable() ?? false;
            BuildTimestampLabel.Text = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
                                        .AddSeconds(LauncherUpdateWatcher.UpdateProperty.time)
                                        .ToLocalTime().ToString("f");

            await GetReleaseNote();
            await StartUpdateRoutine();
        }

        private async Task StartUpdateRoutine()
        {
            try
            {
                // Wait for countdown
                await WaitForCountdown();

                // Hide/Show progress
                UpdateCountdownPanel.Visibility = Visibility.Collapsed;
                UpdateBtnBox.Visibility = Visibility.Collapsed;
                AskUpdateCheckbox.Visibility = Visibility.Collapsed;
                UpdateProgressBox.Visibility = Visibility.Visible;

                // Start Squirrel update routine
                await GetSquirrelUpdate();
            }
            catch (OperationCanceledException)
            {
                Logger.LogWriteLine("Update has been canceled!", LogType.Warning, true);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"Error occurred while updating the launcher!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex, ErrorType.Unhandled);
            }
        }

        private async Task GetSquirrelUpdate()
        {
            string ChannelName = (IsPreview ? "Preview" : "Stable");
            if (IsPortable) ChannelName += "Portable";

            string ExecutableLocation = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            Updater updater = new Updater(ChannelName.ToLower());
            updater.UpdaterProgressChanged += Updater_UpdaterProgressChanged;
            updater.UpdaterStatusChanged += Updater_UpdaterStatusChanged;

            UpdateInfo updateInfo = await updater.StartCheck();

            if (!await updater.StartUpdate(updateInfo))
            {
                RemindMeClick(new Button(), new RoutedEventArgs());
                return;
            }
            await updater.FinishUpdate();
        }

        private async Task WaitForCountdown()
        {
            UpdateCountdownPanel.Visibility = Visibility.Visible;
            int maxCount = 5;
            while (maxCount > 0)
            {
                UpdateCountdownText.Text = string.Format("Your launcher will be updated in {0}...", maxCount);
                await Task.Delay(1000, _tokenSource.Token);
                maxCount--;
            }
        }

        private async Task GetReleaseNote()
        {
            ReleaseNotesBox.Text = Lang._UpdatePage.LoadingRelease;

            try
            {
                string Content = "";
                using (Http _httpClient = new Http(true))
                using (MemoryStream _stream = new MemoryStream())
                {
                    await FallbackCDNUtil.DownloadCDNFallbackContent(_httpClient, _stream, string.Format("changelog_{0}.md", IsPreview ? "preview" : "stable"), _tokenSource.Token);
                    Content = Encoding.UTF8.GetString(_stream.ToArray());
                }

                ReleaseNotesBox.Text = Content;
            }
            catch (Exception ex)
            {
                ReleaseNotesBox.Text = string.Format(Lang._UpdatePage.LoadingReleaseFailed, ex);
            }
        }

        private void AskUpdateToggle(object sender, RoutedEventArgs e)
        {
            bool AskForUpdateLater = (sender as CheckBox).IsChecked ?? false;
            SetAndSaveConfigValue("DontAskUpdate", AskForUpdateLater);
        }

        private void RemindMeClick(object sender, RoutedEventArgs e)
        {
            _tokenSource.Cancel();
            ForceInvokeUpdate = true;
            LauncherUpdateWatcher.GetStatus(new LauncherUpdateProperty { QuitFromUpdateMenu = true });
        }

        private void Updater_UpdaterStatusChanged(object sender, Updater.UpdaterStatus e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Status.Text = e.status;
                if (!string.IsNullOrEmpty(e.newver))
                {
                    GameVersion Version = new GameVersion(e.newver);
                    NewVersionLabel.Text = Version.VersionString;
                }
            });
        }

        private void Updater_UpdaterProgressChanged(object sender, Updater.UpdaterProgress e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.IsIndeterminate = false;
                progressBar.Value = e.ProgressPercentage;
                TimeEstimation.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.TimeLeft);
            });
        }

        private async void ReleaseNotesBox_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            await Task.Run(() =>
            {
                new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = e.Link
                    }
                }.Start();
            });
        }

        private async void ReleaseNotesBox_ImageClicked(object sender, LinkClickedEventArgs e)
        {
            await Task.Run(() =>
            {
                new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = e.Link
                    }
                }.Start();
            });
        }
    }
}
