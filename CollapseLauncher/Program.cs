using CollapseLauncher.Classes.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Database;
using CollapseLauncher.Helper.Update;
using Hi3Helper;
using Hi3Helper.Http.Legacy;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Win32.Native.LibraryImport;
using Hi3Helper.Win32.Native.ManagedTools;
using InnoSetupHelper;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinRT;
using static CollapseLauncher.ArgumentParser;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable UnusedMember.Global

namespace CollapseLauncher
{
    public static partial class MainEntryPointExtension
    {
        [LibraryImport("Microsoft.ui.xaml.dll", EntryPoint = "XamlCheckProcessRequirements")]
        public static partial void XamlCheckProcessRequirements();
    }

    public static class MainEntryPoint
    {
        // Decide AUMID string
        public const string AppAumid = "Collapse";
#nullable enable
        public static int  InstanceCount      { get; set; }
        public static App? CurrentAppInstance { get; set; }
    #nullable restore

        [STAThread]
        public static void Main(params string[] args)
        {
            // Basically, the Libzstd's DLL will be checked if they exist on Non-AOT build.
            // But due to AOT build uses Static Library in favor of Shared ones (that comes
            // with .dll files), the check will be ignored.
        #if AOT
            ZstdNet.DllUtils.IsIgnoreMissingLibrary = true;
        #endif

            AppCurrentArgument = args.ToList();
        #if PREVIEW
            IsPreview = true;
        #endif
            try
            {
                // Extract icons from the executable file
                string mainModulePath = AppExecutablePath;
                var    iconCount      = PInvoke.ExtractIconEx(mainModulePath, -1, null, null, 0);
                if (iconCount > 0)
                {
                    var largeIcons = new IntPtr[1];
                    var smallIcons = new IntPtr[1];
                    PInvoke.ExtractIconEx(mainModulePath, 0, largeIcons, smallIcons, 1);
                    AppIconLarge = largeIcons[0];
                    AppIconSmall = smallIcons[0];
                }

                // Start Updater Hook
                VelopackLocatorExtension.StartUpdaterHook(AppAumid);

                InitAppPreset();
                var logPath = AppGameLogsFolder;
                CurrentLogger = IsConsoleEnabled
                    ? new LoggerConsole(logPath, Encoding.UTF8)
                    : new LoggerNull(logPath, Encoding.UTF8);

                if (Directory.GetCurrentDirectory() != AppExecutableDir)
                {
                    LogWriteLine(
                                 $"Force changing the working directory from {Directory.GetCurrentDirectory()} to {AppExecutableDir}!",
                                 LogType.Warning, true);
                    Directory.SetCurrentDirectory(AppExecutableDir);
                }

                InitializeAppSettings();

                SentryHelper.IsPreview = IsPreview;
            #pragma warning disable CS0618 // Type or member is obsolete
                SentryHelper.AppBuildCommit = ThisAssembly.Git.Sha;
                SentryHelper.AppBuildBranch = ThisAssembly.Git.Branch;
                SentryHelper.AppBuildRepo   = ThisAssembly.Git.RepositoryUrl;
                SentryHelper.AppCdnOption   = FallbackCDNUtil.GetPreferredCDN().URLPrefix;
            #pragma warning restore CS0618 // Type or member is obsolete
                if (SentryHelper.IsEnabled)
                {
                    try
                    {
                        // Sentry SDK Entry
                        LogWriteLine("Loading Sentry SDK...", LogType.Sentry, true);
                        SentryHelper.InitializeSentrySdk();
                        LogWriteLine("Setting up global exception handler redirection", LogType.Scheme, true);
                        SentryHelper.InitializeExceptionRedirect();
                    }
                    catch (Exception ex)
                    {
                        LogWriteLine($"Failed to load Sentry SDK.\r\n{ex}", LogType.Sentry, true);
                    }
                }

                LogWriteLine(string.Format("Running Collapse Launcher [{0}], [{3}], under {1}, as {2}",
                                           LauncherUpdateHelper.LauncherCurrentVersionString,
                                           GetVersionString(),
                                       #if DEBUG
                                           Environment.UserName,
                                       #else
                "[REDACTED]",
                                       #endif
                                           IsPreview ? "Preview" : "Stable"), LogType.Scheme, true);

            #pragma warning disable CS0618 // Type or member is obsolete
                LogWriteLine($"Runtime: {RuntimeInformation.FrameworkDescription} - WindowsAppSDK {WindowsAppSdkVersion}",
                             LogType.Scheme, true);
                LogWriteLine($"Built from repo {ThisAssembly.Git.RepositoryUrl}\r\n\t" +
                             $"Branch {ThisAssembly.Git.Branch} - Commit {ThisAssembly.Git.Commit} at {ThisAssembly.Git.CommitDate}",
                             LogType.Scheme, true);
            #pragma warning restore CS0618 // Type or member is obsolete

                Process.GetCurrentProcess().PriorityBoostEnabled = true;

                ParseArguments(args);

                // Initiate InnoSetupHelper's log event
                InnoSetupLogUpdate.LoggerEvent += InnoSetupLogUpdate_LoggerEvent;
                HttpLogInvoker.DownloadLog += HttpClientLogWatcher!;

                switch (m_appMode)
                {
                    case AppMode.ElevateUpdater:
                        RunElevateUpdate();
                        return;
                    case AppMode.InvokerTakeOwnership:
                        TakeOwnership.StartTakingOwnership(m_arguments.TakeOwnership.AppPath);
                        return;
                    case AppMode.InvokerMigrate:
                        if (m_arguments.Migrate.IsBhi3L)
                        {
                            new Migrate().DoMigrationBHI3L(
                                                           m_arguments.Migrate.GameVer,
                                                           m_arguments.Migrate.RegLoc,
                                                           m_arguments.Migrate.InputPath,
                                                           m_arguments.Migrate.OutputPath);
                        }
                        else
                        {
                            new Migrate().DoMigration(
                                                      m_arguments.Migrate.InputPath,
                                                      m_arguments.Migrate.OutputPath);
                        }

                        return;
                    case AppMode.InvokerMoveSteam:
                        new Migrate().DoMoveSteam(
                                                  m_arguments.Migrate.InputPath,
                                                  m_arguments.Migrate.OutputPath,
                                                  m_arguments.Migrate.GameVer,
                                                  m_arguments.Migrate.KeyName);
                        return;
                    case AppMode.GenerateVelopackMetadata:
                        VelopackLocatorExtension.GenerateVelopackMetadata(AppAumid);
                        return;
                }

            #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                // Reason: These are methods that either has its own error handling and/or not that important,
                // so the execution could continue without anything to worry about **technically**
                _ = InitDatabaseHandler();
                _ = CheckRuntimeFeatures();
            #pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                AppDomain.CurrentDomain.ProcessExit += OnProcessExit!;

                InstanceCount = ProcessChecker.EnumerateInstances(ILoggerHelper.GetILogger());

                AppActivation.Enable();
                if (AppActivation.DecideRedirection())
                {
                    return;
                }

                MainEntryPointExtension.XamlCheckProcessRequirements();
                ComWrappersSupport.InitializeComWrappers();

                StartMainApplication();
            }
        #if !DEBUG
        catch (Exception ex)
        {
            SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
            SpawnFatalErrorConsole(ex);
        }
        #else
            // ReSharper disable once RedundantCatchClause
            // Reason: warning shaddap-er
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                throw;
            }
        #endif
            finally
            {
                HttpLogInvoker.DownloadLog -= HttpClientLogWatcher!;
            }
        }

        private static async Task InitDatabaseHandler()
        {
            await DbHandler.Init();
        }

        private static void InnoSetupLogUpdate_LoggerEvent(object sender, InnoSetupLogStruct e)
        {
            LogWriteLine(
                         e.Message,
                         e.LogType switch
                         {
                             InnoSetupLogType.Warning => LogType.Warning,
                             InnoSetupLogType.Error => LogType.Error,
                             _ => LogType.Default
                         },
                         e.IsWriteToLog);
        }

        public static void SpawnFatalErrorConsole(Exception ex)
        {
            CurrentAppInstance?.Exit();
            LoggerConsole.AllocateConsole();
            Console.Error
                   .WriteLine($"FATAL ERROR ON APP MAIN() LEVEL AND THE MAIN THREAD HAS BEEN TERMINATED!!!\r\n{ex}");
            Console.Error.WriteLine("\r\nIf you are sure that this is not intended, " +
                                    "please report it to: https://github.com/CollapseLauncher/Collapse/issues\r\n" +
                                    "Press any key to exit or Press 'R' to restart the main thread app...");

        #if !DEBUG
            if (ConsoleKey.R == Console.ReadKey().Key)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = AppExecutablePath,
                    UseShellExecute = false
                };

                foreach (string arg in AppCurrentArgument)
                {
                    startInfo.ArgumentList.Add(arg);
                }
                Process process = new Process()
                {
                    StartInfo = startInfo
                };
                process.Start();
            }
        #endif
        }

        public static void StartMainApplication()
        {
            Application.Start(_ =>
                              {
                                  DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();

                                  var context = new DispatcherQueueSynchronizationContext(dispatcherQueue);
                                  SynchronizationContext.SetSynchronizationContext(context);

                                  // ReSharper disable once ObjectCreationAsStatement
                                  CurrentAppInstance = new App
                                  {
                                      HighContrastAdjustment = ApplicationHighContrastAdjustment.None
                                  };
                              });
        }

        private static void HttpClientLogWatcher(object sender, DownloadLogEvent e)
        {
            var severity = e.Severity switch
                           {
                               DownloadLogSeverity.Warning => LogType.Warning,
                               DownloadLogSeverity.Error => LogType.Error,
                               _ => LogType.Default
                           };

            LogWriteLine(e.Message, severity, true);
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            // TODO: #671 This App.IsAppKilled will be replaced with cancellable-awaitable event
            //       to ensure no hot-exit being called before all background tasks
            //       hasn't being cancelled.
            // App.IsAppKilled = true;
        }

        private static async Task CheckRuntimeFeatures()
        {
            try
            {
                await Task.Run(() =>
                               {
                                   // RuntimeFeature docs https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.runtimefeature?view=net-9.0
                                   LogWriteLine($"Available Runtime Features:\r\n\t" +
                                                $"PortablePdb: {RuntimeFeature.IsSupported(RuntimeFeature.PortablePdb)}\r\n\t" +
                                                $"IsDynamicCodeCompiled:   {RuntimeFeature.IsDynamicCodeCompiled}\r\n\t" +
                                                $"IsDynamicCodeSupported:  {RuntimeFeature.IsDynamicCodeSupported}\r\n\t" +
                                                $"UnmanagedSignatureCallingConventions:    {RuntimeFeature.IsSupported(RuntimeFeature.UnmanagedSignatureCallingConvention)}",
                                                LogType.Debug, true);
                               }
                              );
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"[CheckRuntimeFeatures] Failed when enumerating available runtime features!\r\n{ex}",
                             LogType.Error, true);
            }
        }

        private static void InitializeAppSettings()
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

            var themeValue = GetAppConfigValue("ThemeMode").ToString();
            if (Enum.TryParse(themeValue, true, out CurrentAppTheme))
            {
                return;
            }

            CurrentAppTheme = AppThemeMode.Dark;
            LogWriteLine($"ThemeMode: {themeValue} is invalid! Falling back to Dark-mode (Valid values are: {string.Join(',', Enum.GetNames(typeof(AppThemeMode)))})",
                         LogType.Warning, true);
        }

        private static void RunElevateUpdate()
        {
            var elevatedProc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName         = UpdaterWindow.SourcePath,
                    WorkingDirectory = UpdaterWindow.WorkingDir,
                    Arguments =
                        $"update --input \"{m_arguments.Updater.AppPath}\" --channel {m_arguments.Updater.UpdateChannel}",
                    UseShellExecute = true,
                    Verb            = "runas"
                }
            };
            elevatedProc.Start();
        }

        public static string GetVersionString()
        {
            var version = Environment.OSVersion.Version;
            m_isWindows11 = version.Build >= 22000;
            return m_isWindows11 ? $"Windows 11 (build: {version.Build}.{version.Revision})" : $"Windows {version.Major} (build: {version.Build}.{version.Revision})";
        }

        public static string MD5Hash(string path)
        {
            if (!File.Exists(path))
            {
                return "";
            }

            FileStream stream = File.OpenRead(path);
            var        hash   = Hash.GetCryptoHash<MD5>(stream);
            stream.Close();
            return Convert.ToHexStringLower(hash);
        }
    }
}