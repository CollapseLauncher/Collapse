using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using System;
using System.Runtime.InteropServices;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    public sealed partial class TrayIcon
    {
        public TrayIcon()
        {
            this.InitializeComponent();
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [RelayCommand]
        public void ToggleVisibility()
        {
            IntPtr mainWindowHandle = m_windowHandle;
            bool isVisible = IsWindowVisible(mainWindowHandle);

            if (isVisible)
            {
                WindowExtensions.Hide(m_window);
                TaskbarToggleText.Text = "Show App from Taskbar";
                LogWriteLine("Main window is hidden!");
            }
            else
            {
                WindowExtensions.Show(m_window);
                TaskbarToggleText.Text = "Hide App to Taskbar";
                LogWriteLine("Main window is shown!");
            }
        }

        [RelayCommand]
        public void CloseApp()
        {
            App.Current.Exit();
        }
    }
}
