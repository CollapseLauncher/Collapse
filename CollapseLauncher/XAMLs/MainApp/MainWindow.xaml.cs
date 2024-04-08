using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.Loading;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.InvokeProp;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow() { }

        public void InitializeWindowProperties(bool startOOBE = false)
        {
            try
            {
                InitializeWindowSettings();
                LoadingMessageHelper.Initialize();

                if (WindowUtility.CurrentWindowTitlebarExtendContent) WindowUtility.CurrentWindowTitlebarHeightOption = TitleBarHeightOption.Tall;

                if (IsFirstInstall || startOOBE)
                {
                    WindowUtility.CurrentWindowTitlebarExtendContent = true;
                    WindowUtility.SetWindowSize(WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Width, WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Height);
                    WindowUtility.ApplyWindowTitlebarLegacyColor();
                    WindowUtility.CurrentWindowIsResizable = false;
                    WindowUtility.CurrentWindowIsMaximizable = false;
                    rootFrame.Navigate(typeof(Pages.OOBE.OOBEStartUpMenu), null, new DrillInNavigationTransitionInfo());
                }
                else
                    StartMainPage();

                TitleBarFrameGrid.EnableImplicitAnimation(true);
                LoadingStatusUIContainer.EnableImplicitAnimation(true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failure while initializing window properties!!!\r\n{ex}", Hi3Helper.LogType.Error, true);
                Console.ReadLine();
            }
        }

        public void StartMainPage()
        {
            WindowUtility.SetWindowSize(WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Width, WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Height);
            rootFrame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }

        private void InitializeAppWindowAndIntPtr()
        {
            this.InitializeComponent();
            this.Activate();
            this.Closed += (_, _) => { App.IsAppKilled = true; };

            // Initialize Window Handlers and register to Window Utility
            WindowUtility.RegisterWindow(this);

            string title = $"Collapse";
            if (IsPreview)
                title += " Preview";
#if DEBUG
            title += " [Debug]";
#endif
            var instanceCount = MainEntryPoint.InstanceCount;
            if (instanceCount > 1)
                title += $" - #{instanceCount}";

            WindowUtility.CurrentWindowTitle = title;
        }

        private void LoadWindowIcon()
        {
            WindowUtility.SetWindowTitlebarIcon(AppIconLarge, AppIconSmall);
            WindowUtility.CurrentWindowTitlebarIconShowOption = IconShowOptions.HideIconAndSystemMenu;
        }

        public void InitializeWindowSettings()
        {
            InitializeAppWindowAndIntPtr();
            LoadWindowIcon();

            WindowUtility.SetWindowSize(WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Width, WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Height);

            // Check to see if customization is supported.
            m_windowSupportCustomTitle = AppWindowTitleBar.IsCustomizationSupported();

            if (m_windowSupportCustomTitle)
            {
                WindowUtility.SetWindowTitlebarDefaultDragArea();
                WindowUtility.CurrentWindowTitlebarExtendContent = true;
                WindowUtility.CurrentWindowTitlebarIconShowOption = IconShowOptions.HideIconAndSystemMenu;

                WindowUtility.CurrentWindowIsResizable = false;
                WindowUtility.CurrentWindowIsMaximizable = false;

                WindowUtility.CurrentAppWindow.TitleBar.ButtonBackgroundColor = new Windows.UI.Color { A = 0, B = 0, G = 0, R = 0 };
                WindowUtility.CurrentAppWindow.TitleBar.ButtonInactiveBackgroundColor = new Windows.UI.Color { A = 0, B = 0, G = 0, R = 0 };

                // Hide system menu
                var controlsHwnd = FindWindowEx(WindowUtility.CurrentWindowPtr, 0, "ReunionWindowingCaptionControls", "ReunionCaptionControlsWindow");
                if (controlsHwnd != IntPtr.Zero)
                {
                    DestroyWindow(controlsHwnd);
                }
            }
            else
            {
                // Shouldn't happen
                // https://learn.microsoft.com/en-us/windows/apps/develop/title-bar#colors

                if (WindowUtility.CurrentOverlappedPresenter != null && WindowUtility.CurrentWindow != null)
                {
                    WindowUtility.CurrentOverlappedPresenter.IsResizable = false;
                    WindowUtility.CurrentOverlappedPresenter.IsMaximizable = false;
                    WindowUtility.CurrentWindowTitlebarExtendContent = false;
                    AppTitleBar.Visibility = Visibility.Collapsed;
                }
            }

            MainFrameChangerInvoker.WindowFrameEvent += MainFrameChangerInvoker_WindowFrameEvent;
            LauncherUpdateInvoker.UpdateEvent += LauncherUpdateInvoker_UpdateEvent;

            m_consoleCtrlHandler += ConsoleCtrlHandler;
            SetConsoleCtrlHandler(m_consoleCtrlHandler, true);
        }

        private bool ConsoleCtrlHandler(uint dwCtrlType)
        {
            ImageLoaderHelper.DestroyWaifu2X();
            return true;
        }

        private void LauncherUpdateInvoker_UpdateEvent(object sender, LauncherUpdateProperty e)
        {
            if (e.QuitFromUpdateMenu)
            {
                overlayFrame.Navigate(typeof(Pages.NullPage), null, new EntranceNavigationTransitionInfo());
                return;
            }

            if (e.IsUpdateAvailable)
            {
                overlayFrame.Navigate(typeof(Pages.UpdatePage));
            }
        }

        private void MainFrameChangerInvoker_WindowFrameEvent(object sender, MainFrameProperties e)
        {
            rootFrame.Navigate(e.FrameTo, null, e.Transition);
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowUtility.WindowMinimize();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseApp();
        
        /// <summary>
        /// Close app and do necessary events before closing
        /// </summary>
        public void CloseApp()
        {
            _TrayIcon?.Dispose();
            Close();
        }
        
        private void MainWindow_OnSizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            // Recalculate non-client area size
            WindowUtility.EnableWindowNonClientArea();
        }
    }
}
