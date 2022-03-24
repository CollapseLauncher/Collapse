using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Windowing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ViewManagement;

using WinRT;

using PInvoke;

using static CollapseLauncher.AppConfig;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CollapseLauncher
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            try
            {
                InitializeComponent();
                SetTitleBar(CustomTitleBar);

                string title = $"Collapse Launcher - v{Assembly.GetExecutingAssembly().GetName().Version} ";
#if PREVIEW
                title = title + "[PREVIEW]";
#endif
#if DEBUG
                title = title + "[DEBUG]";
#endif
                this.Title = title;

                GetAppWindowAndPresenter();

                _presenter.IsResizable = false;
                _presenter.IsMaximizable = false;
                ExtendsContentIntoTitleBar = true;

                MainFrameChangerInvoker.WindowFrameEvent += MainFrameChangerInvoker_WindowFrameEvent;

                if (!File.Exists(AppConfigFile))
                    rootFrame.Navigate(typeof(Pages.StartupPage));
                else
                {
                    rootFrame.Navigate(typeof(MainPage));
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", Hi3Helper.LogType.Error, true);
                Console.ReadLine();
            }
        }

        private void MainFrameChangerInvoker_WindowFrameEvent(object sender, MainFrameProperties e)
        {
            rootFrame.Navigate(e.FrameTo, null, e.Transition);
        }

        private void Minimize(object sender, RoutedEventArgs e) => _presenter.Minimize();
        private void Maximize(object sender, RoutedEventArgs e) => _presenter.Maximize();
        private void Restore(object sender, RoutedEventArgs e) => _presenter.Restore();

        /*
        private void Maximize(object sender, RoutedEventArgs e) => User32.ShowWindow(m_windowHandle, PInvoke.User32.WindowShowStyle.SW_MAXIMIZE);
        private void Restore(object sender, RoutedEventArgs e) => User32.ShowWindow(m_windowHandle, PInvoke.User32.WindowShowStyle.SW_RESTORE);
        */

        private void Close(object sender, RoutedEventArgs e)
        {
            App.IsAppKilled = true;
            Application.Current.Exit();
        }

        public void GetAppWindowAndPresenter()
        {
            m_windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId myWndId = Win32Interop.GetWindowIdFromWindow(m_windowHandle);
            _apw = AppWindow.GetFromWindowId(myWndId);
            _presenter = _apw.Presenter as OverlappedPresenter;
        }

        private IntPtr m_windowHandle;
    }
    public static class AppWindowHandle
    {

        [DllImport("Microsoft.UI.Windowing.Core.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowHandleFromWindowId(WindowId windowId, out IntPtr result);

        [DllImport("Microsoft.UI.Windowing.Core.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowIdFromWindowHandle(IntPtr hwnd, out WindowId result);

        public static AppWindow GetAppWindow(this Microsoft.UI.Xaml.Window window)
        {
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            _ = GetWindowIdFromWindowHandle(windowHandle, out WindowId windowId);
            return AppWindow.GetFromWindowId(windowId);
        }
    }
}
