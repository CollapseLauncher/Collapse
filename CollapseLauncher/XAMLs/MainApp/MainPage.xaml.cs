using Hi3Helper;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Preset.ConfigStore;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            try
            {
                LoadGamePreset();
                LogWriteLine($"Welcome to Collapse Launcher v{AppCurrentVersion} - {MainEntryPoint.GetVersionString()}", LogType.Default, false);
                LogWriteLine($"Application Data Location:\r\n\t{AppDataFolder}", LogType.Default);
                InitializeComponent();
                SetThemeParameters();

                m_actualMainFrameSize = new Size(LauncherFrame.Width, LauncherFrame.Height);

                ErrorSenderInvoker.ExceptionEvent += ErrorSenderInvoker_ExceptionEvent;
                MainFrameChangerInvoker.FrameEvent += MainFrameChangerInvoker_FrameEvent;
                NotificationInvoker.EventInvoker += NotificationInvoker_EventInvoker;
                BackgroundImgChangerInvoker.ImgEvent += CustomBackgroundChanger_Event;
                SpawnWebView2Invoker.SpawnEvent += SpawnWebView2Invoker_SpawnEvent;

                LauncherUpdateWatcher.StartCheckUpdate();

                CheckRunningGameInstance();

                InitializeStartup();
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void SpawnWebView2Invoker_SpawnEvent(object sender, SpawnWebView2Property e) => SpawnWebView2Panel(e.URL);

        private async void CustomBackgroundChanger_Event(object sender, BackgroundImgProperty e)
        {
            e.IsImageLoaded = false;
            regionBackgroundProp.imgLocalPath = e.ImgPath;
            if (e.IsCustom)
                SetAndSaveConfigValue("CustomBGPath", regionBackgroundProp.imgLocalPath);

            try
            {
                await ApplyBackground();
            }
            catch (Exception ex)
            {
                regionBackgroundProp.imgLocalPath = AppDefaultBG;
                await ApplyBackground(false);
                LogWriteLine($"An error occured while loading background {e.ImgPath}\r\n{ex}", LogType.Error, true);
            }

            await GenerateThumbnail();
            ApplyAccentColor();
            e.IsImageLoaded = true;
        }

        private async void CheckRunningGameInstance()
        {
            while (true && !App.IsAppKilled)
            {
                string execName = Path.GetFileNameWithoutExtension(CurrentRegion.GameExecutableName);
                App.IsGameRunning = Process.GetProcessesByName(execName).Length != 0 && !App.IsAppKilled;
                await Task.Delay(250);
            }
        }

        private void NotificationInvoker_EventInvoker(object sender, NotificationInvokerProp e)
        {
            SpawnNotificationPush(e.Notification.Title, e.Notification.Message, e.Notification.Severity,
                e.Notification.MsgId, e.Notification.IsClosable ?? true, e.Notification.IsDisposable ?? true, e.CloseAction,
                e.OtherContent, e.IsAppNotif);
        }

        private async void GetAppNotificationPush()
        {
            try
            {
                await GetNotificationFeed();
                await Task.Run(() =>
                {
                    GetAppUpdateNotification();
                });
                PushAppNotification();
            }
            catch (JsonReaderException ex)
            {
                LogWriteLine($"Error while trying to get Notification Feed\r\n{ex}", LogType.Error, true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while trying to get Notification Feed\r\n{ex}", LogType.Error, true);
            }
        }

        bool IsLoadNotifComplete = false;
        private async Task GetNotificationFeed()
        {
            try
            {
                IsLoadNotifComplete = false;
                NotificationData = new NotificationPush();
                CancellationTokenSource TokenSource = new CancellationTokenSource();
                RunTimeoutCancel(TokenSource);
                using (MemoryStream buffer = new MemoryStream())
                {
                    await new Http().DownloadStream(string.Format(AppNotifURLPrefix, (IsPreview ? "preview" : "stable")),
                        buffer, TokenSource.Token);
                    NotificationData = JsonConvert.DeserializeObject<NotificationPush>(Encoding.UTF8.GetString(buffer.ToArray()));
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
                    SpawnNotificationPush(Entry.Title, Entry.Message, Entry.Severity, Entry.MsgId, Entry.IsClosable ?? true, Entry.IsDisposable ?? true, ClickCloseAction, null, true);
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
                        InfoBarSeverity.Success,
                        0xAF,
                        true,
                        false,
                        ClickClose
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

        private void SpawnNotificationPush(string Title, string Content, InfoBarSeverity Severity, int MsgId = 0, bool IsClosable = true,
            bool Disposable = false, TypedEventHandler<InfoBar, object> CloseClickHandler = null, UIElement OtherContent = null, bool IsAppNotif = true)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Grid Container = new Grid
                {
                    // Background = (Brush)Application.Current.Resources["InfoBarAnnouncementBrush"]
                };

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
                    Severity = Severity,
                    IsClosable = IsClosable,
                    IsIconVisible = true,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = (Brush)Application.Current.Resources["InfoBarAnnouncementBrush"],
                    Shadow = SharedShadow,
                    Translation = Shadow16,
                    IsOpen = true
                };

                Notification.Closed += ((a, b) => { a.Translation -= Shadow16; });

                if (OtherContent != null)
                    OtherContentContainer.Children.Add(OtherContent);

                if (Disposable)
                {
                    CheckBox NeverAskNotif = new CheckBox
                    {
                        Content = Lang._MainPage.NotifNeverAsk,
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

        private void ReloadPageTheme(ElementTheme startTheme)
        {
            try
            {
                if (this.RequestedTheme == ElementTheme.Dark)
                    this.RequestedTheme = ElementTheme.Light;
                else if (this.RequestedTheme == ElementTheme.Light)
                    this.RequestedTheme = ElementTheme.Default;
                else if (this.RequestedTheme == ElementTheme.Default)
                    this.RequestedTheme = ElementTheme.Dark;

                if (this.RequestedTheme != startTheme)
                    ReloadPageTheme(startTheme);
            }
            catch (ArgumentException ex)
            {
                LogWriteLine($"TODO: Investigate issue while theme is getting reloaded\r\n{ex}", LogType.Error, true);
            }
        }

        private ElementTheme ConvertAppThemeToElementTheme(AppThemeMode Theme)
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
            GetAppNotificationPush();

            bool IsMetaStampExist = IsMetadataStampExist();
            bool IsMetaContentExist = IsMetadataContentExist();

            if (!IsMetaStampExist || !IsMetaContentExist)
            {
                LogWriteLine($"Loading config metadata for the first time...", LogType.Default, true);
                await HideLoadingPopup(false, Lang._MainPage.RegionLoadingAPITitle1, Lang._MainPage.RegionLoadingAPITitle2);
                await DownloadMetadataFiles(true, true);
            }

            LoadConfigTemplate();
            LoadRegionSelectorItems();
            await LoadRegion(GetAppConfigValue("CurrentRegion").ToInt());
            CheckMetadataUpdateInBackground();
            MainFrameChanger.ChangeMainFrame(typeof(Pages.HomePage));
        }

        private async void CheckMetadataUpdateInBackground()
        {
            bool IsUpdate = await CheckForNewMetadata();
            if (IsUpdate)
            {
                TextBlock Text = new TextBlock { Text = Lang._MainPage.MetadataUpdateBtn, VerticalAlignment = VerticalAlignment.Center };
                Button UpdateMetadatabtn = new Button
                {
                    Content = Text,
                    Margin = new Thickness(0, 0, 0, 16),
                    Style = (Application.Current.Resources["AccentButtonStyle"] as Style)
                };

                UpdateMetadatabtn.Click += (async (a, b) =>
                {
                    TextBlock Text = new TextBlock { Text = Lang._MainPage.MetadataUpdateBtnUpdating, VerticalAlignment = VerticalAlignment.Center };
                    ProgressRing LoadBar = new ProgressRing
                    {
                        IsIndeterminate = true,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0,0,8,0),
                        Width = 16,
                        Height = 16
                    };
                    StackPanel StackPane = new StackPanel() { Orientation = Orientation.Horizontal};
                    StackPane.Children.Add(LoadBar);
                    StackPane.Children.Add(Text);
                    (a as Button).Content = StackPane;
                    (a as Button).IsEnabled = false;

                    try
                    {
                        await DownloadMetadataFiles(true, true);
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
                    InfoBarSeverity.Informational,
                    -886135731,
                    true,
                    false,
                    null,
                    UpdateMetadatabtn
                    );
            }
        }

        private void InitializeNavigationItems()
        {
            NavigationViewControl.IsSettingsVisible = true;
            NavigationViewControl.MenuItems.Clear();

            NavigationViewControl.MenuItems.Add(new NavigationViewItem()
            { Content = Lang._HomePage.PageTitle, Icon = new SymbolIcon(Symbol.Home), Tag = "launcher" });

            if (!(CurrentRegion.IsGenshin ?? false))
            {
                NavigationViewControl.MenuItems.Add(new NavigationViewItemSeparator());

                NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                { Content = Lang._GameRepairPage.PageTitle, Icon = new SymbolIcon(Symbol.Repair), Tag = "repair" });
                NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                { Content = Lang._CachesPage.PageTitle, Icon = new SymbolIcon(Symbol.Download), Tag = "caches" });
                // NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                // { Content = Lang._CutscenesPage.PageTitle, Icon = new SymbolIcon(Symbol.Video), Tag = "cutscenes" });
                NavigationViewControl.MenuItems.Add(new NavigationViewItem()
                { Content = Lang._GameSettingsPage.PageTitle, Icon = new SymbolIcon(Symbol.Library), Tag = "gamesettings" });
            }

            NavigationViewControl.SelectedItem = (NavigationViewItem)NavigationViewControl.MenuItems[0];
            (NavigationViewControl.SettingsItem as NavigationViewItem).Content = Lang._SettingsPage.PageTitle;
        }

        public void LoadRegionSelectorItems()
        {
            ComboBoxGameRegion.ItemsSource = GameConfigName;
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
                previousTag = "settings";
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
            if (((CurrentRegion.IsGenshin ?? false) && (string)tag.Tag != "launcher"))
            {
                sourceType = typeof(Pages.UnavailablePage);
                tagStr = "unavailable";
            }
            MainFrameChanger.ChangeMainFrame(sourceType, new DrillInNavigationTransitionInfo());
            previousTag = tagStr;
        }

        string previousTag = string.Empty;
        private void NavView_Navigate(NavigationViewItem item)
        {
            try
            {
                if (!(previousTag == (string)item.Tag))
                {
                    switch (item.Tag)
                    {
                        case "launcher":
                            Navigate(typeof(Pages.HomePage), false, item);
                            break;

                        case "repair":
                            if (string.IsNullOrEmpty(CurrentRegion.ZipFileURL))
                                Navigate(typeof(Pages.UnavailablePage), true, item);
                            else
                                Navigate(typeof(Pages.RepairPage), true, item);
                            break;

                        case "caches":
                            if (CurrentRegion.CachesListGameVerID != null
                                && CurrentRegion.CachesListAPIURL != null
                                && CurrentRegion.CachesEndpointURL != null)
                                Navigate(typeof(Pages.CachesPage), true, item);
                            else
                                Navigate(typeof(Pages.UnavailablePage), true, item);
                            break;

                        case "cutscenes":
                            throw new NotImplementedException("Cutscenes Downloading Page isn't yet implemented for now.");

                        case "gamesettings":
                            Navigate(typeof(Pages.GameSettingsPage), true, item);
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

        private void EnableRegionChangeButton(object sender, SelectionChangedEventArgs e) => ChangeRegionConfirmBtn.IsEnabled = true;

        private void ErrorSenderInvoker_ExceptionEvent(object sender, ErrorProperties e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (e.Exception.GetType() == typeof(NotImplementedException))
                {
                    previousTag = "unavailable";
                    HideBackgroundImage();
                    MainFrameChanger.ChangeMainFrame(typeof(Pages.UnavailablePage));
                }
                else
                {
                    previousTag = "crashinfo";
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
                    previousTag = "repair";
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
                WebViewWindow.Visibility = Visibility.Visible;
                WebView2Panel.Visibility = Visibility.Visible;
                WebView2Panel.Translation += Shadow32;
                await WebViewWindow.EnsureCoreWebView2Async();
                WebViewWindow.Source = URL;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while initialize WebView2\r\n{ex}", LogType.Error, true);
                WebViewWindow.Close();
            }
        }

        private void WebViewWindow_PageLoaded(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args) => WebViewLoadingStatus.IsIndeterminate = false;

        private void WebViewWindow_PageLoading(WebView2 sender, CoreWebView2NavigationStartingEventArgs args) => WebViewLoadingStatus.IsIndeterminate = true;

        private void WebViewBackBtn_Click(object sender, RoutedEventArgs e) => WebViewWindow.GoBack();
        private void WebViewForwardBtn_Click(object sender, RoutedEventArgs e) => WebViewWindow.GoForward();
        private void WebViewReloadBtn_Click(object sender, RoutedEventArgs e) => WebViewWindow.Reload();
        private void WebViewOpenExternalBtn_Click(object sender, RoutedEventArgs e)
        {
            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = WebViewWindow.Source.ToString()
                }
            }.Start();
        }

        private void WebViewCloseBtn_Click(object sender, RoutedEventArgs e)
        {
            WebViewWindow.Visibility = Visibility.Collapsed;
            WebViewWindow.Reload();
            WebView2Panel.Visibility = Visibility.Collapsed;
            WebView2Panel.Translation -= Shadow32;
        }
        private void WebViewWindow_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args) => sender.CoreWebView2.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;
        private void CoreWebView2_DocumentTitleChanged(Microsoft.Web.WebView2.Core.CoreWebView2 sender, object args) => WebViewWindowTitle.Text = sender.DocumentTitle;
    }
}