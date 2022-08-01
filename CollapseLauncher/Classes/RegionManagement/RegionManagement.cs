using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.GameConfig;
using static Hi3Helper.Shared.Region.InstallationManagement;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static CollapseLauncher.InnerLauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        private Http Http = new Http(default, 4, 250);
        private bool IsLoadRegionComplete;
        private bool IsInnerTaskSuccess;
        private string PreviousTag = string.Empty;

        private uint CurrentRetry;
        private uint MaxRetry = 5; // Max 5 times of retry attempt
        private uint LoadTimeout = 10; // 10 seconds of initial Load Timeout
        private uint LoadTimeoutStep = 5; // Step 5 seconds for each timeout retries

        public async Task LoadRegionByIndex(uint Index = 0)
        {
            // If Index > Length of the region list, return back to 0
            if ((Index + 1) > ConfigStore.Config.Count) Index = 0;

            // Set CurrentRegion from Index
            SetCurrentRegionByIndex(Index);
            LogWriteLine($"Initializing Region {CurrentRegion.ZoneName}...", Hi3Helper.LogType.Scheme, true);

            // Clear MainPage State, like NavigationView, Load State, etc.
            await ClearMainPageState();

            // Load Game Region File
            LoadGameRegionFile();

            // Load Region Resource from Launcher API
            await TryLoadGameRegionTask(FetchLauncherLocalizedResources(), LoadTimeout, LoadTimeoutStep);
            await ChangeBackgroundImageAsRegion();
            await TryLoadGameRegionTask(FetchLauncherResourceAsRegion(), LoadTimeout, LoadTimeoutStep);

            // Finalize Region Load
            await FinalizeLoadRegion();
        }

        public async Task ClearMainPageState()
        {
            // Reset Retry Counter
            CurrentRetry = 0;

            // Set IsLoadRegionComplete to false
            IsLoadRegionComplete = false;

            // Clear NavigationViewControl Items and Reset Region props
            NavigationViewControl.MenuItems.Clear();
            NavigationViewControl.IsSettingsVisible = false;
            PreviousTag = "launcher";
            ResetRegionProp();

            // Set LoadingFooter empty
            LoadingFooter.Text = string.Empty;

            // Toggle Loading Popup
            await HideLoadingPopup(false, Lang._MainPage.RegionLoadingTitle, CurrentRegion.ZoneName);

            IsLoadRegionComplete = true;
        }

        private async Task TryLoadGameRegionTask(Task InnerTask, uint Timeout, uint TimeoutStep)
        {
            while (await IsTryLoadGameRegionTaskFail(InnerTask, Timeout))
            {
                // If true (fail), then log the retry attempt
                LoadingFooter.Text = string.Format(Lang._MainPage.RegionLoadingSubtitleTimeOut, CurrentRegion.ZoneName, LoadTimeout);
                LogWriteLine($"Loading Region {CurrentRegion.ZoneName} has timed-out (> {Timeout} seconds). Retrying...", Hi3Helper.LogType.Warning);
                Timeout += TimeoutStep;
                CurrentRetry++;
            }
        }

        private async Task<bool> IsTryLoadGameRegionTaskFail(Task InnerTask, uint Timeout)
        {
            // Check for Retry Count. If it reaches max, then return to DisconnectedPage
            if (CurrentRetry > MaxRetry)
            {
                MainFrameChanger.ChangeWindowFrame(typeof(DisconnectedPage));
                return false;
            }

            // Reset Task State
            CancellationTokenSource InnerTokenSource = new CancellationTokenSource();
            IsInnerTaskSuccess = false;

            try
            {
                // Run Inner Task and watch for timeout
                WatchAndCancelIfTimeout(InnerTokenSource, Timeout);
                await InnerTask.WaitAsync(InnerTokenSource.Token);
                IsInnerTaskSuccess = true;
            }
            catch (OperationCanceledException)
            {
                // Return false if cancel triggered
                return true;
            }

            // Return false if successful
            return false;
        }

        private async void WatchAndCancelIfTimeout(CancellationTokenSource TokenSource, uint Timeout)
        {
            // Wait until it timeout
            await Task.Delay((int)Timeout * 1000, TokenSource.Token);

            // If InnerTask still not loaded successfully, then cancel it
            if (!IsInnerTaskSuccess || !IsLoadRegionComplete)
            {
                TokenSource.Cancel();
            }
        }

        private void SetCurrentRegionByIndex(uint Index)
        {
            CurrentRegion = ConfigStore.Config[(int)Index];
            ComboBoxGameRegion.SelectedIndex = (int)Index;
        }

        private async Task FinalizeLoadRegion()
        {
            // Init Registry Key and Push Region Notification
            InitRegKey();
            PushRegionNotification(CurrentRegion.ProfileName);

            // Init NavigationPanel Items
            if (m_appMode != AppMode.Hi3CacheUpdater)
            {
                InitializeNavigationItems();
            }
            else
            {
                NavigationViewControl.IsSettingsVisible = false;
            }

            // Set LoadingFooter empty
            LoadingFooter.Text = string.Empty;

            // Hide Loading Popup
            await HideLoadingPopup(true, Lang._MainPage.RegionLoadingTitle, CurrentRegion.ZoneName);

            // Log if region has been successfully loaded
            LogWriteLine($"Initializing Region {CurrentRegion.ZoneName} Done!", Hi3Helper.LogType.Scheme, true);
        }

        private void PushRegionNotification(string RegionProfileName)
        {
            if (NotificationData.RegionPush == null) return;

            NotificationData.EliminatePushList();
            foreach (NotificationProp Entry in NotificationData.RegionPush)
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
        }

        private void LoadGameRegionFile()
        {
            gameIni = new GameIniStruct();

            gamePath = Path.Combine(AppGameFolder, CurrentRegion.ProfileName);
            gameIni.ProfilePath = Path.Combine(gamePath, $"config.ini");

            ComboBoxGameRegion.PlaceholderText = CurrentRegion.ZoneName;

            if (!Directory.Exists(gamePath))
                Directory.CreateDirectory(gamePath);

            if (!File.Exists(gameIni.ProfilePath))
                PrepareInstallation();

            gameIni.Profile = new IniFile();
            gameIni.Profile.Load(gameIni.ProfilePath);
        }

        private async void ChangeRegion(object sender, RoutedEventArgs e)
        {
            // Disable ChangeRegionBtn and hide flyout
            ToggleChangeRegionBtn(in sender, true);

            // Set CurrentRegion in AppConfig
            SetAndSaveConfigValue("CurrentRegion", ComboBoxGameRegion.SelectedIndex);

            // Start region loading
            await LoadRegionByIndex((uint)ComboBoxGameRegion.SelectedIndex);

            // Finalize loading
            ToggleChangeRegionBtn(in sender, false);
            LogWriteLine($"Region changed to {ComboBoxGameRegion.SelectedValue}", Hi3Helper.LogType.Scheme, true);
        }

        private void ToggleChangeRegionBtn(in object sender, bool IsHide)
        {
            Type page = null;
            if (IsHide)
            {
                // Hide element
                ChangeRegionConfirmBtn.Flyout.Hide();
                ChangeRegionConfirmProgressBar.Visibility = Visibility.Visible;
                page = typeof(Pages.BlankPage);
            }
            else
            {
                // Show element
                ChangeRegionConfirmBtn.IsEnabled = false;
                ChangeRegionConfirmProgressBar.Visibility = Visibility.Collapsed;
                page = m_appMode == AppMode.Hi3CacheUpdater ? typeof(Pages.CachesPage) : typeof(Pages.HomePage);
            }
            
            (sender as Button).IsEnabled = !IsHide;

            // Load page
            MainFrameChanger.ChangeMainFrame(page);
        }
    }
}
