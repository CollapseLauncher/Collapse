using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Windowing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

using WinRT;

using PInvoke;

using static CollapseLauncher.LauncherConfig;
using static CollapseLauncher.InvokeProp;
using static Hi3Helper.Logger;
using Hi3Helper;

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
            m_windowHandle = this.As<IWindowNative>().WindowHandle;

            InitializeComponent();
            SetTitleBar(CustomTitleBar);
            GetAppWindowAndPresenter();

            _presenter.IsResizable = false;
            _presenter.IsMaximizable = false;
            ExtendsContentIntoTitleBar = true;

            rootFrame.Navigate(typeof(MainPage));
        }

        private void Minimize(object sender, RoutedEventArgs e) => _presenter.Minimize();
        private void Maximize(object sender, RoutedEventArgs e) => _presenter.Maximize();
        private void Restore(object sender, RoutedEventArgs e) => _presenter.Restore();

        /*
        private void Maximize(object sender, RoutedEventArgs e) => User32.ShowWindow(m_windowHandle, PInvoke.User32.WindowShowStyle.SW_MAXIMIZE);
        private void Restore(object sender, RoutedEventArgs e) => User32.ShowWindow(m_windowHandle, PInvoke.User32.WindowShowStyle.SW_RESTORE);
        */

        private void Close(object sender, RoutedEventArgs e) => Application.Current.Exit();

        public void GetAppWindowAndPresenter()
        {
            WindowId myWndId = Win32Interop.GetWindowIdFromWindow(m_windowHandle);
            _apw = AppWindow.GetFromWindowId(myWndId);
            _presenter = _apw.Presenter as OverlappedPresenter;
        }

        private IntPtr m_windowHandle;
        
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("EECDBF0E-BAE9-4CB6-A68E-9598E1CB57BB")]
        internal interface IWindowNative
        {
            IntPtr WindowHandle { get; }
        }
    }
}
