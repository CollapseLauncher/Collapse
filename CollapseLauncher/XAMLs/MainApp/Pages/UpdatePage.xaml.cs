using CollapseLauncher.Helper.Update;
using CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock;
using Hi3Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
#if !USEVELOPACK
using Squirrel;
#else
using Velopack;
#endif
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class UpdatePage : Page
    {
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private MarkdownConfig _markdownConfig = new MarkdownConfig();

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
            CurrentVersionLabel.Text = $"{LauncherUpdateHelper.LauncherCurrentVersionString}";

            if (!LauncherUpdateHelper.AppUpdateVersionProp?.Version.HasValue ?? false)
                throw new NullReferenceException($"New version property in LauncherUpdateHelper.AppUpdateVersionProp should haven't be null!");

            GameVersion NewUpdateVersion = LauncherUpdateHelper.AppUpdateVersionProp.Version.Value;

            NewVersionLabel.Text = NewUpdateVersion.VersionString;
            UpdateChannelLabel.Text = ChannelName;
            AskUpdateCheckbox.IsChecked = GetAppConfigValue("DontAskUpdate").ToBoolNullable() ?? false;
            BuildTimestampLabel.Text = LauncherUpdateHelper.AppUpdateVersionProp?.TimeLocalTime?.ToString("f");

            await GetReleaseNote();

            try
            {
                if (!(LauncherUpdateHelper.AppUpdateVersionProp?.IsForceUpdate ?? false))
                    await WaitForCountdown();

                await StartUpdateRoutine();
            }
            catch (TaskCanceledException)
            {
                Logger.LogWriteLine("Update countdown has been cancelled!", LogType.Default, true);
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
                Logger.LogWriteLine($"Update has failed!\r\n{ex}", LogType.Error, true);
            }
        }

        private async Task StartUpdateRoutine()
        {
            try
            {
                if (LauncherUpdateWatcher.isMetered && !(LauncherUpdateHelper.AppUpdateVersionProp?.IsForceUpdate ?? false))
                {
                    switch (await Dialog_MeteredConnectionWarning(Content))
                    {
                        case ContentDialogResult.Primary:
                            await _StartUpdateRoutine();
                            break;
                        case ContentDialogResult.None:
                            CancelUpdateCountdownBox.Visibility = Visibility.Collapsed;
                            UpdateCountdownPanel.Visibility = Visibility.Collapsed;
                            UpdateBox.Visibility = Visibility.Visible;
                            return;
                    }
                }

                await _StartUpdateRoutine();
            }
            catch (OperationCanceledException)
            {
                Logger.LogWriteLine("Update has been canceled!", LogType.Warning, true);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"Error occurred while updating the launcher!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex, ErrorType.Unhandled);
                ForceInvokeUpdate = true;
                LauncherUpdateWatcher.GetStatus(new LauncherUpdateProperty { QuitFromUpdateMenu = true });
            }
        }

        private async Task _StartUpdateRoutine()
        {
            // Hide/Show progress
            UpdateCountdownPanel.Visibility = Visibility.Collapsed;
            UpdateBox.Visibility = Visibility.Collapsed;
            CancelUpdateCountdownBox.Visibility = Visibility.Collapsed;
            AskUpdateCheckbox.Visibility = Visibility.Collapsed;
            UpdateProgressBox.Visibility = Visibility.Visible;

            // Start Update manager check routine
            await GetUpdateInformation();
        }

        private async Task GetUpdateInformation()
        {
            string ChannelName = IsPreview ? "Preview" : "Stable";

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
            CancelUpdateCountdownBox.Visibility = Visibility.Visible;
            UpdateCountdownPanel.Visibility = Visibility.Visible;
            int maxCount = 10;
            while (maxCount > 0)
            {
                UpdateCountdownText.Text = string.Format(Lang._UpdatePage.UpdateCountdownMessage1, maxCount);
                await Task.Delay(1000, _tokenSource.Token);
                maxCount--;
            }
        }

        private async Task GetReleaseNote()
        {
            ReleaseNotesBox.Text = Lang._UpdatePage.LoadingRelease;

            try
            {
                await using BridgedNetworkStream networkStream = await FallbackCDNUtil.TryGetCDNFallbackStream(string.Format("changelog_{0}.md", IsPreview ? "preview" : "stable"), _tokenSource.Token, true);
                byte[] buffer = new byte[networkStream.Length];
                await networkStream.ReadExactlyAsync(buffer, _tokenSource.Token);

                ReleaseNotesBox.Text = Encoding.UTF8.GetString(buffer);
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

        private async void DoUpdateClick(object sender, RoutedEventArgs e)
        {
            await StartUpdateRoutine();
        }

        private void CancelCountdownClick(object sender, RoutedEventArgs e)
        {
            _tokenSource.Cancel();
            (sender as Button).Visibility = Visibility.Collapsed;
            UpdateCountdownPanel.Visibility = Visibility.Collapsed;
            UpdateBox.Visibility = Visibility.Visible;
        }

        private void RemindMeClick(object sender, RoutedEventArgs e)
        {
            _tokenSource.Cancel();
            ForceInvokeUpdate = true;
            LauncherUpdateWatcher.GetStatus(new LauncherUpdateProperty { QuitFromUpdateMenu = true });
        }

        private void Updater_UpdaterStatusChanged(object sender, Updater.UpdaterStatus e)
        {
            DispatcherQueue?.TryEnqueue(() =>
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
            DispatcherQueue?.TryEnqueue(() =>
            {
                progressBar.IsIndeterminate = false;
                progressBar.Value = e.ProgressPercentage;
                TimeEstimation.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.TimeLeft);
            });
        }
    }
}
