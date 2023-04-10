using CollapseLauncher.GameSettings.Genshin;
using CollapseLauncher.GameSettings.Honkai;
using CollapseLauncher.GameSettings.StarRail;
using CollapseLauncher.GameVersioning;
using CollapseLauncher.InstallManager.Genshin;
using CollapseLauncher.InstallManager.Honkai;
using CollapseLauncher.Pages;
using CollapseLauncher.Statics;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        private enum ResourceLoadingType
        {
            LocalizedResource,
            DownloadInformation
        }

        private Http _httpClient;
        private bool IsLoadRegionComplete;
        private bool IsExplicitCancel;
        private string PreviousTag = string.Empty;
        private CancellationTokenSource InnerTokenSource = new CancellationTokenSource();

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
                RegionToChangeName = $"{CurrentConfigV2GameCategory} - {CurrentConfigV2GameRegion}";
                LogWriteLine($"Initializing {RegionToChangeName}...", Hi3Helper.LogType.Scheme, true);

                // Set IsLoadRegionComplete to false
                IsLoadRegionComplete = false;

                // Clear MainPage State, like NavigationView, Load State, etc.
                ClearMainPageState();

                // Load Region Resource from Launcher API
                bool IsLoadLocalizedResourceSuccess = await TryLoadResourceInfo(ResourceLoadingType.LocalizedResource, preset);
                bool IsLoadResourceRegionSuccess = false;
                if (IsLoadLocalizedResourceSuccess)
                {
                    IsLoadResourceRegionSuccess = await TryLoadResourceInfo(ResourceLoadingType.DownloadInformation, preset);
                }

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
            // Clear NavigationViewControl Items and Reset Region props
            LastNavigationItem = new List<object>(NavigationViewControl.MenuItems);
            NavigationViewControl.MenuItems.Clear();
            NavigationViewControl.IsSettingsVisible = false;
            PreviousTag = "launcher";
            ResetRegionProp();
        }

        private async ValueTask<bool> TryLoadResourceInfo(ResourceLoadingType resourceType, PresetConfigV2 preset)
        {
            uint CurrentTimeout = LoadTimeout;
            uint RetryCount = 0;
            while (RetryCount < MaxRetry)
            {
                // Assign new cancellation token source
                InnerTokenSource = new CancellationTokenSource();

                // Watch for timeout
                WatchAndCancelIfTimeout(InnerTokenSource, CurrentTimeout);

                // Assign task based on type
                ConfiguredValueTaskAwaitable loadTask = (resourceType switch
                {
                    ResourceLoadingType.LocalizedResource => FetchLauncherLocalizedResources(InnerTokenSource.Token, preset),
                    ResourceLoadingType.DownloadInformation => FetchLauncherDownloadInformation(InnerTokenSource.Token, preset),
                    _ => throw new InvalidOperationException($"Operation is not supported!")
                }).ConfigureAwait(false);

                try
                {
                    // Run and await task
                    await loadTask;

                    // Return true as successful
                    return true;
                }
                catch (OperationCanceledException)
                {
                    CurrentTimeout = SendTimeoutCancelationMessage(new OperationCanceledException($"Loading was cancelled because timeout has been exceeded!"), CurrentTimeout);
                }
                catch (Exception ex)
                {
                    CurrentTimeout = SendTimeoutCancelationMessage(ex, CurrentTimeout);
                }

                // If explicit cancel was triggered, then return false
                if (IsExplicitCancel)
                {
                    return false;
                }

                // Increment retry count
                RetryCount++;
            }

            // Return false as fail
            return false;
        }

        private uint SendTimeoutCancelationMessage(Exception ex, uint currentTimeout)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // Send the message to loading status
                LoadingCancelBtn.Visibility = Visibility.Visible;
                LoadingTitle.Text = string.Empty;
                LoadingSubtitle.Text = string.Format(Lang._MainPage.RegionLoadingSubtitleTimeOut, $"{CurrentConfigV2GameCategory} - {CurrentConfigV2GameRegion}", currentTimeout);
                LogWriteLine($"Loading Game: {CurrentConfigV2GameCategory} - {CurrentConfigV2GameRegion} has timed-out (> {currentTimeout} seconds). Retrying...", Hi3Helper.LogType.Warning);

                // Send the exception without changing into the Error page
                ErrorSender.SendExceptionWithoutPage(ex, ErrorType.Connection);
            });

            // Increment the timeout per step
            currentTimeout += LoadTimeoutStep;

            // Return new timeout second
            return currentTimeout;
        }

        private async void WatchAndCancelIfTimeout(CancellationTokenSource TokenSource, uint Timeout)
        {
            // Wait until it timeout
            await Task.Delay((int)Timeout * 1000);

            // If cancel has been triggered, then return
            if (TokenSource.IsCancellationRequested) return;

            // If InnerTask still not loaded successfully, then cancel it
            if (!IsLoadRegionComplete)
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
                    PageStatics._GameSettings = new HonkaiSettings();
                    PageStatics._GameCache = new HonkaiCache(this);
                    PageStatics._GameRepair = new HonkaiRepair(this);
                    PageStatics._GameInstall = new HonkaiInstall(this);
                    break;
                case GameType.Genshin:
                    PageStatics._GameVersion = new GameTypeGenshinVersion(this, _gameAPIProp, preset);
                    PageStatics._GameSettings = new GenshinSettings();
                    PageStatics._GameCache = null;
                    PageStatics._GameRepair = new GenshinRepair(this, PageStatics._GameVersion.GameAPIProp.data.game.latest.decompressed_path);
                    PageStatics._GameInstall = new GenshinInstall(this);
                    break;
                case GameType.StarRail:
                    PageStatics._GameVersion = new GameTypeStarRailVersion(this, _gameAPIProp, preset);
                    PageStatics._GameSettings = new StarRailSettings();
                    PageStatics._GameCache = null;
                    PageStatics._GameRepair = null;
                    PageStatics._GameInstall = new HonkaiInstall(this);
                    break;
            }

            // Spawn Region Notification
            SpawnRegionNotification(PageStatics._GameVersion.GamePreset.ProfileName);
        }

        private void DisposeAllPageStatics()
        {
            PageStatics._GameRepair?.Dispose();
            PageStatics._GameCache?.Dispose();
        }

        private async void SpawnRegionNotification(string RegionProfileName)
        {
            // Wait until the notification is ready
            while (!IsLoadNotifComplete)
            {
                await Task.Delay(250);
            }

            if (NotificationData.RegionPush == null) return;

            foreach (NotificationProp Entry in NotificationData.RegionPush)
            {
                NotificationInvokerProp toEntry = new NotificationInvokerProp
                {
                    CloseAction = null,
                    IsAppNotif = false,
                    Notification = Entry,
                    OtherContent = null
                };

                if (Entry.ActionProperty != null)
                {
                    toEntry.OtherContent = Entry.ActionProperty.GetUIElement();
                }

                GameVersion? ValidForVerBelow = Entry.ValidForVerBelow != null ? new GameVersion(Entry.ValidForVerBelow) : null;
                GameVersion? ValidForVerAbove = Entry.ValidForVerAbove != null ? new GameVersion(Entry.ValidForVerAbove) : null;

                if (Entry.RegionProfile == RegionProfileName && IsNotificationTimestampValid(Entry) && (Entry.ValidForVerBelow == null
                        || (LauncherUpdateWatcher.CompareVersion(AppCurrentVersion, ValidForVerBelow)
                        && LauncherUpdateWatcher.CompareVersion(ValidForVerAbove, AppCurrentVersion))
                        || LauncherUpdateWatcher.CompareVersion(AppCurrentVersion, ValidForVerBelow)))
                {
                    NotificationSender.SendNotification(toEntry);
                }
                await Task.Delay(250);
            }
        }

        private bool IsNotificationTimestampValid(NotificationProp Entry)
        {
            long nowDateTime = DateTime.Now.ToLocalTime().ToFileTime();
            long? beginDateTime = Entry.TimeBegin?.ToLocalTime().ToFileTime() ?? 0;
            long? endDateTime = Entry.TimeEnd?.ToLocalTime().ToFileTime() ?? 0;

            bool isBeginValid = Entry.TimeBegin.HasValue ? beginDateTime < nowDateTime : true;
            bool isEndValid = Entry.TimeEnd.HasValue ? endDateTime > nowDateTime : true;

            return isBeginValid && isEndValid;
        }

        private async void ChangeRegion(object sender, RoutedEventArgs e)
        {
            // Disable ChangeRegionBtn and hide flyout
            ToggleChangeRegionBtn(in sender, true);

            string GameCategory = (string)ComboBoxGameCategory.SelectedValue;
            string GameRegion = GetComboBoxGameRegionValue(ComboBoxGameRegion.SelectedValue);

            // Set and Save CurrentRegion in AppConfig
            SetAndSaveConfigValue("GameCategory", GameCategory);
            SetPreviousGameRegion(GameCategory, GameRegion);

            // Load Game ConfigV2 List before loading the region
            IsLoadRegionComplete = false;
            PresetConfigV2 Preset = LoadCurrentConfigV2(GameCategory, GameRegion);

            // Start region loading
            DelayedLoadingRegionPageTask();
            if (await LoadRegionFromCurrentConfigV2(Preset))
            {
                // Finalize loading
                ToggleChangeRegionBtn(in sender, false);
                LogWriteLine($"Region changed to {Preset.ZoneFullname}", Hi3Helper.LogType.Scheme, true);
            }
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
