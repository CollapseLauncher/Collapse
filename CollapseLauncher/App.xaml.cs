using Hi3Helper;
using Hi3Helper.Http;
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
                HttpLogInvoker.DownloadLog += HttpClientLogWatcher;
                this.InitializeComponent();
                RequestedTheme = CurrentRequestedAppTheme = GetAppTheme();

                switch (m_appMode)
                {
                    case AppMode.Updater:
                        m_window = new UpdaterWindow();
                        break;
                    case AppMode.Hi3CacheUpdater:
                    case AppMode.Launcher:
                        m_window = new MainWindow();
                        ((MainWindow)m_window).InitializeWindowProperties();
                        break;
                }

                m_window.Activate();
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL ERROR ON APP INITIALIZER LEVEL!!!\r\n{ex}", LogType.Error, true);
                LogWriteLine("\r\nIf this is not intended, please report it to: https://github.com/neon-nyan/CollapseLauncher/issues\r\nPress any key to exit...");
                Console.ReadLine();
            }
            finally
            {
                HttpLogInvoker.DownloadLog -= HttpClientLogWatcher;
            }
        }

        private void HttpClientLogWatcher(object sender, DownloadLogEvent e)
        {
            LogType severity = e.Severity switch
            {
                DownloadLogSeverity.Warning => LogType.Warning,
                DownloadLogSeverity.Error => LogType.Error,
                _ => LogType.Default
            };

            LogWriteLine(e.Message, severity, true);
        }
    }
}
