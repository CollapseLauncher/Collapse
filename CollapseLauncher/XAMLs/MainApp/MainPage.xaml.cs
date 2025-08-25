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
using CollapseLauncher.Pages;
using CollapseLauncher.Plugins;
using CollapseLauncher.XAMLs.Theme.CustomControls.FullPageOverlay;
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
using static CollapseLauncher.Dialogs.KeyboardShortcuts;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using UIElementExtensions = CollapseLauncher.Extension.UIElementExtensions;
// ReSharper disable CheckNamespace
// ReSharper disable RedundantExtendsListEntry
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable IdentifierTypo
// ReSharper disable AsyncVoidMethod
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault

namespace CollapseLauncher
{
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

        internal static List<string> PreviousTagString = [];

#nullable enable
        internal static BackgroundMediaUtility? CurrentBackgroundHandler;
        private         BackgroundMediaUtility? _localBackgroundHandler;
#nullable restore
        #endregion

        #region Main Routine
        public MainPage()
        {
            try
            {
                LogWriteLine($"Welcome to Collapse Launcher v{LauncherUpdateHelper.LauncherCurrentVersionString} - {MainEntryPoint.GetVersionString()}");
                LogWriteLine($"Application Data Location:\r\n\t{AppDataFolder}");
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
            AppDiscordPresence?.Dispose();
#endif
            ImageLoaderHelper.DestroyWaifu2X();
            _localBackgroundHandler?.Dispose();
            CurrentBackgroundHandler = null;
            _localBackgroundHandler = null;

            Interlocked.Exchange(ref m_mainPage, null);
        }

        private async void StartRoutine(object sender, RoutedEventArgs e)
        {
            SubscribeEvents();
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
                        _ = mainWindow.CloseApp();
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
            // Initialize the background image utility
            await InitBackgroundHandler();

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
                Page = m_appMode == AppMode.Hi3CacheUpdater && presetConfig.GameType == GameNameType.Honkai ? typeof(CachesPage) : typeof(NotInstalledPage);

            InitKeyboardShortcuts();

            InvokeLoadingRegionPopup(true, Lang._MainPage.RegionLoadingTitle, RegionToChangeName);
            if (await LoadRegionFromCurrentConfigV2(presetConfig, gameName, gameRegion))
            {
                MainFrameChanger.ChangeMainFrame(Page);
            }

            // Unlock ChangeBtn for first start
            LockRegionChangeBtn = false;
            InvokeLoadingRegionPopup(false);

            // After all activities were complete, run background check, including
            // invoking notifications
            _ = RunBackgroundCheck();
        }

        private async Task InitBackgroundHandler()
        {
            CurrentBackgroundHandler ??= await BackgroundMediaUtility.CreateInstanceAsync(this, BackgroundAcrylicMask, BackgroundOverlayTitleBar, BackgroundNewBackGrid, BackgroundNewMediaPlayerGrid);
            _localBackgroundHandler = CurrentBackgroundHandler;
        }
        #endregion

        #region Invokers
        private void UpdateBindingsEvent(object sender, EventArgs e)
        {
            // Find the last selected category/title and region
            string lastName = LauncherMetadataHelper.CurrentMetadataConfigGameName;
            string lastRegion = LauncherMetadataHelper.CurrentMetadataConfigGameRegion;

#nullable enable
            NavigationViewControl?.ApplyNavigationViewItemLocaleTextBindings();

            List<string>? gameNameCollection = LauncherMetadataHelper.GetGameNameCollection();
            List<string>? gameRegionCollection = LauncherMetadataHelper.GetGameRegionCollection(lastName!);

            int indexOfName = gameNameCollection?.IndexOf(lastName!) ?? -1;
            int indexOfRegion = gameRegionCollection?.IndexOf(lastRegion!) ?? -1;
#nullable restore
                
            // Rebuild Game Titles and Regions ComboBox items
            ComboBoxGameCategory.ItemsSource = BuildGameTitleListUI();
            ComboBoxGameCategory.SelectedIndex = indexOfName;
            ComboBoxGameRegion.SelectedIndex = indexOfRegion;

            ChangeTitleDragArea.Change(DragAreaTemplate.Default);

            UpdateLayout();
            Bindings.Update();
        }

        private static void ShowLoadingPageInvoker_PageEvent(object sender, ShowLoadingPageProperty e)
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
                if (!DispatcherQueue?.HasThreadAccessSafe() ?? false)
                    DispatcherQueue?.TryEnqueue(() => MainFrameChanger.ChangeMainFrame(typeof(UnavailablePage)));
                else
                    MainFrameChanger.ChangeMainFrame(typeof(UnavailablePage));

            }
            // CRC error show
            else if (e.Exception.GetType() == typeof(IOException) && e.Exception.HResult == unchecked((int)0x80070017))
            {
                PreviousTag = "crashinfo";
                ErrorSender.ExceptionType = ErrorType.DiskCrc;
                await SimpleDialogs.Dialog_ShowUnhandledExceptionMenu();
            }
            else
            {
                PreviousTag = "crashinfo";
                await SimpleDialogs.Dialog_ShowUnhandledExceptionMenu();
            }
        }

        private void MainFrameChangerInvoker_FrameEvent(object sender, MainFrameProperties e)
        {
            IsLoadFrameCompleted  = false;
            m_appCurrentFrameName = e.FrameTo.Name;
            LauncherFrame.Navigate(e.FrameTo, null, e.Transition);
            IsLoadFrameCompleted = true;
        }

        private void MainFrameChangerInvoker_FrameGoBackEvent(object sender, EventArgs e)
        {
            if (LauncherFrame.CanGoBack)
                LauncherFrame.GoBack();
        }
        #endregion

        #region Drag Area
        internal RectInt32[] DragAreaMode_Normal
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

        internal static RectInt32[] DragAreaMode_Full
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
            using WindowsIdentity identity  = WindowsIdentity.GetCurrent();
            WindowsPrincipal      principal = new WindowsPrincipal(identity);
            return !principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        #endregion

        #region Theme Methods
        private void SetThemeParameters()
        {
            if (m_windowSupportCustomTitle)
            {
                return;
            }

            GridBG_RegionMargin.Width              = new GridLength(0, GridUnitType.Pixel);
            GridBG_RegionGrid.HorizontalAlignment  = HorizontalAlignment.Left;
            GridBG_RegionInner.HorizontalAlignment = HorizontalAlignment.Left;
        }
        #endregion

        #region Events
        private void SubscribeEvents()
        {
            ErrorSenderInvoker.ExceptionEvent += ErrorSenderInvoker_ExceptionEvent;
            MainFrameChangerInvoker.FrameEvent += MainFrameChangerInvoker_FrameEvent;
            MainFrameChangerInvoker.FrameGoBackEvent += MainFrameChangerInvoker_FrameGoBackEvent;
            NotificationInvoker.EventInvoker += NotificationInvoker_EventInvoker;
            BackgroundImgChangerInvoker.ImgEvent += CustomBackgroundChanger_Event;
            BackgroundImgChangerInvoker.IsImageHide += BackgroundImg_IsImageHideEvent;
            SpawnWebView2Invoker.SpawnEvent += SpawnWebView2Invoker_SpawnEvent;
            ShowLoadingPageInvoker.PageEvent += ShowLoadingPageInvoker_PageEvent;
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
            SettingsPage.KeyboardShortcutsEvent -= SettingsPage_KeyboardShortcutsEvent;
            KeyboardShortcutsEvent -= SettingsPage_KeyboardShortcutsEvent;
            UpdateBindingsInvoker.UpdateEvents -= UpdateBindingsEvent;
            GridBG_RegionGrid.SizeChanged -= GridBG_RegionGrid_SizeChanged;
            MainPageGrid.SizeChanged -= MainPageGrid_SizeChanged;
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
                await LoadLocalNotificationData();

                // Then Spawn the Notification Feed
                await SpawnPushAppNotification();

                // Check Metadata Update in Background
                if (await CheckMetadataUpdateInBackground())
                    return; // Cancel any routine below to avoid conflict with app update

#if !DEBUG
                // Run the update check and trigger routine
                await LauncherUpdateHelper.RunUpdateCheckDetached();
#else 
                LogWriteLine("Running debug build, stopping update checks!", LogType.Error);
#endif
            }
            catch (JsonException ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex);
                LogWriteLine($"Error while trying to get Notification Feed or Metadata Update\r\n{ex}", LogType.Error, true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while trying to run background tasks!\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
                ErrorSender.SendException(ex);
            }
        }
        #endregion

        #region Game Selector Method
        private async Task<(PresetConfig, string, string)> LoadSavedGameSelection()
        {
            ComboBoxGameCategory.ItemsSource = BuildGameTitleListUI();

            string gameName = GetAppConfigValue("GameCategory")!;

            #nullable enable
            List<string>? gameCollection   = LauncherMetadataHelper.GetGameNameCollection();
            List<string>? regionCollection = LauncherMetadataHelper.GetGameRegionCollection(gameName);
            
            if (regionCollection == null)
                gameName = LauncherMetadataHelper.LauncherGameNameRegionCollection?.Keys.FirstOrDefault();

            ComboBoxGameRegion.ItemsSource = BuildGameRegionListUI(gameName);

            var indexCategory= gameCollection?.IndexOf(gameName!) ?? -1;
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
            
            ChangeRegionWarningText.Text = preset!.GameChannel != GameChannel.Stable
                ? string.Format(Lang._MainPage.RegionChangeWarnExper1, preset.GameChannel)
                : string.Empty;
            ChangeRegionWarning.Visibility =
                preset.GameChannel != GameChannel.Stable ? Visibility.Visible : Visibility.Collapsed;
            
            ChangeRegionConfirmBtn.IsEnabled          = !LockRegionChangeBtn;
            ChangeRegionConfirmBtnNoWarning.IsEnabled = !LockRegionChangeBtn;

            if (!IsShowRegionChangeWarning && IsInstantRegionChange && !DisableInstantRegionChange && !IsFirstStartup)
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
                }.AddTextBlockLine(string.Format(Lang._Dialogs.PluginManagerUpdateAvailableSubtitle1, pluginUpdateNameList.Count))
                 .AddTextBlockNewLine(2), 0);

                CheckBox enablePluginAutoUpdateCheck = textGridBox.AddElementToGridRow(new CheckBox(), 1);
                enablePluginAutoUpdateCheck.Content = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap
                }.AddTextBlockLine(Lang._PluginManagerPage.ListViewMainActionButton3);
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
                    .AddTextBlockLine(Lang._Dialogs.PluginManagerUpdateAvailableSubtitle2);

                ContentDialogResult pluginUpdateConfirm =
                    await SimpleDialogs.SpawnDialog(string.Format(Lang._Dialogs.PluginManagerUpdateAvailableTitle, pluginUpdateNameList.Count),
                                                    textGridBox,
                                                    null,
                                                    Lang._Dialogs.PluginManagerUpdateAvailableCancelBtn,
                                                    Lang._Dialogs.PluginManagerUpdateAvailableConfirmBtn,
                                                    string.Format(Lang._Dialogs.PluginManagerUpdateAvailableToManagerMenuBtn, Lang._PluginManagerPage.PageTitle),
                                                    ContentDialogButton.Primary,
                                                    ContentDialogTheme.Success);

                if (pluginUpdateConfirm == ContentDialogResult.None)
                {
                    return false;
                }

                if (pluginUpdateConfirm == ContentDialogResult.Secondary)
                {
                    FullPageOverlay overlayMenu = new FullPageOverlay(new PluginManagerPage(), XamlRoot, true)
                    {
                        Size               = FullPageOverlaySize.Full,
                        OverlayTitleSource = () => Lang._PluginManagerPage.PageTitle,
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
                    LogWriteLine($"Error has occured while updating metadata!\r\n{ex}", LogType.Error, true);
                    ErrorSender.SendException(ex);
                }

                return false;
            }

            Button UpdateMetadatabtn =
                UIElementExtensions.CreateButtonWithIcon<Button>(Lang._AppNotification!.NotifMetadataUpdateBtn,
                                                                 "",
                                                                 "FontAwesomeSolid",
                                                                 "AccentButtonStyle"
                                                                )
                                   .WithMargin(0d, 0d, 0d, 16d);

            UpdateMetadatabtn.Loaded += async (a, _) =>
                                        {
                                            TextBlock Text = new TextBlock
                                            {
                                                Text       = Lang._AppNotification.NotifMetadataUpdateBtnUpdating,
                                                FontWeight = FontWeights.Medium
                                            }.WithVerticalAlignment(VerticalAlignment.Center);
                                            ProgressRing LoadBar = new ProgressRing
                                            {
                                                IsIndeterminate = true,
                                                Visibility      = Visibility.Collapsed
                                            }.WithWidthAndHeight(16d).WithMargin(0d, 0d, 8d, 0d).WithVerticalAlignment(VerticalAlignment.Center);
                                            StackPanel StackPane = UIElementExtensions.CreateStackPanel(Orientation.Horizontal);
                                            StackPane.AddElementToStackPanel(LoadBar);
                                            StackPane.AddElementToStackPanel(Text);
                                            Button aButton = a as Button;
                                            if (aButton != null)
                                            {
                                                aButton.Content   = StackPane;
                                                aButton.IsEnabled = false;
                                            }

                                            // Put 2 seconds delay before updating
                                            int i = 2;
                                            while (i != 0)
                                            {
                                                Text.Text = string.Format(Lang._AppNotification.NotifMetadataUpdateBtnCountdown, i);
                                                await Task.Delay(1000);
                                                i--;
                                            }

                                            LoadBar.Visibility = Visibility.Visible;
                                            Text.Text          = Lang._AppNotification.NotifMetadataUpdateBtnUpdating;

                                            try
                                            {
                                                await LauncherMetadataHelper.RunMetadataUpdate();
                                                MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
                                            }
                                            catch (Exception ex)
                                            {
                                                LogWriteLine($"Error has occured while updating metadata!\r\n{ex}", LogType.Error, true);
                                                ErrorSender.SendException(ex);
                                            }
                                        };
            SpawnNotificationPush(Lang._AppNotification.NotifMetadataUpdateTitle,
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
            return true;
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
            if (IsTitleIconForceShow)
            {
                return;
            }

            Thickness curMargin = GridBG_Icon.Margin;
            curMargin.Left     = 50;
            GridBG_Icon.Margin = curMargin;
            ToggleTitleIcon(false);
        }

        private void GridBG_Icon_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (IsTitleIconForceShow)
            {
                return;
            }

            Thickness curMargin = GridBG_Icon.Margin;
            curMargin.Left     = 58;
            GridBG_Icon.Margin = curMargin;
            ToggleTitleIcon(true);
        }

        private void GridBG_Icon_Click(object sender, RoutedEventArgs e)
        {
            if (PreviousTag.Equals("launcher", StringComparison.OrdinalIgnoreCase)) return;

            MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            PreviousTag = "launcher";
            PreviousTagString.Add(PreviousTag);

            NavigationViewItem navItem = NavigationViewControl.MenuItems
                                                              .OfType<NavigationViewItem>()
                                                              .FirstOrDefault(x => ((string)x.Tag).Equals(PreviousTag, StringComparison.OrdinalIgnoreCase));

            if (navItem != null)
            {
                NavigationViewControl.SelectedItem = navItem;
            }
        }
        #endregion

        #region AppActivation
        #nullable enable
        private static bool SetActivatedRegion()
        {
            var args = m_arguments.StartGame;
            if (args == null) return true;

            string? oldGameCategory = GetAppConfigValue("GameCategory");

            string gameName = args.Game;

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

            if (args is not { Region: not null })
            {
                return false;
            }

            string gameRegion = args.Region;
            if (!gameRegionCollection.Contains(gameRegion))
            {
                bool res = int.TryParse(args.Region, out int regionIndex);
                if (!res || regionIndex < 0 || regionIndex >= gameRegionCollection.Count) return true;

                gameRegion = gameRegionCollection[regionIndex];
            }

            int oldGameRegionIndex = LauncherMetadataHelper.GetPreviousGameRegion(gameName);
            string oldGameRegion = gameRegionCollection.ElementAt(oldGameRegionIndex);

            LauncherMetadataHelper.SetPreviousGameRegion(gameName, gameRegion);
            SetAndSaveConfigValue("GameRegion", gameRegion);

            return oldGameCategory == gameName && oldGameRegion == gameRegion;
        }

        private async void ChangeToActivatedRegion()
        {
            if (!IsLoadRegionComplete || CannotUseKbShortcuts) return;

            bool sameRegion = SetActivatedRegion();

            DisableInstantRegionChange = true;
            LockRegionChangeBtn        = true;
            IsLoadRegionComplete       = false;

            (PresetConfig preset, string gameName, string gameRegion) = await LoadSavedGameSelection();

            _ = ShowAsyncLoadingTimedOutPill();
            if (await LoadRegionFromCurrentConfigV2(preset, gameName, gameRegion))
            {
            #if !DISABLEDISCORD
                if (GetAppConfigValue("EnableDiscordRPC").ToBool() && !sameRegion)
                    AppDiscordPresence?.SetupPresence();
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
            if (m_arguments is { StartGame: null }) return;

            DispatcherQueue?.TryEnqueue(ChangeToActivatedRegion);
        }
        #endregion
        
        #region Misc Methods
        private static bool IsGameInstalled() => GameInstallationState
            is GameInstallStateEnum.Installed
            or GameInstallStateEnum.InstalledHavePreload
            or GameInstallStateEnum.InstalledHavePlugin
            or GameInstallStateEnum.NeedsUpdate;

        private void SpawnWebView2Panel(Uri URL)
        {
            try
            {
                WebView2FramePage.WebView2URL = URL;
                WebView2Frame.Navigate(typeof(WebView2FramePage), null, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromBottom });
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex);
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
        #endregion
    }
}
