using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.ApplicationModel.Core;
using Windows.Foundation.Metadata;
using Windows.ApplicationModel;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Controls;

using muxc = Microsoft.UI.Xaml.Controls;

using static CollapseLauncher.LauncherConfig;
using static Hi3Helper.Logger;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace CollapseLauncher
{
    public class ChangeLogItem
    {
        public ChangeLogItem(Color color, bool isDarkMode, DateTimeOffset time)
        {
            Color = color;
            Time = time;
            DarkModeVisibility = isDarkMode ? Visibility.Visible : Visibility.Collapsed;
        }

        public Color Color { get; }

        public DateTimeOffset Time { get; }

        public Visibility DarkModeVisibility { get; }
    }
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public partial class MainPage : Page
    {
        public MainPage()
        {
            LoadGamePreset();
            InitializeConsole(true, AppFolder);
            LogWriteLine($"Welcome to CollapseLauncher v{Assembly.GetExecutingAssembly().GetName().Version} - {GetVersionString()}", Hi3Helper.LogType.Default, false);
            LogWriteLine($"Application Data Location:\r\n\t{AppFolder}", Hi3Helper.LogType.Default);
            InitializeComponent();

            float DPI = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi;

            ApplicationView.PreferredLaunchViewSize = new Size(((float)1240 * 96.0f / DPI), ((float)730 * 96.0f / DPI));
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;

            var titleBar = ApplicationView.GetForCurrentView().TitleBar;

            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            // Hide default title bar.
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;
            UpdateTitleBarLayout(coreTitleBar);

            // Set XAML element as a draggable region.
            Window.Current.SetTitleBar(AppTitleBar);

            // Register a handler for when the size of the overlaid caption control changes.
            // For example, when the app moves to a screen with a different DPI.
            coreTitleBar.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;

            // Register a handler for when the title bar visibility changes.
            // For example, when the title bar is invoked in full screen mode.
            coreTitleBar.IsVisibleChanged += CoreTitleBar_IsVisibleChanged;

            //Register a handler for when the window changes focus
            Window.Current.Activated += Current_Activated;
            LoadConfig();
            LoadRegion().GetAwaiter();
            LauncherFrame.Navigate(typeof(Pages.HomePage));
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

        /*
        public ObservableCollection<ChangeLogItem> ChangeLog { get; } =
            new ObservableCollection<ChangeLogItem>();

        private void ColorValuesChanged(UISettings sender, object args)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // var accentColor = sender.GetColorValue(UIColorType.Complement);
                //OR
                (App.Current.Resources["SystemAccentColorLight2"] as SolidColorBrush).Color = new Color() { A = 255, B = 255, G = 255, R = 255 };
                
                Color accentColor = new Color() { A = 255, B = 255, G = 255, R = 255};

                var backgroundColor = sender.GetColorValue(UIColorType.Background);
                var isDarkMode = backgroundColor == Colors.Black;

                ChangeLog.Insert(0, new ChangeLogItem(accentColor, isDarkMode, DateTimeOffset.Now));

                //Example - update title bar
                UpdateTitleBar(accentColor);
            }).AsTask().GetAwaiter();
        }
        */

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // you can also add items in code behind

            NavigationViewControl.MenuItems.Add(new muxc.NavigationViewItemSeparator());

            NavigationViewControl.MenuItems.Add(new muxc.NavigationViewItem()
            { Content = "Game Repair", Icon = new SymbolIcon(Symbol.Repair), Tag = "repair" });
            NavigationViewControl.MenuItems.Add(new muxc.NavigationViewItem()
            { Content = "Caches", Icon = new SymbolIcon(Symbol.Download), Tag = "caches" });
            NavigationViewControl.MenuItems.Add(new muxc.NavigationViewItem()
            { Content = "Cutscenes", Icon = new SymbolIcon(Symbol.Video), Tag = "cutscenes" });
            NavigationViewControl.MenuItems.Add(new muxc.NavigationViewItem()
            { Content = "Game Settings", Icon = new SymbolIcon(Symbol.Library), Tag = "gamesettings" });

            // set the initial SelectedItem 
            foreach (muxc.NavigationViewItemBase item in NavigationViewControl.MenuItems)
            {
                if (item is muxc.NavigationViewItem && item.Tag.ToString() == "launcher")
                {
                    NavigationViewControl.SelectedItem = item;
                    break;
                }
            }
        }

        private void NavView_ItemInvoked(muxc.NavigationView sender, muxc.NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                // LauncherFrame.Navigate(typeof(SettingsPage));
            }
            else
            {
                // find NavigationViewItem with Content that equals InvokedItem
                var item = sender.MenuItems.OfType<muxc.NavigationViewItem>().First(x => (string)x.Content == (string)args.InvokedItem);
                NavView_Navigate(item);
            }
        }

        string previousTag = string.Empty;
        private void NavView_Navigate(muxc.NavigationViewItem item)
        {
            // Prevent repeated call of pages
            if (!(previousTag == (string)item.Tag))
            {
                switch (item.Tag)
                {
                    case "launcher":
                        LauncherFrame.Navigate(typeof(Pages.HomePage));
                        HideBackgroundImage(false);
                        previousTag = (string)item.Tag;
                        break;

                    case "repair":
                        LauncherFrame.Navigate(typeof(Pages.RepairPage));
                        HideBackgroundImage();
                        previousTag = (string)item.Tag;
                        break;

                    case "caches":
                        LauncherFrame.Navigate(typeof(Pages.CachesPage));
                        HideBackgroundImage();
                        previousTag = (string)item.Tag;
                        break;
                        /*

                    case "games":
                        LauncherFrame.Navigate(typeof(GamesPage));
                        break;

                    case "music":
                        LauncherFrame.Navigate(typeof(MusicPage));
                        break;

                    case "content":
                        LauncherFrame.Navigate(typeof(MyContentPage));
                        break;

                        */
                }
                LogWriteLine($"Page changed to {item.Content}", Hi3Helper.LogType.Scheme);
            }
        }

        private void HideBackgroundImage(bool hideImage = true)
        {
            Storyboard storyboardFront = new Storyboard();
            Storyboard storyboardBack = new Storyboard();

            if (!(hideImage && BackgroundFront.Opacity == 0))
            {
                DoubleAnimation OpacityAnimation = new DoubleAnimation();
                OpacityAnimation.From = hideImage ? 1 : 0;
                OpacityAnimation.To = hideImage ? 0 : 1;
                OpacityAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.25));

                DoubleAnimation OpacityAnimationBack = new DoubleAnimation();
                OpacityAnimationBack.From = hideImage ? 0.50 : 0.30;
                OpacityAnimationBack.To = hideImage ? 0.30 : 0.50;
                OpacityAnimationBack.Duration = new Duration(TimeSpan.FromSeconds(0.25));

                Storyboard.SetTarget(OpacityAnimation, BackgroundFront);
                Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
                storyboardFront.Children.Add(OpacityAnimation);

                Storyboard.SetTarget(OpacityAnimationBack, BackgroundBack);
                Storyboard.SetTargetProperty(OpacityAnimationBack, "Opacity");
                storyboardBack.Children.Add(OpacityAnimationBack);
            }

            storyboardFront.Begin();
            storyboardBack.Begin();
        }

        private static void UpdateTitleBar(Color accentColor)
        {
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.BackgroundColor = accentColor;
            titleBar.ButtonBackgroundColor = accentColor;
            titleBar.InactiveBackgroundColor = accentColor;
            titleBar.ButtonInactiveBackgroundColor = accentColor;
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            UpdateTitleBarLayout(sender);
        }

        private void UpdateTitleBarLayout(CoreApplicationViewTitleBar coreTitleBar)
        {
            // Update title bar control size as needed to account for system size changes.
            AppTitleBar.Height = coreTitleBar.Height;

            // Ensure the custom title bar does not overlap window caption controls
            Thickness currMargin = AppTitleBar.Margin;
            AppTitleBar.Margin = new Thickness(currMargin.Left, currMargin.Top, coreTitleBar.SystemOverlayRightInset, currMargin.Bottom);
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
        private void Current_Activated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            SolidColorBrush defaultForegroundBrush = (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            SolidColorBrush inactiveForegroundBrush = (SolidColorBrush)Application.Current.Resources["TextFillColorDisabledBrush"];

            if (e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.Deactivated)
            {
                AppTitle.Foreground = inactiveForegroundBrush;
            }
            else
            {
                AppTitle.Foreground = defaultForegroundBrush;
            }
        }

        // Update the TitleBar content layout depending on NavigationView DisplayMode
        private void NavigationViewControl_DisplayModeChanged(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewDisplayModeChangedEventArgs args)
        {
            const int topIndent = 16;
            const int expandedIndent = 48;
            int minimalIndent = 104;

            // If the back button is not visible, reduce the TitleBar content indent.
            if (NavigationViewControl.IsBackButtonVisible.Equals(Microsoft.UI.Xaml.Controls.NavigationViewBackButtonVisible.Collapsed))
            {
                minimalIndent = 48;
            }

            Thickness currMargin = AppTitleBar.Margin;

            // Set the TitleBar margin dependent on NavigationView display mode
            if (sender.PaneDisplayMode == Microsoft.UI.Xaml.Controls.NavigationViewPaneDisplayMode.Top)
            {
                AppTitleBar.Margin = new Thickness(topIndent, currMargin.Top, currMargin.Right, currMargin.Bottom);
            }
            else if (sender.DisplayMode == Microsoft.UI.Xaml.Controls.NavigationViewDisplayMode.Minimal)
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
