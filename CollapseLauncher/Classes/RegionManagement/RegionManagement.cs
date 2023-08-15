using CollapseLauncher.GameSettings.Genshin;
using CollapseLauncher.GameSettings.Honkai;
using CollapseLauncher.GameSettings.StarRail;
using CollapseLauncher.GameVersioning;
using CollapseLauncher.InstallManager.Genshin;
using CollapseLauncher.InstallManager.Honkai;
using CollapseLauncher.InstallManager.StarRail;
using CollapseLauncher.Pages;
using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
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

        private GamePresetProperty CurrentGameProperty;
        private bool IsLoadRegionComplete;
        private bool IsExplicitCancel;
        private CancellationTokenSource InnerTokenSource = new CancellationTokenSource();

        private uint MaxRetry = 5; // Max 5 times of retry attempt
        private uint LoadTimeout = 10; // 10 seconds of initial Load Timeout
        private uint LoadTimeoutStep = 5; // Step 5 seconds for each timeout retries

        private string RegionToChangeName;
        private IList<object> LastNavigationItem;
        private HomeMenuPanel LastRegionNewsProp;
        public static string PreviousTag = string.Empty;

        public async Task<bool> LoadRegionFromCurrentConfigV2(PresetConfigV2 preset)
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
            CurrentGameProperty = GamePropertyVault.GetCurrentGameProperty();

            GamePropertyVault.AttachNotifForCurrentGame(GamePropertyVault.LastGameHashID);
            GamePropertyVault.DetachNotifForCurrentGame(GamePropertyVault.CurrentGameHashID);

            // Set IsLoadRegionComplete to false
            IsLoadRegionComplete = true;

            return true;
        }

        public void ClearMainPageState()
        {
            // Clear NavigationViewControl Items and Reset Region props
            LastNavigationItem = new List<object>(NavigationViewControl.MenuItems);
            NavigationViewControl.MenuItems.Clear();
            NavigationViewControl.IsSettingsVisible = false;
            PreviousTag = "launcher";
            PreviousTagString.Clear();
            PreviousTagString.Add(PreviousTag);
            LauncherFrame.BackStack.Clear();
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

        private async ValueTask FetchLauncherLocalizedResources(CancellationToken Token, PresetConfigV2 Preset)
        {
            regionBackgroundProp = Preset.LauncherSpriteURLMultiLang ?
                await TryGetMultiLangResourceProp(Token, Preset) :
                await TryGetSingleLangResourceProp(Token, Preset);

            await DownloadBackgroundImage(Token);

            await GetLauncherAdvInfo(Token, Preset);
            await GetLauncherCarouselInfo(Token);
            await GetLauncherEventInfo(Token);
            GetLauncherPostInfo();
        }

        private async ValueTask DownloadBackgroundImage(CancellationToken Token)
        {
            regionBackgroundProp.imgLocalPath = Path.Combine(AppGameImgFolder, "bg", Path.GetFileName(regionBackgroundProp.data.adv.background));
            SetAndSaveConfigValue("CurrentBackground", regionBackgroundProp.imgLocalPath);

            if (!Directory.Exists(Path.Combine(AppGameImgFolder, "bg")))
                Directory.CreateDirectory(Path.Combine(AppGameImgFolder, "bg"));

            FileInfo fI = new FileInfo(regionBackgroundProp.imgLocalPath);
            if (fI.Exists) return;

            using (Stream netStream = await FallbackCDNUtil.DownloadAsStream(regionBackgroundProp.data.adv.background, Token))
            using (Stream outStream = fI.Create())
            {
                netStream.CopyTo(outStream);
            }
        }

        private async ValueTask FetchLauncherDownloadInformation(CancellationToken token, PresetConfigV2 Preset)
        {
            _gameAPIProp = await FallbackCDNUtil.DownloadAsJSONType<RegionResourceProp>(Preset.LauncherResourceURL, CoreLibraryJSONContext.Default, token);
#if DEBUG
            if (_gameAPIProp.data.game.latest.decompressed_path != null) LogWriteLine($"Decompressed Path: {_gameAPIProp.data.game.latest.decompressed_path}", LogType.Default, true);
            if (_gameAPIProp.data.game.latest.path != null) LogWriteLine($"ZIP Path: {_gameAPIProp.data.game.latest.path}", LogType.Default, true);
            if (_gameAPIProp.data.pre_download_game?.latest?.decompressed_path != null) LogWriteLine($"Decompressed Path Pre-load: {_gameAPIProp.data.pre_download_game?.latest?.decompressed_path}", LogType.Default, true);
            if (_gameAPIProp.data.pre_download_game?.latest?.path != null) LogWriteLine($"ZIP Path Pre-load: {_gameAPIProp.data.pre_download_game?.latest?.path}", LogType.Default, true);
#endif

#if SIMULATEPRELOAD && !SIMULATEAPPLYPRELOAD
            if (_gameAPIProp.data.pre_download_game == null)
            {
                LogWriteLine("[FetchLauncherDownloadInformation] SIMULATEPRELOAD: Simulating Pre-load!");
                RegionResourceVersion simDataLatest = _gameAPIProp.data.game.latest.Copy();
                List<RegionResourceVersion> simDataDiff = _gameAPIProp.data.game.diffs.Copy();

                simDataLatest.version = new GameVersion(simDataLatest.version).GetIncrementedVersion().ToString();
                _gameAPIProp.data.pre_download_game = new RegionResourceLatest() { latest = simDataLatest };

                if (simDataDiff == null || simDataDiff.Count == 0) return;
                foreach (RegionResourceVersion diff in simDataDiff)
                {
                    diff.version = new GameVersion(diff.version)
                        .GetIncrementedVersion()
                        .ToString();
                }
                _gameAPIProp.data.pre_download_game.diffs = simDataDiff;
            }
#endif
#if !SIMULATEPRELOAD && SIMULATEAPPLYPRELOAD
            if (_gameAPIProp.data.pre_download_game != null)
            {
                _gameAPIProp.data.game = _gameAPIProp.data.pre_download_game;
            }
#endif
        }

        private async ValueTask<RegionResourceProp> TryGetMultiLangResourceProp(CancellationToken Token, PresetConfigV2 Preset)
        {
            RegionResourceProp ret = await GetMultiLangResourceProp(Lang.LanguageID.ToLower(), Token, Preset);

            return ret.data.adv == null
              || ((ret.data.adv.version ?? 5) <= 4
                && Preset.GameType == GameType.Honkai) ?
                    await GetMultiLangResourceProp(Preset.LauncherSpriteURLMultiLangFallback ?? "en-us", Token, Preset) :
                    ret;
        }

        private async ValueTask<RegionResourceProp> GetMultiLangResourceProp(string langID, CancellationToken token, PresetConfigV2 Preset)
            => await FallbackCDNUtil.DownloadAsJSONType<RegionResourceProp>(string.Format(Preset.LauncherSpriteURL, langID), CoreLibraryJSONContext.Default, token);


        private async ValueTask<RegionResourceProp> TryGetSingleLangResourceProp(CancellationToken token, PresetConfigV2 Preset)
            => await FallbackCDNUtil.DownloadAsJSONType<RegionResourceProp>(Preset.LauncherSpriteURL, CoreLibraryJSONContext.Default, token);

        private void ResetRegionProp()
        {
            LastRegionNewsProp = regionNewsProp.Copy();
            regionNewsProp = new HomeMenuPanel()
            {
                sideMenuPanel = null,
                imageCarouselPanel = null,
                articlePanel = null,
                eventPanel = null
            };
        }

        private async ValueTask GetLauncherAdvInfo(CancellationToken Token, PresetConfigV2 Preset)
        {
            if (regionBackgroundProp.data.icon.Count == 0) return;

            regionNewsProp.sideMenuPanel = new List<MenuPanelProp>();
            foreach (RegionSocMedProp item in regionBackgroundProp.data.icon)
            {
                regionNewsProp.sideMenuPanel.Add(new MenuPanelProp
                {
                    URL = item.url,
                    Icon = await GetCachedSprites(item.img, Token),
                    IconHover = await GetCachedSprites(item.img_hover, Token),
                    QR = string.IsNullOrEmpty(item.qr_img) ? null : await GetCachedSprites(item.qr_img, Token),
                    QR_Description = string.IsNullOrEmpty(item.qr_desc) ? null : item.qr_desc,
                    Description = string.IsNullOrEmpty(item.title) || Preset.IsHideSocMedDesc ? item.url : item.title
                });
            }
        }

        private async ValueTask GetLauncherCarouselInfo(CancellationToken Token)
        {
            if (regionBackgroundProp.data.banner.Count == 0) return;

            regionNewsProp.imageCarouselPanel = new List<MenuPanelProp>();
            foreach (RegionSocMedProp item in regionBackgroundProp.data.banner)
            {
                regionNewsProp.imageCarouselPanel.Add(new MenuPanelProp
                {
                    URL = item.url,
                    Icon = await GetCachedSprites(item.img, Token),
                    Description = string.IsNullOrEmpty(item.name) ? item.url : item.name
                });
            }
        }

        private async ValueTask GetLauncherEventInfo(CancellationToken Token)
        {
            if (string.IsNullOrEmpty(regionBackgroundProp.data.adv.icon)) return;

            regionNewsProp.eventPanel = new RegionBackgroundProp
            {
                url = regionBackgroundProp.data.adv.url,
                icon = await GetCachedSprites(regionBackgroundProp.data.adv.icon, Token)
            };
        }

        private void GetLauncherPostInfo()
        {
            if (regionBackgroundProp.data.post.Count == 0) return;

            regionNewsProp.articlePanel = new PostCarouselTypes();
            foreach (RegionSocMedProp item in regionBackgroundProp.data.post)
            {
                switch (item.type)
                {
                    case PostCarouselType.POST_TYPE_ACTIVITY:
                        regionNewsProp.articlePanel.Events.Add(item);
                        break;
                    case PostCarouselType.POST_TYPE_ANNOUNCE:
                        regionNewsProp.articlePanel.Notices.Add(item);
                        break;
                    case PostCarouselType.POST_TYPE_INFO:
                        regionNewsProp.articlePanel.Info.Add(item);
                        break;
                }
            }
        }

        public async ValueTask<string> GetCachedSprites(string URL, CancellationToken token)
        {
            string cacheFolder = Path.Combine(AppGameImgFolder, "cache");
            string cachePath = Path.Combine(cacheFolder, Path.GetFileNameWithoutExtension(URL));
            if (!Directory.Exists(cacheFolder))
                Directory.CreateDirectory(cacheFolder);

            FileInfo fInfo = new FileInfo(cachePath);

            if (!fInfo.Exists || fInfo.Length < (1 << 10))
            {
                using (FileStream fs = fInfo.Create())
                using (Stream netStream = await FallbackCDNUtil.DownloadAsStream(URL, token))
                {
                    netStream.CopyTo(fs);
                }
            }

            return cachePath;
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
            LogWriteLine($"Initializing Region {preset.ZoneFullname} Done!", LogType.Scheme, true);

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
            GamePropertyVault.AttachNotifForCurrentGame();
            DisposeAllPageStatics();

            GamePropertyVault.LoadGameProperty(this, _gameAPIProp, preset);

            // Spawn Region Notification
            SpawnRegionNotification(preset.ProfileName);
        }

        private void DisposeAllPageStatics()
        {
            // CurrentGameProperty._GameInstall?.CancelRoutine();
            CurrentGameProperty?._GameRepair?.CancelRoutine();
            CurrentGameProperty?._GameRepair?.Dispose();
            CurrentGameProperty?._GameCache?.CancelRoutine();
            CurrentGameProperty?._GameCache?.Dispose();
#if DEBUG
            LogWriteLine("Page statics have been disposed!", LogType.Debug, true);
#endif
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

        private async void ChangeRegionNoWarning(object sender, RoutedEventArgs e)
        {
            (sender as Button).IsEnabled = false;
            await LoadRegionRootButton();
            HideLoadingPopup(true, Lang._MainPage.RegionLoadingTitle, RegionToChangeName);
            MainFrameChanger.ChangeMainFrame(m_appMode == AppMode.Hi3CacheUpdater ? typeof(CachesPage) : typeof(HomePage));
            LauncherFrame.BackStack.Clear();
        }

        private async void ChangeRegion(object sender, RoutedEventArgs e)
        {
            // Disable ChangeRegionBtn and hide flyout
            ToggleChangeRegionBtn(sender, true);
            if (await LoadRegionRootButton())
            {
                // Finalize loading
                ToggleChangeRegionBtn(sender, false);
            }
        }

        private async Task<bool> LoadRegionRootButton()
        {
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
                LogWriteLine($"Region changed to {Preset.ZoneFullname}", Hi3Helper.LogType.Scheme, true);
#if !DISABLEDISCORD
                AppDiscordPresence.SetupPresence();
#endif
                return true;
            }

            return false;
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
                LauncherFrame.BackStack.Clear();
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
            ChangeRegionConfirmBtnNoWarning.IsEnabled = true;
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
