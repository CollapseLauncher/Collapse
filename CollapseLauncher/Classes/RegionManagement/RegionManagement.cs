using CollapseLauncher.GameSettings.Genshin;
using CollapseLauncher.GameSettings.Honkai;
using CollapseLauncher.GameSettings.StarRail;
using CollapseLauncher.GameVersioning;
using CollapseLauncher.Pages;
using CollapseLauncher.Statics;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Preset.ConfigV2Store;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        private Http _httpClient;
        private bool IsLoadRegionComplete;
        private bool IsInnerTaskSuccess;
        private bool IsExplicitCancel;
        private string PreviousTag = string.Empty;
        private CancellationTokenSource InnerTokenSource = new CancellationTokenSource();
        private CancellationTokenSource InnerTaskTokenSource;

        private uint CurrentRetry;
        private uint MaxRetry = 5; // Max 5 times of retry attempt
        private uint LoadTimeout = 10; // 10 seconds of initial Load Timeout
        private uint LoadTimeoutStep = 5; // Step 5 seconds for each timeout retries

        private string RegionToChangeName;
        private IList<object> LastNavigationItem;
        private HomeMenuPanel LastRegionNewsProp;

        public async Task<bool> LoadRegionFromCurrentConfigV2(PresetConfigV2 preset)
        {
            using (_httpClient = new Http(default, 4, 250))
            {
                IsExplicitCancel = false;
                InnerTokenSource = new CancellationTokenSource();
                RegionToChangeName = $"{CurrentConfigV2GameCategory} - {CurrentConfigV2GameRegion}";
                LogWriteLine($"Initializing {RegionToChangeName}...", Hi3Helper.LogType.Scheme, true);

                // Set IsLoadRegionComplete to false
                IsLoadRegionComplete = false;

                // Clear MainPage State, like NavigationView, Load State, etc.
                ClearMainPageState();

                // Load Region Resource from Launcher API
                bool IsLoadLocalizedResourceSuccess = await TryLoadGameRegionTask(Task.Run(() =>
                    FetchLauncherLocalizedResources(InnerTokenSource.Token, preset)),
                    LoadTimeout,
                    LoadTimeoutStep);
                bool IsLoadResourceRegionSuccess = await TryLoadGameRegionTask(Task.Run(() =>
                    FetchLauncherResourceAsRegion(InnerTokenSource.Token, preset)),
                    LoadTimeout,
                    LoadTimeoutStep);

                if (IsExplicitCancel)
                {
                    // If explicit cancel was triggered, restore the navigation menu item then return false
                    foreach (object item in LastNavigationItem)
                    {
                        NavigationViewControl.MenuItems.Add(item);
                    }
                    NavigationViewControl.IsSettingsVisible = true;
                    regionNewsProp = LastRegionNewsProp.Copy();
                    LastRegionNewsProp = null;
                    LastNavigationItem.Clear();
                    return false;
                }

                if (!IsLoadLocalizedResourceSuccess || !IsLoadResourceRegionSuccess)
                {
                    MainFrameChanger.ChangeWindowFrame(typeof(DisconnectedPage));
                    return false;
                }

                // Finalize Region Load
                await ChangeBackgroundImageAsRegion();
                FinalizeLoadRegion(preset);

                // Set IsLoadRegionComplete to false
                IsLoadRegionComplete = true;

                return true;
            }
        }

        public void ClearMainPageState()
        {
            // Reset Retry Counter
            CurrentRetry = 0;

            // Clear NavigationViewControl Items and Reset Region props
            LastNavigationItem = new List<object>(NavigationViewControl.MenuItems);
            NavigationViewControl.MenuItems.Clear();
            NavigationViewControl.IsSettingsVisible = false;
            PreviousTag = "launcher";
            ResetRegionProp();
        }

        private async Task<bool> TryLoadGameRegionTask(Task InnerTask, uint Timeout, uint TimeoutStep)
        {
            bool IsContinue;
            while (IsContinue = await IsTryLoadGameRegionTaskFail(InnerTask, Timeout))
            {
                // If true (fail), then log the retry attempt
                LoadingCancelBtn.Visibility = Visibility.Visible;
                LoadingTitle.Text = string.Empty;
                LoadingSubtitle.Text = string.Format(Lang._MainPage.RegionLoadingSubtitleTimeOut, $"{CurrentConfigV2GameCategory} - {CurrentConfigV2GameRegion}", Timeout);
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
            InnerTaskTokenSource = new CancellationTokenSource();
            IsInnerTaskSuccess = false;

            try
            {
                // Run Inner Task and watch for timeout
                WatchAndCancelIfTimeout(InnerTaskTokenSource, Timeout);
                await InnerTask.WaitAsync(InnerTaskTokenSource.Token);
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

        private void FinalizeLoadRegion(PresetConfigV2 preset)
        {
            // Log if region has been successfully loaded
            LogWriteLine($"Initializing Region {preset.ZoneFullname} Done!", Hi3Helper.LogType.Scheme, true);

            // Initializing Game Statics
            LoadGameStaticsByGameType(preset);

            // Init NavigationPanel Items
            if (m_appMode != AppMode.Hi3CacheUpdater)
                InitializeNavigationItems();
            else
                NavigationViewControl.IsSettingsVisible = false;
        }

        private void LoadGameStaticsByGameType(PresetConfigV2 preset)
        {
            DisposeAllPageStatics();

            switch (preset.GameType)
            {
                case GameType.Honkai:
                    PageStatics._GameVersion = new GameTypeHonkaiVersion(this, _gameAPIProp, preset);
                    PageStatics._GameSettings = new HonkaiSettings(preset);
                    PageStatics._GameRepair = new HonkaiRepair(this, PageStatics._GameVersion.GameAPIProp.data.game.latest.decompressed_path, preset);
                    PageStatics._GameCache = new HonkaiCache(this, preset);
                    break;
                case GameType.Genshin:
                    PageStatics._GameVersion = new GameTypeGenshinVersion(this, _gameAPIProp, preset);
                    PageStatics._GameSettings = new GenshinSettings(preset);
                    PageStatics._GameRepair = new GenshinRepair(this, PageStatics._GameVersion.GameAPIProp.data.game.latest.decompressed_path, preset);
                    PageStatics._GameCache = null;
                    break;
                case GameType.StarRail:
                    PageStatics._GameVersion = new GameTypeStarRailVersion(this, _gameAPIProp, preset);
                    PageStatics._GameSettings = new StarRailSettings(preset);
                    PageStatics._GameRepair = null;
                    PageStatics._GameCache = null;
                    break;
            }
        }

        private void DisposeAllPageStatics()
        {
            PageStatics._GameRepair?.Dispose();
            PageStatics._GameCache?.Dispose();
        }

        private void SpawnRegionNotification(string RegionProfileName)
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
            IsLoadRegionComplete = false;
            PresetConfigV2 Preset = LoadCurrentConfigV2((string)ComboBoxGameCategory.SelectedValue, GameRegion);

            // Start region loading
            DelayedLoadingRegionPageTask();
            await LoadRegionFromCurrentConfigV2(Preset);

            // If explicit cancel triggered, then ignore and return
            if (IsExplicitCancel)
            {
                return;
            }

            // Finalize loading
            ToggleChangeRegionBtn(in sender, false);
            LogWriteLine($"Region changed to {Preset.ZoneFullname}", Hi3Helper.LogType.Scheme, true);
        }

        private void ToggleChangeRegionBtn(in object sender, bool IsHide)
        {
            if (IsHide)
            {
                // Hide element
                ChangeRegionConfirmBtn.Flyout.Hide();
                ChangeRegionConfirmProgressBar.Visibility = Visibility.Visible;
            }
            else
            {
                // Show element
                ChangeRegionConfirmBtn.IsEnabled = false;
                ChangeRegionConfirmProgressBar.Visibility = Visibility.Collapsed;
                HideLoadingPopup(true, Lang._MainPage.RegionLoadingTitle, RegionToChangeName);
                MainFrameChanger.ChangeMainFrame(m_appMode == AppMode.Hi3CacheUpdater ? typeof(CachesPage) : typeof(HomePage));
            }

            (sender as Button).IsEnabled = !IsHide;
        }

        private void CancelLoadRegion(object sender, RoutedEventArgs e)
        {
            IsExplicitCancel = true;
            LockRegionChangeBtn = false;
            InnerTaskTokenSource.Cancel();
            InnerTokenSource.Cancel();
            ChangeRegionConfirmProgressBar.Visibility = Visibility.Collapsed;
            ChangeRegionConfirmBtn.IsEnabled = true;
            ChangeRegionBtn.IsEnabled = true;
            HideLoadingPopup(true, Lang._MainPage.RegionLoadingTitle, RegionToChangeName);

            (sender as Button).Visibility = Visibility.Collapsed;
        }

        private async void DelayedLoadingRegionPageTask()
        {
            await Task.Delay(1000);
            if (!IsLoadRegionComplete)
            {
                HideLoadingPopup(false, Lang._MainPage.RegionLoadingTitle, RegionToChangeName);
                // MainFrameChanger.ChangeMainFrame(typeof(BlankPage));
                while (!IsLoadRegionComplete) { await Task.Delay(1000); }
            }
        }
    }
}
