using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Hi3Helper.Preset;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.Logger;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.Shared.Region.InstallationManagement;
using static Hi3Helper.Shared.Region.GameSettingsManagement;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        HttpClientHelper httpHelper;
        CancellationTokenSource tokenSource;
        bool loadRegionComplete;
        Task loader = new Task(() => { });
        int LoadTimeoutSec = 10;
        int LoadTimeoutJump = 2;
        public async Task LoadRegion(int regionIndex = 0)
        {
            int prevTimeout = LoadTimeoutSec;
            loadRegionComplete = false;
            CurrentRegion = ConfigStore.Config[regionIndex];
            previousTag = "launcher";
            LoadGameRegionFile();
            LogWriteLine($"Initializing Region {CurrentRegion.ZoneName}...");
            DispatcherQueue.TryEnqueue(() => LoadingFooter.Text = "");
            await HideLoadingPopup(false, "Loading Region", CurrentRegion.ZoneName);
            while (!await TryGetRegionResource())
            {
                int lastTimeout = LoadTimeoutSec;
                DispatcherQueue.TryEnqueue(() => LoadingFooter.Text = string.Format(Lang._MainPage.RegionLoadingSubtitleTimeOut, CurrentRegion.ZoneName, lastTimeout * 2));
                LogWriteLine($"Loading Region {CurrentRegion.ZoneName} has timed-out (> {lastTimeout * 2} seconds). Retrying...", Hi3Helper.LogType.Warning);

                LoadTimeoutSec += LoadTimeoutJump;
            }
            LoadTimeoutSec = prevTimeout;
            LogWriteLine($"Initializing Region {CurrentRegion.ZoneName} Done!");
            InitRegKey();
            PushRegionNotification(CurrentRegion.ProfileName);
            await HideLoadingPopup(true, Lang._MainPage.RegionLoadingTitle, CurrentRegion.ZoneName);
            InitializeNavigationItems();
            // HideBackgroundImage(false);
        }

        private async void PushRegionNotification(string RegionProfileName)
        {
            await Task.Run(() =>
            {
                InnerLauncherConfig.NotificationData.EliminatePushList();
                foreach (NotificationProp Entry in InnerLauncherConfig.NotificationData.RegionPush)
                {
                    if (Entry.RegionProfile == RegionProfileName)
                    {
                        NotificationSender.SendNotification(new NotificationInvokerProp
                        {
                            CloseAction = null,
                            IsAppNotif = false,
                            Notification = Entry,
                            OtherContent = null
                        });
                    }
                }
            });
        }

        private async Task<bool> TryGetRegionResource()
        {
            try
            {
                await Task.Run(() =>
                {
                    tokenSource = new CancellationTokenSource();
                    RetryWatcher();

                    ChangeBackgroundImageAsRegion(tokenSource.Token);
                    FetchLauncherResourceAsRegion(tokenSource.Token);

                    tokenSource.Cancel();

                    loadRegionComplete = true;
                });
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            return true;
        }

        private async void RetryWatcher()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(LoadTimeoutSec * 1000, tokenSource.Token);
                    DispatcherQueue.TryEnqueue(() => LoadingFooter.Text = Lang._MainPage.RegionLoadingSubtitleTooLong);
                    if (!loadRegionComplete)
                    {
                        await Task.Delay(LoadTimeoutSec * 1000, tokenSource.Token);
                        tokenSource.Cancel();
                    }
                    else
                        return;
                }
                catch
                {
                    return;
                }
            }
        }

        private void LoadGameRegionFile()
        {
            gameIni = new GameIniStruct();

            gamePath = Path.Combine(AppGameFolder, CurrentRegion.ProfileName);
            DispatcherQueue.TryEnqueue(() => ComboBoxGameRegion.PlaceholderText = CurrentRegion.ZoneName);
            gameIni.ProfilePath = Path.Combine(gamePath, $"config.ini");

            if (!Directory.Exists(gamePath))
                Directory.CreateDirectory(gamePath);

            if (!File.Exists(gameIni.ProfilePath))
                PrepareInstallation();

            gameIni.Profile = new IniFile();
            gameIni.ProfileStream = new FileStream(gameIni.ProfilePath, FileMode.Open, FileAccess.ReadWrite);
            gameIni.Profile.Load(gameIni.ProfileStream);
        }

        public async void ChangeRegion(object sender, RoutedEventArgs e)
        {
            ChangeRegionBtn.IsEnabled = false;
            ChangeRegionConfirmBtn.Flyout.Hide();
            ChangeRegionConfirmProgressBar.Visibility = Visibility.Visible;
            appIni.Profile["app"]["CurrentRegion"] = ComboBoxGameRegion.SelectedIndex;
            SaveAppConfig();
            MainFrameChanger.ChangeMainFrame(typeof(Pages.BlankPage));
            await InvokeLoadRegion(ComboBoxGameRegion.SelectedIndex);
            if (ChangeRegionConfirmBtn.Flyout is Flyout f)
            {
                MainFrameChanger.ChangeMainFrame(typeof(Pages.HomePage));
                ChangeRegionConfirmProgressBar.Visibility = Visibility.Collapsed;
                f.Hide();
                ChangeRegionConfirmBtn.IsEnabled = false;
                ChangeRegionBtn.IsEnabled = true;
                LogWriteLine($"Region changed to {ComboBoxGameRegion.SelectedValue}", Hi3Helper.LogType.Scheme);
            }
        }

        private async Task InvokeLoadRegion(int index) => await LoadRegion(index);
    }
}
