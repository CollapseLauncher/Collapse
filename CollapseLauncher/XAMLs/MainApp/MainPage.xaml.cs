using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.DiscordPresence;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Background;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.Update;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Pages;
using CollapseLauncher.Statics;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using InnoSetupHelper;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using static CollapseLauncher.Dialogs.KeyboardShortcuts;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.Statics.GamePropertyVault;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using UIElementExtensions = CollapseLauncher.Extension.UIElementExtensions;

namespace CollapseLauncher
{
    using KeybindAction = TypedEventHandler<KeyboardAccelerator, KeyboardAcceleratorInvokedEventArgs>;

    public partial class MainPage : Page
    {
        #region Properties
        private bool LockRegionChangeBtn;
        private bool DisableInstantRegionChange;
        private bool IsTitleIconForceShow;
        private bool IsNotificationPanelShow;
        private bool IsLoadNotifComplete;
        private bool IsLoadFrameCompleted = true;
        private bool IsFirstStartup       = true;
        private int  CurrentGameCategory  = -1;
        private int  CurrentGameRegion    = -1;

        internal static List<string> PreviousTagString       = new();

#nullable enable
        internal static BackgroundMediaUtility? CurrentBackgroundHandler = null;
        private         BackgroundMediaUtility? LocalBackgroundHandler = null;
#nullable restore
        #endregion

        #region Main Routine
        public MainPage()
        {
            try
            {
                LogWriteLine($"Welcome to Collapse Launcher v{LauncherUpdateHelper.LauncherCurrentVersionString} - {MainEntryPoint.GetVersionString()}", LogType.Default, false);
                LogWriteLine($"Application Data Location:\r\n\t{AppDataFolder}", LogType.Default);
                InitializeComponent();
                m_mainPage                             =  this;
                ToggleNotificationPanelBtn.Translation += Shadow16;
                WebView2Frame.Navigate(typeof(BlankPage));
                Loaded += StartRoutine;

                // Enable implicit animation on certain elements
                AnimationHelper.EnableImplicitAnimation(true, null, GridBG_RegionGrid, GridBG_NotifBtn, NotificationPanelClearAllGrid);
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeEvents();
#if !DISABLEDISCORD
            AppDiscordPresence.Dispose();
#endif
            ImageLoaderHelper.DestroyWaifu2X();
            LocalBackgroundHandler?.Dispose();
        }

        private async void StartRoutine(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!IsShowRegionChangeWarning && IsInstantRegionChange)
                {
                    ChangeGameBtnGrid.Visibility = Visibility.Collapsed;
                    ChangeGameBtnGridShadow.Visibility = Visibility.Collapsed;
                    ChangeRegionConfirmBtn.Visibility = Visibility.Collapsed;
                    ChangeRegionConfirmBtnNoWarning.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ChangeRegionConfirmBtn.Visibility = !IsShowRegionChangeWarning ? Visibility.Collapsed : Visibility.Visible;
                    ChangeRegionConfirmBtnNoWarning.Visibility = !IsShowRegionChangeWarning ? Visibility.Visible : Visibility.Collapsed;
                }
                
                if (!await CheckForAdminAccess(this))
                {
                    if (WindowUtility.CurrentWindow is MainWindow mainWindow)
                        mainWindow?.CloseApp();
                    return;
                }

                LoadGamePreset();
                SetThemeParameters();

                VersionNumberIndicator.Text = LauncherUpdateHelper.LauncherCurrentVersionString;
                #if DEBUG
                VersionNumberIndicator.Text += "d";
                #endif
                if (IsPreview) VersionNumberIndicator.Text += "-PRE";

                if (WindowUtility.CurrentWindow is MainWindow)
                    m_actualMainFrameSize = new Size((float)WindowUtility.CurrentWindow.Bounds.Width, (float)WindowUtility.CurrentWindow.Bounds.Height);

                SubscribeEvents();
                ChangeTitleDragArea.Change(DragAreaTemplate.Default);

                await InitializeStartup();
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private async Task InitializeStartup()
        {
            RunBackgroundCheck();

            // Initialize the background image utility
            CurrentBackgroundHandler = await BackgroundMediaUtility.CreateInstanceAsync(this, BackgroundAcrylicMask, BackgroundOverlayTitleBar, BackgroundNewBackGrid, BackgroundNewMediaPlayerGrid);
            LocalBackgroundHandler = CurrentBackgroundHandler;

            // Load community tools properties
            PageStatics._CommunityToolsProperty = CommunityToolsProperty.LoadCommunityTools();

            Type Page = typeof(HomePage);

            bool isCacheUpdaterMode = m_appMode == AppMode.Hi3CacheUpdater;
            await LauncherMetadataHelper.Initialize(isCacheUpdaterMode);

            if (!isCacheUpdaterMode) SetActivatedRegion();

#if !DISABLEDISCORD
            bool isInitialStart = GetAppConfigValue("EnableDiscordRPC").ToBool();
            AppDiscordPresence = new DiscordPresenceManager(isInitialStart);
            AppDiscordPresence.SetActivity(ActivityType.Idle);
#endif

            // Lock ChangeBtn for first start
            LockRegionChangeBtn = true;

            (PresetConfig presetConfig, string gameName, string gameRegion) = await LoadSavedGameSelection();
            if (m_appMode == AppMode.Hi3CacheUpdater)
                Page = (m_appMode == AppMode.Hi3CacheUpdater && presetConfig.GameType == GameNameType.Honkai) ? typeof(CachesPage) : typeof(NotInstalledPage);

            InitKeyboardShortcuts();

            InvokeLoadingRegionPopup(true, Lang._MainPage.RegionLoadingTitle, RegionToChangeName);
            if (await LoadRegionFromCurrentConfigV2(presetConfig, gameName, gameRegion))
            {
                MainFrameChanger.ChangeMainFrame(Page);
            }

            // Unlock ChangeBtn for first start
            LockRegionChangeBtn = false;
            InvokeLoadingRegionPopup(false);
        }
        #endregion

        #region Invokers
        private void UpdateBindingsEvent(object sender, EventArgs e)
        {
            NavigationViewControl.MenuItems.Clear();
            Bindings.Update();
            UpdateLayout();

            // Find the last selected category/title and region
            string lastName = LauncherMetadataHelper.CurrentMetadataConfigGameName;
            string lastRegion = LauncherMetadataHelper.CurrentMetadataConfigGameRegion;

            #nullable enable
            List<string>? gameNameCollection = LauncherMetadataHelper.GetGameNameCollection()!;
            List<string>? gameRegionCollection = LauncherMetadataHelper.GetGameRegionCollection(lastName!)!;

            int indexOfName = gameNameCollection.IndexOf(lastName!);
            int indexOfRegion = gameRegionCollection.IndexOf(lastRegion!);
            #nullable restore
                
            // Rebuild Game Titles and Regions ComboBox items
            ComboBoxGameCategory.ItemsSource = BuildGameTitleListUI();
            ComboBoxGameCategory.SelectedIndex = indexOfName;
            ComboBoxGameRegion.SelectedIndex = indexOfRegion;

            InitializeNavigationItems(false);
            ChangeTitleDragArea.Change(DragAreaTemplate.Default);
        }

        private void ShowLoadingPageInvoker_PageEvent(object sender, ShowLoadingPageProperty e)
        {
            BackgroundImgChanger.ToggleBackground(e.Hide);
            InvokeLoadingRegionPopup(!e.Hide, e.Title, e.Subtitle);
        }

        private void SpawnWebView2Invoker_SpawnEvent(object sender, SpawnWebView2Property e)
        {
            if (e.URL == null)
            {
                WebView2Frame.Navigate(typeof(BlankPage));
                return;
            }

            SpawnWebView2Panel(new Uri(e.URL));
        }

        private async void ErrorSenderInvoker_ExceptionEvent(object sender, ErrorProperties e)
        {
            if (e.Exception.GetType() == typeof(NotImplementedException))
            {
                PreviousTag = "unavailable";
                if (!DispatcherQueue?.HasThreadAccess ?? false)
                    DispatcherQueue?.TryEnqueue(() => MainFrameChanger.ChangeMainFrame(typeof(UnavailablePage)));
                else
                    MainFrameChanger.ChangeMainFrame(typeof(UnavailablePage));

            }
            else
            {
                PreviousTag = "crashinfo";
                await SimpleDialogs.Dialog_ShowUnhandledExceptionMenu(this);
                // MainFrameChanger.ChangeMainFrame(typeof(UnhandledExceptionPage));
            }
        }

        private void MainFrameChangerInvoker_FrameEvent(object sender, MainFrameProperties e)
        {
            IsLoadFrameCompleted  = false;
            m_appCurrentFrameName = e.FrameTo.Name;
            LauncherFrame.Navigate(e.FrameTo, null, e.Transition);
            IsLoadFrameCompleted = true;
        }
        #endregion

        #region Drag Area
        private RectInt32[] DragAreaMode_Normal
        {
            get
            {
                double scaleFactor = WindowUtility.CurrentWindowMonitorScaleFactor;
                RectInt32[] rect = new RectInt32[2]
                {

                    new RectInt32((int)(TitleBarDrag1.ActualOffset.X * scaleFactor),
                                  0,
                                  (int)(TitleBarDrag1.ActualWidth * scaleFactor),
                                  (int)(48 * scaleFactor)),
                    new RectInt32((int)(TitleBarDrag2.ActualOffset.X * scaleFactor),
                                  0,
                                  (int)(TitleBarDrag2.ActualWidth * scaleFactor),
                                  (int)(48 * scaleFactor))
                };
                return rect;
            }
        }

        private RectInt32[] DragAreaMode_Full
        {
            get
            {
                Rect currentWindowPos = WindowUtility.CurrentWindowPosition;
                double scaleFactor = WindowUtility.CurrentWindowMonitorScaleFactor;

                RectInt32[] rect = new RectInt32[1]
                {
                    new RectInt32(0,
                                  0,
                                  (int)((currentWindowPos.Width - 96) * scaleFactor),
                                  (int)(48 * scaleFactor))
                };
                return rect;
            }
        }

        private RectInt32 GetElementPos(FrameworkElement element)
        {
            GeneralTransform transformTransform = element.TransformToVisual(null);
            Rect bounds = transformTransform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            double scaleFactor = WindowUtility.CurrentWindowMonitorScaleFactor;

            return new RectInt32(
                _X: (int)Math.Round(bounds.X * scaleFactor),
                _Y: (int)Math.Round(bounds.Y * scaleFactor),
                _Width: (int)Math.Round(bounds.Width * scaleFactor),
                _Height: (int)Math.Round(bounds.Height * scaleFactor)
            );
        }

        private void GridBG_RegionGrid_SizeChanged(object sender, SizeChangedEventArgs e) => ChangeTitleDragArea.Change(DragAreaTemplate.Default);

        private void MainPageGrid_SizeChanged(object sender, SizeChangedEventArgs e) => ChangeTitleDragArea.Change(DragAreaTemplate.Default);

        private async void ChangeTitleDragAreaInvoker_TitleBarEvent(object sender, ChangeTitleDragAreaProperty e)
        {
            await Task.Delay(250);

            InputNonClientPointerSource nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(WindowUtility.CurrentWindowId.Value);
            WindowUtility.EnableWindowNonClientArea();
            WindowUtility.SetWindowTitlebarDragArea(DragAreaMode_Full);

            switch (e.Template)
            {
                case DragAreaTemplate.Full:
                    nonClientInputSrc.ClearRegionRects(NonClientRegionKind.Passthrough);
                    break;
                case DragAreaTemplate.Default:
                    nonClientInputSrc.ClearAllRegionRects();
                    nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, new RectInt32[]
                    {
                        GetElementPos(GridBG_RegionGrid),
                        GetElementPos(GridBG_IconGrid),
                        GetElementPos(GridBG_NotifBtn),
                        GetElementPos((WindowUtility.CurrentWindow as MainWindow)?.MinimizeButton),
                        GetElementPos((WindowUtility.CurrentWindow as MainWindow)?.CloseButton)
                    });
                    break;
            }

            nonClientInputSrc.SetRegionRects(NonClientRegionKind.Close, null);
            nonClientInputSrc.SetRegionRects(NonClientRegionKind.Minimize, null);
        }
        #endregion

        #region Admin Checks
        public static async Task<bool> CheckForAdminAccess(UIElement root)
        {
            if (!IsPrincipalHasNoAdministratorAccess()) return true;

            ContentDialogCollapse dialog = new ContentDialogCollapse(ContentDialogTheme.Warning)
            {
                Title = Lang._Dialogs.PrivilegeMustRunTitle,
                Content = Lang._Dialogs.PrivilegeMustRunSubtitle,
                PrimaryButtonText = Lang._Misc.Yes,
                CloseButtonText = Lang._Misc.Close,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = root.XamlRoot
            };

            while (true)
            {
                switch (await dialog.QueueAndSpawnDialog())
                {
                    case ContentDialogResult.Primary:
                        try
                        {
                            Process proc = new()
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    UseShellExecute = true,
                                    Verb = "runas",
                                    FileName = AppExecutablePath,
                                    WorkingDirectory = AppFolder,
                                    Arguments = string.Join(' ', AppCurrentArgument)
                                }
                            };
                            proc.Start();
                            return false;
                        }
                        catch (Exception ex)
                        {
                            LogWriteLine($"Restarting the launcher can't be completed! {ex}", LogType.Error, true);
                        }
                        break;
                    default:
                        return false;
                }
            }
        }

        private static bool IsPrincipalHasNoAdministratorAccess()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return !principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
        #endregion

        #region Theme Methods
        public void SetThemeParameters()
        {
            if (!m_windowSupportCustomTitle)
            {
                GridBG_RegionMargin.Width              = new GridLength(0, GridUnitType.Pixel);
                GridBG_RegionGrid.HorizontalAlignment  = HorizontalAlignment.Left;
                GridBG_RegionInner.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }
        #endregion

        #region Background Image
        private void BackgroundImg_IsImageHideEvent(object sender, bool e)
        {
            if (IsFirstStartup) return;

            if (e) CurrentBackgroundHandler?.Dimm();
            else CurrentBackgroundHandler?.Undimm();
        }

        private void CustomBackgroundChanger_Event(object sender, BackgroundImgProperty e)
        {
            e.IsImageLoaded                   = false;
            LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal = e.ImgPath;
            IsCustomBG                        = e.IsCustom;

            if (e.IsCustom)
                SetAndSaveConfigValue("CustomBGPath", LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal);

            if (!File.Exists(LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal))
            {
                LogWriteLine($"Custom background file {e.ImgPath} is missing!", LogType.Warning, true);
                LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal = AppDefaultBG;
            }

            CurrentBackgroundHandler?.LoadBackground(LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal, e.IsRequestInit, e.IsForceRecreateCache, (Exception ex) =>
            {
                LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal = AppDefaultBG;
                LogWriteLine($"An error occured while loading background {e.ImgPath}\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            });
            
            e.IsImageLoaded                   = true;
        }

        internal async void ChangeBackgroundImageAsRegionAsync(bool ShowLoadingMsg = false)
        {
            IsCustomBG = GetAppConfigValue("UseCustomBG").ToBool();
            if (IsCustomBG)
            {
                string BGPath = GetAppConfigValue("CustomBGPath").ToString();
                LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal = string.IsNullOrEmpty(BGPath) ? AppDefaultBG : BGPath;
            }
            else
            {
                try
                {
                    await DownloadBackgroundImage(default);
                }
                catch (Exception ex)
                {
                    ErrorSender.SendException(ex);
                    LogWriteLine($"Failed while downloading default background image!\r\n{ex}", LogType.Error, true);
                    LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal = AppDefaultBG;
                }
            }

            if (!IsCustomBG || IsFirstStartup)
            {
                BackgroundImgChanger.ChangeBackground(LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal, IsCustomBG);
                await BackgroundImgChanger.WaitForBackgroundToLoad();
            }

            IsFirstStartup = false;
            ColorPaletteUtility.ReloadPageTheme(this, CurrentAppTheme);
        }
        #endregion

        #region Events
        private void SubscribeEvents()
        {
            ErrorSenderInvoker.ExceptionEvent += ErrorSenderInvoker_ExceptionEvent;
            MainFrameChangerInvoker.FrameEvent += MainFrameChangerInvoker_FrameEvent;
            NotificationInvoker.EventInvoker += NotificationInvoker_EventInvoker;
            BackgroundImgChangerInvoker.ImgEvent += CustomBackgroundChanger_Event;
            BackgroundImgChangerInvoker.IsImageHide += BackgroundImg_IsImageHideEvent;
            SpawnWebView2Invoker.SpawnEvent += SpawnWebView2Invoker_SpawnEvent;
            ShowLoadingPageInvoker.PageEvent += ShowLoadingPageInvoker_PageEvent;
            ChangeTitleDragAreaInvoker.TitleBarEvent += ChangeTitleDragAreaInvoker_TitleBarEvent;
            SettingsPage.KeyboardShortcutsEvent += SettingsPage_KeyboardShortcutsEvent;
            KeyboardShortcutsEvent += SettingsPage_KeyboardShortcutsEvent;
            UpdateBindingsInvoker.UpdateEvents += UpdateBindingsEvent;
            GridBG_RegionGrid.SizeChanged += GridBG_RegionGrid_SizeChanged;
            MainPageGrid.SizeChanged += MainPageGrid_SizeChanged;
        }

        private void UnsubscribeEvents()
        {
            ErrorSenderInvoker.ExceptionEvent -= ErrorSenderInvoker_ExceptionEvent;
            MainFrameChangerInvoker.FrameEvent -= MainFrameChangerInvoker_FrameEvent;
            NotificationInvoker.EventInvoker -= NotificationInvoker_EventInvoker;
            BackgroundImgChangerInvoker.ImgEvent -= CustomBackgroundChanger_Event;
            BackgroundImgChangerInvoker.IsImageHide -= BackgroundImg_IsImageHideEvent;
            SpawnWebView2Invoker.SpawnEvent -= SpawnWebView2Invoker_SpawnEvent;
            ShowLoadingPageInvoker.PageEvent -= ShowLoadingPageInvoker_PageEvent;
            ChangeTitleDragAreaInvoker.TitleBarEvent -= ChangeTitleDragAreaInvoker_TitleBarEvent;
            SettingsPage.KeyboardShortcutsEvent -= SettingsPage_KeyboardShortcutsEvent;
            KeyboardShortcutsEvent -= SettingsPage_KeyboardShortcutsEvent;
            UpdateBindingsInvoker.UpdateEvents -= UpdateBindingsEvent;
            GridBG_RegionGrid.SizeChanged -= GridBG_RegionGrid_SizeChanged;
            MainPageGrid.SizeChanged -= MainPageGrid_SizeChanged;
        }
        #endregion

        #region Background Tasks
        private async void RunBackgroundCheck()
        {
            try
            {
                // Fetch the Notification Feed in CollapseLauncher-Repo
                await FetchNotificationFeed();

                // Generate local notification
                // For Example: Starter notification
                GenerateLocalAppNotification();

                // Spawn Updated App Notification if Applicable
                SpawnAppUpdatedNotification();

                // Load local settings
                // For example: Ignore list
                LoadLocalNotificationData();

                // Then Spawn the Notification Feed
                await SpawnPushAppNotification();

                // Check Metadata Update in Background
                await CheckMetadataUpdateInBackground();

#if !DEBUG
                // Run the update check and trigger routine
                LauncherUpdateHelper.RunUpdateCheckDetached();
#else 
                LogWriteLine("Running debug build, stopping update checks!", LogType.Error, false);
#endif
            }
            catch (JsonException ex)
            {
                LogWriteLine($"Error while trying to get Notification Feed or Metadata Update\r\n{ex}", LogType.Error, true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while trying to run background tasks!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }
        #endregion

        #region Notification
        private void NotificationInvoker_EventInvoker(object sender, NotificationInvokerProp e)
        {
            if (e.IsCustomNotif)
            {
                if (e.CustomNotifAction == NotificationCustomAction.Add)
                {
                    SpawnNotificationoUI(e.Notification.MsgId, e.OtherContent as InfoBar);
                }
                else
                {
                    RemoveNotificationUI(e.Notification.MsgId);
                }
                return;
            }

            SpawnNotificationPush(e.Notification.Title, e.Notification.Message, e.Notification.Severity,
                e.Notification.MsgId, e.Notification.IsClosable ?? true, e.Notification.IsDisposable ?? true, e.CloseAction,
                e.OtherContent, e.IsAppNotif, e.Notification.Show, e.Notification.IsForceShowNotificationPanel);
        }

        private async Task FetchNotificationFeed()
        {
            try
            {
                NotificationData = new NotificationPush();
                IsLoadNotifComplete = false;
                CancellationTokenSource TokenSource = new CancellationTokenSource();
                RunTimeoutCancel(TokenSource);

                await using BridgedNetworkStream networkStream = await FallbackCDNUtil.TryGetCDNFallbackStream(string.Format(AppNotifURLPrefix, IsPreview ? "preview" : "stable"), TokenSource.Token);
                NotificationData = await networkStream.DeserializeAsync<NotificationPush>(InternalAppJSONContext.Default, TokenSource.Token);
                IsLoadNotifComplete = true;

                NotificationData.EliminatePushList();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to load notification push!\r\n{ex}", LogType.Warning, true);
            }
        }

        private void GenerateLocalAppNotification()
        {
            NotificationData.AppPush.Add(new NotificationProp
            {
                Show = true,
                MsgId = 0,
                IsDisposable = false,
                Severity = NotifSeverity.Success,
                Title = Lang._AppNotification.NotifFirstWelcomeTitle,
                Message = string.Format(Lang._AppNotification.NotifFirstWelcomeSubtitle, Lang._AppNotification.NotifFirstWelcomeBtn),
                OtherUIElement = GenerateNotificationButtonStartProcess(
                    "",
                    "https://github.com/CollapseLauncher/Collapse/wiki",
                    Lang._AppNotification.NotifFirstWelcomeBtn)
            });

            if (IsPreview)
            {
                NotificationData.AppPush.Add(new NotificationProp
                {
                    Show = true,
                    MsgId = -1,
                    IsDisposable = true,
                    Severity = NotifSeverity.Informational,
                    Title = Lang._AppNotification.NotifPreviewBuildUsedTitle,
                    Message = string.Format(Lang._AppNotification.NotifPreviewBuildUsedSubtitle, Lang._AppNotification.NotifPreviewBuildUsedBtn),
                    OtherUIElement = GenerateNotificationButtonStartProcess(
                        "",
                        "https://github.com/CollapseLauncher/Collapse/issues",
                        Lang._AppNotification.NotifPreviewBuildUsedBtn)
                });
            }

            if (!IsNotificationPanelShow && IsFirstInstall)
            {
                ForceShowNotificationPanel();
            }
        }

        private Button GenerateNotificationButtonStartProcess(string IconGlyph, string PathOrURL, string Text, bool IsUseShellExecute = true)
        {
            return NotificationPush.GenerateNotificationButton(IconGlyph, Text, (s, e) =>
            {
                new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = IsUseShellExecute,
                        FileName = PathOrURL
                    }
                }.Start();
            });
        }

        private async void RunTimeoutCancel(CancellationTokenSource Token)
        {
            await Task.Delay(10000);
            if (!IsLoadNotifComplete)
            {
                LogWriteLine("Cancel to load notification push! > 10 seconds", LogType.Error, true);
                Token.Cancel();
            }
        }

        private async Task SpawnPushAppNotification()
        {
            TypedEventHandler<InfoBar, object> ClickCloseAction = null;
            if (NotificationData.AppPush == null) return;
            foreach (NotificationProp Entry in NotificationData.AppPush)
            {
                // Check for Close Action for certain MsgIds
                switch (Entry.MsgId)
                {
                    case 0:
                        {
                            ClickCloseAction = new TypedEventHandler<InfoBar, object>((sender, args) =>
                            {
                                NotificationData.AddIgnoredMsgIds(0);
                                SaveLocalNotificationData();
                            });
                        }
                        break;
                    default:
                        ClickCloseAction = null;
                        break;
                }

                GameVersion? ValidForVerBelow = Entry.ValidForVerBelow != null ? new GameVersion(Entry.ValidForVerBelow) : null;
                GameVersion? ValidForVerAbove = Entry.ValidForVerAbove != null ? new GameVersion(Entry.ValidForVerAbove) : null;

                if (Entry.ValidForVerBelow == null && IsNotificationTimestampValid(Entry)
                    || (LauncherUpdateHelper.LauncherCurrentVersion.Compare(ValidForVerBelow)
                        && ValidForVerAbove.Compare(LauncherUpdateHelper.LauncherCurrentVersion))
                    || LauncherUpdateHelper.LauncherCurrentVersion.Compare(ValidForVerBelow))
                {
                    if (Entry.ActionProperty != null)
                    {
                        Entry.OtherUIElement = Entry.ActionProperty.GetFrameworkElement();
                    }

                    SpawnNotificationPush(Entry.Title, Entry.Message, Entry.Severity, Entry.MsgId, Entry.IsClosable ?? true,
                        Entry.IsDisposable ?? true, ClickCloseAction, (FrameworkElement)Entry.OtherUIElement, true, Entry.Show, Entry.IsForceShowNotificationPanel);
                }
                await Task.Delay(250);
            }
        }

        private void SpawnAppUpdatedNotification()
        {
            try
            {
                string UpdateNotifFile = Path.Combine(AppDataFolder, "_NewVer");
                string NeedInnoUpdateFile = Path.Combine(AppDataFolder, "_NeedInnoLogUpdate");
                TypedEventHandler<InfoBar, object> ClickClose = new TypedEventHandler<InfoBar, object>((sender, args) =>
                {
                    File.Delete(UpdateNotifFile);
                });

                // If the update was handled by squirrel and it needs Inno Setup Log file to get updated, then do the routine
                if (File.Exists(NeedInnoUpdateFile))
                {
                    try
                    {
                        string InnoLogPath = Path.Combine(Path.GetDirectoryName(AppFolder), "unins000.dat");
                        if (File.Exists(InnoLogPath)) InnoSetupLogUpdate.UpdateInnoSetupLog(InnoLogPath);
                        File.Delete(NeedInnoUpdateFile);
                    }
                    catch (Exception ex)
                    {
                        LogWriteLine($"Something wrong while opening the \"unins000.dat\" or deleting the \"_NeedInnoLogUpdate\" file\r\n{ex}", LogType.Error, true);
                    }
                }

                if (File.Exists(UpdateNotifFile))
                {
                    string VerString = File.ReadAllLines(UpdateNotifFile)[0];
                    GameVersion Version = new GameVersion(VerString);
                    SpawnNotificationPush(
                        Lang._Misc.UpdateCompleteTitle,
                        string.Format(Lang._Misc.UpdateCompleteSubtitle, Version.VersionString, IsPreview ? "Preview" : "Stable"),
                        NotifSeverity.Success,
                        0xAF,
                        true,
                        false,
                        ClickClose,
                        null,
                        true,
                        true,
                        true
                        );

                    string target;
                    string fold = Path.Combine(AppFolder, "_Temp");
                    if (Directory.Exists(fold))
                    {
                        foreach (string file in Directory.EnumerateFiles(fold))
                        {
                            if (Path.GetFileNameWithoutExtension(file).Contains("ApplyUpdate"))
                            {
                                target = Path.Combine(AppFolder, Path.GetFileName(file));
                                File.Move(file, target, true);
                            }
                        }

                        Directory.Delete(fold, true);
                    }
                }
            }
            catch { }
        }

        private InfoBarSeverity NotifSeverity2InfoBarSeverity(NotifSeverity inp)
        {
            switch (inp)
            {
                default:
                    return InfoBarSeverity.Informational;
                case NotifSeverity.Success:
                    return InfoBarSeverity.Success;
                case NotifSeverity.Warning:
                    return InfoBarSeverity.Warning;
                case NotifSeverity.Error:
                    return InfoBarSeverity.Error;
            }
        }

        private void SpawnNotificationPush(string Title, string Content, NotifSeverity Severity, int MsgId = 0, bool IsClosable = true,
            bool Disposable = false, TypedEventHandler<InfoBar, object> CloseClickHandler = null, FrameworkElement OtherContent = null, bool IsAppNotif = true,
            bool? Show = false, bool ForceShowNotificationPanel = false)
        {
            if (!(Show ?? false)) return;
            if (NotificationData.CurrentShowMsgIds.Contains(MsgId)) return;

            if (NotificationData.IsMsgIdIgnored(MsgId)) return;

            NotificationData.CurrentShowMsgIds.Add(MsgId);

            DispatcherQueue?.TryEnqueue(() =>
            {
                StackPanel OtherContentContainer = UIElementExtensions.CreateStackPanel().WithMargin(0d, -4d, 0d, 8d);

                InfoBar Notification = new InfoBar
                {
                    Title = Title,
                    Message = Content,
                    Severity = NotifSeverity2InfoBarSeverity(Severity),
                    IsClosable = IsClosable,
                    IsIconVisible = true,
                    Shadow = SharedShadow,
                    IsOpen = true
                }
                .WithMargin(4d, 4d, 4d, 0d).WithWidth(600)
                .WithCornerRadius(8).WithHorizontalAlignment(HorizontalAlignment.Right);

                Notification.Translation += Shadow32;

                if (OtherContent != null)
                    OtherContentContainer.AddElementToStackPanel(OtherContent);

                if (Disposable)
                {
                    CheckBox NeverAskNotif = new CheckBox
                    {
                        Content = new TextBlock { Text = Lang._MainPage.NotifNeverAsk, FontWeight = FontWeights.Medium },
                        Tag = $"{MsgId},{IsAppNotif}"
                    };
                    NeverAskNotif.Checked += NeverAskNotif_Checked;
                    NeverAskNotif.Unchecked += NeverAskNotif_Unchecked;
                    OtherContentContainer.AddElementToStackPanel(NeverAskNotif);
                }

                if (Disposable || OtherContent != null)
                    Notification.Content = OtherContentContainer;

                Notification.Tag = MsgId;
                Notification.CloseButtonClick += CloseClickHandler;

                SpawnNotificationoUI(MsgId, Notification);

                if (ForceShowNotificationPanel && !IsNotificationPanelShow)
                {
                    this.ForceShowNotificationPanel();
                }
            });
        }

        private void SpawnNotificationoUI(int tagID, InfoBar Notification)
        {
            Grid Container = UIElementExtensions.CreateGrid().WithTag(tagID);
            Notification.Loaded += (a, b) =>
            {
                NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;
                NewNotificationCountBadge.Visibility = Visibility.Visible;
                NewNotificationCountBadge.Value++;

                NotificationPanelClearAllGrid.Visibility = NotificationContainer.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            };

            Notification.Closed += (s, a) =>
            {
                s.Translation -= Shadow32;
                s.SetHeight(0d);
                s.SetMargin(0d);
                int msg = (int)s.Tag;

                if (NotificationData.CurrentShowMsgIds.Contains(msg))
                {
                    NotificationData.CurrentShowMsgIds.Remove(msg);
                }
                NotificationContainer.Children.Remove(Container);
                NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;

                if (NewNotificationCountBadge.Value > 0)
                {
                    NewNotificationCountBadge.Value--;
                }
                NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;
                NewNotificationCountBadge.Visibility = NewNotificationCountBadge.Value > 0 ? Visibility.Visible : Visibility.Collapsed;
                NotificationPanelClearAllGrid.Visibility = NotificationContainer.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            };

            Container.AddElementToGridRowColumn(Notification);
            NotificationContainer.AddElementToStackPanel(Container);
        }

        private void RemoveNotificationUI(int tagID)
        {
            Grid notif = NotificationContainer.Children.OfType<Grid>().Where(x => (int)x.Tag == tagID).FirstOrDefault();
            if (notif != null)
            {
                NotificationContainer.Children.Remove(notif);
                InfoBar notifBar = notif.Children.OfType<InfoBar>()?.FirstOrDefault();
                if (notifBar != null && notifBar.IsClosable)
                    notifBar.IsOpen = false;
            }
        }

        private async void ClearAllNotification(object sender, RoutedEventArgs args)
        {
            Button button = sender is Button ? sender as Button : null;
            if (button != null) button.IsEnabled = false;

            int stackIndex = 0;
            for (; stackIndex < NotificationContainer.Children.Count;)
            {
                if (NotificationContainer.Children[stackIndex] is not Grid container
                 || container.Children == null || container.Children.Count == 0
                 || container.Children[0] is not InfoBar notifBar || notifBar == null
                 || !notifBar.IsClosable)
                {
                    ++stackIndex;
                    continue;
                }

                NotificationContainer.Children.RemoveAt(stackIndex);
                notifBar.IsOpen = false;
                await Task.Delay(100);
            }

            if (NotificationContainer.Children.Count == 0)
            {
                await Task.Delay(500);
                ToggleNotificationPanelBtn.IsChecked = false;
                IsNotificationPanelShow = false;
                ShowHideNotificationPanel();
            }

            if (button != null) button.IsEnabled = true;
        }

        private void NeverAskNotif_Checked(object sender, RoutedEventArgs e)
        {
            string[] Data = (sender as CheckBox).Tag.ToString().Split(',');
            NotificationData.AddIgnoredMsgIds(int.Parse(Data[0]), bool.Parse(Data[1]));
            SaveLocalNotificationData();
        }

        private void NeverAskNotif_Unchecked(object sender, RoutedEventArgs e)
        {
            string[] Data = (sender as CheckBox).Tag.ToString().Split(',');
            NotificationData.RemoveIgnoredMsgIds(int.Parse(Data[0]), bool.Parse(Data[1]));
            SaveLocalNotificationData();
        }

        private async void ForceShowNotificationPanel()
        {
            ToggleNotificationPanelBtn.IsChecked = true;
            IsNotificationPanelShow              = true;
            ShowHideNotificationPanel();
            await Task.Delay(250);
            double currentVOffset = NotificationContainer.ActualHeight;

            NotificationPanelScrollViewer.ScrollToVerticalOffset(currentVOffset);
        }
        #endregion

        #region Game Selector Method
        private async ValueTask<(PresetConfig, string, string)> LoadSavedGameSelection()
        {
            ComboBoxGameCategory.ItemsSource = BuildGameTitleListUI();

            string gameName = GetAppConfigValue("GameCategory").ToString();

            #nullable enable
            List<string>? gameCollection   = LauncherMetadataHelper.GetGameNameCollection()!;
            List<string>? regionCollection = LauncherMetadataHelper.GetGameRegionCollection(gameName)!;
            
            if (regionCollection == null)
                gameName = LauncherMetadataHelper.LauncherGameNameRegionCollection?.Keys.FirstOrDefault();

            ComboBoxGameRegion.ItemsSource = BuildGameRegionListUI(gameName);

            var indexCategory                    = gameCollection.IndexOf(gameName!);
            if (indexCategory < 0) indexCategory = 0;

            var indexRegion = LauncherMetadataHelper.GetPreviousGameRegion(gameName);

            ComboBoxGameCategory.SelectedIndex = indexCategory;
            ComboBoxGameRegion.SelectedIndex   = indexRegion;
            CurrentGameCategory                = ComboBoxGameCategory.SelectedIndex;
            CurrentGameRegion                  = ComboBoxGameRegion.SelectedIndex;

            string? gameNameLookup = GetComboBoxGameRegionValue(ComboBoxGameCategory.SelectedValue);
            string? gameRegionLookup = GetComboBoxGameRegionValue(ComboBoxGameRegion.SelectedValue);

            return (await LauncherMetadataHelper.GetMetadataConfig(gameNameLookup, gameRegionLookup),
                                                                  gameNameLookup,
                                                                  gameRegionLookup);
        }
        
        private void SetGameCategoryChange(object sender, SelectionChangedEventArgs e)
        {
            object? selectedItem = ((ComboBox)sender).SelectedItem;
            if (selectedItem == null) return;
            string? selectedCategoryString = GetComboBoxGameRegionValue(selectedItem);
            // REMOVED: GetConfigV2Regions(SelectedCategoryString);
            
            ComboBoxGameRegion.ItemsSource   = BuildGameRegionListUI(selectedCategoryString);
            ComboBoxGameRegion.SelectedIndex = GetIndexOfRegionStringOrDefault(selectedCategoryString);
        }
        #nullable disable

        private async void EnableRegionChangeButton(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxGameCategory.SelectedIndex == CurrentGameCategory && ComboBoxGameRegion.SelectedIndex == CurrentGameRegion)
            {
                ChangeRegionConfirmBtn.IsEnabled          = false;
                ChangeRegionConfirmBtnNoWarning.IsEnabled = false;
                return;
            }

            object selValue = ((ComboBox)sender).SelectedValue;
            if (selValue == null) return;

            string category = GetComboBoxGameRegionValue(ComboBoxGameCategory.SelectedValue);
            string region = GetComboBoxGameRegionValue(selValue);
            PresetConfig preset = await LauncherMetadataHelper.GetMetadataConfig(category, region);
            
            ChangeRegionWarningText.Text = preset!.Channel != GameChannel.Stable
                ? string.Format(Lang._MainPage.RegionChangeWarnExper1, preset.Channel)
                : string.Empty;
            ChangeRegionWarning.Visibility =
                preset.Channel != GameChannel.Stable ? Visibility.Visible : Visibility.Collapsed;
            
            ChangeRegionConfirmBtn.IsEnabled          = !LockRegionChangeBtn;
            ChangeRegionConfirmBtnNoWarning.IsEnabled = !LockRegionChangeBtn;

            if (!IsShowRegionChangeWarning && IsInstantRegionChange && !DisableInstantRegionChange && !IsFirstStartup)
                ChangeRegionInstant();
        }
        #endregion

        #region Metadata Update Method
        private async Task CheckMetadataUpdateInBackground()
        {
            bool IsUpdate = await LauncherMetadataHelper.IsMetadataHasUpdate();
            if (IsUpdate)
            {
                Button UpdateMetadatabtn =
                    UIElementExtensions.CreateButtonWithIcon<Button>(
                            Lang._AppNotification!.NotifMetadataUpdateBtn,
                            "",
                            "FontAwesomeSolid",
                            "AccentButtonStyle"
                        )
                    .WithMargin(0d, 0d, 0d, 16d);

                UpdateMetadatabtn.Loaded += async (a, b) =>
                {
                    TextBlock Text = new TextBlock
                    {
                        Text = Lang._AppNotification.NotifMetadataUpdateBtnUpdating,
                        FontWeight = FontWeights.Medium,
                    }.WithVerticalAlignment(VerticalAlignment.Center);
                    ProgressRing LoadBar = new ProgressRing
                    {
                        IsIndeterminate = true,
                        Visibility = Visibility.Collapsed
                    }.WithWidthAndHeight(16d).WithMargin(0d, 0d, 8d, 0d).WithVerticalAlignment(VerticalAlignment.Center);
                    StackPanel StackPane = UIElementExtensions.CreateStackPanel(Orientation.Horizontal);
                    StackPane.AddElementToStackPanel(LoadBar);
                    StackPane.AddElementToStackPanel(Text);
                    (a as Button).Content = StackPane;
                    (a as Button).IsEnabled = false;

                    // Put 2 seconds delay before updating
                    int i = 2;
                    while (i != 0)
                    {
                        Text.Text = string.Format(Lang._AppNotification.NotifMetadataUpdateBtnCountdown, i);
                        await Task.Delay(1000);
                        i--;
                    }

                    LoadBar.Visibility = Visibility.Visible;
                    Text.Text = Lang._AppNotification.NotifMetadataUpdateBtnUpdating;

                    try
                    {
                        await LauncherMetadataHelper.RunMetadataUpdate();
                        MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
                    }
                    catch (Exception ex)
                    {
                        LogWriteLine($"Error has occured while updating metadata!\r\n{ex}", LogType.Error, true);
                        ErrorSender.SendException(ex, ErrorType.Unhandled);
                    }
                };
                SpawnNotificationPush(
                    Lang._AppNotification.NotifMetadataUpdateTitle,
                    Lang._AppNotification.NotifMetadataUpdateSubtitle,
                    NotifSeverity.Informational,
                    -886135731,
                    true,
                    false,
                    null,
                    UpdateMetadatabtn,
                    true,
                    true,
                    true
                    );
            }
        }
        #endregion

        #region Navigation
        private void InitializeNavigationItems(bool ResetSelection = true)
        {
            NavigationViewControl.IsSettingsVisible = true;
            NavigationViewControl.MenuItems.Clear();

            IGameVersionCheck CurrentGameVersionCheck = GetCurrentGameProperty()._GameVersion;

            FontFamily Fnt = FontCollections.FontAwesomeSolid;

            FontIcon IconLauncher = new FontIcon { FontFamily = Fnt, Glyph = "" };
            FontIcon IconRepair = new FontIcon { FontFamily = Fnt, Glyph = "" };
            FontIcon IconCaches = new FontIcon { FontFamily = Fnt, Glyph = "" };
            FontIcon IconGameSettings = new FontIcon { FontFamily = Fnt, Glyph = "" };
            FontIcon IconAppSettings = new FontIcon { FontFamily = Fnt, Glyph = "" };

            IconLauncher.ApplyDropShadow(Colors.Gray, 20);
            IconRepair.ApplyDropShadow(Colors.Gray, 20);
            IconCaches.ApplyDropShadow(Colors.Gray, 20);
            IconGameSettings.ApplyDropShadow(Colors.Gray, 20);
            IconAppSettings.ApplyDropShadow(Colors.Gray, 20);

            if (NavigationViewControl.SettingsItem is not null && NavigationViewControl.SettingsItem is NavigationViewItem SettingsItem)
            {
                SettingsItem.Content = Lang._SettingsPage.PageTitle;
                SettingsItem.Icon = IconAppSettings;
                ToolTipService.SetToolTip(SettingsItem, Lang._SettingsPage.PageTitle);
            }

            if (m_appMode == AppMode.Hi3CacheUpdater)
            {
                if (CurrentGameVersionCheck.GamePreset.IsCacheUpdateEnabled ?? false)
                {
                    NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                    { Content = Lang._CachesPage.PageTitle, Icon = IconCaches, Tag = "caches" });
                }
                return;
            }

            NavigationViewControl.MenuItems.Add(new NavigationViewItem()
            { Content = Lang._HomePage.PageTitle, Icon = IconLauncher, Tag = "launcher" });

            NavigationViewControl.MenuItems.Add(new NavigationViewItemHeader() { Content = Lang._MainPage.NavigationUtilities });

            if (CurrentGameVersionCheck.GamePreset.IsRepairEnabled ?? false)
            {
                NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                { Content = Lang._GameRepairPage.PageTitle, Icon = IconRepair, Tag = "repair" });
            }

            if (CurrentGameVersionCheck.GamePreset.IsCacheUpdateEnabled ?? false)
            {
                NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                { Content = Lang._CachesPage.PageTitle, Icon = IconCaches, Tag = "caches" });
            }

            switch (CurrentGameVersionCheck.GameType)
            {
                case GameNameType.Honkai:
                    NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                    { Content = Lang._GameSettingsPage.PageTitle, Icon = IconGameSettings, Tag = "honkaigamesettings" });
                    break;
                case GameNameType.StarRail:
                    NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                    { Content = Lang._StarRailGameSettingsPage.PageTitle, Icon = IconGameSettings, Tag = "starrailgamesettings" });
                    break;
            }

            if (CurrentGameVersionCheck.GameType == GameNameType.Genshin)
            {
                NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                { Content = Lang._GenshinGameSettingsPage.PageTitle, Icon = IconGameSettings, Tag = "genshingamesettings" });
            }

            if (ResetSelection)
            {
                NavigationViewControl.SelectedItem = (NavigationViewItem)NavigationViewControl.MenuItems[0];
            }
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (NavigationViewItemBase item in NavigationViewControl.MenuItems)
            {
                if (item is NavigationViewItem && item.Tag.ToString() == "launcher")
                {
                    NavigationViewControl.SelectedItem = item;
                    break;
                }
            }

            var paneRoot = (Grid)NavigationViewControl.FindDescendant("PaneRoot");
            if (paneRoot != null)
            {
                paneRoot.PointerEntered += NavView_PanePointerEntered;
                paneRoot.PointerExited  += NavView_PanePointerExited;
            }

            // The toggle button is not a part of pane. Why Microsoft!!!
            var paneToggleButtonGrid = (Grid)NavigationViewControl.FindDescendant("PaneToggleButtonGrid");
            if (paneToggleButtonGrid != null)
            {
                paneToggleButtonGrid.PointerEntered += NavView_PanePointerEntered;
                paneToggleButtonGrid.PointerExited  += NavView_PanePointerEntered;
            }

            var backIcon = NavigationViewControl.FindDescendant("NavigationViewBackButton")?.FindDescendant<AnimatedIcon>();
            backIcon?.ApplyDropShadow(Colors.Gray, 20);

            var toggleIcon = NavigationViewControl.FindDescendant("TogglePaneButton")?.FindDescendant<AnimatedIcon>();
            toggleIcon?.ApplyDropShadow(Colors.Gray, 20);
        }

        private async void NavView_PanePointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (!NavigationViewControl.IsPaneOpen)
            {
                var duration = TimeSpan.FromSeconds(0.25);
                var current = (float)NavViewPaneBackground.Opacity;
                var animation = NavViewPaneBackground.GetElementCompositor()!
                                                     .CreateScalarKeyFrameAnimation("Opacity", 1, current);
                await NavViewPaneBackground.StartAnimation(duration, animation);
            }
        }

        private async void NavView_PanePointerExited(object sender, PointerRoutedEventArgs e)
        {
            var duration = TimeSpan.FromSeconds(0.25);
            var current = (float)NavViewPaneBackground.Opacity;
            var animation = NavViewPaneBackground.GetElementCompositor()!
                                                 .CreateScalarKeyFrameAnimation("Opacity", 0, current);
            await NavViewPaneBackground.StartAnimation(duration, animation);
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (!IsLoadFrameCompleted) return;
            if (args.IsSettingsInvoked && PreviousTag != "settings") Navigate(typeof(SettingsPage), "settings");

            NavigationViewItem item = sender.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => (string)x.Content == (string)args.InvokedItem);
            if (item == null) return;

            string itemTag = (string)item.Tag;

            NavigateInnerSwitch(itemTag);
        }

        void NavigateInnerSwitch(string itemTag)
        {
            if (itemTag == PreviousTag) return;
            switch (itemTag)
            {
                case "launcher":
                    Navigate(typeof(HomePage), itemTag);
                    break;

                case "repair":
                    if (!(GetCurrentGameProperty()._GameVersion.GamePreset.IsRepairEnabled ?? false))
                        Navigate(typeof(UnavailablePage), itemTag);
                    else
                        Navigate(IsGameInstalled() ? typeof(RepairPage) : typeof(NotInstalledPage), itemTag);
                    break;

                case "caches":
                    if (GetCurrentGameProperty()._GameVersion.GamePreset.IsCacheUpdateEnabled ?? false)
                        Navigate(IsGameInstalled() || (m_appMode == AppMode.Hi3CacheUpdater && GetCurrentGameProperty()._GameVersion.GamePreset.GameType == GameNameType.Honkai) ? typeof(CachesPage) : typeof(NotInstalledPage), itemTag);
                    else
                        Navigate(typeof(UnavailablePage), itemTag);
                    break;

                case "honkaigamesettings":
                    Navigate(IsGameInstalled() ? typeof(HonkaiGameSettingsPage) : typeof(NotInstalledPage), itemTag);
                    break;

                case "starrailgamesettings":
                    Navigate(IsGameInstalled() ? typeof(StarRailGameSettingsPage) : typeof(NotInstalledPage), itemTag);
                    break;

                case "genshingamesettings":
                    Navigate(IsGameInstalled() ? typeof(GenshinGameSettingsPage) : typeof(NotInstalledPage), itemTag);
                    break;
            }
        }

        void Navigate(Type sourceType, string tagStr)
        {
            MainFrameChanger.ChangeMainFrame(sourceType, new DrillInNavigationTransitionInfo());
            PreviousTag = tagStr;
            PreviousTagString.Add(tagStr);
            LogWriteLine($"Page changed to {sourceType.Name} with Tag: {tagStr}", LogType.Scheme);
        }

        internal void InvokeMainPageNavigateByTag(string tagStr)
        {
            NavigationViewItem item = NavigationViewControl.MenuItems.OfType<NavigationViewItem>()?.Where(x => x.Tag.GetType() == typeof(string) && (string)x.Tag == tagStr)?.FirstOrDefault();
            if (item != null)
            {
                NavigationViewControl.SelectedItem = item;
                string tag = (string)item.Tag;
                NavigateInnerSwitch(tag);
            }
        }

        private void ToggleNotificationPanelBtnClick(object sender, RoutedEventArgs e)
        {
            IsNotificationPanelShow = ToggleNotificationPanelBtn.IsChecked ?? false;
            ShowHideNotificationPanel();
        }

        private void ShowHideNotificationPanel()
        {
            NewNotificationCountBadge.Value = 0;
            NewNotificationCountBadge.Visibility = Visibility.Collapsed;
            Thickness lastMargin = NotificationPanel.Margin;
            lastMargin.Right = IsNotificationPanelShow ? 0 : NotificationPanel.ActualWidth * -1;
            NotificationPanel.Margin = lastMargin;

            ShowHideNotificationLostFocusBackground(IsNotificationPanelShow);
        }

        private async void ShowHideNotificationLostFocusBackground(bool show)
        {
            if (show)
            {
                NotificationLostFocusBackground.Visibility = Visibility.Visible;
                NotificationLostFocusBackground.Opacity = 0.3;
                NotificationPanel.Translation += Shadow48;
                ToggleNotificationPanelBtn.Translation -= Shadow16;
                (ToggleNotificationPanelBtn.Content as FontIcon).FontFamily = FontCollections.FontAwesomeSolid;
            }
            else
            {
                NotificationLostFocusBackground.Opacity = 0;
                NotificationPanel.Translation -= Shadow48;
                ToggleNotificationPanelBtn.Translation += Shadow16;
                (ToggleNotificationPanelBtn.Content as FontIcon).FontFamily = FontCollections.FontAwesomeRegular;
                await Task.Delay(200);
                NotificationLostFocusBackground.Visibility = Visibility.Collapsed;
            }
        }

        private void NotificationContainerBackground_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            IsNotificationPanelShow = false;
            ToggleNotificationPanelBtn.IsChecked = false;
            ShowHideNotificationPanel();
        }

        private void NavigationViewControl_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (LauncherFrame.CanGoBack && IsLoadFrameCompleted)
            {
                LauncherFrame.GoBack();
                if (PreviousTagString.Count < 1) return;

                string lastPreviousTag          = PreviousTagString[PreviousTagString.Count - 1];
                string currentNavigationItemTag = (string)((NavigationViewItem)sender.SelectedItem).Tag;

                if (lastPreviousTag.ToLower() == currentNavigationItemTag.ToLower())
                {
                    string goLastPreviousTag = PreviousTagString[PreviousTagString.Count - 2];
                    NavigationViewItem goPreviousNavigationItem = sender.MenuItems.OfType<NavigationViewItem>().Where(x => goLastPreviousTag == (string)x.Tag).FirstOrDefault();

                    if (goLastPreviousTag == "settings")
                    {
                        PreviousTag = goLastPreviousTag;
                        PreviousTagString.RemoveAt(PreviousTagString.Count - 1);
                        sender.SelectedItem = sender.SettingsItem;
                        return;
                    }

                    if (goPreviousNavigationItem != null)
                    {
                        PreviousTag = goLastPreviousTag;
                        PreviousTagString.RemoveAt(PreviousTagString.Count - 1);
                        sender.SelectedItem = goPreviousNavigationItem;
                    }
                }
            }
        }

        private void NavigationPanelOpening_Event(NavigationView sender, object args)
        {
            Thickness curMargin = GridBG_Icon.Margin;
            curMargin.Left       = 48;
            GridBG_Icon.Margin   = curMargin;
            IsTitleIconForceShow = true;
            ToggleTitleIcon(false);
        }

        private void NavigationPanelClosing_Event(NavigationView sender, NavigationViewPaneClosingEventArgs args)
        {
            Thickness curMargin = GridBG_Icon.Margin;
            curMargin.Left       = 58;
            GridBG_Icon.Margin   = curMargin;
            IsTitleIconForceShow = false;
            ToggleTitleIcon(true);
        }
        #endregion

        #region Icons
        private void ToggleTitleIcon(bool hide)
        {
            if (!hide)
            {
                GridBG_IconTitle.Width = double.NaN;
                GridBG_IconTitle.Opacity = 1d;
                GridBG_IconImg.Opacity = 1d;
                return;
            }

            GridBG_IconTitle.Width = 0d;
            GridBG_IconTitle.Opacity = 0d;
            GridBG_IconImg.Opacity = 0.8d;
        }

        private void GridBG_Icon_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (!IsTitleIconForceShow)
            {
                Thickness curMargin = GridBG_Icon.Margin;
                curMargin.Left = 50;
                GridBG_Icon.Margin = curMargin;
                ToggleTitleIcon(false);
            }
        }

        private void GridBG_Icon_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!IsTitleIconForceShow)
            {
                Thickness curMargin = GridBG_Icon.Margin;
                curMargin.Left = 58;
                GridBG_Icon.Margin = curMargin;
                ToggleTitleIcon(true);
            }
        }

        private void GridBG_Icon_Click(object sender, RoutedEventArgs e)
        {
            if (PreviousTag.ToLower() == "launcher") return;

            MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            PreviousTag = "launcher";
            PreviousTagString.Add(PreviousTag);

            NavigationViewItem navItem = NavigationViewControl.MenuItems
                .OfType<NavigationViewItem>()
                .Where(x => ((string)x.Tag).ToLower() == PreviousTag)
                .FirstOrDefault();

            if (navItem != null)
            {
                NavigationViewControl.SelectedItem = navItem;
            }
        }
        #endregion

        #region Misc Methods
        private bool IsGameInstalled() => GameInstallationState == GameInstallStateEnum.Installed ||
                                          GameInstallationState == GameInstallStateEnum.InstalledHavePreload ||
                                          GameInstallationState == GameInstallStateEnum.InstalledHavePlugin ||
                                          GameInstallationState == GameInstallStateEnum.NeedsUpdate;

        private void SpawnWebView2Panel(Uri URL)
        {
            try
            {
                WebView2FramePage.WebView2URL = URL;
                WebView2Frame.Navigate(typeof(WebView2FramePage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromBottom });
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while initialize EdgeWebView2. Opening browser instead!\r\n{ex}", LogType.Error, true);
                new Process
                {
                    StartInfo = new ProcessStartInfo
                                {
                                    UseShellExecute = true,
                                    FileName        = URL.ToString()
                                }
                }.Start();
            }
        }

        private int GetIndexOfRegionStringOrDefault(string category)
        {
            int? index = LauncherMetadataHelper.GetPreviousGameRegion(category);

            return index == -1 || index == null ? 0 : index ?? 0;
        }
        #endregion

        #region Keyboard Shortcuts Methods
        private void InitKeyboardShortcuts()
        {
            if (GetAppConfigValue("EnableShortcuts").ToBoolNullable() == null)
            {
                SetAndSaveConfigValue("EnableShortcuts", true);
                KbShortcutList = null;

                SpawnNotificationPush(
                    Lang._AppNotification.NotifKbShortcutTitle,
                    Lang._AppNotification.NotifKbShortcutSubtitle,
                    NotifSeverity.Informational,
                    -20,
                    true,
                    false,
                    null,
                    NotificationPush.GenerateNotificationButton("", Lang._AppNotification.NotifKbShortcutBtn, (o, e) => ShowKeybinds_Invoked(null, null)),
                    true,
                    true,
                    true
                    );
            }

            if (AreShortcutsEnabled) CreateKeyboardShortcutHandlers();
        }

        private void CreateKeyboardShortcutHandlers()
        {
            try
            {
                if (KbShortcutList == null || KbShortcutList.Count == 0)
                    LoadKbShortcuts();

                int numIndex = 0;
                VirtualKeyModifiers keyModifier = KbShortcutList["GameSelection"].Modifier;
                for (; numIndex <= LauncherMetadataHelper.CurrentGameNameCount; numIndex++)
                {
                    KeyboardAccelerator keystroke = new KeyboardAccelerator()
                    {
                        Modifiers = keyModifier,
                        Key = VirtualKey.Number1 + numIndex,
                    };
                    keystroke.Invoked += KeyboardGameShortcut_Invoked;
                    KeyboardHandler.KeyboardAccelerators.Add(keystroke);

                    KeyboardAccelerator keystrokeNP = new KeyboardAccelerator()
                    {
                        Key = VirtualKey.NumberPad1 + numIndex,
                    };
                    keystrokeNP.Invoked += KeyboardGameShortcut_Invoked;
                    KeyboardHandler.KeyboardAccelerators.Add(keystrokeNP);
                }

                numIndex = 0;
                keyModifier = KbShortcutList["RegionSelection"].Modifier;
                while (numIndex < LauncherMetadataHelper.CurrentGameRegionMaxCount)
                {
                    KeyboardAccelerator keystroke = new KeyboardAccelerator()
                    {
                        Modifiers = keyModifier,
                        Key = VirtualKey.Number1 + numIndex++,
                    };
                    keystroke.Invoked += KeyboardGameRegionShortcut_Invoked;
                    KeyboardHandler.KeyboardAccelerators.Add(keystroke);
                }

                KeyboardAccelerator keystrokeF5 = new KeyboardAccelerator()
                {
                    Key = VirtualKey.F5
                };
                keystrokeF5.Invoked += RefreshPage_Invoked;
                KeyboardHandler.KeyboardAccelerators.Add(keystrokeF5);

                Dictionary<string, KeybindAction> actions = new()
                {
                    // General
                    { "KbShortcutsMenu", ShowKeybinds_Invoked },
                    { "HomePage", GoHome_Invoked },
                    { "SettingsPage", GoSettings_Invoked },
                    { "NotificationPanel", OpenNotify_Invoked },

                    // Game Related
                    { "ScreenshotFolder", OpenScreenshot_Invoked},
                    { "GameFolder", OpenGameFolder_Invoked },
                    { "CacheFolder", OpenGameCacheFolder_Invoked },
                    { "ForceCloseGame", ForceCloseGame_Invoked },

                    { "RepairPage", GoGameRepir_Invoked },
                    { "GameSettingsPage", GoGameSettings_Invoked },
                    { "CachesPage", GoGameCaches_Invoked },

                    { "ReloadRegion", RefreshPage_Invoked }
                };

                foreach (var func in actions)
                {
                    KeyboardAccelerator kbfunc = new KeyboardAccelerator()
                    {
                        Modifiers = KbShortcutList[func.Key].Modifier,
                        Key = KbShortcutList[func.Key].Key
                    };
                    kbfunc.Invoked += func.Value;
                    KeyboardHandler.KeyboardAccelerators.Add(kbfunc);
                }
            }
            catch (Exception error)
            {
                LogWriteLine(error.ToString());
                KbShortcutList = null;
                CreateKeyboardShortcutHandlers();
            }
        }

        private void RefreshPage_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (CannotUseKbShortcuts || !(IsLoadRegionComplete))
                return;

            switch (PreviousTag)
            {
                case "launcher":
                    RestoreCurrentRegion();
                    ChangeRegionNoWarning(IsShowRegionChangeWarning ? ChangeRegionConfirmBtn : ChangeRegionConfirmBtnNoWarning, null);
                    return;
                case "settings":
                    return;
                default:
                    string Tag = PreviousTag;
                    PreviousTag = "Empty";
                    NavigateInnerSwitch(Tag);
                    if (LauncherFrame != null && LauncherFrame.BackStack != null && LauncherFrame.BackStack.Count > 0)
                        LauncherFrame.BackStack.RemoveAt(LauncherFrame.BackStack.Count - 1);
                    if (PreviousTagString != null && PreviousTagString.Count > 0)
                        PreviousTagString.RemoveAt(PreviousTagString.Count - 1);
                    return;
            }
        }

        private void DeleteKeyboardShortcutHandlers() => KeyboardHandler.KeyboardAccelerators.Clear();

        private async void DisableKbShortcuts(int time = 500)
        {
            try
            {
                CannotUseKbShortcuts = true;
                await Task.Delay(time);
                CannotUseKbShortcuts = false;
            }
            catch
            {
                // Ignore warnings
            }
        }

        private void RestoreCurrentRegion()
        {
            var gameName = GetAppConfigValue("GameCategory").ToString();
            #nullable enable
            List<string>? gameNameCollection = LauncherMetadataHelper.GetGameNameCollection()!;
            List<string>? gameRegionCollection = LauncherMetadataHelper.GetGameRegionCollection(gameName)!;
            
            gameName ??= gameRegionCollection.FirstOrDefault();

            var indexCategory                    = gameNameCollection.IndexOf(gameName!);
            if (indexCategory < 0) indexCategory = 0;

            var indexRegion = LauncherMetadataHelper.GetPreviousGameRegion(gameName);

            ComboBoxGameCategory.SelectedIndex = indexCategory;
            ComboBoxGameRegion.SelectedIndex   = indexRegion;
        }

        private void KeyboardGameShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            int index = (int)sender.Key; index -= index < 96 ? 49 : 97;

            DisableInstantRegionChange = true;
            RestoreCurrentRegion();
            
            if (CannotUseKbShortcuts || !(IsLoadRegionComplete)
                                     || index >= ComboBoxGameCategory.Items.Count
                                     || ComboBoxGameCategory.SelectedValue == ComboBoxGameCategory.Items[index]
               )
            {
                DisableInstantRegionChange = false;
                return;
            }

            ComboBoxGameCategory.SelectedValue = ComboBoxGameCategory.Items[index];
            ComboBoxGameRegion.SelectedIndex = GetIndexOfRegionStringOrDefault(GetComboBoxGameRegionValue(ComboBoxGameCategory.SelectedValue));
            ChangeRegionNoWarning(ChangeRegionConfirmBtn, null);
            ChangeRegionConfirmBtn.IsEnabled          = false;
            ChangeRegionConfirmBtnNoWarning.IsEnabled = false;
            CannotUseKbShortcuts                      = true;
            DisableInstantRegionChange                = false;
        }

        private void KeyboardGameRegionShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            int index = (int)sender.Key; index -= index < 96 ? 49 : 97;

            DisableInstantRegionChange = true;
            RestoreCurrentRegion();
            

            if (CannotUseKbShortcuts || !(IsLoadRegionComplete)
                                     || index >= ComboBoxGameRegion.Items.Count 
                                     || ComboBoxGameRegion.SelectedValue == ComboBoxGameRegion.Items[index])
            {
                DisableInstantRegionChange = false;
                return;
            }

            ComboBoxGameRegion.SelectedValue = ComboBoxGameRegion.Items[index];
            ChangeRegionNoWarning(ChangeRegionConfirmBtn, null);
            ChangeRegionConfirmBtn.IsEnabled          = false;
            ChangeRegionConfirmBtnNoWarning.IsEnabled = false;
            CannotUseKbShortcuts                      = true;
            DisableInstantRegionChange                = false;
        }

        private async void ShowKeybinds_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (CannotUseKbShortcuts) return;

            await Dialog_ShowKbShortcuts(this);
        }

        private void GoHome_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!(IsLoadRegionComplete) || CannotUseKbShortcuts) return;

            if (NavigationViewControl.SelectedItem == NavigationViewControl.MenuItems[0]) return;

            DisableKbShortcuts();
            NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems[0];
            NavigateInnerSwitch("launcher");

        }

        private void GoSettings_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!(IsLoadRegionComplete) || CannotUseKbShortcuts) return;

            if (NavigationViewControl.SelectedItem == NavigationViewControl.SettingsItem) return;

            DisableKbShortcuts();
            NavigationViewControl.SelectedItem = NavigationViewControl.SettingsItem;
            Navigate(typeof(SettingsPage), "settings");
        }

        private void OpenNotify_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            ToggleNotificationPanelBtn.IsChecked = !ToggleNotificationPanelBtn.IsChecked;
            ToggleNotificationPanelBtnClick(null, null);
        }

        string GameDirPath { get => CurrentGameProperty._GameVersion.GameDirPath; }
        private void OpenScreenshot_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!IsGameInstalled()) return;

            string ScreenshotFolder = Path.Combine(NormalizePath(GameDirPath), CurrentGameProperty._GameVersion.GamePreset.GameType switch
            {
                GameNameType.StarRail => $"{Path.GetFileNameWithoutExtension(CurrentGameProperty._GameVersion.GamePreset.GameExecutableName)}_Data\\ScreenShots",
                _ => "ScreenShot"
            });

            LogWriteLine($"Opening Screenshot Folder:\r\n\t{ScreenshotFolder}");

            if (!Directory.Exists(ScreenshotFolder))
                Directory.CreateDirectory(ScreenshotFolder);

            new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = "explorer.exe",
                    Arguments = ScreenshotFolder
                }
            }.Start();
        }

        private void OpenGameFolder_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!IsGameInstalled()) return;

            string GameFolder = NormalizePath(GameDirPath);
            LogWriteLine($"Opening Game Folder:\r\n\t{GameFolder}");
            new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = "explorer.exe",
                    Arguments = GameFolder
                }
            }.Start();
        }

        private void OpenGameCacheFolder_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!IsGameInstalled()) return;

            string GameFolder = CurrentGameProperty._GameVersion.GameDirAppDataPath;
            LogWriteLine($"Opening Game Folder:\r\n\t{GameFolder}");
            new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = "explorer.exe",
                    Arguments = GameFolder
                }
            }.Start();
        }

        private void ForceCloseGame_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!CurrentGameProperty.IsGameRunning) return;

            PresetConfig gamePreset = CurrentGameProperty._GameVersion.GamePreset;
            try
            {
                var gameProcess = Process.GetProcessesByName(gamePreset.GameExecutableName!.Split('.')[0]);
                foreach (var p in gameProcess)
                {
                    LogWriteLine($"Trying to stop game process {gamePreset.GameExecutableName.Split('.')[0]} at PID {p.Id}", LogType.Scheme, true);
                    p.Kill();
                }
            }
            catch (Win32Exception ex)
            {
                LogWriteLine($"There is a problem while trying to stop Game with Region: {gamePreset.ZoneName}\r\nTraceback: {ex}", LogType.Error, true);
            }
        }
        private void GoGameRepir_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!(IsLoadRegionComplete) || CannotUseKbShortcuts) return;

            if (NavigationViewControl.SelectedItem == NavigationViewControl.MenuItems[2]) return;

            DisableKbShortcuts();
            NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems[2];
            NavigateInnerSwitch("repair");
        }

        private void GoGameCaches_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!(IsLoadRegionComplete) || CannotUseKbShortcuts)
                return;
            if (NavigationViewControl.SelectedItem == NavigationViewControl.MenuItems[3])
                return;

            DisableKbShortcuts();
            NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems[3];
            NavigateInnerSwitch("caches");
        }

        private void GoGameSettings_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!(IsLoadRegionComplete) || CannotUseKbShortcuts)
                return;

            if (NavigationViewControl.SelectedItem == NavigationViewControl.MenuItems.Last())
                return;

            DisableKbShortcuts();
            NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems.Last();
            switch (CurrentGameProperty._GamePreset.GameType)
            {
                case GameNameType.Honkai:
                    Navigate(typeof(HonkaiGameSettingsPage), "honkaigamesettings");
                    break;
                case GameNameType.Genshin:
                    Navigate(typeof(GenshinGameSettingsPage), "genshingamesettings");
                    break;
                case GameNameType.StarRail:
                    Navigate(typeof(StarRailGameSettingsPage), "starrailgamesettings");
                    break;
            }
        }

        private bool AreShortcutsEnabled
        {
            get => GetAppConfigValue("EnableShortcuts").ToBool(true);
        }

        private void SettingsPage_KeyboardShortcutsEvent(object sender, int e)
        {
            switch (e)
            {
                case 0:
                    CreateKeyboardShortcutHandlers();
                    break;
                case 1:
                    DeleteKeyboardShortcutHandlers();
                    CreateKeyboardShortcutHandlers();
                    break;
                case 2:
                    DeleteKeyboardShortcutHandlers();
                    break;
            }
        }
        #endregion

        #region AppActivation
        private bool SetActivatedRegion()
        {
            var args = m_arguments.StartGame;
            if (args == null) return true;

            string oldGameCategory = GetAppConfigValue("GameCategory").ToString();

            string gameName = args.Game;

            #nullable enable
            List<string>? gameNameCollection = LauncherMetadataHelper.GetGameNameCollection()!;
            List<string>? gameRegionCollection = LauncherMetadataHelper.GetGameRegionCollection(gameName)!;
            if (gameRegionCollection == null)
            {
                bool res = int.TryParse(args.Game, out int gameIndex);
                if (!res || gameIndex < 0 || gameIndex >= gameNameCollection.Count) return true;

                gameName = gameNameCollection[gameIndex];
                gameRegionCollection = LauncherMetadataHelper.GetGameRegionCollection(gameName)!;
            }
            SetAndSaveConfigValue("GameCategory", gameName);
            #nullable restore
            
            if (args.Region != null)
            {
                string gameRegion = args.Region;
                if (!gameRegionCollection.Contains(gameRegion))
                {
                    bool res = int.TryParse(args.Region, out int regionIndex);
                    if (!res || regionIndex < 0 || regionIndex >= gameRegionCollection.Count) return true;

                    gameRegion = gameRegionCollection[regionIndex];
                }

                int oldGameRegionIndex = LauncherMetadataHelper.GetPreviousGameRegion(gameName);
                string oldGameRegion = gameRegionCollection.ElementAt(oldGameRegionIndex);
                if (oldGameRegion == null) return true;

                LauncherMetadataHelper.SetPreviousGameRegion(gameName, gameRegion);
                SetAndSaveConfigValue("GameRegion", gameRegion);

                if (oldGameCategory == gameName && oldGameRegion == gameRegion) return true;
            }

            return false;
        }

        private async void ChangeToActivatedRegion()
        {
            if (!(IsLoadRegionComplete) || CannotUseKbShortcuts) return;

            bool sameRegion = SetActivatedRegion();

            DisableInstantRegionChange = true;
            LockRegionChangeBtn        = true;
            IsLoadRegionComplete       = false;

            (PresetConfig preset, string gameName, string gameRegion) = await LoadSavedGameSelection();

            ShowAsyncLoadingTimedOutPill();
            if (await LoadRegionFromCurrentConfigV2(preset, gameName, gameRegion))
            {
#if !DISABLEDISCORD
                if (GetAppConfigValue("EnableDiscordRPC").ToBool() && !sameRegion)
                    AppDiscordPresence.SetupPresence();
#endif
                InvokeLoadingRegionPopup(false);
                LauncherFrame.BackStack.Clear();
                MainFrameChanger.ChangeMainFrame(m_appMode == AppMode.Hi3CacheUpdater? typeof(CachesPage) : typeof(HomePage));
                LogWriteLine($"Region changed to {preset.ZoneFullname}", LogType.Scheme, true);
            }

            LockRegionChangeBtn        = false;
            DisableInstantRegionChange = false;
        }

        public void OpenAppActivation()
        {
            if (m_arguments.StartGame == null) return;

            DispatcherQueue?.TryEnqueue(ChangeToActivatedRegion);
        }
        #endregion
    }
}
