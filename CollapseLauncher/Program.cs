using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
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

namespace CollapseLauncher
{
    public static class MainEntryPoint
    {
        [DllImport("Microsoft.ui.xaml.dll")]
        private static extern void XamlCheckProcessRequirements();

        [STAThreadAttribute]
        public static void Main(params string[] args)
        {
            AppCurrentArgument = args;
#if PREVIEW
            IsPreview = true;
#endif
#if PORTABLE
            IsPortable = true;
#endif
            AppCurrentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            try
            {
                InitAppPreset();
                string logPath = AppGameLogsFolder;
                _log = IsConsoleEnabled ? new LoggerConsole(logPath, Encoding.UTF8) : new LoggerNull(logPath, Encoding.UTF8);

                LogWriteLine(string.Format("Running Collapse Launcher [{0}], [{3}], under {1}, as {2}",
                    AppCurrentVersion,
                    GetVersionString(),
                    Environment.UserName,
                    (IsPreview ? "Preview" : "Stable") + (IsPortable ? "-Portable" : "")), LogType.Scheme, true);

                InitializeAppSettings();
                ParseArguments(args);

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
                    case AppMode.Reindex:
                        new Reindexer(m_arguments.Reindexer.AppPath, m_arguments.Reindexer.Version, 4).RunReindex();
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
                Console.WriteLine("\r\nif you sure that this is not intended, please report it to: https://github.com/neon-nyan/CollapseLauncher/issues\r\nPress any key to quit...");
                Console.ReadLine();
                return;
            }
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
            CurrentAppTheme = Enum.Parse<AppThemeMode>(GetAppConfigValue("ThemeMode").ToString());
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
