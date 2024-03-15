using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using System;
using System.Drawing;
using Microsoft.UI.Xaml;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.Pages.HomePage;
using static Hi3Helper.InvokeProp;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    public sealed partial class TrayIcon
    {
        #region Locales
        private readonly string _popupHelp1 = Lang._Misc.Taskbar_PopupHelp1;
        private readonly string _popupHelp2 = Lang._Misc.Taskbar_PopupHelp2;

        private readonly string _showApp     = Lang._Misc.Taskbar_ShowApp;
        private readonly string _hideApp     = Lang._Misc.Taskbar_HideApp;
        private readonly string _showConsole = Lang._Misc.Taskbar_ShowConsole;
        private readonly string _hideConsole = Lang._Misc.Taskbar_HideConsole;
        private readonly string _exitApp     = Lang._Misc.Taskbar_ExitApp;

        // ReSharper disable UnusedMember.Local
        private readonly string _preview = Lang._Misc.BuildChannelPreview;
        private readonly string _stable = Lang._Misc.BuildChannelStable;
        // ReSharper restore UnusedMember.Local
        #endregion

        #region Main
        public TrayIcon()
        {
            this.InitializeComponent();

            string instanceIndicator = "";
            var instanceCount = MainEntryPoint.GetInstanceCount();

            if (instanceCount > 1)
            {
                instanceIndicator = $"- #{instanceCount}";
            }
            
            var isPreview = LauncherConfig.IsPreview;
            if (isPreview) CollapseTaskbar.ContextMenuMode = ContextMenuMode.SecondWindow;
#if DEBUG
            CollapseTaskbar.ToolTipText =
                $"Collapse Launcher v{AppCurrentVersion.VersionString}d - Commit {ThisAssembly.Git.Commit}{instanceIndicator}\r\n" +
                $"{_popupHelp1}\r\n" +
                $"{_popupHelp2}";  
#else
            CollapseTaskbar.ToolTipText = 
                $"Collapse Launcher v{AppCurrentVersion.VersionString} {(isPreview ? _preview : _stable)}{instanceIndicator}\r\n" +
                $"{_popupHelp1}\r\n" +
                $"{_popupHelp2}";
#endif
            CloseButton.Text = _exitApp;

            // Switch toggle text to see if its started with Start
            MainTaskbarToggle.Text = (m_appMode == AppMode.StartOnTray) ? _showApp : _hideApp;
            ConsoleTaskbarToggle.Text = (m_appMode == AppMode.StartOnTray) ? _showConsole : _hideConsole;

            CollapseTaskbar.Icon = Icon.FromHandle(LauncherConfig.AppIconSmall);
            CollapseTaskbar.Visibility = Visibility.Visible;
        }

        public void Dispose()
        {
            CollapseTaskbar.Dispose();
        }
        #endregion

        #region Visibility Toggler
        /// <summary>
        /// Using H.NotifyIcon's WindowExtension to toggle visibility of main window (m_window)
        /// </summary>
        [RelayCommand]
        private void ToggleMainVisibilityButton() => ToggleMainVisibility();

        /// <summary>
        /// Toggle console visibility using LoggerConsole's DisposeConsole//AllocateConsole
        /// </summary>
        [RelayCommand]
        private void ToggleConsoleVisibilityButton() => ToggleConsoleVisibility();

        /// <summary>
        /// Toggle both main and console visibility while avoiding flip flop condition
        /// </summary>
        [RelayCommand]
        private void ToggleAllVisibilityInvoke() => ToggleAllVisibility();

        /// <summary>
        /// Using user32's SetForegroundWindow to pull both windows to foreground
        /// </summary>
        [RelayCommand]
        private void BringToForegroundInvoke() => BringToForeground();

        /// <summary>
        /// Update tray context menu
        /// </summary>
        [RelayCommand]
        private void UpdateContextMenuInvoke() => UpdateContextMenu();

        /// <summary>
        /// Close app
        /// </summary>
        [RelayCommand]
        private void CloseApp() => (m_window as MainWindow)?.CloseApp();
        #endregion

        #region Taskbar Public Methods
        /// <summary>
        /// Toggle console window visibility
        /// </summary>
        public void ToggleConsoleVisibility(bool forceShow = false)
        {
            if (LauncherConfig.GetAppConfigValue("EnableConsole").ToBool())
            {
                IntPtr consoleWindowHandle = InvokeProp.GetConsoleWindow();
                if (InvokeProp.m_consoleHandle == IntPtr.Zero) return;
                if (IsWindowVisible(consoleWindowHandle) && !forceShow)
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
        /// Toggle main window visibility
        /// </summary>
        public void ToggleMainVisibility(bool forceShow = false)
        {
            IntPtr mainWindowHandle = m_windowHandle;
            var    isVisible        = IsWindowVisible(mainWindowHandle);

            if (isVisible && !forceShow)
            {
                WindowExtensions.Hide(m_window);
                MainTaskbarToggle.Text     = _showApp;
                // Increase refresh rate to 1000ms when main window is hidden
                RefreshRate = RefreshRateSlow;
                LogWriteLine("Main window is hidden!");
            }
            else
            {
                WindowExtensions.Show(m_window);
                SetForegroundWindow(mainWindowHandle);
                MainTaskbarToggle.Text = _hideApp;
                // Revert refresh rate to its default
                RefreshRate = RefreshRateDefault;
                LogWriteLine("Main window is shown!");
            }
        }

        /// <summary>
        /// Toggle both main and console windows visibility
        /// </summary>
        public void ToggleAllVisibility()
        {
            IntPtr consoleWindowHandle = GetConsoleWindow();
            IntPtr mainWindowHandle    = m_windowHandle;
            bool   isMainWindowVisible = IsWindowVisible(mainWindowHandle);

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

        /// <summary>
        /// Bring all windows into foreground, also brought all windows if they were hidden in taskbar.
        /// </summary>
        public void BringToForeground()
        {
            IntPtr mainWindowHandle    = m_windowHandle;
            IntPtr consoleWindowHandle = InvokeProp.GetConsoleWindow();

            bool isMainWindowVisible = IsWindowVisible(mainWindowHandle);

            if (LauncherConfig.GetAppConfigValue("EnableConsole").ToBool())
            {
                if (!IsWindowVisible(consoleWindowHandle))
                {
                    ToggleConsoleVisibility(true);
                }
                //Stupid workaround for console window not showing up using SetForegroundWindow
                //Basically do minimize then maximize action using ShowWindow 6->9 (nice)
                ShowWindow(consoleWindowHandle, 6);
                ShowWindow(consoleWindowHandle, 9);
                //SetForegroundWindow(consoleWindowHandle);
            }

            if (!isMainWindowVisible)
                ToggleMainVisibility(true);
            ShowWindow(mainWindowHandle, 9);
            SetForegroundWindow(mainWindowHandle);
        }

        /// <summary>
        /// Update tray context menu
        /// </summary>
        public void UpdateContextMenu()
        {
            // Enable visibility toggle for console if the console is enabled
            if (LauncherConfig.GetAppConfigValue("EnableConsole").ToBool())
            {
                ConsoleTaskbarToggle.IsEnabled = true;
            }
            else
            {
                ConsoleTaskbarToggle.IsEnabled = false;
                ConsoleTaskbarToggle.Text = _hideConsole;
            }
            
            // Force refresh all text based on their respective window state
            IntPtr consoleWindowHandle = InvokeProp.GetConsoleWindow();
            IntPtr mainWindowHandle    = m_windowHandle;
            
            bool isMainWindowVisible = IsWindowVisible(mainWindowHandle);
            bool isConsoleVisible    = LauncherConfig.GetAppConfigValue("EnableConsole").ToBool() && IsWindowVisible(consoleWindowHandle);

            if (isConsoleVisible) ConsoleTaskbarToggle.Text = _hideConsole;
            else ConsoleTaskbarToggle.Text                  = _showConsole;

            if (isMainWindowVisible) MainTaskbarToggle.Text = _hideApp;
            else MainTaskbarToggle.Text                     = _showApp;
        }
        #endregion
    }
}
