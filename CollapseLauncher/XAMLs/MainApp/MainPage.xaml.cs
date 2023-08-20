using CollapseLauncher.Pages;
using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.Statics.GamePropertyVault;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Preset.ConfigV2Store;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public partial class MainPage : Page
    {
        private bool LockRegionChangeBtn;
        private bool IsLoadFrameCompleted = true;
        public static bool IsChangeDragArea = true;
        public static List<string> PreviousTagString = new List<string>();

        private RectInt32[] DragAreaMode_Normal
        {
            get => new RectInt32[2]
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
        }

        private RectInt32[] DragAreaMode_Full
        {
            get => new RectInt32[1]
            {
                new RectInt32(0,
                              0,
                              (int)((m_windowPosSize.Width - 96) * m_appDPIScale),
                              (int)(48 * m_appDPIScale))
            };
        }

        public MainPage()
        {
            try
            {
                LogWriteLine($"Welcome to Collapse Launcher v{AppCurrentVersion.VersionString} - {MainEntryPoint.GetVersionString()}", LogType.Default, false);
                LogWriteLine($"Application Data Location:\r\n\t{AppDataFolder}", LogType.Default);
                InitializeComponent();
                m_mainPage = this;
                LoadingPopupPill.Translation += Shadow32;
                LoadingCancelBtn.Translation += Shadow16;
                WebView2Frame.Navigate(typeof(BlankPage));
                Loaded += StartRoutine;
                if (!IsPreview)
                {
                    PreviewBuildIndicator.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        public static async Task<bool> CheckForAdminAccess(UIElement root)
        {
            if (!IsPrincipalHasNoAdministratorAccess()) return true;

            ContentDialog dialog = new ContentDialog
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
                switch (await dialog.ShowAsync())
                {
                    case ContentDialogResult.Primary:
                        try
                        {
                            Process proc = new Process()
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
                return principal != null && !principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private async void StartRoutine(object sender, RoutedEventArgs e)
        {
            try
            {
                ChangeRegionConfirmBtn.Visibility = !IsShowRegionChangeWarning ? Visibility.Collapsed : Visibility.Visible;
                ChangeRegionConfirmBtnNoWarning.Visibility = !IsShowRegionChangeWarning ? Visibility.Visible : Visibility.Collapsed;

                if (!await CheckForAdminAccess(this))
                {
                    Application.Current.Exit();
                    return;
                }
#if !DEBUG
                LauncherUpdateWatcher.StartCheckUpdate();
#endif
                LoadGamePreset();
                SetThemeParameters();

                VersionNumberIndicator.Text = AppCurrentVersion.VersionString;
#if DEBUG
                VersionNumberIndicator.Text += "d";
#endif
                if (IsPreview)VersionNumberIndicator.Text +=  "-PRE";

                m_actualMainFrameSize = new Size((m_window as MainWindow).Bounds.Width, (m_window as MainWindow).Bounds.Height);

                SubscribeEvents();
                SetDefaultDragAreaAsync();

                await InitializeStartup();
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private async void SetDefaultDragAreaAsync()
        {
            await Task.Delay(250);
            ChangeTitleDragArea.Change(DragAreaTemplate.Default);
        }

        public void SetThemeParameters()
        {
            if (!m_windowSupportCustomTitle)
            {
                GridBG_RegionMargin.Width = new GridLength(0, GridUnitType.Pixel);
                GridBG_RegionGrid.HorizontalAlignment = HorizontalAlignment.Left;
                GridBG_RegionInner.HorizontalAlignment = HorizontalAlignment.Left;
            }

            Background.Visibility = Visibility.Visible;
            BackgroundAcrylicMask.Visibility = Visibility.Visible;
        }

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
        }

        private void BackgroundImg_IsImageHideEvent(object sender, bool e) => HideBackgroundImage(e);

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
            BackgroundImgChanger.ToggleBackground(e.Hide);
            HideLoadingPopup(e.Hide, e.Title, e.Subtitle);
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

        private async void CustomBackgroundChanger_Event(object sender, BackgroundImgProperty e)
        {
            e.IsImageLoaded = false;
            regionBackgroundProp.imgLocalPath = e.ImgPath;
            IsCustomBG = e.IsCustom;

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
                string execName = Path.GetFileNameWithoutExtension(CurrentGameProperty._GameVersion.GamePreset.GameExecutableName);
                App.IsGameRunning = Process.GetProcessesByName(execName).Length != 0 && !App.IsAppKilled;
                await Task.Delay(500);
            }
        }

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
        private async Task FetchNotificationFeed()
        {
            try
            {
                IsLoadNotifComplete = false;
                NotificationData = new NotificationPush();
                CancellationTokenSource TokenSource = new CancellationTokenSource();
                RunTimeoutCancel(TokenSource);

                using (Http _http = new Http(true, 5, 1000, null))
                {
                    using (Stream fs = new MemoryStream())
                    {
                        await FallbackCDNUtil.DownloadCDNFallbackContent(_http, fs, string.Format(AppNotifURLPrefix, IsPreview ? "preview" : "stable"), TokenSource.Token);
                        fs.Position = 0;
                        NotificationData = (NotificationPush)JsonSerializer.Deserialize(fs, typeof(NotificationPush), InternalAppJSONContext.Default);
                        IsLoadNotifComplete = true;

                        NotificationData.EliminatePushList();
                    }
                }
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
                    "https://github.com/neon-nyan/CollapseLauncher/wiki",
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
                        "https://github.com/neon-nyan/CollapseLauncher/issues",
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
                    || (LauncherUpdateWatcher.CompareVersion(AppCurrentVersion, ValidForVerBelow)
                        && LauncherUpdateWatcher.CompareVersion(ValidForVerAbove, AppCurrentVersion))
                    || LauncherUpdateWatcher.CompareVersion(AppCurrentVersion, ValidForVerBelow))
                {
                    if (Entry.ActionProperty != null)
                    {
                        Entry.OtherUIElement = Entry.ActionProperty.GetUIElement();
                    }

                    SpawnNotificationPush(Entry.Title, Entry.Message, Entry.Severity, Entry.MsgId, Entry.IsClosable ?? true,
                        Entry.IsDisposable ?? true, ClickCloseAction, (UIElement)Entry.OtherUIElement, true, Entry.Show, Entry.IsForceShowNotificationPanel);
                }
                await Task.Delay(250);
            }
        }

        private void SpawnAppUpdatedNotification()
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
            bool Disposable = false, TypedEventHandler<InfoBar, object> CloseClickHandler = null, UIElement OtherContent = null, bool IsAppNotif = true,
            bool? Show = false, bool ForceShowNotificationPanel = false)
        {
            if (!(Show ?? false)) return;
            if (NotificationData.CurrentShowMsgIds.Contains(MsgId)) return;

            if (NotificationData.IsMsgIdIgnored(MsgId)) return;

            NotificationData.CurrentShowMsgIds.Add(MsgId);

            DispatcherQueue.TryEnqueue(() =>
            {
                StackPanel OtherContentContainer = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(0, -4, 0, 8)
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
                    Width = m_windowSupportCustomTitle ? 600 : double.NaN,
                    HorizontalAlignment = m_windowSupportCustomTitle ? HorizontalAlignment.Right : HorizontalAlignment.Stretch,
                    Shadow = SharedShadow,
                    IsOpen = true
                };

                Notification.Translation += Shadow32;

                if (Severity == NotifSeverity.Informational)
                    Notification.Background = (Brush)Application.Current.Resources["InfoBarAnnouncementBrush"];

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
            Grid Container = new Grid() { Tag = tagID, };
            Notification.Loaded += (a, b) =>
            {
                NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;
                NewNotificationCountBadge.Visibility = Visibility.Visible;
                NewNotificationCountBadge.Value++;
            };

            Notification.Closed += (s, a) =>
            {
                s.Translation -= Shadow32;
                s.Height = 0;
                s.Margin = new Thickness(0);
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
            };

            Container.Children.Add(Notification);
            NotificationContainer.Children.Add(Container);
        }

        private void RemoveNotificationUI(int tagID)
        {
            Grid notif = NotificationContainer.Children.OfType<Grid>().Where(x => (int)x.Tag == tagID).FirstOrDefault();
            if (notif != null)
            {
                NotificationContainer.Children.Remove(notif);
                InfoBar notifBar = notif.Children.OfType<InfoBar>()?.FirstOrDefault();
                if (notifBar != null)
                {
                    notifBar.IsOpen = false;
                }
            }
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

        private async Task InitializeStartup()
        {
            RunBackgroundCheck();

            // Load community tools properties
            PageStatics._CommunityToolsProperty = CommunityToolsProperty.LoadCommunityTools();

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
                Page = typeof(CachesPage);

                // Set background opacity to 0
                BackgroundFront.Opacity = 0;
            }
            else
            {
                LoadConfigV2();
                Page = typeof(HomePage);
            }

            // Lock ChangeBtn for first start
            LockRegionChangeBtn = true;

            PresetConfigV2 Preset = LoadSavedGameSelection();

            HideLoadingPopup(false, Lang._MainPage.RegionLoadingTitle, Preset.ZoneFullname);
            if (await LoadRegionFromCurrentConfigV2(Preset))
            {
                MainFrameChanger.ChangeMainFrame(Page);
                HideLoadingPopup(true, Lang._MainPage.RegionLoadingTitle, Preset.ZoneFullname);
                CheckRunningGameInstance();
            }

            // Unlock ChangeBtn for first start
            LockRegionChangeBtn = false;
        }

        private PresetConfigV2 LoadSavedGameSelection()
        {
            ComboBoxGameCategory.ItemsSource = ConfigV2GameCategory;

            string GameCategory = GetAppConfigValue("GameCategory").ToString();

            if (!GetConfigV2Regions(GameCategory))
                GameCategory = ConfigV2GameCategory.FirstOrDefault();

            ComboBoxGameRegion.ItemsSource = BuildGameRegionListUI(GameCategory);

            int IndexCategory = ConfigV2GameCategory.IndexOf(GameCategory);
            if (IndexCategory < 0) IndexCategory = 0;

            int IndexRegion = GetPreviousGameRegion(GameCategory);

            ComboBoxGameCategory.SelectedIndex = IndexCategory;
            ComboBoxGameRegion.SelectedIndex = IndexRegion;
            return LoadCurrentConfigV2((string)ComboBoxGameCategory.SelectedValue, GetComboBoxGameRegionValue(ComboBoxGameRegion.SelectedValue));
        }

        private void SetGameCategoryChange(object sender, SelectionChangedEventArgs e)
        {
            string SelectedCategoryString = (string)((ComboBox)sender).SelectedItem;
            GetConfigV2Regions(SelectedCategoryString);

            List<StackPanel> CurRegionList = BuildGameRegionListUI(SelectedCategoryString);
            ComboBoxGameRegion.ItemsSource = CurRegionList;
            ComboBoxGameRegion.SelectedIndex = GetIndexOfRegionStringOrDefault(SelectedCategoryString);
        }

        private int GetIndexOfRegionStringOrDefault(string category)
        {
            int? index = GetPreviousGameRegion(category);

            return index == -1 || index == null ? 0 : index ?? 0;
        }

        private async Task CheckMetadataUpdateInBackground()
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
                        Text = Lang._AppNotification.NotifMetadataUpdateBtn,
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

                UpdateMetadatabtn.Loaded += async (a, b) =>
                {
                    TextBlock Text = new TextBlock
                    {
                        Text = Lang._AppNotification.NotifMetadataUpdateBtnUpdating,
                        FontWeight = FontWeights.Medium,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    ProgressRing LoadBar = new ProgressRing
                    {
                        IsIndeterminate = true,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0),
                        Width = 16,
                        Height = 16,
                        Visibility = Visibility.Collapsed
                    };
                    StackPanel StackPane = new StackPanel() { Orientation = Orientation.Horizontal };
                    StackPane.Children.Add(LoadBar);
                    StackPane.Children.Add(Text);
                    (a as Button).Content = StackPane;
                    (a as Button).IsEnabled = false;

                    // Put 5 seconds delay before updating
                    int i = 5;
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
                        await DownloadConfigV2Files(true, true);
                        IsChangeDragArea = false;
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

        private async void ForceShowNotificationPanel()
        {
            ToggleNotificationPanelBtn.IsChecked = true;
            IsNotificationPanelShow = true;
            ShowHideNotificationPanel();
            await Task.Delay(250);
            double currentVOffset = NotificationContainer.ActualHeight;

            NotificationPanel.ScrollToVerticalOffset(currentVOffset);
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

            if ((GetCurrentGameProperty()._GameVersion.GamePreset.IsCacheUpdateEnabled ?? false) || (GetCurrentGameProperty()._GameVersion.GamePreset.IsRepairEnabled ?? false))
            {
                NavigationViewControl.MenuItems.Add(new NavigationViewItemHeader() { Content = Lang._MainPage.NavigationUtilities });

                if (GetCurrentGameProperty()._GameVersion.GamePreset.IsRepairEnabled ?? false)
                {
                    NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                    { Content = Lang._GameRepairPage.PageTitle, Icon = IconRepair, Tag = "repair" });
                }

                if (GetCurrentGameProperty()._GameVersion.GamePreset.IsCacheUpdateEnabled ?? false)
                {
                    NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                    { Content = Lang._CachesPage.PageTitle, Icon = IconCaches, Tag = "caches" });
                }
            }

            switch (GetCurrentGameProperty()._GameVersion.GameType)
            {
                case GameType.Honkai:
                    NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                    { Content = Lang._GameSettingsPage.PageTitle, Icon = IconGameSettings, Tag = "gamesettings" });
                    break;
                case GameType.StarRail:
                    NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                    { Content = Lang._StarRailGameSettingsPage.PageTitle, Icon = IconGameSettings, Tag = "starrailgamesettings" });
                    break;
            }

            if (GetCurrentGameProperty()._GameVersion.GameType == GameType.Genshin)
            {
                NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                { Content = Lang._GenshinGameSettingsPage.PageTitle, Icon = IconGameSettings, Tag = "genshingamesettings" });
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
                        Navigate(IsGameInstalled() ? typeof(CachesPage) : typeof(NotInstalledPage), itemTag);
                    else
                        Navigate(typeof(UnavailablePage), itemTag);
                    break;

                case "gamesettings":
                    Navigate(IsGameInstalled() ? typeof(GameSettingsPage) : typeof(NotInstalledPage), itemTag);
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

        private bool IsGameInstalled() => GameInstallationState == GameInstallStateEnum.Installed ||
                                          GameInstallationState == GameInstallStateEnum.InstalledHavePreload ||
                                          GameInstallationState == GameInstallStateEnum.NeedsUpdate;

        private void EnableRegionChangeButton(object sender, SelectionChangedEventArgs e)
        {
            object selValue = ((ComboBox)sender).SelectedValue;
            if (selValue != null)
            {
                string category = (string)ComboBoxGameCategory.SelectedValue;
                string region = GetComboBoxGameRegionValue(selValue);
                PresetConfigV2 preset = ConfigV2.MetadataV2[category][region];
                ChangeRegionWarningText.Text = preset.GameChannel != GameChannel.Stable ? string.Format(Lang._MainPage.RegionChangeWarnExper1, preset.GameChannel) : string.Empty;
                ChangeRegionWarning.Visibility = preset.GameChannel != GameChannel.Stable ? Visibility.Visible : Visibility.Collapsed;
            }
            ChangeRegionConfirmBtn.IsEnabled = !LockRegionChangeBtn;
            ChangeRegionConfirmBtnNoWarning.IsEnabled = !LockRegionChangeBtn;
        }

        private void ErrorSenderInvoker_ExceptionEvent(object sender, ErrorProperties e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (e.Exception.GetType() == typeof(NotImplementedException))
                {
                    PreviousTag = "unavailable";
                    MainFrameChanger.ChangeMainFrame(typeof(UnavailablePage));
                }
                else
                {
                    PreviousTag = "crashinfo";
                    MainFrameChanger.ChangeMainFrame(typeof(UnhandledExceptionPage));
                }
            });
        }

        private void MainFrameChangerInvoker_FrameEvent(object sender, MainFrameProperties e)
        {
            IsLoadFrameCompleted = false;
            m_appCurrentFrameName = e.FrameTo.Name;
            LauncherFrame.Navigate(e.FrameTo, null, e.Transition);
            IsLoadFrameCompleted = true;
        }

        private void SpawnWebView2Panel(Uri URL)
        {
            try
            {
                WebView2FramePage.WebView2URL = URL;
                WebView2Frame.Navigate(typeof(WebView2FramePage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromBottom });
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
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            if (IsChangeDragArea)
            {
                UnsubscribeEvents();
                MainWindow.SetDragArea(DragAreaMode_Full);
            }
        }

        private bool IsNotificationPanelShow = false;
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
            }
            else
            {
                NotificationLostFocusBackground.Opacity = 0;
                NotificationPanel.Translation -= Shadow48;
                await Task.Delay(200);
                NotificationLostFocusBackground.Visibility = Visibility.Collapsed;
            }
        }

        private void NotificationContainerBackground_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
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

                string lastPreviousTag = PreviousTagString[PreviousTagString.Count - 1];
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

        private bool IsTitleIconForceShow = false;

        private void NavigationPanelOpening_Event(NavigationView sender, object args)
        {
            Thickness curMargin = GridBG_Icon.Margin;
            curMargin.Left = 48;
            GridBG_Icon.Margin = curMargin;
            IsTitleIconForceShow = true;
            ToggleTitleIcon(false);
        }

        private void NavigationPanelClosing_Event(NavigationView sender, NavigationViewPaneClosingEventArgs args)
        {
            Thickness curMargin = GridBG_Icon.Margin;
            curMargin.Left = 58;
            GridBG_Icon.Margin = curMargin;
            IsTitleIconForceShow = false;
            ToggleTitleIcon(true);
        }

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

        private void GridBG_Icon_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!IsTitleIconForceShow)
            {
                Thickness curMargin = GridBG_Icon.Margin;
                curMargin.Left = 50;
                GridBG_Icon.Margin = curMargin;
                ToggleTitleIcon(false);
            }
        }

        private void GridBG_Icon_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
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
    }
}
