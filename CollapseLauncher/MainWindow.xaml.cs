using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;

using Windows.Graphics;

using WinRT.Interop;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

using System.Runtime.InteropServices;

using static CollapseLauncher.AppConfig;
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

                string title = $"Collapse Launcher - v{Assembly.GetExecutingAssembly().GetName().Version} ";
                if (IsPreview)
                    title = title + "[PREVIEW]";
#if DEBUG
                title = title + "[DEBUG]";
#endif
                this.Title = title;

                m_AppWindow = GetAppWindowForCurrentWindow();
                m_AppWindow.Changed += AppWindow_Changed;

                SetWindowSize(m_windowHandle, 1280, 730);

                // Check to see if customization is supported.
                // Currently only supported on Windows 11.
                if (AppWindowTitleBar.IsCustomizationSupported())
                {
                    var titleBar = m_AppWindow.TitleBar;
                    titleBar.ExtendsContentIntoTitleBar = true;
                    AppTitleBar.Loaded += AppTitleBar_Loaded;
                    AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;

                    m_presenter.IsResizable = false;
                    m_presenter.IsMaximizable = false;
                    CustomTitleBar.Visibility = Visibility.Collapsed;

                    m_AppWindow.TitleBar.ButtonBackgroundColor = new Windows.UI.Color { A = 0, B = 0, G = 0, R = 0 };
                    m_AppWindow.TitleBar.ButtonInactiveBackgroundColor = new Windows.UI.Color { A = 0, B = 0, G = 0, R = 0 };
                }
                else
                {
                    m_presenter.IsResizable = false;
                    m_presenter.IsMaximizable = false;
                    Grid.SetColumn(RegionChangerPanel, 4);
                    RegionChangerPanel.HorizontalAlignment = HorizontalAlignment.Right;
                    ExtendsContentIntoTitleBar = true;
                    AppTitleBar.Visibility = Visibility.Collapsed;
                    CustomTitleBar.Visibility = Visibility.Visible;
                    SetTitleBar(CustomTitleBar);
                    Application.Current.Resources["WindowCaptionBackground"] = new SolidColorBrush(new Windows.UI.Color { A = 0, B = 0, G = 0, R = 0 });
                    Application.Current.Resources["WindowCaptionBackgroundDisabled"] = new SolidColorBrush(new Windows.UI.Color { A = 0, B = 0, G = 0, R = 0 });
                }

                MainFrameChangerInvoker.WindowFrameEvent += MainFrameChangerInvoker_WindowFrameEvent;
                LauncherUpdateInvoker.UpdateEvent += LauncherUpdateInvoker_UpdateEvent;

                if (!File.Exists(AppConfigFile))
                    rootFrame.Navigate(typeof(Pages.StartupPage), null, new DrillInNavigationTransitionInfo());
                else
                    rootFrame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", Hi3Helper.LogType.Error, true);
                Console.ReadLine();
            }
        }

        private void LauncherUpdateInvoker_UpdateEvent(object sender, LauncherUpdateProperty e)
        {
            if (e.QuitFromUpdateMenu)
            {
                overlayFrame.Navigate(typeof(Pages.NullPage), null, new EntranceNavigationTransitionInfo());
                return;
            }

            if (e.IsUpdateAvailable)
                overlayFrame.Navigate(typeof(Pages.UpdatePage));
        }

        private void MainFrameChangerInvoker_WindowFrameEvent(object sender, MainFrameProperties e)
        {
            rootFrame.Navigate(e.FrameTo, null, e.Transition);
        }

        private void SetWindowSize(IntPtr hwnd, int width, int height, int x = 0, int y = 0)
        {
            var dpi = PInvoke.User32.GetDpiForWindow(hwnd);
            float scalingFactor = (float)dpi / 96;
            width = (int)(width * scalingFactor);
            height = (int)(height * scalingFactor);

            PInvoke.User32.SetWindowPos(hwnd, PInvoke.User32.SpecialWindowHandles.HWND_TOP,
                                        x, y, width, height,
                                        PInvoke.User32.SetWindowPosFlags.SWP_NOMOVE);
        }

        private void Minimize(object sender, RoutedEventArgs e) => m_presenter.Minimize();
        private void Close(object sender, RoutedEventArgs e) => Application.Current.Exit();

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
                double scaleAdjustment = GetScaleAdjustment();

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
        int i = 0;
        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            // TEMPORARY HACK:
            // This one to prevent app to maximize since Maximize button in Windows 10 cannot be disabled.
            if (args.DidPresenterChange && !AppWindowTitleBar.IsCustomizationSupported())
            {
                if (m_AppWindow.Position.X > -128 || m_AppWindow.Position.Y > -128)
                    m_presenter.Restore();

                sender.Move(LastPos);
                SetWindowSize(m_windowHandle, 1280, 730);
            }

            if (!(m_AppWindow.Position.X < 0 || m_AppWindow.Position.Y < 0))
                LastPos = m_AppWindow.Position;
        }
    }
}
