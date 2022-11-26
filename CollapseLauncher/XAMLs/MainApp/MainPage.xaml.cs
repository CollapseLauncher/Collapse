using Hi3Helper;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Preset.ConfigV2Store;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public partial class MainPage : Page
    {
        private WebView2 WebView2Runtime;
        private bool LockRegionChangeBtn;

        private RectInt32[] DragAreaMode_Normal
        {
            get
            {
                List<RectInt32> area = new List<RectInt32>
                {
                    new RectInt32((int)(TitleBarDrag1.ActualOffset.X * m_appDPIScale),
                                  0,
                                  (int)(TitleBarDrag1.ActualWidth * m_appDPIScale),
                                  (int)(48 * m_appDPIScale)),
                    new RectInt32((int)(TitleBarDrag2.ActualOffset.X * m_appDPIScale),
                                  0,
                                  (int)(TitleBarDrag2.ActualWidth * m_appDPIScale),
                                  (int)(48 * m_appDPIScale))
                };
                return area.ToArray();
            }
        }

        private RectInt32[] DragAreaMode_Full
        {
            get
            {
                List<RectInt32> area = new List<RectInt32>
                {
                    new RectInt32(0,
                                  0,
                                  (int)(m_windowPosSize.Width * m_appDPIScale),
                                  (int)(40 * m_appDPIScale))
                };
                return area.ToArray();
            }
        }

        public MainPage()
        {
            try
            {
                LogWriteLine($"Welcome to Collapse Launcher v{AppCurrentVersion} - {MainEntryPoint.GetVersionString()}", LogType.Default, false);
                LogWriteLine($"Application Data Location:\r\n\t{AppDataFolder}", LogType.Default);
                InitializeComponent();
                Loaded += StartRoutine;
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void StartRoutine(object sender, RoutedEventArgs e)
        {
            try
            {
                MainWindow.SetDragArea(DragAreaMode_Normal);
                LoadGamePreset();
                SetThemeParameters();

                m_actualMainFrameSize = new Size((m_window as MainWindow).Bounds.Width, (m_window as MainWindow).Bounds.Height);

                ErrorSenderInvoker.ExceptionEvent += ErrorSenderInvoker_ExceptionEvent;
                MainFrameChangerInvoker.FrameEvent += MainFrameChangerInvoker_FrameEvent;
                NotificationInvoker.EventInvoker += NotificationInvoker_EventInvoker;
                BackgroundImgChangerInvoker.ImgEvent += CustomBackgroundChanger_Event;
                SpawnWebView2Invoker.SpawnEvent += SpawnWebView2Invoker_SpawnEvent;
                ShowLoadingPageInvoker.PageEvent += ShowLoadingPageInvoker_PageEvent;
                ChangeTitleDragAreaInvoker.TitleBarEvent += ChangeTitleDragAreaInvoker_TitleBarEvent;
                ChangeThemeInvoker.ThemeEvent += ChangeThemeInvoker_ThemeEvent;

                LauncherUpdateWatcher.StartCheckUpdate();

                InitializeStartup();
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private async void ChangeThemeInvoker_ThemeEvent(object sender, ChangeThemeProperty e)
        {
            CurrentAppTheme = e.AppTheme;
            await ApplyAccentColor(this, PaletteBitmap);
        }

        private void ChangeTitleDragAreaInvoker_TitleBarEvent(object sender, ChangeTitleDragAreaProperty e)
        {
            switch (e.Template)
            {
                case DragAreaTemplate.Full:
                    MainWindow.SetDragArea(DragAreaMode_Full);
                    break;
                case DragAreaTemplate.Default:
                    MainWindow.SetDragArea(DragAreaMode_Normal);
                    break;
            }
        }

        private void ShowLoadingPageInvoker_PageEvent(object sender, ShowLoadingPageProperty e)
        {
            HideBackgroundImage(!e.Hide);
            HideLoadingPopup(e.Hide, e.Title, e.Subtitle);
        }

        private void SpawnWebView2Invoker_SpawnEvent(object sender, SpawnWebView2Property e) => SpawnWebView2Panel(e.URL);

        private async void CustomBackgroundChanger_Event(object sender, BackgroundImgProperty e)
        {
            e.IsImageLoaded = false;
            regionBackgroundProp.imgLocalPath = e.ImgPath;
            if (e.IsCustom)
                SetAndSaveConfigValue("CustomBGPath", regionBackgroundProp.imgLocalPath);

            if (!File.Exists(regionBackgroundProp.imgLocalPath))
            {
                LogWriteLine($"Custom background file {e.ImgPath} is missing!", LogType.Warning, true);
                regionBackgroundProp.imgLocalPath = AppDefaultBG;
            }

            try
            {
                await RunApplyBackgroundTask();
            }
            catch (Exception ex)
            {
                regionBackgroundProp.imgLocalPath = AppDefaultBG;
                await RunApplyBackgroundTask();
                LogWriteLine($"An error occured while loading background {e.ImgPath}\r\n{ex}", LogType.Error, true);
            }

            e.IsImageLoaded = true;
        }

        private async Task RunApplyBackgroundTask()
        {
            if (IsFirstStartup)
                await ApplyBackground();
            else
                ApplyBackgroundAsync();
        }

        private async void CheckRunningGameInstance()
        {
            while (true && !App.IsAppKilled)
            {
                string execName = Path.GetFileNameWithoutExtension(CurrentConfigV2.GameExecutableName);
                App.IsGameRunning = Process.GetProcessesByName(execName).Length != 0 && !App.IsAppKilled;
                await Task.Delay(250);
            }
        }

        private void NotificationInvoker_EventInvoker(object sender, NotificationInvokerProp e)
        {
            SpawnNotificationPush(e.Notification.Title, e.Notification.Message, e.Notification.Severity,
                e.Notification.MsgId, e.Notification.IsClosable ?? true, e.Notification.IsDisposable ?? true, e.CloseAction,
                e.OtherContent, e.IsAppNotif, e.Notification.Show);
        }

        private async void RunBackgroundUpdateCheck()
        {
            try
            {
                await Task.Run(() =>
                {
                    GetNotificationFeed();
                    GetAppUpdateNotification();
                });
                PushAppNotification();
                CheckMetadataUpdateInBackground();
            }
            catch (JsonException ex)
            {
                LogWriteLine($"Error while trying to get Notification Feed or Metadata Update\r\n{ex}", LogType.Error, true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while trying to get Notification Feed or Metadata Update\r\n{ex}", LogType.Error, true);
            }
        }

        bool IsLoadNotifComplete = false;
        private void GetNotificationFeed()
        {
            try
            {
                Http _http = new Http(true, 5, 1000, null);
                IsLoadNotifComplete = false;
                NotificationData = new NotificationPush();
                CancellationTokenSource TokenSource = new CancellationTokenSource();
                RunTimeoutCancel(TokenSource);
                using (_http)
                using (MemoryStream buffer = new MemoryStream())
                {
                    _http.DownloadSync(string.Format(AppNotifURLPrefix, (IsPreview ? "preview" : "stable")),
                        buffer, null, null, TokenSource.Token);
                    buffer.Position = 0;
                    NotificationData = (NotificationPush)JsonSerializer.Deserialize(buffer, typeof(NotificationPush), NotificationPushContext.Default);
                    IsLoadNotifComplete = true;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to load notification push!\r\n{ex}", LogType.Warning, true);
            }
            LoadLocalNotificationData();
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

        private async void PushAppNotification()
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
                if (Entry.ValidForVerBelow == null
                    || (LauncherUpdateWatcher.CompareVersion(AppCurrentVersion, Entry.ValidForVerBelow)
                        && LauncherUpdateWatcher.CompareVersion(Entry.ValidForVerAbove, AppCurrentVersion))
                    || LauncherUpdateWatcher.CompareVersion(AppCurrentVersion, Entry.ValidForVerBelow))
                    SpawnNotificationPush(Entry.Title, Entry.Message, Entry.Severity, Entry.MsgId, Entry.IsClosable ?? true,
                        Entry.IsDisposable ?? true, ClickCloseAction, null, true, Entry.Show);
                await Task.Delay(250);
            }
        }

        private void GetAppUpdateNotification()
        {
            try
            {
                string UpdateNotifFile = Path.Combine(AppDataFolder, "_NewVer");
                TypedEventHandler<InfoBar, object> ClickClose = new TypedEventHandler<InfoBar, object>((sender, args) =>
                {
                    File.Delete(UpdateNotifFile);
                });

                if (File.Exists(UpdateNotifFile))
                {
                    string updateElevatorTemp = Path.Combine(AppFolder, "_Temp", "ApplyUpdate.exe");
                    string updateElevator = Path.Combine(AppFolder, "ApplyUpdate.exe");

                    string VerString = File.ReadAllLines(UpdateNotifFile)[0];
                    SpawnNotificationPush(
                        Lang._Misc.UpdateCompleteTitle,
                        string.Format(Lang._Misc.UpdateCompleteSubtitle, VerString, IsPreview ? "Preview" : "Stable"),
                        NotifSeverity.Success,
                        0xAF,
                        true,
                        false,
                        ClickClose,
                        null,
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
            bool Disposable = false, TypedEventHandler<InfoBar, object> CloseClickHandler = null, UIElement OtherContent = null, bool IsAppNotif = true,
            bool? Show = false)
        {
            if (!(Show ?? false)) return;

            DispatcherQueue.TryEnqueue(() =>
            {
                Grid Container = new Grid();

                StackPanel OtherContentContainer = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(0, 0, 0, 8)
                };

                InfoBar Notification = new InfoBar
                {
                    Title = Title,
                    Message = Content,
                    Margin = new Thickness(4, 4, 4, 0),
                    CornerRadius = new CornerRadius(8),
                    Severity = NotifSeverity2InfoBarSeverity(Severity),
                    IsClosable = IsClosable,
                    IsIconVisible = true,
                    Width = m_windowSupportCustomTitle ? 720 : double.NaN,
                    HorizontalAlignment = m_windowSupportCustomTitle ? HorizontalAlignment.Right : HorizontalAlignment.Stretch,
                    Shadow = SharedShadow,
                    IsOpen = true
                };

                Notification.Translation += Shadow32;

                if (Severity == NotifSeverity.Informational)
                    Notification.Background = (Brush)Application.Current.Resources["InfoBarAnnouncementBrush"];

                Notification.Closed += (a, b) =>
                {
                    a.Translation -= Shadow32;
                    a.Height = 0;
                    a.Margin = new Thickness(0);
                };

                if (OtherContent != null)
                    OtherContentContainer.Children.Add(OtherContent);

                if (Disposable)
                {
                    CheckBox NeverAskNotif = new CheckBox
                    {
                        Content = new TextBlock { Text = Lang._MainPage.NotifNeverAsk, FontWeight = FontWeights.Medium },
                        Tag = $"{MsgId},{IsAppNotif}"
                    };
                    NeverAskNotif.Checked += NeverAskNotif_Checked;
                    NeverAskNotif.Unchecked += NeverAskNotif_Unchecked;
                    OtherContentContainer.Children.Add(NeverAskNotif);
                }

                if (Disposable || OtherContent != null)
                    Notification.Content = OtherContentContainer;

                Notification.CloseButtonClick += CloseClickHandler;
                Container.Children.Add(Notification);
                NotificationBar.Children.Add(Container);
            });
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

        public static void ReloadPageTheme(Page page, ElementTheme startTheme)
        {
            bool IsComplete = false;
            while (!IsComplete)
            {
                try
                {
                    if (page.RequestedTheme == ElementTheme.Dark)
                        page.RequestedTheme = ElementTheme.Light;
                    else if (page.RequestedTheme == ElementTheme.Light)
                        page.RequestedTheme = ElementTheme.Default;
                    else if (page.RequestedTheme == ElementTheme.Default)
                        page.RequestedTheme = ElementTheme.Dark;

                    if (page.RequestedTheme != startTheme)
                        ReloadPageTheme(page, startTheme);
                    IsComplete = true;
                }
                catch (Exception)
                {

                }
            }
        }

        public static ElementTheme ConvertAppThemeToElementTheme(AppThemeMode Theme)
        {
            switch (Theme)
            {
                default:
                    return ElementTheme.Default;
                case AppThemeMode.Dark:
                    return ElementTheme.Dark;
                case AppThemeMode.Light:
                    return ElementTheme.Light;
            }
        }

        private async void InitializeStartup()
        {
            RunBackgroundUpdateCheck();

            bool IsLoadSuccess;

            Type Page;

            if (!IsConfigV2StampExist() || !IsConfigV2ContentExist())
            {
                LogWriteLine($"Loading config metadata for the first time...", LogType.Default, true);
                HideLoadingPopup(false, Lang._MainPage.RegionLoadingAPITitle1, Lang._MainPage.RegionLoadingAPITitle2);
                await DownloadConfigV2Files(true, true);
            }

            if (m_appMode == AppMode.Hi3CacheUpdater)
            {
                LoadConfigV2CacheOnly();
                Page = typeof(Pages.CachesPage);
            }
            else
            {
                LoadConfigV2();
                Page = typeof(Pages.HomePage);
            }

            // Lock ChangeBtn for first start
            LockRegionChangeBtn = true;

            LoadSavedGameSelection();

            HideLoadingPopup(false, Lang._MainPage.RegionLoadingTitle, CurrentConfigV2.ZoneFullname);
            IsLoadSuccess = await LoadRegionFromCurrentConfigV2();

            // Unlock ChangeBtn for first start
            LockRegionChangeBtn = false;
            if (IsLoadSuccess) MainFrameChanger.ChangeMainFrame(Page);
            HideLoadingPopup(true, Lang._MainPage.RegionLoadingTitle, CurrentConfigV2.ZoneFullname);

            CheckRunningGameInstance();
        }

        private void LoadSavedGameSelection()
        {
            ComboBoxGameCategory.ItemsSource = ConfigV2GameCategory;

            string GameCategory = GetAppConfigValue("GameCategory").ToString();
            string GameRegion = GetAppConfigValue("GameRegion").ToString();

            if (!GetConfigV2Regions(GameCategory))
                GameCategory = ConfigV2GameCategory.FirstOrDefault();

            ComboBoxGameRegion.ItemsSource = BuildGameRegionListUI(GameCategory);

            int IndexCategory = ConfigV2GameCategory.IndexOf(GameCategory);
            if (IndexCategory < 0) IndexCategory = 0;

            int IndexRegion = ConfigV2GameRegions.IndexOf(GameRegion);
            if (IndexRegion < 0) IndexRegion = 0;

            ComboBoxGameCategory.SelectedIndex = IndexCategory;
            ComboBoxGameRegion.SelectedIndex = IndexRegion;
            LoadCurrentConfigV2((string)ComboBoxGameCategory.SelectedValue, GetComboBoxGameRegionValue(ComboBoxGameRegion.SelectedValue));
        }

        private void SetGameCategoryChange(object sender, SelectionChangedEventArgs e)
        {
            string PreviousRegionString = GetComboBoxGameRegionValue(((List<StackPanel>)ComboBoxGameRegion.ItemsSource)[ComboBoxGameRegion.SelectedIndex == -1 ? 0 : ComboBoxGameRegion.SelectedIndex]);
            string SelectedCategoryString = (string)((ComboBox)sender).SelectedItem;
            GetConfigV2Regions(SelectedCategoryString);

            List<StackPanel> CurRegionList = BuildGameRegionListUI(SelectedCategoryString);
            ComboBoxGameRegion.ItemsSource = CurRegionList;
            ComboBoxGameRegion.SelectedIndex = GetIndexOfRegionStringOrDefault(PreviousRegionString, CurRegionList);
        }

        private int GetIndexOfRegionStringOrDefault(string name, List<StackPanel> CurRegionList)
        {
            int? index = CurRegionList.FindIndex(x => ((TextBlock)x.Children.FirstOrDefault()).Text == name);
            return index == -1 || index == null ? 0 : index ?? 0;
        }

        private async void CheckMetadataUpdateInBackground()
        {
            bool IsUpdate = await CheckForNewConfigV2();
            if (IsUpdate)
            {
                StackPanel Text = new StackPanel { Margin = new Thickness(8, 0, 8, 0), Orientation = Orientation.Horizontal };
                Text.Children.Add(
                    new FontIcon
                    {
                        Glyph = "",
                        FontFamily = (FontFamily)Application.Current.Resources["FontAwesomeSolid"],
                        FontSize = 16
                    });

                Text.Children.Add(
                    new TextBlock
                    {
                        Text = Lang._MainPage.MetadataUpdateBtn,
                        FontWeight = FontWeights.Medium,
                        Margin = new Thickness(8, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    });

                Button UpdateMetadatabtn = new Button
                {
                    Content = Text,
                    Margin = new Thickness(0, 0, 0, 16),
                    Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                    CornerRadius = new CornerRadius(16)
                };

                UpdateMetadatabtn.Click += (async (a, b) =>
                {
                    TextBlock Text = new TextBlock
                    {
                        Text = Lang._MainPage.MetadataUpdateBtnUpdating,
                        FontWeight = FontWeights.Medium,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    ProgressRing LoadBar = new ProgressRing
                    {
                        IsIndeterminate = true,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0),
                        Width = 16,
                        Height = 16
                    };
                    StackPanel StackPane = new StackPanel() { Orientation = Orientation.Horizontal };
                    StackPane.Children.Add(LoadBar);
                    StackPane.Children.Add(Text);
                    (a as Button).Content = StackPane;
                    (a as Button).IsEnabled = false;

                    try
                    {
                        await DownloadConfigV2Files(true, true);
                        MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
                    }
                    catch (Exception ex)
                    {
                        LogWriteLine($"Error has occured while updating metadata!\r\n{ex}", LogType.Error, true);
                        ErrorSender.SendException(ex, ErrorType.Unhandled);
                    }
                });

                SpawnNotificationPush(
                    Lang._MainPage.MetadataUpdateTitle,
                    Lang._MainPage.MetadataUpdateSubtitle,
                    NotifSeverity.Informational,
                    -886135731,
                    true,
                    false,
                    null,
                    UpdateMetadatabtn,
                    true,
                    true
                    );
            }
        }

        private void InitializeNavigationItems()
        {
            NavigationViewControl.IsSettingsVisible = true;
            NavigationViewControl.MenuItems.Clear();

            FontFamily Fnt = Application.Current.Resources["FontAwesomeSolid"] as FontFamily;

            FontIcon IconLauncher = new FontIcon { FontFamily = Fnt, Glyph = "" };
            FontIcon IconRepair = new FontIcon { FontFamily = Fnt, Glyph = "" };
            FontIcon IconCaches = new FontIcon { FontFamily = Fnt, Glyph = "" };
            FontIcon IconGameSettings = new FontIcon { FontFamily = Fnt, Glyph = "" };
            FontIcon IconAppSettings = new FontIcon { FontFamily = Fnt, Glyph = "" };

            NavigationViewControl.MenuItems.Add(new NavigationViewItem()
            { Content = Lang._HomePage.PageTitle, Icon = IconLauncher, Tag = "launcher" });

            if (!(CurrentConfigV2.IsGenshin ?? false))
            {
                NavigationViewControl.MenuItems.Add(new NavigationViewItemSeparator());

                NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                { Content = Lang._GameRepairPage.PageTitle, Icon = IconRepair, Tag = "repair" });
                NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                { Content = Lang._CachesPage.PageTitle, Icon = IconCaches, Tag = "caches" });
                // NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                // { Content = Lang._CutscenesPage.PageTitle, Icon = new SymbolIcon(Symbol.Video), Tag = "cutscenes" });
                NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                { Content = Lang._GameSettingsPage.PageTitle, Icon = IconGameSettings, Tag = "gamesettings" });
            }

            NavigationViewControl.SelectedItem = (NavigationViewItem)NavigationViewControl.MenuItems[0];
            (NavigationViewControl.SettingsItem as NavigationViewItem).Content = Lang._SettingsPage.PageTitle;
            (NavigationViewControl.SettingsItem as NavigationViewItem).Icon = IconAppSettings;
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
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                MainFrameChanger.ChangeMainFrame(typeof(Pages.SettingsPage));
                PreviousTag = "settings";
                LogWriteLine($"Page changed to App Settings", LogType.Scheme);
            }
            else
            {
                var item = sender.MenuItems.OfType<NavigationViewItem>().First(x => (string)x.Content == (string)args.InvokedItem);
                NavView_Navigate(item);
            }
        }

        void Navigate(Type sourceType, bool hideImage, NavigationViewItem tag)
        {
            string tagStr = (string)tag.Tag;
            if (((CurrentConfigV2.IsGenshin ?? false) && (string)tag.Tag != "launcher"))
            {
                sourceType = typeof(Pages.UnavailablePage);
                tagStr = "unavailable";
            }
            MainFrameChanger.ChangeMainFrame(sourceType, new DrillInNavigationTransitionInfo());
            PreviousTag = tagStr;
        }

        private void NavView_Navigate(NavigationViewItem item)
        {
            try
            {
                if (!(PreviousTag == (string)item.Tag))
                {
                    switch (item.Tag)
                    {
                        case "launcher":
                            Navigate(typeof(Pages.HomePage), false, item);
                            break;

                        case "repair":
                            if (!(CurrentConfigV2.IsRepairEnabled ?? false))
                                Navigate(typeof(Pages.UnavailablePage), true, item);
                            else
                                Navigate(IsGameInstalled() ? typeof(Pages.RepairPage) : typeof(Pages.NotInstalledPage), true, item);
                            break;

                        case "caches":
                            if (CurrentConfigV2.IsCacheUpdateEnabled ?? false)
                                Navigate(IsGameInstalled() ? typeof(Pages.CachesPage) : typeof(Pages.NotInstalledPage), true, item);
                            else
                                Navigate(typeof(Pages.UnavailablePage), true, item);
                            break;

                        case "cutscenes":
                            throw new NotImplementedException("Cutscenes Downloading Page isn't yet implemented for now.");

                        case "gamesettings":
                            Navigate(IsGameInstalled() ? typeof(Pages.GameSettingsPage) : typeof(Pages.NotInstalledPage), true, item);
                            break;
                    }
                    LogWriteLine($"Page changed to {item.Content}", LogType.Scheme);
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private bool IsGameInstalled() => GameInstallationState == GameInstallStateEnum.Installed ||
                                          GameInstallationState == GameInstallStateEnum.InstalledHavePreload ||
                                          GameInstallationState == GameInstallStateEnum.NeedsUpdate;

        private void EnableRegionChangeButton(object sender, SelectionChangedEventArgs e) => ChangeRegionConfirmBtn.IsEnabled = !LockRegionChangeBtn;

        private void ErrorSenderInvoker_ExceptionEvent(object sender, ErrorProperties e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (e.Exception.GetType() == typeof(NotImplementedException))
                {
                    PreviousTag = "unavailable";
                    HideBackgroundImage();
                    MainFrameChanger.ChangeMainFrame(typeof(Pages.UnavailablePage));
                }
                else
                {
                    PreviousTag = "crashinfo";
                    HideBackgroundImage();
                    MainFrameChanger.ChangeMainFrame(typeof(Pages.UnhandledExceptionPage));
                }
            });
        }

        private void MainFrameChangerInvoker_FrameEvent(object sender, MainFrameProperties e)
        {
            switch (e.FrameTo.Name)
            {
                case "HomePage":
                    HideBackgroundImage(false);
                    LauncherFrame.Navigate(e.FrameTo, null, e.Transition);
                    break;
                case "RepairPage":
                    PreviousTag = "repair";
                    NavigationViewControl.SelectedItem = (NavigationViewItem)NavigationViewControl.MenuItems[2];
                    HideBackgroundImage();
                    LauncherFrame.Navigate(e.FrameTo, null, e.Transition);
                    break;
                default:
                case "UnhandledExceptionPage":
                case "BlankPage":
                    HideBackgroundImage();
                    LauncherFrame.Navigate(e.FrameTo, null, e.Transition);
                    break;
            }
            m_appCurrentFrameName = e.FrameTo.Name;
        }

        private async void SpawnWebView2Panel(Uri URL)
        {
            try
            {
                Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", Path.Combine(AppGameFolder, "_webView2"));

                WebView2Runtime = new WebView2()
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                WebView2Runtime.CoreWebView2Initialized += WebView2Window_CoreWebView2Initialized;
                WebView2Runtime.NavigationStarting += WebView2Window_PageLoading;
                WebView2Runtime.NavigationCompleted += WebView2Window_PageLoaded;

                WebView2WindowContainer.Children.Clear();
                WebView2WindowContainer.Children.Add(WebView2Runtime);

                WebView2Panel.Visibility = Visibility.Visible;
                WebView2Panel.Translation += Shadow32;
                await WebView2Runtime.EnsureCoreWebView2Async();
                WebView2Runtime.Source = URL;

                SetWebView2Bindings();
                ChangeTitleDragArea.Change(DragAreaTemplate.Full);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while initialize WebView2. Open it to browser instead!\r\n{ex}", LogType.Error, true);
                new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = URL.ToString()
                    }
                }.Start();

                WebView2Runtime?.Close();
            }
        }

        private void SetWebView2Bindings()
        {
            BindingOperations.SetBinding(WebView2BackBtn, IsEnabledProperty, new Binding()
            {
                Source = WebView2Runtime,
                Path = new PropertyPath("CanGoBack"),
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            BindingOperations.SetBinding(WebView2ForwardBtn, IsEnabledProperty, new Binding()
            {
                Source = WebView2Runtime,
                Path = new PropertyPath("CanGoForward"),
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            BindingOperations.SetBinding(WebView2URLBox, TextBox.TextProperty, new Binding()
            {
                Source = WebView2Runtime,
                Path = new PropertyPath("Source"),
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
        }

        private void CoreWebView2_DocumentTitleChanged(CoreWebView2 sender, object args) => WebViewWindowTitle.Text = sender.DocumentTitle;
        private void WebView2Window_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args) => sender.CoreWebView2.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;
        private void WebView2Window_PageLoaded(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args) => WebView2LoadingStatus.IsIndeterminate = false;
        private void WebView2Window_PageLoading(WebView2 sender, CoreWebView2NavigationStartingEventArgs args) => WebView2LoadingStatus.IsIndeterminate = true;
        private void WebView2BackBtn_Click(object sender, RoutedEventArgs e) => WebView2Runtime.GoBack();
        private void WebView2ForwardBtn_Click(object sender, RoutedEventArgs e) => WebView2Runtime.GoForward();
        private void WebView2ReloadBtn_Click(object sender, RoutedEventArgs e) => WebView2Runtime.Reload();
        private void WebView2OpenExternalBtn_Click(object sender, RoutedEventArgs e)
        {
            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = WebView2Runtime.Source.ToString()
                }
            }.Start();
        }

        private void WebView2CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            ChangeTitleDragArea.Change(DragAreaTemplate.Default);
            WebView2Runtime.Visibility = Visibility.Collapsed;
            WebView2Runtime.Close();
            WebView2Panel.Visibility = Visibility.Collapsed;
            WebView2Panel.Translation -= Shadow32;

            WebView2Runtime.CoreWebView2Initialized -= WebView2Window_CoreWebView2Initialized;
            WebView2Runtime.NavigationStarting -= WebView2Window_PageLoading;
            WebView2Runtime.NavigationCompleted -= WebView2Window_PageLoaded;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            MainWindow.SetDragArea(DragAreaMode_Full);
        }
    }
}