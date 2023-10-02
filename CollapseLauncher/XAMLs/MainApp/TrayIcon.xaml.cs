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
        #region Properties
        private int? lastConsoleStatus;
        private IntPtr consoleWindowHandle = InvokeProp.GetConsoleWindow();
        #endregion

        #region Locales
        private string ShowApp = Lang._Misc.Taskbar_ShowApp;
        private string HideApp = Lang._Misc.Taskbar_HideApp;
        private string ShowConsole = Lang._Misc.Taskbar_ShowConsole;
        private string HideConsole = Lang._Misc.Taskbar_HideConsole;
        private string ExitApp = Lang._Misc.Taskbar_ExitApp;

        private string Preview = Lang._Misc.BuildChannelPreview;
        private string Stable = Lang._Misc.BuildChannelStable;
        #endregion

        public TrayIcon()
        {
            this.InitializeComponent();
            CollapseTaskbar.ToolTipText = string.Format("Collapse Launcher v{0} {1}", AppCurrentVersion.VersionString, LauncherConfig.IsPreview ? Preview : Stable);
            MainTaskbarToggle.Text = (m_appMode == AppMode.StartOnTray) ? ShowApp : HideApp;
            CloseButton.Text = ExitApp;

            if (LauncherConfig.GetAppConfigValue("EnableConsole").ToBool())
            {
                ConsoleTaskbarToggle.Text = HideConsole;
                ConsoleTaskbarToggle.Visibility = Visibility.Visible;
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [RelayCommand]
        public void ToggleMainVisibility()
        {
            IntPtr mainWindowHandle = m_windowHandle;
            bool isVisible = IsWindowVisible(mainWindowHandle);

            if (isVisible)
            {
                WindowExtensions.Hide(m_window);
                MainTaskbarToggle.Text = ShowApp;
                LogWriteLine("Main window is hidden!");
            }
            else
            {
                WindowExtensions.Show(m_window);
                SetForegroundWindow(mainWindowHandle);
                MainTaskbarToggle.Text = HideApp;
                LogWriteLine("Main window is shown!");
            }
        }

        [RelayCommand]
        public void ToggleConsoleVisibility()
        {
            if (InvokeProp.m_consoleHandle == IntPtr.Zero) return;
            if (IsWindowVisible(consoleWindowHandle))
            {
                LoggerConsole.DisposeConsole();
                ConsoleTaskbarToggle.Text = ShowConsole;
                LogWriteLine("Console Hidden!");
                lastConsoleStatus = 0;
            }
            else
            {
                LoggerConsole.AllocateConsole();
                SetForegroundWindow(InvokeProp.GetConsoleWindow());
                ConsoleTaskbarToggle.Text = HideConsole;
                LogWriteLine("Console Visible!");
                lastConsoleStatus = 5;
            }
        }

        [RelayCommand]
        public void BringToForeground()
        {
            IntPtr mainWindowHandle = m_windowHandle;
            bool isMainWindowVisible = IsWindowVisible(mainWindowHandle);
            if (!isMainWindowVisible)
                WindowExtensions.Show(m_window);
            SetForegroundWindow(mainWindowHandle);

            if (LauncherConfig.GetAppConfigValue("EnableConsole").ToBool())
            {
                if (!IsWindowVisible(consoleWindowHandle))
                {
                    LoggerConsole.AllocateConsole();
                    lastConsoleStatus = 5;
                }
                SetForegroundWindow(consoleWindowHandle);
            }
        }

        [RelayCommand]
        public void ToggleAllVisibility()
        {
            ToggleConsoleVisibility();
            ToggleMainVisibility();
        }

        [RelayCommand]
        public void CloseApp()
        {
            App.Current.Exit();
        }
    }
}
