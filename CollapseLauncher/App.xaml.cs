using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Windows.UI;

using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;

using WinRT;

using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.InvokeProp;

using static Hi3Helper.Logger;
using static Hi3Helper.Locale;

namespace CollapseLauncher
{
    public partial class App : Application
    {

        public static bool IsAppKilled = false;
        public static bool IsGameRunning = false;

        public static void Main(string[] args)
        {
#if PREVIEW
            IsPreview = true;
#endif
            AppCurrentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            InitializeConsole(true, AppGameLogsFolder);

            try
            {
                InitAppPreset();
                InitConsoleSetting();
                Console.WriteLine($"App Version: {AppCurrentVersion} {(IsPreview ? "Preview" : "Stable")} Started!\r\nOS Version: {GetVersionString()}\r\nCurrent Username: {Environment.UserName}");
                Console.WriteLine($"Initializing...", LogType.Empty);
                
                InitializeAppSettings();

                ComWrappersSupport.InitializeComWrappers();
                Start(new ApplicationInitializationCallback((p) =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));

                    new App();
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL ERROR ON APP MAIN() LEVEL!!!\r\n{ex}", LogType.Error, true);
                Console.WriteLine("\r\nif you sure that this is not intended, please report it to: https://github.com/neon-nyan/CollapseLauncher/issues\r\nPress any key to quit...");
                Console.ReadLine();
            }
        }

        public static void InitializeAppSettings()
        {
            InitLog(true, AppGameLogsFolder);
            TryParseLocalizations();
            LoadLocalization(GetAppConfigValue("AppLanguage").ToString());
        }

        public App()
        {
            try
            {
                this.InitializeComponent();

                InnerLauncherConfig.SystemAppTheme = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
                InnerLauncherConfig.CurrentAppTheme = Enum.Parse<AppThemeMode>(GetAppConfigValue("ThemeMode").ToString());
                RequestedTheme = InnerLauncherConfig.CurrentRequestedAppTheme = InnerLauncherConfig.GetAppTheme();

                m_window = new MainWindow();
                m_window.Activate();
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL ERROR ON APP INITIALIZER LEVEL!!!\r\n{ex}", LogType.Error, true);
                LogWriteLine("\r\nif you sure that this is not intended, please report it to: https://github.com/neon-nyan/CollapseLauncher/issues\r\nPress any key to quit...");
                Console.ReadLine();
            }
        }

        public static string GetVersionString()
        {
            OperatingSystem osDetail = Environment.OSVersion;
            ushort[] buildNumber = osDetail.Version.ToString().Split('.').Select(ushort.Parse).ToArray();
            if (buildNumber[2] >= 22000)
                return $"Windows 11 (build: {buildNumber[2]}.{buildNumber[3]})";
            else
                return $"Windows {buildNumber[0]} (build: {buildNumber[2]}.{buildNumber[3]})";
        }

        private MainWindow m_window;
    }
}
