using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Background;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.Loading;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.Update;
using CollapseLauncher.Pages;
using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]      
    public sealed partial class MainPage
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        private GamePresetProperty CurrentGameProperty { get; set; }
        private bool               IsLoadRegionComplete;

        private static  string        RegionToChangeName { get => $"{GetGameTitleRegionTranslationString(LauncherMetadataHelper.CurrentMetadataConfigGameName, Lang._GameClientTitles)} - {GetGameTitleRegionTranslationString(LauncherMetadataHelper.CurrentMetadataConfigGameRegion, Lang._GameClientRegions)}"; }
        private         List<object>  LastMenuNavigationItem;
        private         List<object>  LastFooterNavigationItem;
        internal static string        PreviousTag = string.Empty;

        private readonly Dictionary<(string, string), bool> RegionLoadingStatus = new();

        private async Task<bool> LoadRegionFromCurrentConfigV2(PresetConfig preset, string gameName, string gameRegion)
        {
            if (RegionLoadingStatus.ContainsKey((gameName,gameRegion)))
            {
                LogWriteLine($"Region {gameName} - {gameRegion} is already loading, aborting...", LogType.Warning, true);
                return false;
            }
            RegionLoadingStatus.Add((gameName, gameRegion), false);
            
            CancellationTokenSourceWrapper tokenSource = new CancellationTokenSourceWrapper();

            string regionToChangeName = $"{preset.GameLauncherApi.GameNameTranslation} - {preset.GameLauncherApi.GameRegionTranslation}";
            bool runResult = await preset.GameLauncherApi
                                         .LoadAsync(BeforeLoadRoutine,
                                                    AfterLoadRoutine,
                                                    ActionOnTimeOutRetry,
                                                    OnErrorRoutine,
                                                    tokenSource.Token);
            
            RegionLoadingStatus.Remove((gameName, gameRegion));
            return runResult;

            void OnErrorRoutine(Exception ex) => OnErrorRoutineInner(ex, ErrorType.Unhandled);

            void OnErrorRoutineInner(Exception ex, ErrorType errorType)
            {
                Interlocked.Exchange(ref IsLoadRegionComplete, true);
                LoadingMessageHelper.HideActionButton();
                LoadingMessageHelper.HideLoadingFrame();

                LogWriteLine($"Error has occurred while loading: {regionToChangeName}!\r\n{ex}", LogType.Scheme, true);
                ErrorSender.SendExceptionWithoutPage(ex, errorType);
                MainFrameChanger.ChangeWindowFrame(typeof(DisconnectedPage));
            }

            void CancelLoadEvent(object sender, RoutedEventArgs args)
            {
                tokenSource.Cancel();
                Interlocked.Exchange(ref IsLoadRegionComplete, true);
                DispatcherQueue.TryEnqueue(() =>
                {
                    // If explicit cancel was triggered, restore the navigation menu item then return false
                    foreach (object item in LastMenuNavigationItem)
                    {
                        NavigationViewControl.MenuItems.Add(item);
                    }
                    foreach (object item in LastFooterNavigationItem)
                    {
                        NavigationViewControl.FooterMenuItems.Add(item);
                    }
                    NavigationViewControl.IsSettingsVisible = true;
                    LastMenuNavigationItem.Clear();
                    LastFooterNavigationItem.Clear();
                    if (m_arguments.StartGame != null)
                        m_arguments.StartGame.Play = false;

                    ChangeRegionConfirmProgressBar.Visibility = Visibility.Collapsed;
                    ChangeRegionConfirmBtn.IsEnabled = true;
                    ChangeRegionConfirmBtnNoWarning.IsEnabled = true;
                    ChangeRegionBtn.IsEnabled = true;
                });
            }

            async ValueTask AfterLoadRoutine(CancellationToken token)
            {
                try
                {
                    if (IsLoadRegionComplete) // Prevent double loading
                    {
                        LogWriteLine("[RegionManagement] Double region loading detected, aborting...", LogType.Warning, true);
                        _ = SentryHelper.ExceptionHandlerAsync(new Exception("Double region loading detected!"));
                        return;
                    }
                    
                    LogWriteLine($"Game: {regionToChangeName} has been completely initialized!", LogType.Scheme, true);
                    await FinalizeLoadRegion(gameName, gameRegion, token);
                    _ = ChangeBackgroundImageAsRegionAsync();
                    Interlocked.Exchange(ref IsLoadRegionComplete, true);

                    LoadingMessageHelper.HideActionButton();
                    LoadingMessageHelper.HideLoadingFrame();

                    KeyboardShortcuts.CannotUseKbShortcuts = false; // Re-enable keyboard shortcuts after loading region
                }
                catch (Exception ex)
                {
                    OnErrorRoutineInner(ex, ErrorType.Unhandled);
                }
            }

            void ActionOnTimeOutRetry(int retryAttemptCount, int retryAttemptTotal, int timeOutSecond, int timeOutStep)
            {
                LoadingMessageHelper.SetMessage(Lang._MainPage.RegionLoadingTitle,
                                                string.Format($"[{retryAttemptCount} / {retryAttemptTotal}] " + Lang._MainPage.RegionLoadingSubtitleTimeOut,
                                                              regionToChangeName,
                                                              timeOutSecond));
                LoadingMessageHelper.ShowActionButton(Lang._Misc.Cancel, "", CancelLoadEvent);
            }

            async ValueTask BeforeLoadRoutine(CancellationToken token)
            {
                try
                {
                    Interlocked.Exchange(ref IsLoadRegionComplete, false);
                    KeyboardShortcuts.CannotUseKbShortcuts = true; // Disable keyboard shortcuts while loading region
                    LogWriteLine($"Initializing game: {regionToChangeName}...", LogType.Scheme, true);

                    await Task.Run(ClearMainPageState, token);
                    _ = ShowAsyncLoadingTimedOutPill(token);
                }
                catch (Exception ex)
                {
                    OnErrorRoutineInner(ex, ErrorType.Unhandled);
                }
            }
        }

        private void ClearMainPageState()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // Clear NavigationViewControl Items and Reset Region props
                LastMenuNavigationItem = [.. NavigationViewControl.MenuItems];
                LastFooterNavigationItem = [.. NavigationViewControl.FooterMenuItems];
                NavigationViewControl.MenuItems.Clear();
                NavigationViewControl.FooterMenuItems.Clear();
                NavigationViewControl.IsSettingsVisible = false;
                PreviousTag = "launcher";
                PreviousTagString.Clear();
                PreviousTagString.Add(PreviousTag);

                // Clear cache on navigation reset
                LauncherFrame.BackStack.Clear();
                int cacheSizeOld = LauncherFrame.CacheSize;
                LauncherFrame.CacheSize = 0;
                LauncherFrame.CacheSize = cacheSizeOld;
            });
        }

        private async Task DownloadBackgroundImage(CancellationToken token)
        {
            var currentProperty = GamePropertyVault.GetCurrentGameProperty();
            // Get and set the current path of the image
            string backgroundFolder = Path.Combine(AppGameImgFolder, "bg");
            string backgroundFileName = Path.GetFileName(LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImg);
            LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal = Path.Combine(backgroundFolder, backgroundFileName);
            SetAndSaveConfigValue("CurrentBackground", LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal);
            await DownloadNonPluginBackgroundImage(backgroundFolder, currentProperty, token);
        }

        private async Task DownloadNonPluginBackgroundImage(string             backgroundFolder,
                                                            GamePresetProperty currentProperty,
                                                            CancellationToken  token)
        {
            // Check if the background folder exist
            if (!Directory.Exists(backgroundFolder))
                Directory.CreateDirectory(backgroundFolder);

            var imgFileInfo =
                new FileInfo(LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal);

            // Start downloading the background image
            var isDownloaded = await ImageLoaderHelper.IsFileCompletelyDownloadedAsync(imgFileInfo, true);
            if (isDownloaded)
            {
                BackgroundImgChanger.ChangeBackground(imgFileInfo.FullName,
                                                      this.ReloadPageTheme,
                                                      false,
                                                      false,
                                                      true);
                return;
            }

        #nullable enable
            string? tempImage = null;
            var lastBgCfg = "lastBg-" + LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameName +
                            "-" + LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameRegion;

            // Check if the last background image exist, then use that temporarily instead
            var lastGameBackground = GetAppConfigValue(lastBgCfg).ToString();
            if (!string.IsNullOrEmpty(lastGameBackground))
            {
                if (File.Exists(lastGameBackground))
                {
                    tempImage = lastGameBackground;
                }
            }

            // If the file is not downloaded, use template image first, then download the image
            GameNameType? currentGameType = currentProperty.GameVersion?.GameType;
            tempImage ??= currentGameType switch
                          {
                              GameNameType.Honkai => Path.Combine(AppExecutableDir,   @"Assets\Images\GameBackground\honkai.webp"),
                              GameNameType.Genshin => Path.Combine(AppExecutableDir,  @"Assets\Images\GameBackground\genshin.webp"),
                              GameNameType.StarRail => Path.Combine(AppExecutableDir, @"Assets\Images\GameBackground\starrail.webp"),
                              GameNameType.Zenless => Path.Combine(AppExecutableDir,  @"Assets\Images\GameBackground\zzz.webp"),
                              _ => BackgroundMediaUtility.GetDefaultRegionBackgroundPath()
                          };
            BackgroundImgChanger.ChangeBackground(tempImage,
                                                  this.ReloadPageTheme,
                                                  false,
                                                  false,
                                                  true);
            if (await ImageLoaderHelper.TryDownloadToCompletenessAsync(LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImg,
                                                                       LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.ApiResourceHttpClient,
                                                                       imgFileInfo,
                                                                       false,
                                                                       token))
            {
                BackgroundImgChanger.ChangeBackground(imgFileInfo.FullName,
                                                      this.ReloadPageTheme,
                                                      false,
                                                      true,
                                                      true);
                SetAndSaveConfigValue(lastBgCfg, imgFileInfo.FullName);
            }
        #nullable restore
        }

        private async Task FinalizeLoadRegion(string gameName, string gameRegion, CancellationToken token)
        {
            PresetConfig preset = LauncherMetadataHelper.LauncherMetadataConfig[gameName][gameRegion];

            // Log if region has been successfully loaded
            LogWriteLine($"Initializing Region {preset.ZoneFullname} Done!", LogType.Scheme, true);

            // Initializing Game Statics
            await LoadGameStaticsByGameType(preset, gameName, gameRegion, token);
            CurrentGameProperty = GamePropertyVault.GetCurrentGameProperty();

            // Init NavigationPanel Items
            await Task.Run(() => InitializeNavigationItems(), token);
        }

        private async Task LoadGameStaticsByGameType(PresetConfig preset, string gameName, string gameRegion, CancellationToken token)
        {
            // Attach notification for the current game and dispose statics
            await GamePropertyVault.AttachNotificationForCurrentGame();
            await Task.Run(DisposeAllPageStatics, token);

            // Load region property (and potentially, cached one)
            GamePropertyVault.LoadGameProperty(this,
                                               preset.GameLauncherApi,
                                               gameName,
                                               gameRegion);

            // Spawn Region Notification
            _ = SpawnRegionNotification(preset.ProfileName);

            // Detach notification from last region
            GamePropertyVault.DetachNotificationForCurrentRegion();
        }

        private void DisposeAllPageStatics()
        {
            CurrentGameProperty?.GameRepair?.CancelRoutine();
            CurrentGameProperty?.GameRepair?.Dispose();
            CurrentGameProperty?.GameCache?.CancelRoutine();
            CurrentGameProperty?.GameCache?.Dispose();
#if DEBUG
            LogWriteLine("Page statics have been disposed!", LogType.Debug, true);
#endif
        }

        private async Task SpawnRegionNotification(string RegionProfileName)
        {
            try
            {
                // Wait until the notification is ready
                while (!IsLoadNotifComplete)
                {
                    await Task.Delay(250);
                }

                if (NotificationData.RegionPush == null) return;
                List<NotificationProp> regionPushCopy = new(NotificationData.RegionPush);

                foreach (NotificationProp Entry in regionPushCopy)
                {
                    DispatcherQueue.TryEnqueue(() => Spawner(Entry));
                    await Task.Delay(250);
                }

                void Spawner(NotificationProp entry)
                {
                    NotificationInvokerProp toEntry = new NotificationInvokerProp
                    {
                        CloseAction  = null,
                        IsAppNotif   = false,
                        Notification = entry,
                        OtherContent = null
                    };

                    if (entry.ActionProperty != null)
                    {
                        toEntry.OtherContent = entry.ActionProperty.GetFrameworkElement();
                    }

                    GameVersion? ValidForVerBelow = entry.ValidForVerBelow;
                    GameVersion? ValidForVerAbove = entry.ValidForVerAbove;

                    if (entry.RegionProfile == RegionProfileName && IsNotificationTimestampValid(entry) && (entry.ValidForVerBelow == null
                            || (LauncherUpdateHelper.LauncherCurrentVersion < ValidForVerBelow
                                && ValidForVerAbove < LauncherUpdateHelper.LauncherCurrentVersion)
                            || LauncherUpdateHelper.LauncherCurrentVersion < ValidForVerBelow))
                    {
                        NotificationSender.SendNotification(toEntry);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while sending notification to the UI\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
        }

        private static bool IsNotificationTimestampValid(NotificationProp Entry)
        {
            long nowDateTime = DateTime.Now.ToLocalTime().ToFileTime();
            long? beginDateTime = Entry.TimeBegin?.ToLocalTime().ToFileTime() ?? 0;
            long? endDateTime = Entry.TimeEnd?.ToLocalTime().ToFileTime() ?? 0;

            bool isBeginValid = !Entry.TimeBegin.HasValue || beginDateTime < nowDateTime;
            bool isEndValid   = !Entry.TimeEnd.HasValue || endDateTime > nowDateTime;

            return isBeginValid && isEndValid;
        }

        private async void ChangeRegionNoWarning(object sender, RoutedEventArgs e)
        {
            try
            {
                (sender as Button).IsEnabled = false;
                if (!IsLoadRegionComplete)
                {
                    return;
                }

                LockRegionChangeBtn = true;
                CurrentGameCategory = ComboBoxGameCategory.SelectedIndex;
                CurrentGameRegion   = ComboBoxGameRegion.SelectedIndex;
                await LoadRegionRootButton();
                InvokeLoadingRegionPopup(false);
                MainFrameChanger.ChangeMainFrame(m_appMode == AppMode.Hi3CacheUpdater
                                                     ? typeof(CachesPage)
                                                     : typeof(HomePage));
                LauncherFrame.BackStack.Clear();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while changing region with no warning\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
            finally
            {
                LockRegionChangeBtn = false;
            }
        }

        private async void ChangeRegionInstant()
        {
            try
            {
                if (!IsLoadRegionComplete)
                {
                    return;
                }

                LockRegionChangeBtn = true;
                CurrentGameCategory = ComboBoxGameCategory.SelectedIndex;
                CurrentGameRegion   = ComboBoxGameRegion.SelectedIndex;
                await LoadRegionRootButton();
                InvokeLoadingRegionPopup(false);
                MainFrameChanger.ChangeMainFrame(m_appMode == AppMode.Hi3CacheUpdater
                                                     ? typeof(CachesPage)
                                                     : typeof(HomePage));
                LauncherFrame.BackStack.Clear();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while changing region with instant method\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
            finally
            {
                LockRegionChangeBtn = false;
            }
        }

        private async void ChangeRegion(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!IsLoadRegionComplete)
                {
                    return;
                }

                // Disable ChangeRegionBtn and hide flyout
                LockRegionChangeBtn = true;
                ToggleChangeRegionBtn(sender, true);
                if (!await LoadRegionRootButton())
                {
                    return;
                }

                // Finalize loading
                ToggleChangeRegionBtn(sender, false);
                CurrentGameCategory = ComboBoxGameCategory.SelectedIndex;
                CurrentGameRegion   = ComboBoxGameRegion.SelectedIndex;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while changing region with normal method\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
            finally
            {
                LockRegionChangeBtn = false;
            }
        }

        private async Task<bool> LoadRegionRootButton()
        {
            string GameCategory = GetComboBoxGameRegionValue(ComboBoxGameCategory.SelectedValue);
            string GameRegion = GetComboBoxGameRegionValue(ComboBoxGameRegion.SelectedValue);

            // Set and Save CurrentRegion in AppConfig
            SetAndSaveConfigValue("GameCategory", GameCategory);
            LauncherMetadataHelper.SetPreviousGameRegion(GameCategory, GameRegion);

            // Load Game ConfigV2 List before loading the region
            Interlocked.Exchange(ref IsLoadRegionComplete, false);
            PresetConfig Preset = await LauncherMetadataHelper.GetMetadataConfig(GameCategory, GameRegion);

            // Start region loading
            _ = ShowAsyncLoadingTimedOutPill();
            if (!await LoadRegionFromCurrentConfigV2(Preset, GameCategory, GameRegion))
            {
                return false;
            }

            LogWriteLine($"Region changed to {Preset.ZoneFullname}", LogType.Scheme, true);
        #if !DISABLEDISCORD
            if (GetAppConfigValue("EnableDiscordRPC").ToBool())
                AppDiscordPresence.SetupPresence();
        #endif
            return true;

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
                InvokeLoadingRegionPopup(false);
                MainFrameChanger.ChangeMainFrame(m_appMode == AppMode.Hi3CacheUpdater ? typeof(CachesPage) : typeof(HomePage));
                LauncherFrame.BackStack.Clear();
            }

            (sender as Button).IsEnabled = !IsHide;
        }

        private async Task ShowAsyncLoadingTimedOutPill(CancellationToken token = default)
        {
            if (IsLoadRegionComplete ||
                token.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await Task.Delay(1000, token);
                if (!IsLoadRegionComplete &&
                    !token.IsCancellationRequested)
                {
                    InvokeLoadingRegionPopup(true, Lang._MainPage.RegionLoadingTitle, RegionToChangeName);
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while trying to show Timed-out Loading Pill\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
        }

        private static void InvokeLoadingRegionPopup(bool ShowLoadingMessage = true, string Title = null, string Message = null)
        {
            if (ShowLoadingMessage)
            {
                LoadingMessageHelper.SetMessage(Title, Message);
                LoadingMessageHelper.SetProgressBarState(isProgressIndeterminate: true);
                LoadingMessageHelper.ShowLoadingFrame();
                return;
            }

            LoadingMessageHelper.HideLoadingFrame();
        }
    }
}
