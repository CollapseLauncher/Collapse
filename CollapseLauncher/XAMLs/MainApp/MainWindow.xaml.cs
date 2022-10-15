using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics;
using WinRT.Interop;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.FileDialogNative;
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
                m_window = this;

                string title = $"Collapse";
#if PORTABLE
                this.Title += " Portable";
#endif
                if (IsPreview)
                    this.Title = title += " Preview";
#if DEBUG
                this.Title = title += " [Debug]";
#endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", Hi3Helper.LogType.Error, true);
                Console.ReadLine();
            }
        }

        public void InitializeWindowProperties()
        {
            try
            {
                InitializeWindowSettings();

                if (IsFirstInstall)
                {
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
                LogWriteLine($"Failure while initializing window properties!!!\r\n{ex}", Hi3Helper.LogType.Error, true);
                Console.ReadLine();
            }
        }

        public void StartSetupPage()
        {
            SetWindowSize(m_windowHandle);
            rootFrame.Navigate(typeof(Pages.StartupPage), null, new DrillInNavigationTransitionInfo());
        }

        public void StartMainPage()
        {
            SetWindowSize(m_windowHandle);
            rootFrame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }

        private void InitializeAppWindowAndIntPtr()
        {
            this.InitializeComponent();
            this.Activate();
            // Initialize Window Handlers
#if DISABLE_COM
            m_windowHandle = GetActiveWindow();
#else
            m_windowHandle = WindowNative.GetWindowHandle(this);
#endif
            m_windowID = Win32Interop.GetWindowIdFromWindow(m_windowHandle);
            m_appWindow = AppWindow.GetFromWindowId(m_windowID);
            m_appWindow.Changed += AppWindow_Changed;
            m_presenter = m_appWindow.Presenter as OverlappedPresenter;
            DisplayArea displayArea = DisplayArea.GetFromWindowId(m_windowID, DisplayAreaFallback.Primary);

            // Get Monitor DPI
            IntPtr hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);
            if (GetDpiForMonitor(hMonitor, Monitor_DPI_Type.MDT_Default, out uint dpiX, out uint _) != 0)
            {
                throw new Exception("Could not get DPI for monitor.");
            }

            m_appDPIScale = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96) / 100.0;

            InitHandlerPointer(m_windowHandle);
        }

        private void LoadWindowIcon()
        {
            string path = Path.Combine(AppFolder, "icon.ico");
            if (!File.Exists(path)) return;
            m_appWindow.SetIcon(path);
        }

        public void InitializeWindowSettings()
        {

#if !DISABLE_COM
            m_backDrop = new BackdropManagement(this);
            m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
            m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();
#endif

            InitializeAppWindowAndIntPtr();
            LoadWindowIcon();

            SetWindowSize(m_windowHandle);

            // Check to see if customization is supported.
            // Currently only supported on Windows 11.
            // m_windowSupportCustomTitle = AppWindowTitleBar.IsCustomizationSupported();

            if (m_windowSupportCustomTitle)
            {
#if MICA && !DISABLE_COM
                m_backDrop.SetBackdrop(BackdropType.Mica);
#elif DISABLETRANSPARENT && !DISABLE_COM
                m_backDrop.SetBackdrop(BackdropType.DefaultColor);
#elif !DISABLE_COM
                m_backDrop.SetBackdrop(BackdropType.DesktopAcrylic);
#endif
                SetThemeParameters();
                AppTitleBar.Loaded += AppTitleBar_Loaded;
                m_appWindow.TitleBar.ExtendsContentIntoTitleBar = true;

                m_presenter.IsResizable = false;
                m_presenter.IsMaximizable = false;
                CustomTitleBar.Visibility = Visibility.Collapsed;

                switch (GetAppTheme())
                {
                    case ApplicationTheme.Light:
                        m_appWindow.TitleBar.ButtonForegroundColor = new Windows.UI.Color { A = 255, B = 0, G = 0, R = 0 };
                        break;
                    case ApplicationTheme.Dark:
                        m_appWindow.TitleBar.ButtonForegroundColor = new Windows.UI.Color { A = 255, B = 255, G = 255, R = 255 };
                        break;
                }

                m_appWindow.TitleBar.ButtonBackgroundColor = new Windows.UI.Color { A = 0, B = 0, G = 0, R = 0 };
                m_appWindow.TitleBar.ButtonInactiveBackgroundColor = new Windows.UI.Color { A = 0, B = 0, G = 0, R = 0 };
            }
            else
            {
#if DISABLETRANSPARENT && !DISABLE_COM
                m_backDrop.SetBackdrop(BackdropType.DefaultColor);
#elif !DISABLE_COM
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

        private void MainFrameChangerInvoker_WindowFrameEvent(object sender, MainFrameProperties e)
        {
            rootFrame.Navigate(e.FrameTo, null, e.Transition);
        }

        public void SetWindowSize(IntPtr hwnd, int width = 1196, int height = 730, int x = 0, int y = 0)
        {
            if (hwnd == IntPtr.Zero) hwnd = m_windowHandle;

            var dpi = GetDpiForWindow(hwnd);
            float scalingFactor = (float)dpi / 96;
            width = (int)(width * scalingFactor);
            height = (int)(height * scalingFactor);

            SetWindowPos(hwnd, (IntPtr)SpecialWindowHandles.HWND_TOP,
                                        x, y, width, height,
                                        SetWindowPosFlags.SWP_NOMOVE);

            m_windowPosSize = this.Bounds;
        }

        private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                SetInitialDragArea();
            }
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

        public static void SetInitialDragArea()
        {
            if (AppWindowTitleBar.IsCustomizationSupported()
                && m_appWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                double scaleAdjustment = m_appDPIScale;
                RectInt32[] dragRects = new RectInt32[] { new RectInt32(0, 0, (int)(m_window.Bounds.Width * scaleAdjustment), (int)(48 * scaleAdjustment)) };

                SetDragArea(dragRects);
            }
        }

        public static void SetDragArea(RectInt32[] area) => m_appWindow.TitleBar.SetDragRectangles(area);

        PointInt32 LastPos;
        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            // TEMPORARY HACK:
            // This one to prevent app to maximize since Maximize button in Windows 10 cannot be disabled.
            if (args.DidPresenterChange && !m_windowSupportCustomTitle)
            {
                if (m_appWindow.Position.X > -128 || m_appWindow.Position.Y > -128)
                    m_presenter.Restore();

                sender.Move(LastPos);
                SetWindowSize(m_windowHandle);
            }

            if (!(m_appWindow.Position.X < 0 || m_appWindow.Position.Y < 0))
                LastPos = m_appWindow.Position;
        }
    }
}
