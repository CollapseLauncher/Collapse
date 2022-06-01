using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

using Windows.Foundation;
using Windows.ApplicationModel.Core;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

using Newtonsoft.Json;

using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.Logger;
using static Hi3Helper.Locale;

using static CollapseLauncher.InnerLauncherConfig;

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

                m_actualMainFrameSize = new Size(LauncherFrame.Width, LauncherFrame.Height);

                ErrorSenderInvoker.ExceptionEvent += ErrorSenderInvoker_ExceptionEvent;
                MainFrameChangerInvoker.FrameEvent += MainFrameChangerInvoker_FrameEvent;
                NotificationInvoker.EventInvoker += NotificationInvoker_EventInvoker;
                BackgroundImgChangerInvoker.ImgEvent += CustomBackgroundChanger_Event;

                LauncherUpdateWatcher.StartCheckUpdate();

                Task.Run(() => CheckRunningGameInstance());

                InitializeStartup().GetAwaiter();
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

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
                await Task.Delay(3000);
            }
        }

        private void NotificationInvoker_EventInvoker(object sender, NotificationInvokerProp e)
        {
            SpawnNotificationPush(e.Notification.Title, e.Notification.Message, e.Notification.Severity,
                e.Notification.MsgId, e.Notification.IsClosable ?? true, e.Notification.IsDisposable ?? true, e.CloseAction,
                e.OtherContent, e.IsAppNotif);
        }

        private async Task GetAppNotificationPush()
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
                    await new HttpClientHelper().DownloadFileAsync(string.Format(AppNotifURLPrefix, (IsPreview ? "preview" : "stable")),
                        buffer, TokenSource.Token, null, null, false);
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
            await Task.Delay(5000);
            if (!IsLoadNotifComplete)
            {
                LogWriteLine("Cancel to load notification push! > 5 seconds", LogType.Error, true);
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
                    Background = (Brush)Application.Current.Resources["InfoBarAnnouncementBrush"]
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
                    Margin = new Thickness(-2),
                    CornerRadius = new CornerRadius(0),
                    Severity = Severity,
                    IsClosable = IsClosable,
                    IsIconVisible = true,
                    IsOpen = true
                };

                if (OtherContent != null)
                    OtherContentContainer.Children.Add(OtherContent);

                if (Disposable)
                {
                    CheckBox NeverAskNotif = new CheckBox
                    {
                        Content = "Never show me this again",
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
            if (this.RequestedTheme == ElementTheme.Dark)
                this.RequestedTheme = ElementTheme.Light;
            else if (this.RequestedTheme == ElementTheme.Light)
                this.RequestedTheme = ElementTheme.Default;
            else if (this.RequestedTheme == ElementTheme.Default)
                this.RequestedTheme = ElementTheme.Dark;

            if (this.RequestedTheme != startTheme)
                ReloadPageTheme(startTheme);
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

        private async Task InitializeStartup()
        {
            await HideLoadingPopup(false, "Loading", "Launcher API");
            LoadConfig();
            await GetAppNotificationPush();
            // InnerLauncherConfig.NotificationData = new NotificationPush();
            await LoadRegion(GetAppConfigValue("CurrentRegion").ToInt());
            if (regionResourceProp.data != null)
                MainFrameChanger.ChangeMainFrame(typeof(Pages.HomePage));
            else
                MainFrameChanger.ChangeWindowFrame(typeof(DisconnectedPage));
        }

        private async void InitializeNavigationItems()
        {
            await Task.Run(() => { });

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
        }

        public void LoadConfig()
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

        private void NavigationViewControl_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
        {
            const int topIndent = 16;
            const int expandedIndent = 48;
            int minimalIndent = 104;

            if (NavigationViewControl.IsBackButtonVisible.Equals(NavigationViewBackButtonVisible.Collapsed))
            {
                minimalIndent = 48;
            }

            Thickness currMargin = AppTitleBar.Margin;

            if (sender.PaneDisplayMode == NavigationViewPaneDisplayMode.Top)
            {
                AppTitleBar.Margin = new Thickness(topIndent, currMargin.Top, currMargin.Right, currMargin.Bottom);
            }
            else if (sender.DisplayMode == NavigationViewDisplayMode.Minimal)
            {
                AppTitleBar.Margin = new Thickness(minimalIndent, currMargin.Top, currMargin.Right, currMargin.Bottom);
            }
            else
            {
                AppTitleBar.Margin = new Thickness(expandedIndent, currMargin.Top, currMargin.Right, currMargin.Bottom);
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
    }
}