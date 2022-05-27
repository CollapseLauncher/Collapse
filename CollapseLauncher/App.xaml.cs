using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

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

        public static void Main(params string[] args)
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
                ParseArguments(args);

                switch (m_appMode)
                {
                    case AppMode.ElevateUpdater:
                        RunElevateUpdate();
                        return;
                    case AppMode.Reindex:
                        new Reindexer(m_arguments.Reindexer.AppPath, m_arguments.Reindexer.Version).RunReindex();
                        return;
                    case AppMode.InvokerTakeOwnership:
                        new TakeOwnership().StartTakingOwnership(m_arguments.TakeOwnership.AppPath);
                        return;
                    case AppMode.InvokerMigrate:
                        if (m_arguments.Migrate.IsBHI3L)
                            new Migrate().DoMigrationBHI3L(
                                m_arguments.Migrate.GameVer,
                                m_arguments.Migrate.RegLoc,
                                m_arguments.Migrate.InputPath,
                                m_arguments.Migrate.OutputPath);
                        else
                            new Migrate().DoMigration(
                                m_arguments.Migrate.InputPath,
                                m_arguments.Migrate.OutputPath);
                        return;
                    case AppMode.InvokerMoveSteam:
                        new Migrate().DoMoveSteam(
                            m_arguments.Migrate.InputPath,
                            m_arguments.Migrate.OutputPath,
                            m_arguments.Migrate.GameVer,
                            m_arguments.Migrate.KeyName);
                        return;
                }

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
            SystemAppTheme = new UISettings().GetColorValue(UIColorType.Background);
            CurrentAppTheme = Enum.Parse<AppThemeMode>(GetAppConfigValue("ThemeMode").ToString());
        }

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

        public static void RunElevateUpdate()
        {
            Process elevatedProc = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = UpdaterWindow.sourcePath,
                    WorkingDirectory = UpdaterWindow.workingDir,
                    Arguments = $"update --input \"{m_arguments.Updater.AppPath}\" --channel {m_arguments.Updater.UpdateChannel}",
                    UseShellExecute = true,
                    Verb = "runas"
                }
            };
            try
            {
                elevatedProc.Start();
            }
            catch { }
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

        private Window m_window;
    }
}
