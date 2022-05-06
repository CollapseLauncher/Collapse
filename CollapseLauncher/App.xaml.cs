using System;
using System.IO;
using System.Reflection;

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
            IsPreview = true;
#endif
            AppCurrentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            InitializeConsole(true, AppDataFolder);

            InitAppPreset();
            InitConsoleSetting();
            WriteLog("App Started!", LogType.Scheme);
            LogWriteLine($"Initializing...", LogType.Empty);

            InnerLauncherConfig.SystemAppTheme = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
            InnerLauncherConfig.CurrentAppTheme = Enum.Parse<AppThemeMode>(GetAppConfigValue("ThemeMode").ToString());

            RequestedTheme = (InnerLauncherConfig.CurrentRequestedAppTheme = InnerLauncherConfig.GetAppTheme());

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
