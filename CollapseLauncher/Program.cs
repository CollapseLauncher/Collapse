﻿using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using Windows.UI.ViewManagement;
using WinRT;
using static CollapseLauncher.ArgumentParser;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.InvokeProp;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public static class MainEntryPoint
    {
        [STAThread]
        public static void Main(params string[] args)
        {
#if PREVIEW
            IsPreview = true;
#endif
#if PORTABLE
            IsPortable = true;
#endif
            AppCurrentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            InitializeConsole(true, AppGameLogsFolder);

            try
            {
                InitAppPreset();
                InitConsoleSetting();

                Console.WriteLine("App Version: {0} {3} Started\r\nOS Version: {1}\r\nCurrent Username: {2}",
                    AppCurrentVersion,
                    GetVersionString(),
                    Environment.UserName,
                    (IsPreview ? "Preview" : "Stable") + (IsPortable ? "-Portable" : ""));
                Console.WriteLine("Initializing...", LogType.Empty);

                InitializeAppSettings();
                CheckRepoStatus();
                ParseArguments(args);

                switch (m_appMode)
                {
                    case AppMode.Launcher:
                        InitConsoleSetting();
                        break;
                    case AppMode.ElevateUpdater:
                        InitConsoleSetting();
                        RunElevateUpdate();
                        return;
                    case AppMode.Reindex:
                        InitConsoleSetting(true);
                        new Reindexer(m_arguments.Reindexer.AppPath, m_arguments.Reindexer.Version, 4).RunReindex();
                        return;
                    case AppMode.InvokerTakeOwnership:
                        InitConsoleSetting(true);
                        new TakeOwnership().StartTakingOwnership(m_arguments.TakeOwnership.AppPath);
                        return;
                    case AppMode.InvokerMigrate:
                        InitConsoleSetting(true);
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
                        InitConsoleSetting(true);
                        new Migrate().DoMoveSteam(
                            m_arguments.Migrate.InputPath,
                            m_arguments.Migrate.OutputPath,
                            m_arguments.Migrate.GameVer,
                            m_arguments.Migrate.KeyName);
                        return;
                }

                if (!DecideRedirection())
                {
                    ComWrappersSupport.InitializeComWrappers();
                    Application.Start(new ApplicationInitializationCallback((p) =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));

                        new App();
                    }));
                }

                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL ERROR ON APP MAIN() LEVEL!!!\r\n{ex}", LogType.Error, true);
                Console.WriteLine("\r\nif you sure that this is not intended, please report it to: https://github.com/neon-nyan/CollapseLauncher/issues\r\nPress any key to quit...");
                InitConsoleSetting(true);
                Console.ReadLine();
                return;
            }
        }

        private static bool DecideRedirection()
        {
            bool isRedirect = false;
            AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
            AppInstance keyInstance = AppInstance.FindOrRegisterForKey(m_appMode.ToString());

            if (!keyInstance.IsCurrent)
            {
                isRedirect = true;
                keyInstance.RedirectActivationToAsync(args).GetAwaiter().GetResult();
            }
            return isRedirect;
        }

        public static void InitializeAppSettings()
        {
            InitLog(true, AppGameLogsFolder);
            TryParseLocalizations();
            if (IsFirstInstall)
            {
                LoadLocalization(CultureInfo.CurrentUICulture.Name);
                SetAppConfigValue("AppLanguage", Lang.LanguageID);
            }
            else
                LoadLocalization(GetAppConfigValue("AppLanguage").ToString());
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
