﻿using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Preset.ConfigV2Store;
using static Hi3Helper.Shared.Region.GameConfig;
using static Hi3Helper.Shared.Region.InstallationManagement;
using static Hi3Helper.Shared.Region.LauncherConfig;

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

        public async Task<bool> LoadRegionFromCurrentConfigV2()
        {
            LogWriteLine($"Initializing {CurrentConfigV2.ZoneFullname}...", Hi3Helper.LogType.Scheme, true);

            // Clear MainPage State, like NavigationView, Load State, etc.
            ClearMainPageState();

            // Load Game Region File
            LoadGameRegionFromCurrentConfigV2();

            // Load Region Resource from Launcher API
            bool IsLoadLocalizedResourceSuccess = await TryLoadGameRegionTask(FetchLauncherLocalizedResources(), LoadTimeout, LoadTimeoutStep);
            bool IsLoadResourceRegionSuccess = await TryLoadGameRegionTask(FetchLauncherResourceAsRegion(), LoadTimeout, LoadTimeoutStep);

            if (!IsLoadLocalizedResourceSuccess || !IsLoadResourceRegionSuccess)
            {
                MainFrameChanger.ChangeWindowFrame(typeof(DisconnectedPage));
                return false;
            }

            // Finalize Region Load
            await ChangeBackgroundImageAsRegion();
            FinalizeLoadRegion();

            return true;
        }

        private void LoadGameRegionFromCurrentConfigV2()
        {
            gameIni = new GameIniStruct();

            gamePath = Path.Combine(AppGameFolder, CurrentConfigV2.ProfileName);
            gameIni.ProfilePath = Path.Combine(gamePath, $"config.ini");

            if (!Directory.Exists(gamePath))
                Directory.CreateDirectory(gamePath);

            if (!File.Exists(gameIni.ProfilePath))
                PrepareInstallation();

            gameIni.Profile = new IniFile();
            gameIni.Profile.Load(gameIni.ProfilePath);
        }

        public void ClearMainPageState()
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
            HideLoadingPopup(false, Lang._MainPage.RegionLoadingTitle, CurrentConfigV2.ZoneFullname);

            IsLoadRegionComplete = true;
        }

        private async Task<bool> TryLoadGameRegionTask(Task InnerTask, uint Timeout, uint TimeoutStep)
        {
            bool IsContinue;
            while (IsContinue = await IsTryLoadGameRegionTaskFail(InnerTask, Timeout))
            {
                // If true (fail), then log the retry attempt
                LoadingFooter.Text = string.Format(Lang._MainPage.RegionLoadingSubtitleTimeOut, $"{CurrentConfigV2GameCategory} - {CurrentConfigV2GameRegion}", LoadTimeout);
                LogWriteLine($"Loading Game: {CurrentConfigV2GameCategory} - {CurrentConfigV2GameRegion} has timed-out (> {Timeout} seconds). Retrying...", Hi3Helper.LogType.Warning);
                Timeout += TimeoutStep;
                CurrentRetry++;
            }

            return !(!IsContinue && (CurrentRetry > MaxRetry));
        }

        private async Task<bool> IsTryLoadGameRegionTaskFail(Task InnerTask, uint Timeout)
        {
            // Check for Retry Count. If it reaches max, then return to DisconnectedPage
            if (CurrentRetry > MaxRetry)
            {
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
            catch (Exception ex)
            {
                ErrorSender.SendExceptionWithoutPage(ex, ErrorType.Connection);
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

        private void FinalizeLoadRegion()
        {
            // Init Registry Key and Push Region Notification
            InitRegKey();
            PushRegionNotification(CurrentConfigV2.ProfileName);

            // Init NavigationPanel Items
            if (m_appMode != AppMode.Hi3CacheUpdater)
                InitializeNavigationItems();
            else
                NavigationViewControl.IsSettingsVisible = false;

            // Set LoadingFooter empty
            LoadingFooter.Text = string.Empty;

            // Hide Loading Popup
            HideLoadingPopup(true, Lang._MainPage.RegionLoadingTitle, CurrentConfigV2.ZoneFullname);

            // Log if region has been successfully loaded
            LogWriteLine($"Initializing Region {CurrentConfigV2.ZoneFullname} Done!", Hi3Helper.LogType.Scheme, true);
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

        private async void ChangeRegion(object sender, RoutedEventArgs e)
        {
            // Disable ChangeRegionBtn and hide flyout
            ToggleChangeRegionBtn(in sender, true);

            string GameRegion = GetComboBoxGameRegionValue(ComboBoxGameRegion.SelectedValue);

            // Set and Save CurrentRegion in AppConfig
            SetAndSaveConfigValue("GameCategory", (string)ComboBoxGameCategory.SelectedValue);
            SetAndSaveConfigValue("GameRegion", GameRegion);

            // Load Game ConfigV2 List before loading the region
            LoadCurrentConfigV2((string)ComboBoxGameCategory.SelectedValue, GameRegion);

            // Start region loading
            await LoadRegionFromCurrentConfigV2();

            // Finalize loading
            ToggleChangeRegionBtn(in sender, false);
            LogWriteLine($"Region changed to {CurrentConfigV2.ZoneFullname}", Hi3Helper.LogType.Scheme, true);
        }

        private void ToggleChangeRegionBtn(in object sender, bool IsHide)
        {
            Type page;
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
