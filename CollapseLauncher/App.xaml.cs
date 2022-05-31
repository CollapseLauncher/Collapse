using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Windows.Storage;

using Windows.UI;
using Windows.UI.ViewManagement;

using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;

using WinRT;

using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.InvokeProp;

using static Hi3Helper.Logger;
using static Hi3Helper.Locale;

using static CollapseLauncher.ArgumentParser;
using static CollapseLauncher.InnerLauncherConfig;

namespace CollapseLauncher
{
    public partial class App : Application
    {
        public static bool IsAppKilled = false;
        public static bool IsGameRunning = false;

        public App()
        {
            try
            {
                this.InitializeComponent();
                RequestedTheme = CurrentRequestedAppTheme = GetAppTheme();

                switch (m_appMode)
                {
                    case AppMode.Updater:
                        m_window = new UpdaterWindow();
                        break;
                    case AppMode.Launcher:
                        m_window = new MainWindow();
                        break;
                }

                m_window.Activate();
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL ERROR ON APP INITIALIZER LEVEL!!!\r\n{ex}", LogType.Error, true);
                LogWriteLine("\r\nif you sure that this is not intended, please report it to: https://github.com/neon-nyan/CollapseLauncher/issues\r\nPress any key to quit...");
                Console.ReadLine();
            }
        }

        private Window m_window;
    }
}
