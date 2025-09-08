using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Database;
using CollapseLauncher.Helper.Update;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Hashes;
using Hi3Helper.Http.Legacy;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.LibraryImport;
using InnoSetupHelper;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Microsoft.Windows.ApplicationModel.WindowsAppRuntime;
using SharpCompress.Common;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
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
    public static class MainEntryPoint
    {
        // Decide AUMID string
        public const string AppAumid = "Collapse";
    #nullable enable
        public static int  InstanceCount      { get; set; }
        public static App? CurrentAppInstance { get; set; }
    #nullable restore

        [STAThread]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Main(params string[] args)
        {
            try
            {
                // Force WinRT's COM Wrappers to be initialized early.
                // The method which this calls placed must not be inlined.
                // So the Main() above has MethodImplOptions.NoInlining applied.
                // 
                // This is a workaround to fix COM 0x80040154 error code under Windows 10 1809 build.
                ComWrappersSupport.InitializeComWrappers(new DefaultComWrappers());

                AppActivation.Enable();
                if (AppActivation.DecideRedirection())
                {
                    return;
                }

                // Basically, the Libzstd's DLL will be checked if they exist on Non-AOT build.
                // But due to AOT build uses Static Library in favor of Shared ones (that comes
                // with .dll files), the check will be ignored.
                ZstdNet.DllUtils.IsIgnoreMissingLibrary = true;

                AppCurrentArgument = args.ToList();
#if PREVIEW
                IsPreview = true;
#endif

                // Add callbacks to apply shared settings
                ApplyExternalConfigCallbackList.Add(HttpClientBuilder.ApplyDnsConfigOnAppConfigLoad);
                
                InitAppPreset();
                UseConsoleLog(IsConsoleEnabled);
                // Initialize the Sentry SDK
                SentryHelper.IsPreview = IsPreview;
            #pragma warning disable CS0618 // Type or member is obsolete
                SentryHelper.AppBuildCommit = ThisAssembly.Git.Sha;
                SentryHelper.AppBuildBranch = ThisAssembly.Git.Branch;
                SentryHelper.AppBuildRepo   = ThisAssembly.Git.RepositoryUrl;
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

                // Extract icons from the executable file
                string mainModulePath = AppExecutablePath;
                uint   iconCount      = PInvoke.ExtractIconEx(mainModulePath, -1, nint.Zero, nint.Zero, 0);
                if (iconCount > 0)
                {
                    IntPtr[] largeIcons       = new IntPtr[1];
                    IntPtr[] smallIcons       = new IntPtr[1];
                    nint     largeIconsArrayP = Marshal.UnsafeAddrOfPinnedArrayElement(largeIcons, 0);
                    nint     smallIconsArrayP = Marshal.UnsafeAddrOfPinnedArrayElement(smallIcons, 0);
                    PInvoke.ExtractIconEx(mainModulePath, 0, largeIconsArrayP, smallIconsArrayP, 1);
                    AppIconLarge = largeIcons[0];
                    AppIconSmall = smallIcons[0];
                }

                // Set ILogger for CDNCacheUtil
                CDNCacheUtil.Logger = ILoggerHelper.GetILogger("CDNCacheUtil");

                // Start Updater Hook
                VelopackLocatorExtension.StartUpdaterHook(AppAumid);
                
                if (Directory.GetCurrentDirectory() != AppExecutableDir)
                {
                    LogWriteLine(
                                 $"Force changing the working directory from {Directory.GetCurrentDirectory()} to {AppExecutableDir}!",
                                 LogType.Warning, true);
                    Directory.SetCurrentDirectory(AppExecutableDir);
                }
                
                InitializeAppSettings();
                SentryHelper.AppCdnOption = FallbackCDNUtil.GetPreferredCDN().URLPrefix;

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

                // Reason: These are methods that either has its own error handling and/or not that important,
                // so the execution could continue without anything to worry about **technically**
                _ = InitDatabaseHandler();
                _ = CheckRuntimeFeatures();
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit!;

                InstanceCount = ProcessChecker.EnumerateInstances(ILoggerHelper.GetILogger());

                // Reload Sentry
                if (SentryHelper.IsEnabled)
                {
                    try
                    {
                        // Sentry SDK Entry
                        SentryHelper.InitializeSentrySdk();
                        SentryHelper.InitializeExceptionRedirect();
                    }
                    catch (Exception ex)
                    {
                        LogWriteLine($"Failed to load Sentry SDK.\r\n{ex}", LogType.Sentry, true);
                    }
                }

                Application.Start(pContext =>
                {
                    DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();

                    DispatcherQueueSynchronizationContext context = new DispatcherQueueSynchronizationContext(dispatcherQueue);
                    SynchronizationContext.SetSynchronizationContext(context);

                    // ReSharper disable once ObjectCreationAsStatement
                    CurrentAppInstance = new App
                    {
                        HighContrastAdjustment = ApplicationHighContrastAdjustment.None
                    };
                });
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
            try
            {
                await DbHandler.Init();
            }
            catch (Exception e)
            {
                LogWriteLine($"There was an error while initializing the database handler!\r\n{e}",
                             LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(e);
            }
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

            UseConsoleLog(false);
            LoggerConsole.AllocateConsole();
            Console.Error.WriteLine($"FATAL ERROR ON APP MAIN() LEVEL AND THE MAIN THREAD HAS BEEN TERMINATED!!!\r\n{ex}");
            Console.Error.WriteLine("\r\nIf you are sure that this is not intended, " +
                                    "please report it to: https://github.com/CollapseLauncher/Collapse/issues");

            ShowAdditionalInfoIfComExceptionNotInstalled(ex);

            Console.Error.WriteLine();
            Console.Error.WriteLine("Activity: Checking for possible fallback/recovery update...");
            Console.Error.WriteLine("Press any key to exit or Press 'R' to restart the main thread app...");

            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            _ = RunCheckForPossibleRecoveryOrFallbackUpdate(tokenSource.Token);

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

            tokenSource.Cancel();
        }

        private static async Task RunCheckForPossibleRecoveryOrFallbackUpdate(CancellationToken token)
        {
            Console.CursorTop--;
            Console.CursorTop--;
            int posV = Console.CursorTop;

            Updater updater = new Updater(IsPreview ? "preview" : "stable");
            if (await TryRunUpdateCheck() is not UpdateInfo updateInfo ||
                !IsCurrentUpToDate(updateInfo.TargetFullRelease.Version.ToString()))
            {
                PrintAndFlush(Console.Out, "Activity: Fallback/Recovery update is currently not available.");
                return;
            }

            updater.UpdaterProgressChanged += Updater_UpdaterProgressChanged;
            PrintAndFlush(Console.Out, "Activity: Fallback/Recovery detected! Recovering...");

            await updater.StartUpdate(updateInfo, token);
            PrintAndFlush(Console.Out, "Activity: Fallback/Recovery finished!\r\n");

            int seconds = 5;
            posV = Console.CursorTop;
            while (seconds > 0)
            {
                PrintAndFlush(Console.Out, $"Launcher will be restarted in {seconds}...");
                await Task.Delay(1000);
                seconds--;
            }

            await updater.FinishUpdate();

            return;

            static bool IsCurrentUpToDate(string versionString)
            {
                string filePath = AppExecutablePath;
                if (!File.Exists(filePath))
                {
                    return true;
                }

                // Try to get the version info and if the version info is null, return false
                FileVersionInfo toCheckVersionInfo = FileVersionInfo.GetVersionInfo(filePath);

                if (!Version.TryParse(versionString, out Version latestVersion))
                {
                    return true;
                }

                // Otherwise, try compare the version info.
                if (!Version.TryParse(toCheckVersionInfo.FileVersion, out Version currentVersion))
                {
                    return true;
                }

                // Try compare. If currentVersion is more or equal to latestVersion, return true. 
                // Otherwise, return false.
                return latestVersion > currentVersion;
            }

            async Task<UpdateInfo> TryRunUpdateCheck()
            {
                try
                {
                    return await updater.StartCheck();
                }
                catch (Exception ex)
                {
                    return null;
                }
            }

            void PrintAndFlush(TextWriter writer, string message)
            {
                Console.CursorTop = posV;
                int buffSize = Console.BufferWidth - 1;

                if (buffSize > 0)
                {
                    writer.Write("\r" + new string(' ', buffSize));
                    writer.Write("\r" + message);
                }
            }

            void Updater_UpdaterProgressChanged(object sender, Updater.UpdaterProgress e)
            {
                PrintAndFlush(Console.Out, $"Activity: Fallback/Recovery detected! Recovering ({e.ProgressPercentage}%)...");
            }
        }

        private static void ShowAdditionalInfoIfComExceptionNotInstalled(Exception? ex)
        {
            if (ex is not COMException exAsComEx ||
                ex.HResult != unchecked((int)0x80040154))
            {
                return;
            }

            bool isLtsc = IsWindowsLTSC();
            bool isRedstone5Update = Environment.OSVersion.Version.Build == 17763;

            if (isRedstone5Update)
            {
                string windowsName = isLtsc
                    ? "Windows 10 Enterprise LTSC Redstone 5 Update (Build 1809)"
                    : "Windows 10 Redstone 5 Update (Build 1809)";

                string recommendedWindowsName = isLtsc
                    ? "Windows 10 Enterprise LTSC November 2021 update (Build 21H2)"
                    : "Windows 10 May 2019 Update (Build 19H1)";

                Console.Error.WriteLine();
                Console.Error.WriteLine("\e[42;1mAdditional Note:\e[0m");
                Console.Error.Write($"We have detected that you're trying to run this launcher under {windowsName}. ");
                Console.Error.WriteLine("This error is expected to happen due to lack of Windows Runtime support for WinUI 3 apps compiled using NativeAOT.\r\n");
                Console.Error.WriteLine($"We are recommending you to update your Windows to at least {recommendedWindowsName} or newer in order to use this launcher.\r\n");
                Console.Error.WriteLine("We apologize for this inconvenience. We will try our best to get our launcher run across any Windows 10 (1809 LTSC/GAC or above) editions or newer in the future.\r\n");

                Console.Error.WriteLine($"Error: {ex}");
            }

            static bool IsWindowsLTSC()
            {
                RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion");
                if (key == null)
                {
                    return false;
                }

                if (key.GetValue("EditionID", null) is string editionId &&
                    editionId.StartsWith("Enterprise", StringComparison.OrdinalIgnoreCase) &&
                    editionId.EndsWith("S", StringComparison.OrdinalIgnoreCase)) 
                {
                    return true;
                }

                if (key.GetValue("ProductName", null) is string productName &&
                    productName.Contains("LTSC", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
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

            string themeValue = GetAppConfigValue("ThemeMode").ToString();
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
            Process elevatedProc = new Process
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
            Version version = Environment.OSVersion.Version;
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
            byte[]     hash   = CryptoHashUtility<MD5>.Shared.GetHashFromStream(stream);
            stream.Close();
            return Convert.ToHexStringLower(hash);
        }

        public static void ForceRestart()
        {
            // Workaround to artificially start new process and wait for the current one to be killed.
            string collapsePath = AppExecutablePath;
            Process.Start("cmd.exe", $"/c timeout /T 1 && start /I \"\" \"{collapsePath}\"");
            Application.Current.Exit();
        }
    }
}