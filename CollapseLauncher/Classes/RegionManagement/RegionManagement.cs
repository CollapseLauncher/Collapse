using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Hi3Helper.Http;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.GameConfig;
using static Hi3Helper.Shared.Region.InstallationManagement;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        Http httpHelper = new Http(default, 4, 250);
        CancellationTokenSource tokenSource;
        bool loadRegionComplete;
        int LoadTimeoutSec = 10;
        int LoadTimeoutJump = 2;
        public async Task LoadRegion(int regionIndex = 0)
        {
            int prevTimeout = LoadTimeoutSec;
            loadRegionComplete = false;
            CurrentRegion = ConfigStore.Config[regionIndex];
            previousTag = "launcher";
            NavigationViewControl.MenuItems.Clear();
            NavigationViewControl.IsSettingsVisible = false;
            LoadGameRegionFile();
            LogWriteLine($"Initializing Region {CurrentRegion.ZoneName}...");
            DispatcherQueue.TryEnqueue(() => LoadingFooter.Text = "");
            await HideLoadingPopup(false, Lang._MainPage.RegionLoadingTitle, CurrentRegion.ZoneName);
            ResetRegionProp();
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
            if (regionResourceProp.data == null)
            {
                MainFrameChanger.ChangeWindowFrame(typeof(DisconnectedPage));
                return;
            }
            InitializeNavigationItems();
        }

        private async void PushRegionNotification(string RegionProfileName)
        {
            if (InnerLauncherConfig.NotificationData.RegionPush == null) return;
            await Task.Run(() =>
            {
                InnerLauncherConfig.NotificationData.EliminatePushList();
                foreach (NotificationProp Entry in InnerLauncherConfig.NotificationData.RegionPush)
                {
                    if (Entry.RegionProfile == RegionProfileName && (Entry.ValidForVerBelow == null
                            || (LauncherUpdateWatcher.CompareVersion(AppCurrentVersion, Entry.ValidForVerBelow)
                            && LauncherUpdateWatcher.CompareVersion(Entry.ValidForVerAbove, AppCurrentVersion))
                            || LauncherUpdateWatcher.CompareVersion(AppCurrentVersion, Entry.ValidForVerBelow)))
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
            }).ConfigureAwait(false);
        }

        private async Task<bool> TryGetRegionResource()
        {
            try
            {
                regionNewsProp = new HomeMenuPanel();
                tokenSource = new CancellationTokenSource();
                RetryWatcher();

                await ChangeBackgroundImageAsRegion(tokenSource.Token);
                await FetchLauncherResourceAsRegion(tokenSource.Token);

                tokenSource.Cancel();

                loadRegionComplete = true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
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
            gameIni.Profile.Load(gameIni.ProfilePath);
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
