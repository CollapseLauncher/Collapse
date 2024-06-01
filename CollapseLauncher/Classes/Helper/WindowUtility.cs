#nullable enable
    using CollapseLauncher.Extension;
    using CollapseLauncher.FileDialogCOM;
    using CollapseLauncher.Helper.Background;
    using Hi3Helper;
    using Hi3Helper.Screen;
    using Hi3Helper.Shared.Region;
    using Microsoft.Graphics.Display;
    using Microsoft.UI;
    using Microsoft.UI.Composition.SystemBackdrops;
    using Microsoft.UI.Input;
    using Microsoft.UI.Windowing;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;
    using System;
    using System.Runtime.InteropServices;
    using Windows.Foundation;
    using Windows.Graphics;
    using Windows.UI;
    using WinRT.Interop;
    using Size = System.Drawing.Size;
    using WindowId = Microsoft.UI.WindowId;

    namespace CollapseLauncher.Helper
    {
        internal enum WindowBackdropKind
        {
            Acrylic,
            Mica,
            MicaAlt,
            None
        }

        internal static class WindowUtility
        {
            private static event EventHandler<RectInt32[]>? DragAreaChangeEvent;
            private static nint                             OldWindowWndProcPtr;

            private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, UIntPtr wParam, IntPtr lParam);

            internal static nint                 CurrentWindowPtr = nint.Zero;
            internal static Window?              CurrentWindow;
            internal static AppWindow?           CurrentAppWindow;
            internal static WindowId?            CurrentWindowId;
            internal static OverlappedPresenter? CurrentOverlappedPresenter;

            internal static DisplayArea? CurrentWindowDisplayArea
            {
                get
                {
                    if (!CurrentWindowId.HasValue)
                    {
                        return null;
                    }

                    return DisplayArea.GetFromWindowId(CurrentWindowId.Value, DisplayAreaFallback.Primary);
                }
            }

            internal static DisplayInformation? CurrentWindowDisplayInformation => CurrentWindowId.HasValue
                ? DisplayInformation.CreateForWindowId(CurrentWindowId.Value)
                : null;

            internal static DisplayAdvancedColorInfo? CurrentWindowDisplayColorInfo
            {
                get
                {
                    DisplayInformation? displayInfo = CurrentWindowDisplayInformation;
                    if (displayInfo == null)
                    {
                        return null;
                    }

                    return displayInfo.GetAdvancedColorInfo();
                }
            }

            internal static bool CurrentWindowIsMaximizable
            {
                get => CurrentOverlappedPresenter != null && CurrentOverlappedPresenter.IsMaximizable;
                set
                {
                    if (CurrentOverlappedPresenter == null)
                    {
                        return;
                    }

                    CurrentOverlappedPresenter.IsMaximizable = value;
                }
            }

            internal static bool CurrentWindowIsResizable
            {
                get => CurrentOverlappedPresenter != null && CurrentOverlappedPresenter.IsResizable;
                set
                {
                    if (CurrentOverlappedPresenter == null)
                    {
                        return;
                    }

                    CurrentOverlappedPresenter.IsResizable = value;
                }
            }

            internal static nint CurrentWindowMonitorPtr
            {
                get
                {
                    DisplayArea? displayArea = CurrentWindowDisplayArea;
                    if (displayArea == null)
                    {
                        return nint.Zero;
                    }

                    nint monitorPtr = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);
                    return monitorPtr;
                }
            }

            internal static uint CurrentWindowMonitorDpi
            {
                get
                {
                    const uint DefaultDpiValue = 96;
                    nint       monitorPtr      = CurrentWindowMonitorPtr;
                    if (monitorPtr == nint.Zero)
                    {
                        return DefaultDpiValue;
                    }

                    if (InvokeProp.GetDpiForMonitor(monitorPtr, InvokeProp.Monitor_DPI_Type.MDT_Default, out uint dpi,
                                                    out uint _) == 0)
                    {
                        return dpi;
                    }

                    Logger.LogWriteLine($"[WindowUtility::CurrentWindowMonitorDpi] Could not get DPI for the current monitor at 0x{monitorPtr:x8}");
                    return DefaultDpiValue;

                }
            }

            internal static double CurrentWindowMonitorScaleFactor
                => (CurrentWindowMonitorDpi * 100 + (96 >> 1)) / 96 / 100.0;

            internal static Rect CurrentWindowPosition
            {
                get => CurrentWindow?.Bounds ?? default;
                set
                {
                    if (CurrentWindowPtr == nint.Zero || CurrentAppWindow == null)
                    {
                        return;
                    }

                    CurrentAppWindow.ResizeClient(new SizeInt32
                    {
                        Width  = (int)value.Width,
                        Height = (int)value.Height
                    });
                    CurrentAppWindow.Move(new PointInt32
                    {
                        X = (int)value.X,
                        Y = (int)value.Y
                    });
                }
            }

            internal static string? CurrentWindowTitle
            {
                get => CurrentAppWindow?.Title;
                set
                {
                    if (CurrentAppWindow == null)
                    {
                        return;
                    }

                    CurrentAppWindow.Title = value;
                }
            }

            internal static bool CurrentWindowTitlebarExtendContent
            {
                get => CurrentWindow?.ExtendsContentIntoTitleBar ?? false;
                set
                {
                    if (CurrentWindow == null)
                    {
                        return;
                    }

                    CurrentWindow.ExtendsContentIntoTitleBar = value;
                }
            }

            internal static TitleBarHeightOption CurrentWindowTitlebarHeightOption
            {
                get => CurrentAppWindow?.TitleBar.PreferredHeightOption ?? TitleBarHeightOption.Standard;
                set
                {
                    if (CurrentAppWindow == null)
                    {
                        return;
                    }

                    CurrentAppWindow.TitleBar.PreferredHeightOption = value;
                }
            }

            internal static IconShowOptions CurrentWindowTitlebarIconShowOption
            {
                get => CurrentAppWindow?.TitleBar.IconShowOptions ?? IconShowOptions.ShowIconAndSystemMenu;
                set
                {
                    if (CurrentAppWindow == null)
                    {
                        return;
                    }

                    CurrentAppWindow.TitleBar.IconShowOptions = value;
                }
            }

            internal static void RegisterWindow(this Window window)
            {
                // Uninstall existing drag area change
                UninstallDragAreaChangeMonitor();

                CurrentWindow    = window;
                CurrentWindowPtr = WindowNative.GetWindowHandle(window);
                CurrentWindowId  = Win32Interop.GetWindowIdFromWindow(CurrentWindowPtr);

                if (!CurrentWindowId.HasValue)
                {
                    throw new NullReferenceException($"Window ID cannot be retrieved from pointer: 0x{CurrentWindowPtr:x8}");
                }

                CurrentAppWindow           = AppWindow.GetFromWindowId(CurrentWindowId.Value);
                CurrentOverlappedPresenter = CurrentAppWindow.Presenter as OverlappedPresenter;

                // Install WndProc callback
                InstallWndProcCallback();

                // Install Drag Area Change monitor
                InstallDragAreaChangeMonitor();

                // Apply fix for mouse event
                ApplyWindowTitlebarContextFix();

                // Apply fix for Window border on Windows 11
                ApplyWindowBorderFix();

                // Initialize FileDialogHandler
                FileDialogNative.InitHandlerPointer(CurrentWindowPtr);
            }

            #region Drag Area Handler

            private static void InstallDragAreaChangeMonitor()
            {
                DragAreaChangeEvent += WindowDragAreaChangeEventHandler;
            }

            private static void UninstallDragAreaChangeMonitor()
            {
                DragAreaChangeEvent -= WindowDragAreaChangeEventHandler;
            }

            private static void WindowDragAreaChangeEventHandler(object? sender, RectInt32[] dragArea)
            {
                if (sender is AppWindow appWindow)
                {
                    appWindow.TitleBar.SetDragRectangles(dragArea);
                }
            }

            #endregion

            #region WndProc Handler

            private static void InstallWndProcCallback()
            {
                // Install WndProc hook
                const int       GWLP_WNDPROC         = -4;
                WndProcDelegate mNewWndProcDelegate = WndProc;
                IntPtr          pWndProc             = Marshal.GetFunctionPointerForDelegate(mNewWndProcDelegate);
                OldWindowWndProcPtr = InvokeProp.SetWindowLongPtr(CurrentWindowPtr, GWLP_WNDPROC, pWndProc);
            }

            private static IntPtr WndProc(IntPtr hwnd, uint msg, UIntPtr wParam, IntPtr lParam)
            {
                const uint WM_SYSCOMMAND    = 0x0112;
                const uint WM_SHOWWINDOW    = 0x0018;
                const uint WM_ACTIVATEAPP   = 0x001C;
                const uint WM_NCHITTEST     = 0x0084;
                const uint WM_NCCALCSIZE    = 0x0083;
                const uint WM_SETTINGCHANGE = 0x001A;
                const uint WM_ACTIVATE      = 0x0006;

                switch (msg)
                {
                    case WM_ACTIVATEAPP:
                    {
                        if (wParam == 1)
                        {
                            MainPage.CurrentBackgroundHandler?.WindowFocused();
                        }
                        else
                        {
                            MainPage.CurrentBackgroundHandler?.WindowUnfocused();
                        }
                        break;
                    }
                    case WM_SYSCOMMAND:
                    {
                        const uint SC_MAXIMIZE = 0xF030;
                        const uint SC_MINIMIZE = 0xF020;
                        const uint SC_RESTORE  = 0xF120;
                        switch (wParam)
                        {
                            case SC_MAXIMIZE:
                            {
                                // TODO: Apply to force disable the "double-click to maximize" feature.
                                //       The feature should expected to be disabled while m_presenter.IsResizable and IsMaximizable
                                //       set to false. But the feature is still not respecting the changes in WindowsAppSDK 1.4.
                                //
                                //       Issues have been described here:
                                //       https://github.com/microsoft/microsoft-ui-xaml/issues/8666
                                //       https://github.com/microsoft/microsoft-ui-xaml/issues/8783

                                // Ignore WM_SYSCOMMAND SC_MAXIMIZE message
                                // Thank you Microsoft :)
                                return 0;
                            }
                            case SC_MINIMIZE:
                            {
                                MainPage.CurrentBackgroundHandler?.WindowUnfocused();
                                if (LauncherConfig.GetAppConfigValue("MinimizeToTray").ToBool())
                                {
                                    // Carousel is handled inside WM_SHOWWINDOW message for minimize to tray
                                    if (CurrentWindow is MainWindow mainWindow && mainWindow._TrayIcon != null)
                                    {
                                        mainWindow._TrayIcon.ToggleAllVisibility();
                                    }

                                    return 0;
                                }

                                InnerLauncherConfig.m_homePage?.CarouselStopScroll();
                                break;
                            }
                            case SC_RESTORE:
                            {
                                MainPage.CurrentBackgroundHandler?.WindowFocused();
                                InnerLauncherConfig.m_homePage?.CarouselRestartScroll();
                                break;
                            }
                        }

                        break;
                    }
                    case WM_SHOWWINDOW:
                    {
                        if (wParam == 0)
                        {
                            InnerLauncherConfig.m_homePage?.CarouselStopScroll();
                        }
                        else
                        {
                            MainPage.CurrentBackgroundHandler?.WindowFocused();
                            InnerLauncherConfig.m_homePage?.CarouselRestartScroll();
                        }

                        break;
                    }
                    case WM_NCHITTEST:
                    {
                        const int HTCLIENT    = 1;
                        const int HTCAPTION   = 2;
                        const int HTMINBUTTON = 8;
                        const int HTTOP       = 12;

                        var result = InvokeProp.CallWindowProc(OldWindowWndProcPtr, hwnd, msg, wParam, lParam);
                        return result switch
                               {
                                   // Fix "Ghost Minimize Button" issue
                                   HTMINBUTTON => HTCLIENT,
                                   // Fix "Caption Resize" issue
                                   HTTOP => HTCAPTION,
                                   _ => result
                               };
                    }
                    case WM_NCCALCSIZE:
                    {
                        if (!InnerLauncherConfig.m_isWindows11 && wParam == 1)
                        {
                            return 0;
                        }

                        break;
                    }
                    case WM_ACTIVATE:
                        // TODO: Find the exact root-cause of this.
                        // ????? IDK WHAT'S HAPPENING... MICROSOFT!!!!!!!
                        // When the WndProc is called with message WM_ACTIVATE and WM_ACTIVATEAPP,
                        // it sets the wParam to 0x200001 instead of 0x1 (WA_ACTIVE) and
                        // WM_SYSCOMMAND message is ignored.
                        // 
                        // Reference to WM_ACTIVATE message:
                        // https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-activate
                        // 
                        // So in order to fix this, if WA_ACTIVE has wParam being set to 0x200001 then
                        // force the "restore window" call.
                        // 
                        // But this fix won't remove the "bell" sound while the window is getting restored.
                        // 
                        // Additional Note - WndProc routine sequence while retrying to restore window
                        // with Video-BG being used:
                        // PARAM: WM_GETICON             0x7F wParam: 0x1        lParam: 0x0
                        // PARAM: WM_GETICON             0x7F wParam: 0x1        lParam: 0x0
                        // PARAM: WM_WINDOWPOSCHANGING   0x46 wParam: 0x0        lParam: 0x6500B7DE10
                        // PARAM: WM_WINDOWPOSCHANGED    0x47 wParam: 0x0        lParam: 0x6500B7DE10
                        // PARAM: WM_ACTIVATEAPP         0x1C wParam: 0x1        lParam: 0x345C
                        // PARAM: WM_NCACTIVATE          0x86 wParam: 0x200001   lParam: 0x0
                        // PARAM: WM_ACTIVATE            0x6  wParam: 0x200001   lParam: 0x0
                        // PARAM: WM_NCACTIVATE          0x86 wParam: 0x0        lParam: 0x0
                        // PARAM: WM_ACTIVATE            0x6  wParam: 0x200000   lParam: 0x0
                        // PARAM: WM_ACTIVATEAPP         0x1C wParam: 0x0        lParam: 0x365C
                        if (0x200001 == wParam)
                        {
                            WindowRestore();
                            return 0; // Return 0 as the message gets processed
                        }
                        break;
                    case WM_SETTINGCHANGE:
                    {
                        var setting = Marshal.PtrToStringAnsi(lParam);
                        if (setting == "ImmersiveColorSet")
                        {
                            InvokeProp.SetPreferredAppMode(InvokeProp.ShouldAppsUseDarkMode()
                                                               ? InvokeProp.PreferredAppMode.AllowDark
                                                               : InvokeProp.PreferredAppMode.Default);
                        }

                        break;
                    }
                }

                return InvokeProp.CallWindowProc(OldWindowWndProcPtr, hwnd, msg, wParam, lParam);
            }

            #endregion

            #region Titlebar Methods

            private static void ApplyWindowTitlebarContextFix()
            {
                if (!CurrentWindowId.HasValue)
                {
                    return;
                }

                InputNonClientPointerSource incps = InputNonClientPointerSource.GetForWindowId(CurrentWindowId.Value);
                incps.SetRegionRects(NonClientRegionKind.Close,    null);
                incps.SetRegionRects(NonClientRegionKind.Minimize, null);
                EnableWindowNonClientArea();
            }

            internal static void ApplyWindowTitlebarLegacyColor()
            {
                UIElementExtensions.SetApplicationResource("WindowCaptionForeground",
                                                           InnerLauncherConfig.IsAppThemeLight
                                                               ? new Color { A = 255, B = 0, G   = 0, R   = 0 }
                                                               : new Color { A = 255, B = 255, G = 255, R = 255 });
                UIElementExtensions.SetApplicationResource("WindowCaptionBackground",
                                                           new SolidColorBrush(new Color { A = 0, B = 0, G = 0, R = 0 }));
                UIElementExtensions.SetApplicationResource("WindowCaptionBackgroundDisabled",
                                                           new SolidColorBrush(new Color { A = 0, B = 0, G = 0, R = 0 }));
            }

            internal static void SetWindowBackdrop(WindowBackdropKind kind)
            {
                if (CurrentWindow == null)
                {
                    return;
                }

                CurrentWindow.SystemBackdrop = kind switch
                                               {
                                                   WindowBackdropKind.Acrylic => new DesktopAcrylicBackdrop(),
                                                   WindowBackdropKind.Mica => !InnerLauncherConfig.m_isWindows11
                                                       ? new DesktopAcrylicBackdrop()
                                                       : new MicaBackdrop(),
                                                   WindowBackdropKind.MicaAlt => !InnerLauncherConfig.m_isWindows11
                                                       ? new DesktopAcrylicBackdrop()
                                                       : new MicaBackdrop { Kind = MicaKind.BaseAlt },
                                                   _ => null
                                               };
            }

            internal static void SetWindowTitlebarDefaultDragArea()
            {
                if (CurrentWindow == null)
                {
                    return;
                }

                double scaleFactor = CurrentWindowMonitorScaleFactor;
                RectInt32[] dragRects =
                    [new(0, 0, (int)(CurrentWindow.Bounds.Width * scaleFactor), (int)(48 * scaleFactor))];

                DragAreaChangeEvent?.Invoke(CurrentAppWindow, dragRects);
            }

            internal static void SetWindowTitlebarDragArea(RectInt32[] dragAreas)
            {
                DragAreaChangeEvent?.Invoke(CurrentAppWindow, dragAreas);
            }

            internal static void SetWindowTitlebarIcon(nint smallIconPtr, nint largeIconPtr)
            {
                if (smallIconPtr == nint.Zero || largeIconPtr == nint.Zero)
                {
                    return;
                }

                InvokeProp.SetWindowIcon(CurrentWindowPtr, smallIconPtr, largeIconPtr);
            }

            #endregion

            #region Window state methods

            private static void ApplyWindowBorderFix()
            {
                // Hide window border but keep drop shadow
                if (!InnerLauncherConfig.m_isWindows11)
                {
                    var margin = new InvokeProp.MARGINS
                    {
                        cyBottomHeight = 1
                    };
                    InvokeProp.DwmExtendFrameIntoClientArea(CurrentWindowPtr, ref margin);

                    var flags = InvokeProp.SetWindowPosFlags.SWP_NOSIZE
                                | InvokeProp.SetWindowPosFlags.SWP_NOMOVE
                                | InvokeProp.SetWindowPosFlags.SWP_NOZORDER
                                | InvokeProp.SetWindowPosFlags.SWP_FRAMECHANGED;
                    InvokeProp.SetWindowPos(CurrentWindowPtr, 0, 0, 0, 0, 0, flags);
                }
            }

            internal static void SetWindowSize(int width, int height)
            {
                if (CurrentWindowPtr == nint.Zero)
                {
                    return;
                }

                // We have no title bar
                const int SM_CYCAPTION      = 4;
                const int SM_CYSIZEFRAME    = 33;
                const int SM_CXPADDEDBORDER = 92;
                var titleBarHeight = InvokeProp.GetSystemMetrics(SM_CYSIZEFRAME) +
                                     InvokeProp.GetSystemMetrics(SM_CYCAPTION) +
                                     InvokeProp.GetSystemMetrics(SM_CXPADDEDBORDER);

                // Get the scale factor and calculate the size and offset
                double scaleFactor      = CurrentWindowMonitorScaleFactor;
                int    lastWindowWidth  = (int)(width * scaleFactor);
                int    lastWindowHeight = (int)(height * scaleFactor);

                Size desktopSize = ScreenProp.GetScreenSize();
                int  xOff        = desktopSize.Width / 2 - lastWindowWidth / 2;
                int  yOff        = desktopSize.Height / 2 - lastWindowHeight / 2;

                // Use CurrentWindowPosition to change the size and position
                CurrentWindowPosition = new Rect
                    { Height = lastWindowHeight - titleBarHeight, Width = lastWindowWidth, X = xOff, Y = yOff };
            }

            internal static void WindowMinimize()
            {
                if (CurrentWindowPtr == nint.Zero)
                {
                    return;
                }

                const uint WM_SYSCOMMAND = 0x0112;
                const uint SC_MINIMIZE   = 0xF020;
                InvokeProp.SendMessage(CurrentWindowPtr, WM_SYSCOMMAND, SC_MINIMIZE, 0);
            }

            internal static void WindowRestore()
            {
                if (CurrentWindowPtr == nint.Zero)
                {
                    return;
                }

                const uint WM_SYSCOMMAND = 0x0112;
                const uint SC_RESTORE    = 0xF120;
                InvokeProp.SendMessage(CurrentWindowPtr, WM_SYSCOMMAND, SC_RESTORE, 0);
            }

            internal static void EnableWindowNonClientArea()
            {
                if (!CurrentWindowId.HasValue || CurrentAppWindow == null)
                {
                    return;
                }

                double scaleFactor = CurrentWindowMonitorScaleFactor;
                var    incps       = InputNonClientPointerSource.GetForWindowId(CurrentWindowId.Value);
                var safeArea = new RectInt32[]
                {
                    new(CurrentAppWindow.Size.Width - (int)((144 + 12) * scaleFactor), 0, (int)((144 + 12) * scaleFactor),
                        (int)(48 * scaleFactor))
                };
                incps.SetRegionRects(NonClientRegionKind.Passthrough, safeArea);
            }

            internal static void DisableWindowNonClientArea()
            {
                if (!CurrentWindowId.HasValue || CurrentAppWindow == null)
                {
                    return;
                }

                double scaleFactor = CurrentWindowMonitorScaleFactor;
                var    incps       = InputNonClientPointerSource.GetForWindowId(CurrentWindowId.Value);
                var    safeArea    = new RectInt32[] { new(0, 0, CurrentAppWindow.Size.Width, (int)(48 * scaleFactor)) };
                incps.SetRegionRects(NonClientRegionKind.Passthrough, safeArea);
            }

            internal static bool IsCurrentWindowInFocus()
            {
                IntPtr currentForegroundWindow = InvokeProp.GetForegroundWindow();
                return CurrentWindowPtr == currentForegroundWindow;
            }

            #endregion

            #region Tray Icon Invoker

            /// <summary>
            ///     <inheritdoc cref="TrayIcon.ToggleMainVisibility" />
            /// </summary>
            public static void ToggleToTray_MainWindow()
            {
                if (CurrentWindow is MainWindow window)
                {
                    window._TrayIcon.ToggleMainVisibility();
                }
            }

            /// <summary>
            ///     <inheritdoc cref="TrayIcon.ToggleAllVisibility" />
            /// </summary>
            public static void ToggleToTray_AllWindow()
            {
                if (CurrentWindow is MainWindow window)
                {
                    window._TrayIcon.ToggleAllVisibility();
                }
            }

            #endregion
        }
    }