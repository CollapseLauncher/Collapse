using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.Helper;
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
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Logger;
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]      
    public sealed partial class MainPage
    {
        public  GamePresetProperty? CurrentGameProperty { get; set; }
        private bool                IsLoadRegionComplete;

        public PresetConfig? CurrentPresetConfig => CurrentGameProperty?.GamePreset;

        private static string RegionToChangeName => MetadataHelper.GetCurrentTranslatedTitleRegion();

        private List<object> LastMenuNavigationItem;
        private List<object> LastFooterNavigationItem;

        private readonly Dictionary<(string, string), bool> RegionLoadingStatus = new();

        private async Task<bool> LoadRegionFromCurrentConfigV2(PresetConfig preset, string gameName, string gameRegion)
        {
            if (RegionLoadingStatus.ContainsKey((gameName,gameRegion)))
            {
                LogWriteLine($"Region {gameName} - {gameRegion} is already loading, aborting...", LogType.Warning, true);
                return false;
            }
            RegionLoadingStatus.Add((gameName, gameRegion), false);
            
            CancellationTokenSourceWrapper tokenSource = new();

            string regionToChangeName = $"{preset.GameLauncherApi?.GameNameTranslation} - {preset.GameLauncherApi?.GameRegionTranslation}";
            bool runResult = await (preset.GameLauncherApi?
                                         .LoadAsync(BeforeLoadRoutine,
                                                    AfterLoadRoutine,
                                                    ActionOnTimeOutRetry,
                                                    OnErrorRoutine,
                                                    tokenSource.Token) ?? ValueTask.FromResult(false));
            
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
                    m_arguments.StartGame?.Play = false;

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
                    Interlocked.Exchange(ref IsLoadRegionComplete, true);

                    LoadingMessageHelper.HideActionButton();
                    LoadingMessageHelper.HideLoadingFrame();

                    KeyboardShortcuts.CannotUseKbShortcuts = false; // Re-enable keyboard shortcuts after loading region
                    ImageBackgroundManager.Shared.Initialize(preset,
                                                             preset.GameLauncherApi.LauncherGameBackground,
                                                             presenterGrid: BackgroundPresenterGrid,
                                                             token: token);

                    DispatcherQueueExtensions.TryEnqueue(() =>
                    {
                        OnPropertyChanged(nameof(CurrentPresetConfig));
                        OnPropertyChanged(nameof(CurrentGameBackgroundData));
                    });
                }
                catch (Exception ex)
                {
                    OnErrorRoutineInner(ex, ErrorType.Unhandled);
                }
            }

            void ActionOnTimeOutRetry(int retryAttemptCount, int retryAttemptTotal, int timeOutSecond, int timeOutStep)
            {
                LoadingMessageHelper.SetMessage(Locale.Current.Lang?._MainPage?.RegionLoadingTitle,
                                                string.Format($"[{retryAttemptCount} / {retryAttemptTotal}] " + Locale.Current.Lang?._MainPage?.RegionLoadingSubtitleTimeOut,
                                                              regionToChangeName,
                                                              timeOutSecond));
                LoadingMessageHelper.ShowActionButton(Locale.Current.Lang?._Misc?.Cancel, "", CancelLoadEvent);
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

                // Clear cache on navigation reset
                LauncherFrame.BackStack.Clear();
                int cacheSizeOld = LauncherFrame.CacheSize;
                LauncherFrame.CacheSize = 0;
                LauncherFrame.CacheSize = cacheSizeOld;
            });
        }

        private async Task FinalizeLoadRegion(string gameName, string gameRegion, CancellationToken token)
        {
            if (!MetadataHelper.TryGetGameConfig(gameName, gameRegion, out PresetConfig? preset))
            {
                return;
            }

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
            await Task.Run(DisposeAllPageStatics, token);

            // Load region property (and potentially, cached one)
            GamePropertyVault.RegisterGameProperty(this,
                                                   preset.GameLauncherApi!,
                                                   gameName,
                                                   gameRegion);

            // Spawn Region Notification
            _ = SpawnRegionNotification(preset.ProfileName ?? "");
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
                while (!_isLoadNotifComplete)
                {
                    await Task.Delay(250);
                }

                if (NotificationData?.RegionPush == null) return;
                List<NotificationProp> regionPushCopy = new(NotificationData.RegionPush);

                foreach (NotificationProp Entry in regionPushCopy)
                {
                    DispatcherQueueExtensions.TryEnqueue(() => Spawner(Entry));
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
                (sender as Button)?.IsEnabled = false;
                if (!IsLoadRegionComplete)
                {
                    return;
                }

                _lockRegionChangeBtn = true;
                _currentGameCategory = ComboBoxGameTitle.SelectedIndex;
                _currentGameRegion   = ComboBoxGameRegion.SelectedIndex;
                await LoadRegionRootButton();
                InvokeLoadingRegionPopup(false);

                MainFrameChanger.ChangeMainFrame(m_appMode == AppMode.Hi3CacheUpdater
                                                     ? typeof(CachesPage)
                                                     : typeof(HomePage), true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while changing region with no warning\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
            finally
            {
                _lockRegionChangeBtn = false;
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

                _lockRegionChangeBtn = true;
                _currentGameCategory = ComboBoxGameTitle.SelectedIndex;
                _currentGameRegion   = ComboBoxGameRegion.SelectedIndex;
                await LoadRegionRootButton();
                InvokeLoadingRegionPopup(false);

                MainFrameChanger.ChangeMainFrame(m_appMode == AppMode.Hi3CacheUpdater
                                                     ? typeof(CachesPage)
                                                     : typeof(HomePage), true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while changing region with instant method\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
            finally
            {
                _lockRegionChangeBtn = false;
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
                _lockRegionChangeBtn = true;
                ToggleChangeRegionBtn(sender, true);
                if (!await LoadRegionRootButton())
                {
                    return;
                }

                // Finalize loading
                ToggleChangeRegionBtn(sender, false);
                _currentGameCategory = ComboBoxGameTitle.SelectedIndex;
                _currentGameRegion   = ComboBoxGameRegion.SelectedIndex;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while changing region with normal method\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
            finally
            {
                _lockRegionChangeBtn = false;
            }
        }

        private async Task<bool> LoadRegionRootButton()
        {
            if (ComboBoxGameTitle.SelectedValue is not string gameTitle ||
                ComboBoxGameRegion.SelectedValue is not PresetConfig gameRegion) return false;

            // Set and Save CurrentRegion in AppConfig
            MetadataHelper.SaveGame(gameTitle, gameRegion.ZoneName);

            // Load Game ConfigV2 List before loading the region
            Interlocked.Exchange(ref IsLoadRegionComplete, false);

            // Start region loading
            _ = ShowAsyncLoadingTimedOutPill();
            if (!await LoadRegionFromCurrentConfigV2(gameRegion, gameTitle, gameRegion.ZoneName ?? ""))
            {
                return false;
            }

            LogWriteLine($"Region changed to {gameRegion.ZoneFullname}", LogType.Scheme, true);
        #if !DISABLEDISCORD
            if (AppDiscordPresence.IsRpcEnabled)
                AppDiscordPresence.SetupPresence(gameRegion);
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

            (sender as Button)?.IsEnabled = !IsHide;
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
                    InvokeLoadingRegionPopup(true, Locale.Current.Lang?._MainPage?.RegionLoadingTitle ?? "", RegionToChangeName);
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while trying to show Timed-out Loading Pill\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
        }

        private static void InvokeLoadingRegionPopup(bool ShowLoadingMessage = true, string? Title = null, string? Message = null)
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
