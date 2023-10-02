using Hi3Helper;
using Hi3Helper.Http;
#if !DISABLEDISCORD
using Hi3Helper.DiscordPresence;
#endif
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Squirrel;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Windows.UI.ViewManagement;
using WinRT;
using static CollapseLauncher.ArgumentParser;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using System.IO;

namespace CollapseLauncher
{
    public static class MainEntryPoint
    {
        [DllImport("Microsoft.ui.xaml.dll")]
        private static extern void XamlCheckProcessRequirements();

        [STAThreadAttribute]
        public static void Main(params string[] args)
        {
#if PREVIEW
            IsPreview = true;
#endif
            AppCurrentVersion = new GameVersion(Assembly.GetExecutingAssembly().GetName().Version);
            AppCurrentVersionString = AppCurrentVersion.VersionString;

            try
            {
                StartSquirrelHook();

                AppCurrentArgument = args;

                InitAppPreset();
                string logPath = AppGameLogsFolder;
                _log = IsConsoleEnabled ? new LoggerConsole(logPath, Encoding.UTF8) : new LoggerNull(logPath, Encoding.UTF8);
                if (Directory.GetCurrentDirectory() != AppFolder)
                {
                    LogWriteLine($"Force changing the working directory from {Directory.GetCurrentDirectory()} to {AppFolder}!", LogType.Warning, true);
                    Directory.SetCurrentDirectory(AppFolder);
                }

                LogWriteLine(string.Format("Running Collapse Launcher [{0}], [{3}], under {1}, as {2}",
                    AppCurrentVersion.VersionString,
                    GetVersionString(),
                    Environment.UserName,
                    IsPreview ? "Preview" : "Stable"), LogType.Scheme, true);

                FileVersionInfo winappSDKver = FileVersionInfo.GetVersionInfo("Microsoft.ui.xaml.dll");
                LogWriteLine(string.Format("Runtime: {0} - WindowsAppSDK {1}", RuntimeInformation.FrameworkDescription, winappSDKver.ProductVersion), LogType.Scheme, true);

                Process.GetCurrentProcess().PriorityBoostEnabled = true;

                InitializeAppSettings();
                ParseArguments(args);

                HttpLogInvoker.DownloadLog += HttpClientLogWatcher;

                switch (m_appMode)
                {
                    case AppMode.Launcher:
                        if (!IsConsoleEnabled)
                        {
                            LoggerConsole.DisposeConsole();
                            _log = new LoggerNull(logPath, Encoding.UTF8);
                        }
                        break;
                    case AppMode.ElevateUpdater:
                        RunElevateUpdate();
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

                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

                if (!DecideRedirection())
                {
                    XamlCheckProcessRequirements();

                    ComWrappersSupport.InitializeComWrappers();
                    Application.Start((p) =>
                    {
                        DispatcherQueueSynchronizationContext context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                        SynchronizationContext.SetSynchronizationContext(context);

                        new App();
                    });
                }

                return;
            }
            catch (Exception ex)
            {
                LoggerConsole.AllocateConsole();
                Console.WriteLine($"FATAL ERROR ON APP MAIN() LEVEL!!!\r\n{ex}");
                Console.WriteLine("\r\nIf you are sure that this is not intended, please report it to: https://github.com/CollapseLauncher/Collapse/issues\r\nPress any key to exit...");
                Console.ReadLine();
                return;
            }
            finally
            {
                HttpLogInvoker.DownloadLog -= HttpClientLogWatcher;
            }
        }

        private static void HttpClientLogWatcher(object sender, DownloadLogEvent e)
        {
            LogType severity = e.Severity switch
            {
                DownloadLogSeverity.Warning => LogType.Warning,
                DownloadLogSeverity.Error => LogType.Error,
                _ => LogType.Default
            };

            if (severity != LogType.Default)
            {
                LogWriteLine(e.Message, severity, true);
            }
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            App.IsAppKilled = true;

#if !DISABLEDISCORD
            AppDiscordPresence.Dispose();
#endif
        }

        private static void StartSquirrelHook()
        {
            // Add Squirrel Hooks
            SquirrelAwareApp.HandleEvents(
                /// Add shortcut and uninstaller entry on first start-up
                onInitialInstall: (_, sqr) =>
                {
                    Console.WriteLine("Please do not close this console window while Collapse is preparing the installation via Squirrel...");
                },
                onAppUpdate: (_, sqr) =>
                {
                    Console.WriteLine("Please do not close this console window while Collapse is updating via Squirrel...");
                },
                onAppUninstall: (_, sqr) =>
                {
                    Console.WriteLine("Uninstalling Collapse via Squirrel...\r\nPlease do not close this console window while action is being performed!");
                },
                onEveryRun: (_, _, _) => { }
            );
        }

        private static bool DecideRedirection()
        {
            bool isRedirect = false;
            AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
            AppInstance keyInstance = AppInstance.FindOrRegisterForKey(m_appMode.ToString());

            if (!keyInstance.IsCurrent && !IsMultipleInstanceEnabled)
            {
                isRedirect = true;
                keyInstance.RedirectActivationToAsync(args).GetAwaiter().GetResult();
            }
            return isRedirect;
        }

        public static void InitializeAppSettings()
        {
            InitializeLocale();
            if (IsFirstInstall)
            {
                LoadLocale(CultureInfo.CurrentUICulture.Name);
                SetAppConfigValue("AppLanguage", Lang.LanguageID);
            }
            else
            {
                LoadLocale(GetAppConfigValue("AppLanguage").ToString());
            }

            SystemAppTheme = new UISettings().GetColorValue(UIColorType.Background);
            string themeValue = GetAppConfigValue("ThemeMode").ToString();
            if (!Enum.TryParse(themeValue, true, out CurrentAppTheme))
            {
                CurrentAppTheme = AppThemeMode.Dark;
                LogWriteLine($"ThemeMode: {themeValue} is invalid! Falling back to Dark-mode (Valid values are: {string.Join(',', Enum.GetNames(typeof(AppThemeMode)))})", LogType.Warning, true);
            }
#if !DISABLEDISCORD
            bool isInitialStart = GetAppConfigValue("EnableDiscordRPC").ToBool();
            AppDiscordPresence = new DiscordPresenceManager(isInitialStart);
#endif
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
            w_windowsVersionNumbers = osDetail.Version.ToString().Split('.').Select(ushort.Parse).ToArray();
            if (w_windowsVersionNumbers[2] >= 22000)
            {
                return $"Windows 11 (build: {w_windowsVersionNumbers[2]}.{w_windowsVersionNumbers[3]})";
            }
            else
            {
                return $"Windows {w_windowsVersionNumbers[0]} (build: {w_windowsVersionNumbers[2]}.{w_windowsVersionNumbers[3]})";
            }
        }
    }
}
