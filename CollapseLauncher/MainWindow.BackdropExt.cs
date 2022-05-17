using System;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;

using Windows.Graphics;

using WinRT;
using WinRT.Interop;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Composition.SystemBackdrops;

using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public class WindowsSystemDispatcherQueueHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        struct DispatcherQueueOptions
        {
            internal int dwSize;
            internal int threadType;
            internal int apartmentType;
        }

        [DllImport("CoreMessaging.dll")]
        private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object dispatcherQueueController);

        object m_dispatcherQueueController = null;
        public void EnsureWindowsSystemDispatcherQueueController()
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            {
                // one already exists, so we'll just use it.
                return;
            }

            if (m_dispatcherQueueController == null)
            {
                DispatcherQueueOptions options;
                options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
                options.threadType = 2;    // DQTYPE_THREAD_CURRENT
                options.apartmentType = 2; // DQTAT_COM_STA

                CreateDispatcherQueueController(options, ref m_dispatcherQueueController);
            }
        }
    }
    public sealed partial class MainWindow : Window
    {
        public void SetThemeParameters()
        {
            switch (m_currentBackdrop)
            {
#if MICA
                case BackdropType.Mica:
                    {
                        (Application.Current.Resources["PagesSolidAcrylicBrush"] as AcrylicBrush).TintOpacity = 0f;
                        (Application.Current.Resources["PagesSolidAcrylicBrush"] as AcrylicBrush).TintLuminosityOpacity = 0f;
                        (Application.Current.Resources["DialogAcrylicBrush"] as AcrylicBrush).TintOpacity = 0f;
                        (Application.Current.Resources["DialogAcrylicBrush"] as AcrylicBrush).TintLuminosityOpacity = 0.75f;
                        (Application.Current.Resources["NavigationBarBrush"] as AcrylicBrush).TintOpacity = 0f;
                        (Application.Current.Resources["NavigationBarBrush"] as AcrylicBrush).TintLuminosityOpacity = 0f;
                    }
                    break;
#endif
                case BackdropType.DefaultColor:
                    {
                        if (CurrentRequestedAppTheme == ApplicationTheme.Dark)
                        {
                            Application.Current.Resources["NavigationBarBrush"] = new AcrylicBrush
                            {
                                TintColor = new Windows.UI.Color { A = 244, R = 34, G = 34, B = 34 },
                                TintOpacity = 1f,
                                TintLuminosityOpacity = 0f
                            };
                            Application.Current.Resources["PagesSolidAcrylicBrush"] = new AcrylicBrush
                            {
                                TintColor = new Windows.UI.Color { A = 244, R = 34, G = 34, B = 34 },
                                TintOpacity = 1f,
                                TintLuminosityOpacity = 0f
                            };
                            Application.Current.Resources["DialogAcrylicBrush"] = new AcrylicBrush
                            {
                                TintColor = new Windows.UI.Color { A = 244, R = 34, G = 34, B = 34 },
                                TintOpacity = 0.4f,
                                TintLuminosityOpacity = 0.5f
                            };
                        }
                    }
                    break;
            }
        }

        public void SetBackdrop(BackdropType type)
        {
            // Reset to default color. If the requested type is supported, we'll update to that.
            // Note: This sample completely removes any previous controller to reset to the default
            //       state. This is done so this sample can show what is expected to be the most
            //       common pattern of an app simply choosing one controller type which it sets at
            //       startup. If an app wants to toggle between Mica and Acrylic it could simply
            //       call RemoveSystemBackdropTarget() on the old controller and then setup the new
            //       controller, reusing any existing m_configurationSource and Activated/Closed
            //       event handlers.
            m_currentBackdrop = BackdropType.DefaultColor;
            if (m_micaController != null)
            {
                m_micaController.Dispose();
                m_micaController = null;
            }
            if (m_acrylicController != null)
            {
                m_acrylicController.Dispose();
                m_acrylicController = null;
            }
            this.Activated -= Window_Activated;
            this.Closed -= Window_Closed;
            m_configurationSource = null;

            if (type == BackdropType.Mica)
                if (TrySetMicaBackdrop())
                    m_currentBackdrop = type;
                else
                    // Mica isn't supported. Try Acrylic.
                    type = BackdropType.DesktopAcrylic;

            if (type == BackdropType.DesktopAcrylic)
                if (TrySetAcrylicBackdrop())
                    m_currentBackdrop = type;
        }

        bool TrySetMicaBackdrop()
        {
            if (MicaController.IsSupported())
            {
                // Hooking up the policy object
                m_configurationSource = new SystemBackdropConfiguration();
                this.Activated += Window_Activated;
                this.Closed += Window_Closed;

                // Initial configuration state.
                m_configurationSource.IsInputActive = true;
                switch (((FrameworkElement)this.Content).ActualTheme)
                {
                    case ElementTheme.Dark: m_configurationSource.Theme = SystemBackdropTheme.Dark; break;
                    case ElementTheme.Light: m_configurationSource.Theme = SystemBackdropTheme.Light; break;
                    case ElementTheme.Default: m_configurationSource.Theme = SystemBackdropTheme.Default; break;
                }

                m_micaController = new MicaController();

                // Enable the system backdrop.
                // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
                m_micaController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                m_micaController.SetSystemBackdropConfiguration(m_configurationSource);
                return true; // succeeded
            }

            return false; // Mica is not supported on this system
        }

        bool TrySetAcrylicBackdrop()
        {
            if (DesktopAcrylicController.IsSupported())
            {
                // Hooking up the policy object
                m_configurationSource = new SystemBackdropConfiguration();
                this.Activated += Window_Activated;
                this.Closed += Window_Closed;

                // Initial configuration state.
                m_configurationSource.IsInputActive = true;
                switch (((FrameworkElement)this.Content).ActualTheme)
                {
                    case ElementTheme.Dark: m_configurationSource.Theme = SystemBackdropTheme.Dark; break;
                    case ElementTheme.Light: m_configurationSource.Theme = SystemBackdropTheme.Light; break;
                    case ElementTheme.Default: m_configurationSource.Theme = SystemBackdropTheme.Default; break;
                }

                m_acrylicController = new Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController();

                // Enable the system backdrop.
                // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
                m_acrylicController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                m_acrylicController.SetSystemBackdropConfiguration(m_configurationSource);
                return true; // succeeded
            }

            return false; // Acrylic is not supported on this system
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args) => m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Make sure any Mica/Acrylic controller is disposed so it doesn't try to
            // use this closed window.
            if (m_micaController != null)
            {
                m_micaController.Dispose();
                m_micaController = null;
            }
            if (m_acrylicController != null)
            {
                m_acrylicController.Dispose();
                m_acrylicController = null;
            }
            this.Activated -= Window_Activated;
            m_configurationSource = null;
        }
    }
}
