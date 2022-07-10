using Hi3Helper;
using Microsoft.UI.Xaml;
using System;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Logger;

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
    }
}
