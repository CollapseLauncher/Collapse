using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.UI.ViewManagement;
using Windows.ApplicationModel.Core;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Windows.UI.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using CollapseLauncher;
using WinRT;

using Hi3Helper;

using static CollapseLauncher.LauncherConfig;
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
            // LoadGamePreset();
            // InitializeConsole(true, AppDataFolder);
            LogWriteLine($"Welcome to CollapseLauncher v{Assembly.GetExecutingAssembly().GetName().Version} - {GetVersionString()}", LogType.Default, false);
            LogWriteLine($"Application Data Location:\r\n\t{AppDataFolder}", LogType.Default);
            // ImageSource defaultBackground = new BitmapImage(new Uri(startupBackgroundPath));
            InitializeComponent();

            // BackgroundBack.Source = defaultBackground;
            // BackgroundFront.Source = defaultBackground;


            InitializeStartup().GetAwaiter();
        }

        private async Task InitializeStartup()
        {
            LoadConfig();
            await LoadRegion(appIni.Profile["app"]["CurrentRegion"].ToInt());
            LauncherFrame.Navigate(typeof(Pages.HomePage), null, new DrillInNavigationTransitionInfo());
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

        private void UpdateTitleBarLayout(CoreApplicationViewTitleBar coreTitleBar)
        {
            // Update title bar control size as needed to account for system size changes.
            AppTitleBar.Height = coreTitleBar.Height;

            // Ensure the custom title bar does not overlap window caption controls
            Thickness currMargin = AppTitleBar.Margin;
            AppTitleBar.Margin = new Thickness(currMargin.Left, currMargin.Top, coreTitleBar.SystemOverlayRightInset, currMargin.Bottom);
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            UpdateTitleBarLayout(sender);
        }

        // Update the TitleBar based on the inactive/active state of the app
        private void Current_Activated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            SolidColorBrush defaultForegroundBrush = (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            SolidColorBrush inactiveForegroundBrush = (SolidColorBrush)Application.Current.Resources["TextFillColorDisabledBrush"];

            if (e.WindowActivationState == CoreWindowActivationState.Deactivated)
            {
                AppTitle.Foreground = inactiveForegroundBrush;
            }
            else
            {
                AppTitle.Foreground = defaultForegroundBrush;
            }
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

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // you can also add items in code behind

            NavigationViewControl.MenuItems.Add(new NavigationViewItemSeparator());

            NavigationViewControl.MenuItems.Add(new NavigationViewItem()
            { Content = "Game Repair", Icon = new SymbolIcon(Symbol.Repair), Tag = "repair" });
            NavigationViewControl.MenuItems.Add(new NavigationViewItem()
            { Content = "Caches", Icon = new SymbolIcon(Symbol.Download), Tag = "caches" });
            NavigationViewControl.MenuItems.Add(new NavigationViewItem()
            { Content = "Cutscenes", Icon = new SymbolIcon(Symbol.Video), Tag = "cutscenes" });
            NavigationViewControl.MenuItems.Add(new NavigationViewItem()
            { Content = "Game Settings", Icon = new SymbolIcon(Symbol.Library), Tag = "gamesettings" });

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
                LauncherFrame.Navigate(typeof(Pages.SettingsPage), null, new DrillInNavigationTransitionInfo());
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
            LauncherFrame.Navigate(sourceType, null, new DrillInNavigationTransitionInfo());
            HideBackgroundImage(hideImage, false);
            previousTag = (string)tag.Tag;
        }

        string previousTag = string.Empty;
        private void NavView_Navigate(NavigationViewItem item)
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
                        Navigate(typeof(Pages.RepairPage), true, item);
                        break;

                    case "caches":
                        Navigate(typeof(Pages.CachesPage), true, item);
                        break;

                    case "cutscenes":
                        Navigate(typeof(Pages.CutscenesPage), true, item);
                        break;

                    case "gamesettings":
                        Navigate(typeof(Pages.GameSettingsPage), true, item);
                        break;

                        /*
                    case "music":
                        LauncherFrame.Navigate(typeof(MusicPage));
                        break;

                    case "content":
                        LauncherFrame.Navigate(typeof(MyContentPage));
                        break;

                        */
                }
                LogWriteLine($"Page changed to {item.Content}", LogType.Scheme);
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
    }
}
