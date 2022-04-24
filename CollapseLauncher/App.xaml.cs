using System;
using System.IO;

using Windows.UI;

using Microsoft.UI.Xaml;

using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.InvokeProp;

using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    public partial class App : Application
    {
        public static bool IsAppKilled = false;
        public static bool IsGameRunning = false;

        public App()
        {
#if PREVIEW
            AppConfig.IsPreview = true;
#endif
            if (!File.Exists(AppConfigFile))
                AppConfig.IsFirstInstall = true;

            InitializeConsole(true, AppDataFolder);

            LoadAppPreset();
            WriteLog("App Started!", LogType.Scheme);
            LogWriteLine($"Initializing...", LogType.Empty);

            AppConfig.SystemAppTheme = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
            AppConfig.CurrentAppTheme = Enum.Parse<AppThemeMode>(GetAppConfigValue("ThemeMode").ToString());

            RequestedTheme = (AppConfig.CurrentRequestedAppTheme = AppConfig.GetAppTheme());

            try
            {
                this.InitializeComponent();
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL ERROR!!!\r\n{ex}\r\n\r\nIf you're sure that this problem is unintentional, please report this problem by open an issue in https://github.com/neon-nyan/CollapseLauncher/issues", LogType.Error, true);
                Console.ReadLine();
            }
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();
        }

        private Window m_window;
    }
}
