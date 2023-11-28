using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using static CollapseLauncher.FileDialogCOM.FileDialogNative;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.InvokeProp;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using SizeN = System.Drawing.Size;

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

        public void InitializeWindowProperties(bool startOOBE = false)
        {
            try
            {
                InitializeWindowSettings();
                if (m_appWindow.TitleBar.ExtendsContentIntoTitleBar) m_appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

                if (IsFirstInstall || startOOBE == true)
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
            // rootFrame.Navigate(typeof(Prototype.MainPageNew), null, new DrillInNavigationTransitionInfo());
        }

        private void InitializeAppWindowAndIntPtr()
        {
            this.InitializeComponent();
            this.Activate();
            this.Closed += (_, _) => { App.IsAppKilled = true; };
            RunSetDragAreaQueue();
            // Initialize Window Handlers
            m_windowHandle = GetActiveWindow();
            m_windowID = Win32Interop.GetWindowIdFromWindow(m_windowHandle);
            m_appWindow = AppWindow.GetFromWindowId(m_windowID);
            m_presenter = m_appWindow.Presenter as OverlappedPresenter;

            string title = $"Collapse";
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
                m_appWindow.TitleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;

                m_presenter.IsResizable = false;
                m_presenter.IsMaximizable = false;

                m_appWindow.TitleBar.ButtonBackgroundColor = new Windows.UI.Color { A = 0, B = 0, G = 0, R = 0 };
                m_appWindow.TitleBar.ButtonInactiveBackgroundColor = new Windows.UI.Color { A = 0, B = 0, G = 0, R = 0 };

                // Hide system menu
                var controlsHwnd = FindWindowEx(m_windowHandle, 0, "ReunionWindowingCaptionControls", "ReunionCaptionControlsWindow");
                if (controlsHwnd != IntPtr.Zero)
                {
                    DestroyWindow(controlsHwnd);
                }

                // Fix mouse event
                var incps = InputNonClientPointerSource.GetForWindowId(m_windowID);
                incps.SetRegionRects(NonClientRegionKind.Close, null);
                incps.SetRegionRects(NonClientRegionKind.Minimize, null);
                var safeArea = new RectInt32[] { new(m_appWindow.Size.Width - (int)((144 + 12) * m_appDPIScale), 0, (int)((144 + 12) * m_appDPIScale), (int)(48 * m_appDPIScale)) };
                incps.SetRegionRects(NonClientRegionKind.Passthrough, safeArea);
            }
            else
            {
                // Shouldn't happen
                // https://learn.microsoft.com/en-us/windows/apps/develop/title-bar#colors

                m_presenter.IsResizable = false;
                m_presenter.IsMaximizable = false;
                ExtendsContentIntoTitleBar = false;
                AppTitleBar.Visibility = Visibility.Collapsed;
            }

            MainFrameChangerInvoker.WindowFrameEvent += MainFrameChangerInvoker_WindowFrameEvent;
            LauncherUpdateInvoker.UpdateEvent += LauncherUpdateInvoker_UpdateEvent;

            // Install WndProc hook
            const int GWLP_WNDPROC = -4;
            m_newWndProcDelegate = (WndProcDelegate)WndProc;
            IntPtr pWndProc = Marshal.GetFunctionPointerForDelegate(m_newWndProcDelegate);
            m_oldWndProc = SetWindowLongPtr(m_windowHandle, GWLP_WNDPROC, pWndProc);
        }

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, UIntPtr wParam, IntPtr lParam);

        private IntPtr WndProc(IntPtr hwnd, uint msg, UIntPtr wParam, IntPtr lParam)
        {
            const uint WM_SYSCOMMAND = 0x0112;
            const uint SC_MAXIMIZE = 0xF030;
            if (msg == WM_SYSCOMMAND && wParam == SC_MAXIMIZE)
            {
                // Ignore WM_SYSCOMMAND SC_MAXIMIZE message
                // Thank you Microsoft :)
                return 1;
            }
            return CallWindowProc(m_oldWndProc, hwnd, msg, wParam, lParam);
        }

        private void SetLegacyTitleBarColor()
        {
            Application.Current.Resources["WindowCaptionForeground"] = IsAppThemeLight ? new Windows.UI.Color { A = 255, B = 0, G = 0, R = 0 } : new Windows.UI.Color { A = 255, B = 255, G = 255, R = 255 };
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

        private TypedEventHandler<AppWindow, AppWindowChangedEventArgs> _eventWindowPosChange;
        private int _lastWindowWidth;
        private int _lastWindowHeight;
        private WindowRect _windowPosAndSize = new WindowRect();

        public void SetWindowSize(IntPtr hwnd, int width = 1028, int height = 634)
        {
            if (hwnd == IntPtr.Zero) hwnd = m_windowHandle;

            var dpi = GetDpiForWindow(hwnd);
            float scalingFactor = (float)dpi / 96;
            _lastWindowWidth = (int)(width * scalingFactor);
            _lastWindowHeight = (int)(height * scalingFactor);

            SizeN desktopSize = Hi3Helper.Screen.ScreenProp.GetScreenSize();
            int xOff = (desktopSize.Width - _lastWindowWidth) / 2;
            int yOff = (desktopSize.Height - _lastWindowHeight) / 2;

            // Old Interop ver. Call
            // SetWindowPos(hwnd, (IntPtr)SpecialWindowHandles.HWND_TOP,
            //                             xOff, yOff, width, height,
            //                             SetWindowPosFlags.SWP_SHOWWINDOW);

            // New m_appWindow built-in Move and Resize
            m_appWindow.MoveAndResize(new RectInt32
            {
                Width = _lastWindowWidth,
                Height = _lastWindowHeight,
                X = xOff,
                Y = yOff
            });
            AssignCurrentWindowPosition(hwnd);

            // TODO: Apply to force disable the "double-click to maximize" feature.
            //       The feature should expected to be disabled while m_presenter.IsResizable and IsMaximizable
            //       set to false. But the feature is still not respecting the changes in WindowsAppSDK 1.4.
            //       
            //       Issues have been described here:
            //       https://github.com/microsoft/microsoft-ui-xaml/issues/8666
            //       https://github.com/microsoft/microsoft-ui-xaml/issues/8783
            //       
            // Also TODO: If a fix hasn't been applied yet, then find the way to disable the
            //            "double-click 2 maximize" feature via Native Methods/PInvoke.
            AssignWinAppSDK14WindowSizeFix(hwnd);
        }

        private void AssignWinAppSDK14WindowSizeFix(IntPtr hwnd)
        {
            if (_eventWindowPosChange != null) m_appWindow.Changed -= _eventWindowPosChange;
            _eventWindowPosChange = (sender, args) =>
            {
                if (args.DidSizeChange && args.DidPositionChange
                && !args.DidPresenterChange)
                {
                    lock (this)
                    {
                        AssignCurrentWindowPosition(hwnd);
                        if (m_windowPosSize.X > (_lastWindowWidth * -1)
                         && m_windowPosSize.Y > (_lastWindowHeight * -1))
                        {
                            m_presenter.Restore();
                            sender.MoveAndResize(new RectInt32
                            {
                                Width = _lastWindowWidth,
                                Height = _lastWindowHeight,
                                X = (int)m_windowPosSize.X,
                                Y = (int)m_windowPosSize.Y
                            });
                        }
                    }
                }
            };

            m_appWindow.Changed += _eventWindowPosChange;
        }

        private void AssignCurrentWindowPosition(IntPtr hwnd)
        {
            GetWindowRect(hwnd, ref _windowPosAndSize);
            Rect lastRect = this.Bounds;

            lastRect.X = _windowPosAndSize.Left;
            lastRect.Y = _windowPosAndSize.Top;

            m_windowPosSize = lastRect;
        }

        public static void SetInitialDragArea()
        {
            double scaleAdjustment = m_appDPIScale;
            RectInt32[] dragRects = new RectInt32[] { new RectInt32(0, 0, (int)(m_window.Bounds.Width * scaleAdjustment), (int)(48 * scaleAdjustment)) };

            SetDragArea(dragRects);
        }

        public static void SetDragArea(RectInt32[] area)
        {
            if (m_appWindow.TitleBar != null && m_windowSupportCustomTitle && m_appWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                titleBarDragQueue.Add(area);
            }
        }

        private static List<RectInt32[]> titleBarDragQueue = new List<RectInt32[]>();

        private static async void RunSetDragAreaQueue()
        {
            while (!App.IsAppKilled)
            {
                while (titleBarDragQueue.Count > 0)
                {
                    m_appWindow.TitleBar.SetDragRectangles(titleBarDragQueue[0]);
                    titleBarDragQueue.RemoveAt(0);
                }
                titleBarDragQueue.Clear();
                await Task.Delay(250);
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (GetAppConfigValue("MinimizeToTray").ToBool())
            {
                TrayIcon.ToggleAllVisibility();
            }
            else m_presenter.Minimize();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
