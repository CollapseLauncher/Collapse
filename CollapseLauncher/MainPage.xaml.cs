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

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            try
            {
                LoadGamePreset();
                LogWriteLine($"Welcome to Collapse Launcher v{AppCurrentVersion} - {GetVersionString()}", LogType.Default, false);
                LogWriteLine($"Application Data Location:\r\n\t{AppDataFolder}", LogType.Default);
                InitializeComponent();
                ErrorSenderInvoker.ExceptionEvent += ErrorSenderInvoker_ExceptionEvent;
                MainFrameChangerInvoker.FrameEvent += MainFrameChangerInvoker_FrameEvent;
                NotificationInvoker.EventInvoker += NotificationInvoker_EventInvoker;

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

        private async Task GetNotificationFeed()
        {
            using (MemoryStream buffer = new MemoryStream())
            {
                await new HttpClientHelper().DownloadFileAsync(string.Format(AppNotifURLPrefix, (AppConfig.IsPreview ? "preview" : "stable")),
                    buffer, new CancellationToken(), -1, -1, false);
                AppConfig.NotificationData = JsonConvert.DeserializeObject<NotificationPush>(Encoding.UTF8.GetString(buffer.ToArray()));
                AppConfig.LoadLocalNotificationData();
            }
        }

        private async void PushAppNotification()
        {
            TypedEventHandler<InfoBar, object> ClickCloseAction = null;
            foreach (NotificationProp Entry in AppConfig.NotificationData.AppPush)
            {
                // Check for Close Action for certain MsgIds
                switch (Entry.MsgId)
                {
                    case 0:
                        {
                            ClickCloseAction = new TypedEventHandler<InfoBar, object>((sender, args) =>
                            {
                                AppConfig.NotificationData.AddIgnoredMsgIds(0);
                                AppConfig.SaveLocalNotificationData();
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
            string UpdateNotifFile = Path.Combine(AppDataFolder, "_NewVer");
            TypedEventHandler<InfoBar, object> ClickClose = new TypedEventHandler<InfoBar, object>((sender, args) =>
            {
                File.Delete(UpdateNotifFile);
                try
                {
                    string updateElevator = Path.Combine(AppDataFolder, "CollapseLauncher.Updater.Elevated.exe");
                    if (File.Exists(updateElevator))
                        File.Delete(updateElevator);
                }
                catch { }
            });

            if (File.Exists(UpdateNotifFile))
            {
                string VerString = File.ReadAllLines(UpdateNotifFile)[0];
                SpawnNotificationPush(
                    "Update Completed!",
                    string.Format("Your launcher version has been updated to {0}! (Release Channel: {1})", VerString, AppConfig.IsPreview ? "Preview" : "Stable"),
                    InfoBarSeverity.Success,
                    0xAF,
                    true,
                    false,
                    ClickClose
                    );
            }
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
            AppConfig.NotificationData.AddIgnoredMsgIds(int.Parse(Data[0]), bool.Parse(Data[1]));
            AppConfig.SaveLocalNotificationData();
        }

        private void NeverAskNotif_Unchecked(object sender, RoutedEventArgs e)
        {
            string[] Data = (sender as CheckBox).Tag.ToString().Split(',');
            AppConfig.NotificationData.RemoveIgnoredMsgIds(int.Parse(Data[0]), bool.Parse(Data[1]));
            AppConfig.SaveLocalNotificationData();
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
            await LoadRegion(GetAppConfigValue("CurrentRegion").ToInt());
            MainFrameChanger.ChangeMainFrame(typeof(Pages.HomePage));

            NavigationViewControl.IsSettingsVisible = true;

            NavigationViewControl.MenuItems.Add(new NavigationViewItemSeparator());

            NavigationViewControl.MenuItems.Add(new NavigationViewItem()
            { Content = "Game Repair", Icon = new SymbolIcon(Symbol.Repair), Tag = "repair" });
            NavigationViewControl.MenuItems.Add(new NavigationViewItem()
            { Content = "Caches", Icon = new SymbolIcon(Symbol.Download), Tag = "caches" });
            NavigationViewControl.MenuItems.Add(new NavigationViewItem()
            { Content = "Cutscenes", Icon = new SymbolIcon(Symbol.Video), Tag = "cutscenes" });
            NavigationViewControl.MenuItems.Add(new NavigationViewItem()
            { Content = "Game Settings", Icon = new SymbolIcon(Symbol.Library), Tag = "gamesettings" });
        }

        public void LoadConfig()
        {
            ComboBoxGameRegion.ItemsSource = GameConfigName;
        }

        private string GetVersionString()
        {
            OperatingSystem osDetail = Environment.OSVersion;
            ushort[] buildNumber = osDetail.Version.ToString().Split('.').Select(ushort.Parse).ToArray();
            if (buildNumber[2] >= 22000)
                return $"Windows 11 (build: {buildNumber[2]}.{buildNumber[3]})";
            else
                return $"Windows {buildNumber[0]} (build: {buildNumber[2]}.{buildNumber[3]})";
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
                case "BlankPage":
                    HideBackgroundImage();
                    LauncherFrame.Navigate(e.FrameTo, null, e.Transition);
                    break;
            }
        }
    }
}
