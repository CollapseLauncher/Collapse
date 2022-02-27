using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

// using Windows.UI;
using Windows.ApplicationModel.Core;

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Diagnostics;

// using Windows.UI.Core;
// using Windows.UI.ViewManagement;

using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.Logger;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace CollapseLauncher
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            try
            {
                LogWriteLine($"Welcome to CollapseLauncher v{Assembly.GetExecutingAssembly().GetName().Version} - {GetVersionString()}", LogType.Default, false);
                LogWriteLine($"Application Data Location:\r\n\t{AppDataFolder}", LogType.Default);
                InitializeComponent();
                ErrorSenderInvoker.ExceptionEvent += ErrorSenderInvoker_ExceptionEvent;
                MainFrameChangerInvoker.FrameEvent += MainFrameChangerInvoker_FrameEvent;

                InitializeStartup().GetAwaiter();
                Task.Run(() => CheckRunningGameInstance());
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
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

        private async void CheckRunningGameInstance()
        {
            while (true && !App.IsAppKilled)
            {
                App.IsGameRunning = Process.GetProcessesByName("BH3").Length != 0 && !App.IsAppKilled;
                await Task.Delay(3000);
            }
        }

        private async Task InitializeStartup()
        {
            LoadConfig();
            await LoadRegion(GetAppConfigValue("CurrentRegion").ToInt());
            MainFrameChanger.ChangeMainFrame(typeof(Pages.HomePage));
            // LauncherFrame.Navigate(typeof(Pages.HomePage), null, new DrillInNavigationTransitionInfo());

            // you can also add items in code behind
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

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            UpdateTitleBarLayout(sender);
        }

        private void CoreTitleBar_IsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
        {
            if (sender.IsVisible)
            {
                AppTitleBar.Visibility = Visibility.Visible;
            }
            else
            {
                AppTitleBar.Visibility = Visibility.Collapsed;
            }
        }

        // Update the TitleBar based on the inactive/active state of the app
        private void Current_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs e)
        {
            SolidColorBrush defaultForegroundBrush = (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            SolidColorBrush inactiveForegroundBrush = (SolidColorBrush)Application.Current.Resources["TextFillColorDisabledBrush"];

            if (e.WindowActivationState == WindowActivationState.Deactivated)
            {
                AppTitle.Foreground = inactiveForegroundBrush;
            }
            else
            {
                AppTitle.Foreground = defaultForegroundBrush;
            }
        }

        private void UpdateTitleBarLayout(CoreApplicationViewTitleBar coreTitleBar)
        {
            // Update title bar control size as needed to account for system size changes.
            AppTitleBar.Height = coreTitleBar.Height;

            // Ensure the custom title bar does not overlap window caption controls
            Thickness currMargin = AppTitleBar.Margin;
            AppTitleBar.Margin = new Thickness(currMargin.Left, currMargin.Top, coreTitleBar.SystemOverlayRightInset, currMargin.Bottom);
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // set the initial SelectedItem 
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
                // LauncherFrame.Navigate(typeof(Pages.SettingsPage), null, new DrillInNavigationTransitionInfo());
                HideBackgroundImage(true, false);
                previousTag = "settings";
                LogWriteLine($"Page changed to App Settings", LogType.Scheme);
            }
            else
            {
                // find NavigationViewItem with Content that equals InvokedItem
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
            // LauncherFrame.Navigate(sourceType, null, new DrillInNavigationTransitionInfo());
            HideBackgroundImage(hideImage, false);
            previousTag = tagStr;
        }

        string previousTag = string.Empty;
        private void NavView_Navigate(NavigationViewItem item)
        {
            try
            {
                // Prevent repeated call of pages
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
                            // throw new NotImplementedException("Caches Page isn't yet implemented for now.");
                            break;

                        case "cutscenes":
                            // Navigate(typeof(Pages.CutscenesPage), true, item);
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

            // If the back button is not visible, reduce the TitleBar content indent.
            if (NavigationViewControl.IsBackButtonVisible.Equals(NavigationViewBackButtonVisible.Collapsed))
            {
                minimalIndent = 48;
            }

            Thickness currMargin = AppTitleBar.Margin;

            // Set the TitleBar margin dependent on NavigationView display mode
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
                    // LauncherFrame.Navigate(typeof(Pages.UnavailablePage), null, new DrillInNavigationTransitionInfo());
                }
                else
                {
                    previousTag = "crashinfo";
                    HideBackgroundImage();
                    MainFrameChanger.ChangeMainFrame(typeof(Pages.UnhandledExceptionPage));
                    // LauncherFrame.Navigate(typeof(Pages.UnhandledExceptionPage), null, new SlideNavigationTransitionInfo());
                }
            });
        }

        private void MainFrameChangerInvoker_FrameEvent(object sender, MainFrameProperties e) => LauncherFrame.Navigate(e.FrameTo, null, e.Transition);
    }
}
