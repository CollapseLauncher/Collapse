using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics;
using WinRT.Interop;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.InvokeProp;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            try
            {
                this.InitializeComponent();
                m_window = this;

                string title = $"Collapse Launcher - v{AppCurrentVersion} ";
                if (IsPreview)
                    this.Title = title += "[PREVIEW]";
#if DEBUG
                this.Title = title += "[DEBUG]";
#endif
#if PORTABLE
                this.Title += "[PORTABLE]";
#endif

                if (IsFirstInstall)
                {
                    TryInitWindowHandler();
                    ExtendsContentIntoTitleBar = false;
                    SetWindowSize(m_windowHandle, 360, 230);
                    SetLegacyTitleBarColor();
                    m_presenter.IsResizable = false;
                    m_presenter.IsMaximizable = false;
                    rootFrame.Navigate(typeof(StartupLanguageSelect), null, new DrillInNavigationTransitionInfo());
                }
                else
                    StartMainPage();
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", Hi3Helper.LogType.Error, true);
                Console.ReadLine();
            }
        }

        public void StartSetupPage()
        {
            InitializeWindowSettings();
            rootFrame.Navigate(typeof(Pages.StartupPage), null, new DrillInNavigationTransitionInfo());
        }

        public void StartMainPage()
        {
            InitializeWindowSettings();
            rootFrame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }

        public void TryInitWindowHandler()
        {
            m_backDrop = new BackdropManagement(this);
            m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
            m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();
            m_appDPIScale = GetScaleAdjustment();

            m_AppWindow = GetAppWindowForCurrentWindow();
            m_AppWindow.Changed += AppWindow_Changed;
        }

        public void InitializeWindowSettings()
        {
            TryInitWindowHandler();
            SetWindowSize(m_windowHandle);

            // Check to see if customization is supported.
            // Currently only supported on Windows 11.
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
#if MICA
                m_backDrop.SetBackdrop(BackdropType.Mica);
#elif DISABLETRANSPARENT
                m_backDrop.SetBackdrop(BackdropType.DefaultColor);
#else
                m_backDrop.SetBackdrop(BackdropType.DesktopAcrylic);
#endif
                SetThemeParameters();
                var titleBar = m_AppWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                AppTitleBar.Loaded += AppTitleBar_Loaded;
                AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;

                m_presenter.IsResizable = false;
                m_presenter.IsMaximizable = false;
                CustomTitleBar.Visibility = Visibility.Collapsed;

                switch (GetAppTheme())
                {
                    case ApplicationTheme.Light:
                        m_AppWindow.TitleBar.ButtonForegroundColor = new Windows.UI.Color { A = 255, B = 0, G = 0, R = 0 };
                        break;
                    case ApplicationTheme.Dark:
                        m_AppWindow.TitleBar.ButtonForegroundColor = new Windows.UI.Color { A = 255, B = 255, G = 255, R = 255 };
                        break;
                }

                m_AppWindow.TitleBar.ButtonBackgroundColor = new Windows.UI.Color { A = 0, B = 0, G = 0, R = 0 };
                m_AppWindow.TitleBar.ButtonInactiveBackgroundColor = new Windows.UI.Color { A = 0, B = 0, G = 0, R = 0 };
            }
            else
            {
#if DISABLETRANSPARENT
                m_backDrop.SetBackdrop(BackdropType.DefaultColor);
#else
                m_backDrop.SetBackdrop(BackdropType.DesktopAcrylic);
#endif
                SetThemeParameters();
                m_presenter.IsResizable = false;
                m_presenter.IsMaximizable = false;
                Grid.SetColumn(RegionChangerPanel, 4);
                RegionChangerPanel.HorizontalAlignment = HorizontalAlignment.Right;
                ExtendsContentIntoTitleBar = true;
                AppTitleBar.Visibility = Visibility.Collapsed;
                CustomTitleBar.Visibility = Visibility.Visible;
                SetTitleBar(CustomTitleBar);
                SetLegacyTitleBarColor();
            }

            MainFrameChangerInvoker.WindowFrameEvent += MainFrameChangerInvoker_WindowFrameEvent;
            LauncherUpdateInvoker.UpdateEvent += LauncherUpdateInvoker_UpdateEvent;
        }

        private void SetLegacyTitleBarColor()
        {
            switch (GetAppTheme())
            {
                case ApplicationTheme.Light:
                    Application.Current.Resources["WindowCaptionForeground"] = new Windows.UI.Color { A = 255, B = 0, G = 0, R = 0 };
                    break;
                case ApplicationTheme.Dark:
                    Application.Current.Resources["WindowCaptionForeground"] = new Windows.UI.Color { A = 255, B = 255, G = 255, R = 255 };
                    break;
            }

            Application.Current.Resources["WindowCaptionBackground"] = new SolidColorBrush(new Windows.UI.Color { A = 0, B = 0, G = 0, R = 0 });
            Application.Current.Resources["WindowCaptionBackgroundDisabled"] = new SolidColorBrush(new Windows.UI.Color { A = 0, B = 0, G = 0, R = 0 });
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

        public async void HideRootFrame(bool hide)
        {
            Storyboard storyboardBack = new Storyboard();

            DoubleAnimation OpacityAnimationBack = new DoubleAnimation();
            OpacityAnimationBack.From = hide ? 1 : 0;
            OpacityAnimationBack.To = hide ? 0 : 1;
            OpacityAnimationBack.Duration = new Duration(TimeSpan.FromSeconds(0.25));

            Storyboard.SetTarget(OpacityAnimationBack, rootFrame);
            Storyboard.SetTargetProperty(OpacityAnimationBack, "Opacity");
            storyboardBack.Children.Add(OpacityAnimationBack);

            storyboardBack.Begin();

            await Task.Delay(250);
        }

        private void MainFrameChangerInvoker_WindowFrameEvent(object sender, MainFrameProperties e)
        {
            rootFrame.Navigate(e.FrameTo, null, e.Transition);
        }

        public void SetWindowSize(IntPtr hwnd, int width = 1280, int height = 730, int x = 0, int y = 0)
        {
            if (hwnd == IntPtr.Zero) hwnd = m_windowHandle;

            var dpi = GetDpiForWindow(hwnd);
            float scalingFactor = (float)dpi / 96;
            width = (int)(width * scalingFactor);
            height = (int)(height * scalingFactor);

            SetWindowPos(hwnd, (IntPtr)SpecialWindowHandles.HWND_TOP,
                                        x, y, width, height,
                                        SetWindowPosFlags.SWP_NOMOVE);
        }

        private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                SetDragRegionForCustomTitleBar(m_AppWindow);
            }
        }

        private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (AppWindowTitleBar.IsCustomizationSupported()
                && m_AppWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                SetDragRegionForCustomTitleBar(m_AppWindow);
            }
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            m_windowHandle = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(m_windowHandle);
            AppWindow window = AppWindow.GetFromWindowId(wndId);
            m_presenter = window.Presenter as OverlappedPresenter;
            m_windowHandle = WindowNative.GetWindowHandle(this);
            return window;
        }

        [DllImport("Shcore.dll", SetLastError = true)]
        internal static extern int GetDpiForMonitor(IntPtr hmonitor, Monitor_DPI_Type dpiType, out uint dpiX, out uint dpiY);

        internal enum Monitor_DPI_Type : int
        {
            MDT_Effective_DPI = 0,
            MDT_Angular_DPI = 1,
            MDT_Raw_DPI = 2,
            MDT_Default = MDT_Effective_DPI
        }

        private double GetScaleAdjustment()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            DisplayArea displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
            IntPtr hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);

            int result = GetDpiForMonitor(hMonitor, Monitor_DPI_Type.MDT_Default, out uint dpiX, out uint _);
            if (result != 0)
            {
                throw new Exception("Could not get DPI for monitor.");
            }

            uint scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
            return scaleFactorPercent / 100.0;
        }

        private void SetDragRegionForCustomTitleBar(AppWindow appWindow)
        {
            if (AppWindowTitleBar.IsCustomizationSupported()
                && appWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                double scaleAdjustment = m_appDPIScale;

                RightPaddingColumn.Width = new GridLength(appWindow.TitleBar.RightInset / scaleAdjustment);

                List<RectInt32> dragRectsList = new();

                RectInt32 dragRectL;
                dragRectL.X = (int)((LeftPaddingColumn.ActualWidth) * scaleAdjustment);
                dragRectL.Y = 0;
                dragRectL.Height = (int)(AppTitleBar.ActualHeight * scaleAdjustment);
                dragRectL.Width = (int)((IconColumn.ActualWidth
                                        + TitleColumn.ActualWidth) * scaleAdjustment);
                dragRectsList.Add(dragRectL);

                RectInt32 dragRectR;
                dragRectR.X = (int)((LeftPaddingColumn.ActualWidth
                                    + IconColumn.ActualWidth
                                    + TitleTextBlock.ActualWidth
                                    + SearchColumn.ActualWidth) * scaleAdjustment) + 24;
                dragRectR.Y = 0;
                dragRectR.Height = (int)(AppTitleBar.ActualHeight * scaleAdjustment);
                dragRectR.Width = (int)(RightDragColumn.ActualWidth * scaleAdjustment) - 24;
                dragRectsList.Add(dragRectR);

                RectInt32[] dragRects = dragRectsList.ToArray();

                appWindow.TitleBar.SetDragRectangles(dragRects);
            }
        }

        PointInt32 LastPos;
        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            // TEMPORARY HACK:
            // This one to prevent app to maximize since Maximize button in Windows 10 cannot be disabled.
            if (args.DidPresenterChange && !AppWindowTitleBar.IsCustomizationSupported())
            {
                if (m_AppWindow.Position.X > -128 || m_AppWindow.Position.Y > -128)
                    m_presenter.Restore();

                sender.Move(LastPos);
                SetWindowSize(m_windowHandle);
            }

            if (!(m_AppWindow.Position.X < 0 || m_AppWindow.Position.Y < 0))
                LastPos = m_AppWindow.Position;
        }
    }
}
