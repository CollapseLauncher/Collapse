using System;
using System.IO;
using System.Reflection;
using System.Linq;

using Windows.UI;

using Microsoft.UI.Xaml;

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

        public App()
        {
            try
            {
#if PREVIEW
                IsPreview = true;
#endif
                AppCurrentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

                InitializeConsole(true, AppGameLogsFolder);
                LogWriteLine($"Initializing...", LogType.Empty);

                InitAppPreset();

                InitializeConsole(true, AppGameLogsFolder);

                InitConsoleSetting();
                WriteLog($"App Version: {AppCurrentVersion} {(IsPreview ? "Preview" : "Stable")} Started! -> {GetVersionString()}", LogType.Scheme);
                TryParseLocalizations();
                LoadLocalization(GetAppConfigValue("AppLanguage").ToString());

                InnerLauncherConfig.SystemAppTheme = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
                InnerLauncherConfig.CurrentAppTheme = Enum.Parse<AppThemeMode>(GetAppConfigValue("ThemeMode").ToString());

                RequestedTheme = (InnerLauncherConfig.CurrentRequestedAppTheme = InnerLauncherConfig.GetAppTheme());
                this.InitializeComponent();
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL ERROR!!!\r\n{ex}\r\n\r\nPlease report this problem by open an issue in https://github.com/neon-nyan/CollapseLauncher/issues", LogType.Error, true);
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

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();
        }

        private Window m_window;
    }
}
