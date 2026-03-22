using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.Update;
using CollapseLauncher.Pages;
using CollapseLauncher.Plugins;
using CollapseLauncher.XAMLs.Theme.ContentDialog;
using CollapseLauncher.XAMLs.Theme.CustomControls;
using Hi3Helper;
using Hi3Helper.Plugin.Core.Update;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using static Hi3Helper.Shared.Region.LauncherConfig;
using UIElementExtensions = CollapseLauncher.Extension.UIElementExtensions;
// ReSharper disable StringLiteralTypo
// ReSharper disable ConvertIfStatementToSwitchStatement
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher
{
    public partial class MainPage
    {
        #region Properties
        private bool _lockRegionChangeBtn;
        private bool _disableInstantRegionChange;
        private bool _isTitleIconForceShow;
        private bool _isNotificationPanelShow;
        private bool _isLoadNotifComplete;
        private int  _currentGameCategory  = -1;
        private int  _currentGameRegion    = -1;

        internal Uri PlaceholderBackgroundImage => new(ImageBackgroundManager.GetPlaceholderBackgroundImageFrom(CurrentGameProperty?.GamePreset));
        internal string PlaceholderDecodedCacheDir => AppGameImgFolder;
        internal ImageBackgroundManager CurrentBackgroundManager => ImageBackgroundManager.Shared;

        #endregion

        #region Main Routine
        public MainPage()
        {
            try
            {
                Logger.LogWriteLine($"Welcome to Collapse Launcher v{LauncherUpdateHelper.LauncherCurrentVersionString} - {MainEntryPoint.GetVersionString()}");
                Logger.LogWriteLine($"Application Data Location:\r\n\t{AppDataFolder}");
                InitializeComponent();
                Interlocked.Exchange(ref InnerLauncherConfig.m_mainPage, this);
                ToggleNotificationPanelBtn.Translation = new Vector3(0,0,16);
                WebView2Frame.Navigate(typeof(BlankPage));
                Loaded += StartRoutine;

                NotificationElementsCollection.CollectionChanged += NotificationElementsCollection_CollectionChanged;

                ImageBackgroundManager.Shared.GlobalParallaxHoverSource = MainPageGrid;
                ImageBackgroundManager.Shared.ColorAccentChanged += SharedOnColorAccentChanged;

                // Enable implicit animation on certain elements
                AnimationHelper.EnableImplicitAnimation(true, null, GridBGRegionGrid, GridBGNotifBtn, NotificationPanelClearAllGrid);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void SharedOnColorAccentChanged(Color color)
        {
            DispatcherQueue.TryEnqueue(() => this.ChangeAccentColor(color));
            DispatcherQueue.TryEnqueue(() => this.ChangeAccentColor(color));
            DispatcherQueue.TryEnqueue(() => this.ChangeAccentColor(color));
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeEvents();
#if !DISABLEDISCORD
            InnerLauncherConfig.AppDiscordPresence.Dispose();
#endif
            ImageLoaderHelper.DestroyWaifu2X();

            // Reset image background manager context before reloading.
            ImageBackgroundManager.Shared.ResetContexts();
        }

        private async void StartRoutine(object sender, RoutedEventArgs e)
        {
            try
            {
                SubscribeEvents();
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
                        _ = mainWindow.CloseApp();
                    return;
                }

                LoadGamePreset();
                SetThemeParameters();

                VersionNumberIndicator.Text = LauncherUpdateHelper.LauncherCurrentVersionString;
                #if DEBUG
                VersionNumberIndicator.Text += "d";
                DebugBadge.Visibility = Visibility.Visible;
                #endif
                if (IsPreview) VersionNumberIndicator.Text += "-PRE";

                if (WindowUtility.CurrentWindow is MainWindow)
                {
                    InnerLauncherConfig.m_actualMainFrameSize = new Size((float)WindowUtility.CurrentWindow.Bounds.Width, (float)WindowUtility.CurrentWindow.Bounds.Height);
                }

                ChangeTitleDragArea.Change(DragAreaTemplate.Default);

                await InitializeStartup();
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
                Logger.LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
            }
        }

        private async Task InitializeStartup()
        {
            Type page = typeof(HomePage);

            bool isCacheUpdaterMode = InnerLauncherConfig.m_appMode == InnerLauncherConfig.AppMode.Hi3CacheUpdater;
            await LauncherMetadataHelper.Initialize(isCacheUpdaterMode);

            if (!isCacheUpdaterMode) SetActivatedRegion();

            // Lock ChangeBtn for first start
            _lockRegionChangeBtn = true;

            (PresetConfig presetConfig, string gameName, string gameRegion) = await LoadSavedGameSelection();
            if (InnerLauncherConfig.m_appMode == InnerLauncherConfig.AppMode.Hi3CacheUpdater)
                page = InnerLauncherConfig.m_appMode == InnerLauncherConfig.AppMode.Hi3CacheUpdater && presetConfig.GameType == GameNameType.Honkai ? typeof(CachesPage) : typeof(NotInstalledPage);

            InitKeyboardShortcuts();

            InvokeLoadingRegionPopup(true, Locale.Current.Lang?._MainPage?.RegionLoadingTitle, RegionToChangeName);
            if (await LoadRegionFromCurrentConfigV2(presetConfig, gameName, gameRegion))
            {
                MainFrameChanger.ChangeMainFrame(page, true);
                bool isEnableDiscord = GetAppConfigValue("EnableDiscordRPC");
                if (isEnableDiscord)
                {
                    InnerLauncherConfig.AppDiscordPresence.SetupPresence();
                }
            }

            // Unlock ChangeBtn for first start
            _lockRegionChangeBtn = false;
            InvokeLoadingRegionPopup(false);

            // After all activities were complete, run background check, including
            // invoking notifications
            _ = RunBackgroundCheck();
        }
        #endregion

        #region Invokers

        private static void ShowLoadingPageInvoker_PageEvent(object? sender, ShowLoadingPageProperty e)
        {
            InvokeLoadingRegionPopup(!e.Hide, e.Title, e.Subtitle);
        }

        private void SpawnWebView2Invoker_SpawnEvent(object? sender, SpawnWebView2Property e)
        {
            if (e.URL == null)
            {
                WebView2Frame.Navigate(typeof(BlankPage));
                return;
            }

            SpawnWebView2Panel(new Uri(e.URL));
        }

        private async void ErrorSenderInvoker_ExceptionEvent(object? sender, ErrorProperties e)
        {
            try
            {
                if (e.Exception.GetType() == typeof(NotImplementedException))
                {
                    if (!DispatcherQueue?.HasThreadAccessSafe() ?? false)
                        DispatcherQueue?.TryEnqueue(() => MainFrameChanger.ChangeMainFrame(typeof(UnavailablePage)));
                    else
                        MainFrameChanger.ChangeMainFrame(typeof(UnavailablePage));

                }
                // CRC error show
                else if (e.Exception.GetType() == typeof(IOException) && e.Exception.HResult == unchecked((int)0x80070017))
                {
                    ErrorSender.ExceptionType = ErrorType.DiskCrc;
                    await SimpleDialogs.Dialog_ShowUnhandledExceptionMenu();
                }
                else
                {
                    await SimpleDialogs.Dialog_ShowUnhandledExceptionMenu();
                }
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
                Logger.LogWriteLine($"{ex}", LogType.Error, true);
            }
        }

        private void MainFrameChangerInvoker_FrameEvent(object? sender, MainFrameProperties e)
        {
            InnerLauncherConfig.m_appCurrentFrameName = e.FrameTo.Name;

            int previousStackLimit = LauncherFrame.CacheSize;
            if (e.RequireCacheReset)
            {
                LauncherFrame.BackStack.Clear();
                LauncherFrame.CacheSize            = 0;
                NavigationViewControl.SelectedItem = null;
            }

            TryNavigateFrom(e.FrameTo, e.Transition, e.RequireCacheReset);
            if (e.RequireCacheReset)
            {
                LauncherFrame.CacheSize = previousStackLimit;
            }
        }

        private void MainFrameChangerInvoker_FrameGoBackEvent(object? sender, EventArgs e)
        {
            if (LauncherFrame.CanGoBack)
                LauncherFrame.GoBack();
        }
        #endregion

        #region Drag Area
        internal RectInt32[] DragAreaModeNormal
        {
            get
            {
                double scaleFactor = WindowUtility.CurrentWindowMonitorScaleFactor;
                RectInt32[] rect =
                [
                    new((int)(TitleBarDrag1.ActualOffset.X * scaleFactor),
                        0,
                        (int)(TitleBarDrag1.ActualWidth * scaleFactor),
                        (int)(48 * scaleFactor)),
                    new((int)(TitleBarDrag2.ActualOffset.X * scaleFactor),
                        0,
                        (int)(TitleBarDrag2.ActualWidth * scaleFactor),
                        (int)(48 * scaleFactor))
                ];
                return rect;
            }
        }

        internal static RectInt32[] DragAreaModeFull
        {
            get
            {
                Rect currentWindowPos = WindowUtility.CurrentWindowPosition;
                double scaleFactor = WindowUtility.CurrentWindowMonitorScaleFactor;

                RectInt32[] rect =
                [
                    new(0,
                        0,
                        (int)((currentWindowPos.Width - 96) * scaleFactor),
                        (int)(48 * scaleFactor))
                ];
                return rect;
            }
        }

        private static void GridBG_RegionGrid_SizeChanged(object sender, SizeChangedEventArgs e) => ChangeTitleDragArea.Change(DragAreaTemplate.Default);

        private static void MainPageGrid_SizeChanged(object sender, SizeChangedEventArgs e) => ChangeTitleDragArea.Change(DragAreaTemplate.Default);

        #endregion

        #region Admin Checks
        private static async Task<bool> CheckForAdminAccess(UIElement root)
        {
            if (!IsPrincipalHasNoAdministratorAccess()) return true;

            ContentDialogCollapse dialog = new(ContentDialogTheme.Warning)
            {
                Title             = Locale.Current.Lang?._Dialogs?.PrivilegeMustRunTitle,
                Content           = Locale.Current.Lang?._Dialogs?.PrivilegeMustRunSubtitle,
                PrimaryButtonText = Locale.Current.Lang?._Misc?.Yes,
                CloseButtonText   = Locale.Current.Lang?._Misc?.Close,
                DefaultButton     = ContentDialogButton.Primary,
                XamlRoot          = root.XamlRoot
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
                                    WorkingDirectory = AppExecutableDir,
                                    Arguments = string.Join(' ', AppCurrentArgument)
                                }
                            };
                            proc.Start();
                            return false;
                        }
                        catch (Exception ex)
                        {
                            await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                            Logger.LogWriteLine($"Restarting the launcher can't be completed! {ex}", LogType.Error, true);
                        }
                        break;
                    default:
                        return false;
                }
            }
        }

        private static bool IsPrincipalHasNoAdministratorAccess()
        {
            using WindowsIdentity identity  = WindowsIdentity.GetCurrent();
            WindowsPrincipal      principal = new(identity);
            return !principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        #endregion

        #region Theme Methods
        private void SetThemeParameters()
        {
            if (InnerLauncherConfig.m_windowSupportCustomTitle)
            {
                return;
            }

            GridBGRegionMargin.Width              = new GridLength(0, GridUnitType.Pixel);
            GridBGRegionGrid.HorizontalAlignment  = HorizontalAlignment.Left;
            GridBGRegionInner.HorizontalAlignment = HorizontalAlignment.Left;
        }
        #endregion

        #region Events
        private void SubscribeEvents()
        {
            ErrorSenderInvoker.ExceptionEvent        += ErrorSenderInvoker_ExceptionEvent;
            MainFrameChangerInvoker.FrameEvent       += MainFrameChangerInvoker_FrameEvent;
            MainFrameChangerInvoker.FrameGoBackEvent += MainFrameChangerInvoker_FrameGoBackEvent;
            NotificationInvoker.EventInvoker         += NotificationInvoker_EventInvoker;
            SpawnWebView2Invoker.SpawnEvent          += SpawnWebView2Invoker_SpawnEvent;
            ShowLoadingPageInvoker.PageEvent         += ShowLoadingPageInvoker_PageEvent;
            SettingsPage.KeyboardShortcutsEvent      += SettingsPage_KeyboardShortcutsEvent;
            KeyboardShortcuts.KeyboardShortcutsEvent += SettingsPage_KeyboardShortcutsEvent;
            GridBGRegionGrid.SizeChanged             += GridBG_RegionGrid_SizeChanged;
            MainPageGrid.SizeChanged                 += MainPageGrid_SizeChanged;
        }

        private void UnsubscribeEvents()
        {
            ErrorSenderInvoker.ExceptionEvent        -= ErrorSenderInvoker_ExceptionEvent;
            MainFrameChangerInvoker.FrameEvent       -= MainFrameChangerInvoker_FrameEvent;
            NotificationInvoker.EventInvoker         -= NotificationInvoker_EventInvoker;
            SpawnWebView2Invoker.SpawnEvent          -= SpawnWebView2Invoker_SpawnEvent;
            ShowLoadingPageInvoker.PageEvent         -= ShowLoadingPageInvoker_PageEvent;
            SettingsPage.KeyboardShortcutsEvent      -= SettingsPage_KeyboardShortcutsEvent;
            KeyboardShortcuts.KeyboardShortcutsEvent -= SettingsPage_KeyboardShortcutsEvent;
            GridBGRegionGrid.SizeChanged             -= GridBG_RegionGrid_SizeChanged;
            MainPageGrid.SizeChanged                 -= MainPageGrid_SizeChanged;
        }
        #endregion

        #region Background Tasks
        private async Task RunBackgroundCheck()
        {
            try
            {
                // Fetch the Notification Feed in CollapseLauncher-Repo
                await FetchNotificationFeed();

                // Generate local notification
                // For Example: Starter notification
                await GenerateLocalAppNotification();

                // Spawn Updated App Notification if Applicable
                await SpawnAppUpdatedNotification();

                // Load local settings
                // For example: Ignore list
                await InnerLauncherConfig.LoadLocalNotificationData();

                // Then Spawn the Notification Feed
                await SpawnPushAppNotification();

                // Check Metadata Update in Background
                if (await CheckMetadataUpdateInBackground())
                    return; // Cancel any routine below to avoid conflict with app update

#if !DEBUG
                // Run the update check and trigger routine
                await LauncherUpdateHelper.RunUpdateCheckDetached();
#else 
                Logger.LogWriteLine("Running debug build, stopping update checks!", LogType.Error);
#endif
            }
            catch (JsonException ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex);
                ErrorSender.SendException(ex);
                Logger.LogWriteLine($"Error while trying to get Notification Feed or Metadata Update\r\n{ex}", LogType.Error, true);
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex);
                ErrorSender.SendException(ex);
                Logger.LogWriteLine($"Error while trying to run background tasks!\r\n{ex}", LogType.Error, true);
            }
        }
        #endregion

        #region Game Selector Method
        private async Task<(PresetConfig, string, string)> LoadSavedGameSelection()
        {
            List<string> gameTitles = LauncherMetadataHelper.GetGameTitleList();
            string savedLastGameTitle = GetAppConfigValue("GameCategory").Value
                                     ?? (gameTitles.Count == 0
                                            ? throw new InvalidOperationException("Launcher has no game title available")
                                            : gameTitles[0]);

            List<PresetConfig> gameRegions = LauncherMetadataHelper.GetGameRegionList(savedLastGameTitle);

            // Fallback to default game title if no last game title is found.
            if (gameRegions.Count == 0)
            {
                savedLastGameTitle = gameTitles[0];
                gameRegions        = LauncherMetadataHelper.GetGameRegionList(savedLastGameTitle);
            }

            int indexTitle                   = gameTitles.IndexOf(savedLastGameTitle);
            int indexRegion                  = LauncherMetadataHelper.GetGameRegionLastSavedIndexOrDefault(savedLastGameTitle);
            if (indexTitle < 0) indexTitle   = 0;
            if (indexRegion < 0) indexRegion = 0;

            savedLastGameTitle = gameTitles[indexTitle];
            string savedLastGameRegion = gameRegions[indexRegion].ZoneName ?? "";

            ComboBoxGameTitle.ItemsSource    = gameTitles;
            ComboBoxGameRegion.ItemsSource   = gameRegions;
            ComboBoxGameTitle.SelectedIndex  = indexTitle;
            ComboBoxGameRegion.SelectedIndex = indexRegion;
            _currentGameCategory             = indexTitle;
            _currentGameRegion               = indexRegion;

            return (await LauncherMetadataHelper.GetMetadataConfig(savedLastGameTitle, savedLastGameRegion), savedLastGameTitle, savedLastGameRegion);
        }
        
        private void SetGameTitleChange(object sender, SelectionChangedEventArgs e)
        {
            object? selectedItem = ((ComboBox)sender).SelectedItem;
            if (selectedItem is not string asGameTitleString) return;

            ComboBoxGameRegion.ItemsSource   = LauncherMetadataHelper.GetGameRegionList(asGameTitleString);
            ComboBoxGameRegion.SelectedIndex = LauncherMetadataHelper.GetGameRegionLastSavedIndexOrDefault(asGameTitleString);
        }

        private bool _isDisableInstantRegionChangeTemporary = true;
        private async void EnableRegionChangeButton(object sender, SelectionChangedEventArgs e)
        {
            if (_isDisableInstantRegionChangeTemporary) // Disabling instant change for the first start-up to avoid conflict
            {
                _isDisableInstantRegionChangeTemporary = false;
                return;
            }

            if (ComboBoxGameRegion.SelectedIndex < 0)
            {
                return;
            }

            if (ComboBoxGameTitle.SelectedIndex == _currentGameCategory && ComboBoxGameRegion.SelectedIndex == _currentGameRegion)
            {
                ChangeRegionConfirmBtn.IsEnabled          = false;
                ChangeRegionConfirmBtnNoWarning.IsEnabled = false;
                return;
            }

            if (ComboBoxGameTitle.SelectedValue is not string asGameTitleString) return;
            if (((ComboBox)sender).SelectedValue is not PresetConfig asGameRegionString) return;

            PresetConfig preset = await LauncherMetadataHelper.GetMetadataConfig(asGameTitleString, asGameRegionString.ZoneName ?? "");

            ChangeRegionWarningText.Text = preset.GameChannel != GameChannel.Stable
                ? string.Format(Locale.Current.Lang?._MainPage?.RegionChangeWarnExper1 ?? "", preset.GameChannel)
                : string.Empty;
            ChangeRegionWarning.Visibility =
                preset.GameChannel != GameChannel.Stable ? Visibility.Visible : Visibility.Collapsed;
            
            ChangeRegionConfirmBtn.IsEnabled          = !_lockRegionChangeBtn;
            ChangeRegionConfirmBtnNoWarning.IsEnabled = !_lockRegionChangeBtn;

            if (!IsShowRegionChangeWarning && IsInstantRegionChange && !_disableInstantRegionChange)
                ChangeRegionInstant();
        }

        private void GameComboBox_OnDropDownOpened(object sender, object e)
        {
            ChangeTitleDragArea.Change(DragAreaTemplate.None);
        }

        private void GameComboBox_OnDropDownClosed(object sender, object e)
        {
            ChangeTitleDragArea.Change(DragAreaTemplate.Default);
        }
        #endregion

        #region Metadata Update Method
        private async ValueTask<bool> CheckMetadataUpdateInBackground()
        {
            bool isMetadataHasUpdate = await LauncherMetadataHelper.IsMetadataHasUpdate();
            (List<(string, PluginManifest)> pluginUpdateNameList, bool isPluginHasUpdate) = await PluginManager.StartUpdateBackgroundRoutine();

            if (!isMetadataHasUpdate && !isPluginHasUpdate)
            {
                return false;
            }

            if (isPluginHasUpdate)
            {
            StartSpawn:
                Grid textGridBox = UIElementExtensions.CreateGrid()
                                                      .WithRows(new GridLength(),
                                                                new GridLength(1, GridUnitType.Auto))
                                                      .WithRowSpacing(8d);

                TextBlock textBlock = textGridBox.AddElementToGridRow(new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap
                }.AddTextBlockLine(string.Format(Locale.Current.Lang?._Dialogs?.PluginManagerUpdateAvailableSubtitle1 ?? "", pluginUpdateNameList.Count))
                 .AddTextBlockNewLine(2), 0);

                CheckBox enablePluginAutoUpdateCheck = textGridBox.AddElementToGridRow(new CheckBox(), 1);
                enablePluginAutoUpdateCheck.Content = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap
                }.AddTextBlockLine(Locale.Current.Lang?._PluginManagerPage?.ListViewMainActionButton3);
                enablePluginAutoUpdateCheck.BindProperty(ToggleButton.IsCheckedProperty,
                                                         PluginManagerPage.Context,
                                                         nameof(PluginManagerPage.Context.IsEnableAutoUpdate),
                                                         bindingMode: BindingMode.TwoWay);
                enablePluginAutoUpdateCheck.Scale  = new Vector3(0.80f);
                enablePluginAutoUpdateCheck.Margin = new Thickness(0, 0, 0, -16);

                foreach ((string, PluginManifest) pluginUpdateName in pluginUpdateNameList)
                {
                    try
                    {
                    #nullable enable
                        textBlock.AddTextBlockLine($"    • {pluginUpdateName.Item1}", FontWeights.Bold);
                    #nullable restore
                    }
                    catch
                    {
                        // ignored
                    }
                }
                textBlock.AddTextBlockNewLine(2)
                    .AddTextBlockLine(Locale.Current.Lang?._Dialogs?.PluginManagerUpdateAvailableSubtitle2);

                ContentDialogResult pluginUpdateConfirm =
                    await SimpleDialogs.SpawnDialog(string.Format(Locale.Current.Lang?._Dialogs?.PluginManagerUpdateAvailableTitle, pluginUpdateNameList.Count),
                                                    textGridBox,
                                                    null,
                                                    Locale.Current.Lang?._Dialogs?.PluginManagerUpdateAvailableCancelBtn,
                                                    Locale.Current.Lang?._Dialogs?.PluginManagerUpdateAvailableConfirmBtn,
                                                    string.Format(Locale.Current.Lang?._Dialogs?.PluginManagerUpdateAvailableToManagerMenuBtn,
                                                                  Locale.Current.Lang?._PluginManagerPage?.PageTitle),
                                                    ContentDialogButton.Primary,
                                                    ContentDialogTheme.Success);

                if (pluginUpdateConfirm == ContentDialogResult.None)
                {
                    return false;
                }

                if (pluginUpdateConfirm == ContentDialogResult.Secondary)
                {
                    FullPageOverlay overlayMenu = new(new PluginManagerPage(), XamlRoot, true)
                    {
                        Size               = FullPageOverlaySize.Full,
                        OverlayTitleSource = () => Locale.Current.Lang?._PluginManagerPage?.PageTitle,
                        OverlayTitleIcon = new FontIconSource
                        {
                            Glyph    = "\uE912",
                            FontSize = 16
                        }
                    };

                    await overlayMenu.ShowAsync();
                    goto StartSpawn;
                }

                try
                {
                    PluginManagerPage.AskLauncherRestart(null, null);
                    return true;
                }
                catch (Exception ex)
                {
                    ErrorSender.SendException(ex);
                    Logger.LogWriteLine($"Error has occured while updating metadata!\r\n{ex}", LogType.Error, true);
                }

                return false;
            }

            Button updateMetadataBtn =
                UIElementExtensions.CreateButtonWithIcon<Button>(Locale.Current.Lang?._AppNotification!.NotifMetadataUpdateBtn,
                                                                 "",
                                                                 "FontAwesomeSolid",
                                                                 "AccentButtonStyle"
                                                                )
                                   .WithMargin(0d, 0d, 0d, 16d);

            updateMetadataBtn.Loaded += async (a, _) =>
                                        {
                                            TextBlock text = new TextBlock
                                            {
                                                Text       = Locale.Current.Lang?._AppNotification?.NotifMetadataUpdateBtnUpdating,
                                                FontWeight = FontWeights.Medium
                                            }.WithVerticalAlignment(VerticalAlignment.Center);
                                            ProgressRing loadBar = new ProgressRing
                                            {
                                                IsIndeterminate = true,
                                                Visibility      = Visibility.Collapsed
                                            }.WithWidthAndHeight(16d).WithMargin(0d, 0d, 8d, 0d).WithVerticalAlignment(VerticalAlignment.Center);
                                            StackPanel stackPane = UIElementExtensions.CreateStackPanel(Orientation.Horizontal);
                                            stackPane.AddElementToStackPanel(loadBar);
                                            stackPane.AddElementToStackPanel(text);
                                            Button aButton = a as Button;
                                            if (aButton != null)
                                            {
                                                aButton.Content   = stackPane;
                                                aButton.IsEnabled = false;
                                            }

                                            // Put 2 seconds delay before updating
                                            int i = 2;
                                            while (i != 0)
                                            {
                                                text.Text = string.Format(Locale.Current.Lang?._AppNotification?.NotifMetadataUpdateBtnCountdown ?? "", i);
                                                await Task.Delay(1000);
                                                i--;
                                            }

                                            loadBar.Visibility = Visibility.Visible;
                                            text.Text          = Locale.Current.Lang?._AppNotification?.NotifMetadataUpdateBtnUpdating;

                                            try
                                            {
                                                await LauncherMetadataHelper.RunMetadataUpdate();
                                                MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
                                            }
                                            catch (Exception ex)
                                            {
                                                ErrorSender.SendException(ex);
                                                Logger.LogWriteLine($"Error has occured while updating metadata!\r\n{ex}", LogType.Error, true);
                                            }
                                        };
            SpawnNotificationPush(Locale.Current.Lang?._AppNotification?.NotifMetadataUpdateTitle ?? "",
                                  Locale.Current.Lang?._AppNotification?.NotifMetadataUpdateSubtitle ?? "",
                                  NotifSeverity.Informational,
                                  -886135731,
                                  true,
                                  false,
                                  null,
                                  updateMetadataBtn,
                                  true,
                                  true,
                                  true
                                 );
            return true;
        }
        #endregion

        #region Icons
        private void ToggleTitleIcon(bool hide)
        {
            if (!hide)
            {
                GridBGIconTitle.Width = double.NaN;
                GridBGIconTitle.Opacity = 1d;
                GridBGIconImg.Opacity = 1d;
                return;
            }

            GridBGIconTitle.Width = 0d;
            GridBGIconTitle.Opacity = 0d;
            GridBGIconImg.Opacity = 0.8d;
        }

        private void GridBG_Icon_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (_isTitleIconForceShow)
            {
                return;
            }

            Thickness curMargin = GridBGIcon.Margin;
            curMargin.Left     = 50;
            GridBGIcon.Margin = curMargin;
            ToggleTitleIcon(false);
        }

        private void GridBG_Icon_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_isTitleIconForceShow)
            {
                return;
            }

            Thickness curMargin = GridBGIcon.Margin;
            curMargin.Left     = 58;
            GridBGIcon.Margin = curMargin;
            ToggleTitleIcon(true);
        }

        private void GridBG_Icon_Click(object sender, RoutedEventArgs e) => TryNavigateFrom(typeof(HomePage));
        #endregion

        #region AppActivation
        #nullable enable
        private static bool SetActivatedRegion()
        {
            ArgumentStartGame? args = InnerLauncherConfig.m_arguments.StartGame;
            if (args == null) return true;

            string? oldGameCategory = GetAppConfigValue("GameCategory");
            string? gameName        = args.Game;

            List<string>       gameTitleList        = LauncherMetadataHelper.GetGameTitleList();
            List<PresetConfig> gameRegionCollection = LauncherMetadataHelper.GetGameRegionList(gameName);
            if (gameRegionCollection.Count == 0)
            {
                bool res = int.TryParse(args.Game, out int gameIndex);
                if (!res || gameIndex < 0 || gameIndex >= gameTitleList.Count) return true;

                gameName             = gameTitleList[gameIndex];
                gameRegionCollection = LauncherMetadataHelper.GetGameRegionList(gameName);
            }

            SetAndSaveConfigValue("GameCategory", gameName);

            if (args is not { Region: not null })
            {
                return false;
            }

            PresetConfig? gameRegion = gameRegionCollection.FirstOrDefault(x => x.ZoneName?.Equals(args.Region, StringComparison.OrdinalIgnoreCase) ?? false);
            if (gameRegion == null)
            {
                bool res = int.TryParse(args.Region, out int regionIndex);
                if (!res || regionIndex < 0 || regionIndex >= gameRegionCollection.Count) return true;

                gameRegion = gameRegionCollection[regionIndex];
            }

            int          oldGameRegionIndex = LauncherMetadataHelper.GetGameRegionLastSavedIndexOrDefault(gameName);
            PresetConfig oldGameRegion      = gameRegionCollection.ElementAt(oldGameRegionIndex);

            SetAndSaveConfigValue("GameRegion", gameRegion.ZoneName);
            LauncherMetadataHelper.SaveGameRegionIndex(gameName, gameRegion.ZoneName);

            return oldGameCategory == gameName && oldGameRegion == gameRegion;
        }

        private async void ChangeToActivatedRegion()
        {
            try
            {
                if (!IsLoadRegionComplete || KeyboardShortcuts.CannotUseKbShortcuts) return;

                bool sameRegion = SetActivatedRegion();

                _disableInstantRegionChange = true;
                _lockRegionChangeBtn        = true;
                Interlocked.Exchange(ref IsLoadRegionComplete, false);

                (PresetConfig preset, string gameName, string gameRegion) = await LoadSavedGameSelection();

                _ = ShowAsyncLoadingTimedOutPill();
                if (await LoadRegionFromCurrentConfigV2(preset, gameName, gameRegion))
                {
                #if !DISABLEDISCORD
                    if (InnerLauncherConfig.AppDiscordPresence.IsRpcEnabled && !sameRegion)
                        InnerLauncherConfig.AppDiscordPresence.SetupPresence();
                #endif
                    InvokeLoadingRegionPopup(false);
                    LauncherFrame.BackStack.Clear();
                    MainFrameChanger.ChangeMainFrame(InnerLauncherConfig.m_appMode == InnerLauncherConfig.AppMode.Hi3CacheUpdater ? typeof(CachesPage) : typeof(HomePage));
                    Logger.LogWriteLine($"Region changed to {preset.ZoneFullname}", LogType.Scheme, true);
                }

                _lockRegionChangeBtn        = false;
                _disableInstantRegionChange = false;
            }
            catch (Exception e)
            {
                SentryHelper.ExceptionHandler(e);
                Logger.LogWriteLine($"{e}", LogType.Error, true);
            }
        }

        public void OpenAppActivation()
        {
            if (InnerLauncherConfig.m_arguments is { StartGame: null }) return;

            DispatcherQueue?.TryEnqueue(ChangeToActivatedRegion);
        }
        #endregion
        
        #region Misc Methods
        private static bool IsGameInstalled() => GameInstallationState
            is GameInstallStateEnum.Installed
            or GameInstallStateEnum.InstalledHavePreload
            or GameInstallStateEnum.InstalledHavePlugin
            or GameInstallStateEnum.NeedsUpdate;

        private void SpawnWebView2Panel(Uri url)
        {
            try
            {
                WebView2FramePage.WebView2URL = url;
                WebView2Frame.Navigate(typeof(WebView2FramePage), null, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromBottom });
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex);
                Logger.LogWriteLine($"Error while initialize EdgeWebView2. Opening browser instead!\r\n{ex}", LogType.Error, true);
                new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName        = url.ToString()
                    }
                }.Start();
            }
        }
        #endregion
    }
}
