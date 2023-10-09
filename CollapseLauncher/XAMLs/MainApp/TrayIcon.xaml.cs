using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    public sealed partial class TrayIcon
    {
        #region External Methods
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        #endregion

        #region Locales
        private readonly string _showApp = Lang._Misc.Taskbar_ShowApp;
        private readonly string _hideApp = Lang._Misc.Taskbar_HideApp;
        private readonly string _showConsole = Lang._Misc.Taskbar_ShowConsole;
        private readonly string _hideConsole = Lang._Misc.Taskbar_HideConsole;
        private readonly string _exitApp = Lang._Misc.Taskbar_ExitApp;

        private readonly string _preview = Lang._Misc.BuildChannelPreview;
        private readonly string _stable = Lang._Misc.BuildChannelStable;
        #endregion

        #region Main
        public TrayIcon()
        {
            this.InitializeComponent();
#if DEBUG
            CollapseTaskbar.ToolTipText =
                $"Collapse Launcher v{AppCurrentVersion.VersionString}d {(LauncherConfig.IsPreview ? "DEBUG" : _stable)}";  
#else
            CollapseTaskbar.ToolTipText = 
                $"Collapse Launcher v{AppCurrentVersion.VersionString}d {(LauncherConfig.IsPreview ? _preview : _stable)}"; 
#endif
            CloseButton.Text = _exitApp;

            // Switch toggle text to see if its started with Start
            MainTaskbarToggle.Text = (m_appMode == AppMode.StartOnTray) ? _showApp : _hideApp;
            ConsoleTaskbarToggle.Text = (m_appMode == AppMode.StartOnTray) ? _showConsole : _hideConsole;
            // Show visibility toggle for console if the console is enabled
            if (LauncherConfig.GetAppConfigValue("EnableConsole").ToBool()) ConsoleTaskbarToggle.Visibility = Visibility.Visible;
        }
        #endregion

        #region Visibility Toggler
        /// <summary>
        /// Using H.NotifyIcon's WindowExtension to toggle visibility of main window (m_window)
        /// </summary>
        [RelayCommand]
        public void ToggleMainVisibility()
        {
            IntPtr mainWindowHandle = m_windowHandle;
            var isVisible = IsWindowVisible(mainWindowHandle);

            if (isVisible)
            {
                WindowExtensions.Hide(m_window);
                MainTaskbarToggle.Text = _showApp;
                LogWriteLine("Main window is hidden!");
            }
            else
            {
                WindowExtensions.Show(m_window);
                SetForegroundWindow(mainWindowHandle);
                MainTaskbarToggle.Text = _hideConsole;
                LogWriteLine("Main window is shown!");
            }
        }

        /// <summary>
        /// Toggle console visibility using LoggerConsole's DisposeConsole//AllocateConsole
        /// </summary>
        [RelayCommand]
        public void ToggleConsoleVisibility()
        {
            if (LauncherConfig.GetAppConfigValue("EnableConsole").ToBool())
            {
                IntPtr consoleWindowHandle = InvokeProp.GetConsoleWindow();
                if (InvokeProp.m_consoleHandle == IntPtr.Zero) return;
                if (IsWindowVisible(consoleWindowHandle))
                {
                    LoggerConsole.DisposeConsole();
                    ConsoleTaskbarToggle.Text = _showConsole;
                    LogWriteLine("Console is hidden!");
                }
                else
                {
                    LoggerConsole.AllocateConsole();
                    SetForegroundWindow(InvokeProp.GetConsoleWindow());
                    ConsoleTaskbarToggle.Text = _hideConsole;
                    LogWriteLine("Console is visible!");
                }
            }
        }

        /// <summary>
        /// Toggle both main and console visibility while avoiding flip flop condition
        /// </summary>
        [RelayCommand]
        public void ToggleAllVisibility()
        {
            IntPtr consoleWindowHandle = InvokeProp.GetConsoleWindow();
            IntPtr mainWindowHandle = m_windowHandle;
            bool isMainWindowVisible = IsWindowVisible(mainWindowHandle);

            bool isConsoleVisible = LauncherConfig.GetAppConfigValue("EnableConsole").ToBool() && IsWindowVisible(consoleWindowHandle);

            if (isMainWindowVisible && !isConsoleVisible)
            {
                ToggleConsoleVisibility();
            }
            if (!isMainWindowVisible && isConsoleVisible)
            {
                ToggleMainVisibility();
            }
            else
            {
                ToggleConsoleVisibility();
                ToggleMainVisibility();
            }
        }
        #endregion

        /// <summary>
        /// Using user32's SetForegroundWindow to pull both windows to foreground
        /// </summary>
        [RelayCommand]
        public void BringToForeground()
        {
            IntPtr mainWindowHandle = m_windowHandle;
            IntPtr consoleWindowHandle = InvokeProp.GetConsoleWindow();

            bool isMainWindowVisible = IsWindowVisible(mainWindowHandle);

            if (LauncherConfig.GetAppConfigValue("EnableConsole").ToBool())
            {
                if (!IsWindowVisible(consoleWindowHandle))
                {
                    LoggerConsole.AllocateConsole();
                }
                //Stupid workaround for console window not showing up using SetForegroundWindow
                //Basically do minimize then maximize action using ShowWindow 6->9 (nice)
                ShowWindow(consoleWindowHandle, 6);
                ShowWindow(consoleWindowHandle, 9);
                //SetForegroundWindow(consoleWindowHandle);
            }

            if (!isMainWindowVisible)
                WindowExtensions.Show(m_window);
            ShowWindow(mainWindowHandle, 9);
            SetForegroundWindow(mainWindowHandle);
        }

        /// <summary>
        /// Close app
        /// </summary>
        [RelayCommand]
        public void CloseApp()
        {
            App.Current.Exit();
        }
    }
}
