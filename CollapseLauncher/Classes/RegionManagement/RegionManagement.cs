﻿using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.LauncherApiLoader;
using CollapseLauncher.Helper.LauncherApiLoader.Sophon;
using CollapseLauncher.Helper.Loading;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.Update;
using CollapseLauncher.Pages;
using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.RegionResourceListHelper;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public class CancellationTokenSourceWrapper : CancellationTokenSource
    {
        public bool IsDisposed;
        public bool IsCancelled = false;

        public new async ValueTask CancelAsync()
        {
            await base.CancelAsync();
            IsCancelled = true;
        }
        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]      
    public sealed partial class MainPage
    {
        private enum ResourceLoadingType
        {
            LocalizedResource,
            DownloadInformation,
            DownloadBackground
        }

        private GamePresetProperty CurrentGameProperty { get; set; }
        private bool IsLoadRegionComplete { get; set; }

        private const uint                           MaxRetry                   = 5; // Max 5 times of retry attempt
        private const uint                           LoadTimeout                = 10; // 10 seconds of initial Load Timeout
        private const uint                           BackgroundImageLoadTimeout = 3600; // Give background image download 1 hour of timeout
        private const uint                           LoadTimeoutStep            = 5; // Step 5 seconds for each timeout retries

        private static  string        RegionToChangeName { get => $"{GetGameTitleRegionTranslationString(LauncherMetadataHelper.CurrentMetadataConfigGameName, Lang._GameClientTitles)} - {GetGameTitleRegionTranslationString(LauncherMetadataHelper.CurrentMetadataConfigGameRegion, Lang._GameClientRegions)}"; }
        private         List<object>  LastNavigationItem;
        internal static string        PreviousTag = string.Empty;

        internal async Task<bool> LoadRegionFromCurrentConfigV2(PresetConfig preset, string gameName, string gameRegion)
        {
            CancellationTokenSourceWrapper tokenSource = new CancellationTokenSourceWrapper();

            string regionToChangeName = $"{preset.GameLauncherApi.GameNameTranslation} - {preset.GameLauncherApi.GameRegionTranslation}";

            async void BeforeLoadRoutine(CancellationToken token)
            {
                LogWriteLine($"Initializing game: {regionToChangeName}...", LogType.Scheme, true);

                ClearMainPageState();
                DisableKbShortcuts(1000);
                await Task.Delay(TimeSpan.FromSeconds(1));
                if (preset.GameLauncherApi.IsLoadingCompleted || token.IsCancellationRequested) return;

                LoadingMessageHelper.SetMessage(Lang._MainPage.RegionLoadingTitle, regionToChangeName);
                LoadingMessageHelper.SetProgressBarState(isProgressIndeterminate: true);
                LoadingMessageHelper.ShowLoadingFrame();

                IsLoadRegionComplete = false;
            }

            void AfterLoadRoutine(CancellationToken token)
            {
                LoadingMessageHelper.HideActionButton();
                LoadingMessageHelper.HideLoadingFrame();

                IsLoadRegionComplete = true;
            }

            void OnErrorRoutine(Exception ex)
            {
                LogWriteLine($"Error has occurred while loading: {regionToChangeName}!\r\n{ex}", LogType.Scheme, true);
                ErrorSender.SendExceptionWithoutPage(ex, ErrorType.Connection);
            }

            async void CancelLoadEvent(object sender, RoutedEventArgs args)
            {
                await tokenSource.CancelAsync();

                // If explicit cancel was triggered, restore the navigation menu item then return false
                foreach (object item in LastNavigationItem)
                {
                    NavigationViewControl.MenuItems.Add(item);
                }
                NavigationViewControl.IsSettingsVisible = true;
                LastNavigationItem.Clear();
                if (m_arguments.StartGame != null)
                    m_arguments.StartGame.Play = false;

                ChangeRegionConfirmProgressBar.Visibility = Visibility.Collapsed;
                ChangeRegionConfirmBtn.IsEnabled = true;
                ChangeRegionConfirmBtnNoWarning.IsEnabled = true;
                ChangeRegionBtn.IsEnabled = true;

                DisableKbShortcuts();
            }

            void ActionOnTimeOutRetry(int retryAttemptCount, int retryAttemptTotal, int timeOutSecond, int timeOutStep)
            {
                LoadingMessageHelper.SetMessage(Lang._MainPage.RegionLoadingTitle,
                    string.Format($"[{retryAttemptCount} / {retryAttemptTotal}] " + Lang._MainPage.RegionLoadingSubtitleTimeOut,
                        regionToChangeName,
                        timeOutSecond));
                LoadingMessageHelper.ShowActionButton(Lang._Misc.Cancel, "", CancelLoadEvent);
            }

            await preset.GameLauncherApi.LoadAsync(BeforeLoadRoutine, AfterLoadRoutine, ActionOnTimeOutRetry, OnErrorRoutine, tokenSource.Token);

            LogWriteLine($"Game: {regionToChangeName} has been completely initialized!", LogType.Scheme, true);
            FinalizeLoadRegion(gameName, gameRegion);
            ChangeBackgroundImageAsRegionAsync();

            return true;
        }

        public void ClearMainPageState()
        {
            // Clear NavigationViewControl Items and Reset Region props
            LastNavigationItem = [..NavigationViewControl.MenuItems];
            NavigationViewControl.MenuItems.Clear();
            NavigationViewControl.IsSettingsVisible = false;
            PreviousTag = "launcher";
            PreviousTagString.Clear();
            PreviousTagString.Add(PreviousTag);
            LauncherFrame.BackStack.Clear();
        }

        private async ValueTask DownloadBackgroundImage(CancellationToken Token)
        {
            // Get and set the current path of the image
            string backgroundFolder = Path.Combine(AppGameImgFolder, "bg");
            string backgroundFileName = Path.GetFileName(LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImg);
            LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal =  Path.Combine(backgroundFolder, backgroundFileName);
            SetAndSaveConfigValue("CurrentBackground", LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal);

            // Check if the background folder exist
            if (!Directory.Exists(backgroundFolder))
                Directory.CreateDirectory(backgroundFolder);

            // Start downloading the background image
            await ImageLoaderHelper.DownloadAndEnsureCompleteness(LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImg, LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal, Token);
        }

        private void FinalizeLoadRegion(string gameName, string gameRegion)
        {
            PresetConfig preset = LauncherMetadataHelper.LauncherMetadataConfig[gameName][gameRegion];

            // Log if region has been successfully loaded
            LogWriteLine($"Initializing Region {preset.ZoneFullname} Done!", LogType.Scheme, true);

            // Initializing Game Statics
            LoadGameStaticsByGameType(preset, gameName, gameRegion);

            // Init NavigationPanel Items
            InitializeNavigationItems();
        }

        private void LoadGameStaticsByGameType(PresetConfig preset, string gameName, string gameRegion)
        {
            GamePropertyVault.AttachNotifForCurrentGame();
            DisposeAllPageStatics();

            GamePropertyVault.LoadGameProperty(this, preset.GameLauncherApi.LauncherGameResource, gameName, gameRegion);

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
                    toEntry.OtherContent = Entry.ActionProperty.GetFrameworkElement();
                }

                GameVersion? ValidForVerBelow = Entry.ValidForVerBelow != null ? new GameVersion(Entry.ValidForVerBelow) : null;
                GameVersion? ValidForVerAbove = Entry.ValidForVerAbove != null ? new GameVersion(Entry.ValidForVerAbove) : null;

                if (Entry.RegionProfile == RegionProfileName && IsNotificationTimestampValid(Entry) && (Entry.ValidForVerBelow == null
                        || (LauncherUpdateHelper.LauncherCurrentVersion.Compare(ValidForVerBelow)
                        && ValidForVerAbove.Compare(LauncherUpdateHelper.LauncherCurrentVersion))
                        || LauncherUpdateHelper.LauncherCurrentVersion.Compare(ValidForVerBelow)))
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
            CurrentGameCategory = ComboBoxGameCategory.SelectedIndex;
            CurrentGameRegion = ComboBoxGameRegion.SelectedIndex;
            await LoadRegionRootButton();
            InvokeLoadingRegionPopup(false);
            MainFrameChanger.ChangeMainFrame(m_appMode == AppMode.Hi3CacheUpdater ? typeof(CachesPage) : typeof(HomePage));
            LauncherFrame.BackStack.Clear();
        }

        private async void ChangeRegionInstant()
        {
            CurrentGameCategory = ComboBoxGameCategory.SelectedIndex;
            CurrentGameRegion = ComboBoxGameRegion.SelectedIndex;
            await LoadRegionRootButton();
            InvokeLoadingRegionPopup(false);
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
                CurrentGameCategory = ComboBoxGameCategory.SelectedIndex;
                CurrentGameRegion = ComboBoxGameRegion.SelectedIndex;
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
            IsLoadRegionComplete = false;
            PresetConfig Preset = await LauncherMetadataHelper.GetMetadataConfig(GameCategory, GameRegion);

            // Start region loading
            ShowAsyncLoadingTimedOutPill();
            if (await LoadRegionFromCurrentConfigV2(Preset, GameCategory, GameRegion))
            {
                LogWriteLine($"Region changed to {Preset.ZoneFullname}", LogType.Scheme, true);
#if !DISABLEDISCORD
                if (GetAppConfigValue("EnableDiscordRPC").ToBool())
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
                InvokeLoadingRegionPopup(false);
                MainFrameChanger.ChangeMainFrame(m_appMode == AppMode.Hi3CacheUpdater ? typeof(CachesPage) : typeof(HomePage));
                LauncherFrame.BackStack.Clear();
            }

            (sender as Button).IsEnabled = !IsHide;
        }

        private async void ShowAsyncLoadingTimedOutPill()
        {
            await Task.Delay(1000);
            if (!IsLoadRegionComplete)
            {
                InvokeLoadingRegionPopup(true, Lang._MainPage.RegionLoadingTitle, RegionToChangeName);
                // MainFrameChanger.ChangeMainFrame(typeof(BlankPage));
                while (!IsLoadRegionComplete) { await Task.Delay(1000); }
            }
            InvokeLoadingRegionPopup(false);
        }

        private void InvokeLoadingRegionPopup(bool ShowLoadingMessage = true, string Title = null, string Message = null)
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
