using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Graphics;
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
                m_appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

                if (IsFirstInstall)
                {
                    ExtendsContentIntoTitleBar = false;
                    SetWindowSize(m_windowHandle, WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Width, WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Height);
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
            SetWindowSize(m_windowHandle, WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Width, WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Height);
            rootFrame.Navigate(typeof(Pages.StartupPage), null, new DrillInNavigationTransitionInfo());
        }

        public void StartMainPage()
        {
            SetWindowSize(m_windowHandle, WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Width, WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Height);
            rootFrame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }

        private void InitializeAppWindowAndIntPtr()
        {
            this.InitializeComponent();
            this.Activate();
            // Initialize Window Handlers
            m_windowHandle = GetActiveWindow();
            m_windowID = Win32Interop.GetWindowIdFromWindow(m_windowHandle);
            m_appWindow = AppWindow.GetFromWindowId(m_windowID);
            m_presenter = m_appWindow.Presenter as OverlappedPresenter;

            string title = $"Collapse";
#if PORTABLE
                title += " Portable";
#endif
            if (IsPreview)
                title += " Preview";
#if DEBUG
            title += " [Debug]";
#endif
            m_appWindow.Title = title;

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
            m_appWindow.TitleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
        }

        public void InitializeWindowSettings()
        {
            InitializeAppWindowAndIntPtr();
            LoadWindowIcon();

            SetWindowSize(m_windowHandle, WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Width, WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Height);

            // Check to see if customization is supported.
            m_windowSupportCustomTitle = AppWindowTitleBar.IsCustomizationSupported();

            if (m_windowSupportCustomTitle)
            {
                SetInitialDragArea();
                m_appWindow.TitleBar.ExtendsContentIntoTitleBar = true;

                m_presenter.IsResizable = false;
                m_presenter.IsMaximizable = false;

                switch (GetAppTheme())
                {
                    case ApplicationTheme.Light:
                        m_appWindow.TitleBar.ButtonForegroundColor = new Windows.UI.Color { A = 255, B = 0, G = 0, R = 0 };
                        m_appWindow.TitleBar.ButtonHoverBackgroundColor = new Windows.UI.Color { A = 96, B = 0, G = 0, R = 0 };
                        break;
                    case ApplicationTheme.Dark:
                        m_appWindow.TitleBar.ButtonForegroundColor = new Windows.UI.Color { A = 255, B = 255, G = 255, R = 255 };
                        m_appWindow.TitleBar.ButtonHoverBackgroundColor = new Windows.UI.Color { A = 64, B = 0, G = 0, R = 0 };
                        break;
                }

                m_appWindow.TitleBar.ButtonBackgroundColor = new Windows.UI.Color { A = 0, B = 0, G = 0, R = 0 };
                m_appWindow.TitleBar.ButtonInactiveBackgroundColor = new Windows.UI.Color { A = 0, B = 0, G = 0, R = 0 };
            }
            else
            {
                m_presenter.IsResizable = false;
                m_presenter.IsMaximizable = false;
                ExtendsContentIntoTitleBar = false;
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

        public void SetWindowSize(IntPtr hwnd, int width = 1028, int height = 634, int x = 0, int y = 0)
        {
            if (hwnd == IntPtr.Zero) hwnd = m_windowHandle;

            var dpi = GetDpiForWindow(hwnd);
            float scalingFactor = (float)dpi / 96;
            width = (int)(width * scalingFactor);
            height = (int)(height * scalingFactor);

            Size desktopSize = Hi3Helper.Screen.ScreenProp.GetScreenSize();
            int xOff = (desktopSize.Width - width) / 2;
            int hOff = (desktopSize.Height - height) / 2;

            SetWindowPos(hwnd, (IntPtr)SpecialWindowHandles.HWND_TOP,
                                        xOff, hOff, width, height,
                                        SetWindowPosFlags.SWP_SHOWWINDOW);

            m_windowPosSize = this.Bounds;
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
            double scaleAdjustment = m_appDPIScale;
            RectInt32[] dragRects = new RectInt32[] { new RectInt32(0, 0, (int)(m_window.Bounds.Width * scaleAdjustment), (int)(48 * scaleAdjustment)) };

            SetDragArea(dragRects);
        }

        public static void SetDragArea(RectInt32[] area)
        {
            if (m_windowSupportCustomTitle && m_appWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                m_appWindow.TitleBar.SetDragRectangles(area);
            }
        }
    }
}
