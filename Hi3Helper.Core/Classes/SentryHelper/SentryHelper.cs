using Sentry;
using System;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Shared.Region;
using Microsoft.Win32;
using Sentry.Infrastructure;
using Sentry.Protocol;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable HeuristicUnreachableCode
// ReSharper disable RedundantIfElseBlock
#nullable enable
namespace Hi3Helper.SentryHelper
{
    public static partial class SentryHelper
    {
        #region Sentry Configuration

        /// <summary>
        /// DSN (Data Source Name a.k.a. upstream server) to be used for error reporting.
        /// </summary>
        private const string SentryDsn =
            "https://38df0c6a1a779a4d663def402084187f@o4508297437839360.ingest.de.sentry.io/4508302961410128";

        /// <summary>
        /// <inheritdoc cref="SentryOptions.MaxAttachmentSize"/>
        /// </summary>
        private const long SentryMaxAttachmentSize = 2 * 1024 * 1024; // 2 MB

        /// <summary>
        /// Whether to upload log file when exception is caught.
        /// </summary>
        private const bool SentryUploadLog = true;

        private static bool IsDebugSentry => Convert.ToBoolean(Environment.GetEnvironmentVariable("DEBUG_SENTRY"));

        #endregion

        #region Enums

        /// <summary>
        /// Defines Exception Types to be used in Sentry's data reporting.
        /// </summary>
        public enum ExceptionType
        {
            /// <summary>
            /// Use this for any unhandled exception that comes from anything Xaml/WinUI related.
            /// </summary>
            UnhandledXaml,

            /// <summary>
            /// Use this method for method that has no meaningful error handling.
            /// </summary>
            UnhandledOther,

            /// <summary>
            /// Use this for exception that is handled directly by the catcher.
            /// </summary>
            Handled
        }

        #endregion

        #region Enable/Disable Sentry

        public static bool IsDisableEnvVarDetected =>
            Convert.ToBoolean(Environment.GetEnvironmentVariable("DISABLE_SENTRY"));

        private static bool? _isEnabled;

        public static bool IsEnabled
        {
            get
            {
                if (IsDisableEnvVarDetected)
                {
                    Logger.LogWriteLine("Detected 'DISABLE_SENTRY' environment variable! Disabling crash data reporter...");
                    LauncherConfig.SetAndSaveConfigValue("SendRemoteCrashData", false);
                    return false;
                }

                return _isEnabled ??= LauncherConfig.GetAppConfigValue("SendRemoteCrashData").ToBool();
            }
            set
            {
                if (value == _isEnabled) return;

                if (value)
                {
                    Task.Run(() =>
                             {
                                 InitializeSentrySdk();
                                 InitializeExceptionRedirect();
                             });
                }
                else
                {
                    StopSentrySdk();
                }

                _isEnabled = value;
                LauncherConfig.SetAndSaveConfigValue("SendRemoteCrashData", value);
            }
        }

        #endregion

        #region Initializer/Releaser

        public static  bool         IsPreview { get; set; }
        private static IDisposable? _sentryInstance;

        public static void InitializeSentrySdk()
        {
        #if DEBUG
            _isEnabled = false;
            return;
        #pragma warning disable CS0162 // Unreachable code detected
        #endif
            _sentryInstance =
                SentrySdk.Init(o =>
                             {
                                 o.Dsn = SentryDsn;
                                 o.AddEventProcessor(new SentryEventProcessor());
                                 o.CacheDirectoryPath = LauncherConfig.AppDataFolder;
                                 o.Debug              = IsDebugSentry;
                                 o.DiagnosticLogger = IsDebugSentry
                                     ? new ConsoleAndTraceDiagnosticLogger(SentryLevel.Debug) : null;
                                 o.DiagnosticLevel     = IsDebugSentry ? SentryLevel.Debug : SentryLevel.Error;
                                 o.AutoSessionTracking = true;
                                 o.StackTraceMode      = StackTraceMode.Enhanced;
                                 o.DisableSystemDiagnosticsMetricsIntegration();
                                 o.IsGlobalModeEnabled = true;
                                 o.DisableWinUiUnhandledExceptionIntegration(); // Use this for trimmed/NativeAOT published app
                                 o.StackTraceMode    = StackTraceMode.Enhanced;
                                 o.SendDefaultPii    = false;
                                 o.MaxAttachmentSize = SentryMaxAttachmentSize;
                                 o.DeduplicateMode   = DeduplicateMode.All;
                                 o.Environment       = Debugger.IsAttached ? "debug" : IsPreview ? "non-debug" : "stable";
                                 o.AddExceptionFilter(new NetworkException());
                             });
            SentrySdk.ConfigureScope(s =>
                                     {
                                         s.User = new SentryUser
                                         {
                                             // Do not send user IP address.
                                             // Geolocation should not be uploaded for new users that has not sent any data.
                                             IpAddress = null
                                         };
                                     });
        #if DEBUG
        #pragma warning restore CS0162 // Unreachable code detected
        #endif
        }

        /// <summary>
        /// Flush, ends, and dispose SentrySdk instance.
        /// </summary>
        public static void StopSentrySdk()
        {
            _ = Task.Run(async () =>
                         {
                             try
                             {
                                 await SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));
                                 SentrySdk.EndSession();
                                 ReleaseExceptionRedirect();
                             }
                             catch (Exception ex)
                             {
                                 Logger
                                    .LogWriteLine($"Failed when preparing to stop SentryInstance, Dispose will still be invoked!\r\n{ex}",
                                                  LogType.Error, true);
                             }
                             finally
                             {
                                 _sentryInstance?.Dispose();
                             }
                         });
        }

        public static void InitializeExceptionRedirect()
        {
            AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledExceptionEvent;
            TaskScheduler.UnobservedTaskException      += TaskScheduler_UnobservedTaskException;
        }

        private static void ReleaseExceptionRedirect()
        {
            AppDomain.CurrentDomain.UnhandledException -= AppDomain_UnhandledExceptionEvent;
            TaskScheduler.UnobservedTaskException      -= TaskScheduler_UnobservedTaskException;
        }

        #endregion

        #region Exception Handlers

        private static void AppDomain_UnhandledExceptionEvent(object sender, UnhandledExceptionEventArgs a)
        {
            // Handle any unhandled errors in app domain
            // https://learn.microsoft.com/en-us/dotnet/api/system.appdomain?view=net-9.0
            var ex = a.ExceptionObject as Exception;
            if (ex == null) return;
            ex.Data[Mechanism.HandledKey]   = false;
            ex.Data[Mechanism.MechanismKey] = "Application.UnhandledException";
            ExceptionHandler(ex, ExceptionType.UnhandledOther);
        }

        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            e.Exception.Data[Mechanism.HandledKey] = false;
            ExceptionHandler(e.Exception, ExceptionType.UnhandledOther);
        }

        /// <summary>
        /// Sends exception to Sentry's set DSN
        /// </summary>
        /// <param name="ex">Exception data</param>
        /// <param name="exT">Exception type, default to ExceptionType.Handled</param>
        public static void ExceptionHandler(Exception ex, ExceptionType exT = ExceptionType.Handled)
        {
            if (!IsEnabled) return;
            if (ex is AggregateException && ex.InnerException != null) ex = ex.InnerException;
            if (ex is TaskCanceledException or OperationCanceledException)
            {
                Logger.LogWriteLine($"Caught TCE/OCE exception from: {ex.Source}. Exception will not be uploaded!\r\n{ex}",
                                    LogType.Sentry);
                return;
            }

            ExceptionHandlerInner(ex, exT);
        }

        /// <summary>
        /// <inheritdoc cref="ExceptionHandler"/>
        /// </summary>
        /// <param name="ex">Exception data</param>
        /// <param name="exT">Exception type, default to ExceptionType.Handled</param>
        public static async Task ExceptionHandlerAsync(Exception ex, ExceptionType exT = ExceptionType.Handled)
        {
            if (!IsEnabled) return;
            if (ex is AggregateException && ex.InnerException != null) ex = ex.InnerException;
            if (ex is TaskCanceledException or OperationCanceledException)
            {
                Logger.LogWriteLine($"Caught TCE/OCE exception from: {ex.Source}. Exception will not be uploaded!\r\n{ex}",
                                    LogType.Sentry);
                return;
            }

            await Task.Run(() => ExceptionHandler(ex, exT));
            await Task.Delay(250); // Delay to ensure that the exception is uploaded
            await Task.Run(async () => await SentrySdk.FlushAsync(TimeSpan.FromSeconds(10)));
        }

        private static Exception?              _exHLoopLastEx;
        private static CancellationTokenSource _loopToken = new();

        // ReSharper disable once AsyncVoidMethod
        /// <summary>
        /// Clean loop last exception data to be cleaned after 20s so the exception data will be sent to Dsn again.
        /// </summary>
        private static async void ExHLoopLastEx_AutoClean()
        {
            // if (loopToken.Token.IsCancellationRequested) return;
            try
            {
                var t = _loopToken.Token;
                await Task.Delay(20000, t);
                if (_exHLoopLastEx == null) return;

                lock (_exHLoopLastEx)
                {
                    _exHLoopLastEx = null;
                }
            }
            catch (Exception)
            {
                //ignore
            }
        }

        /// <summary>
        /// Sends exception to Sentry's set DSN. <br/>
        /// Use this method for any method that may repeat same exceptions multiple time in the time span of 20s.
        /// </summary>
        /// <param name="ex">Exception data</param>
        /// <param name="exT">Exception type, default to ExceptionType.Handled</param>
        public static void ExceptionHandler_ForLoop(Exception ex, ExceptionType exT = ExceptionType.Handled) =>
            ExceptionHandler_ForLoopAsync(ex, exT).GetAwaiter().GetResult();

        /// <summary>
        /// <inheritdoc cref="ExceptionHandler_ForLoop"/>
        /// </summary>
        /// <param name="ex">Exception data</param>
        /// <param name="exT">Exception type, default to ExceptionType.Handled</param>
        public static async Task ExceptionHandler_ForLoopAsync(Exception ex, ExceptionType exT = ExceptionType.Handled)
        {
            if (!IsEnabled) return;
            if (ex is AggregateException && ex.InnerException != null) ex = ex.InnerException;
            if (ex is TaskCanceledException or OperationCanceledException)
            {
                Logger.LogWriteLine($"Caught TCE/OCE exception from: {ex.Source}. Exception will not be uploaded!\r\n{ex}",
                                    LogType.Sentry);
                return;
            }

            if (ex == _exHLoopLastEx) return; // If exception pointer is the same as the last one, ignore it.
            await _loopToken.CancelAsync(); // Cancel the previous loop
            _loopToken.Dispose();
            _loopToken     = new CancellationTokenSource(); // Create new token
            _exHLoopLastEx = ex;
            ExHLoopLastEx_AutoClean(); // Start auto clean loop
            await Task.Run(() => ExceptionHandlerInner(ex, exT));
        }

        #region Breadcrumbs Data

        public static string AppBuildCommit        { get; set; } = "";
        public static string AppBuildBranch        { get; set; } = "";
        public static string AppBuildRepo          { get; set; } = "";
        public static string AppCdnOption          { get; set; } = "";
        public static string CurrentGameCategory   { get; set; } = "";
        public static string CurrentGameRegion     { get; set; } = "";
        public static string CurrentGameLocation   { get; set; } = "";
        public static bool   CurrentGameInstalled  { get; set; }
        public static bool   CurrentGameUpdated    { get; set; }
        public static bool   CurrentGameHasPreload { get; set; }
        public static bool   CurrentGameHasDelta   { get; set; }

        private static int CpuThreadsTotal => Environment.ProcessorCount;

        private static string CpuName
        {
            get
            {
                try
                {
                    string cpuName;
                    var    env = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU";
                    var reg =
                        Registry.GetValue("HKEY_LOCAL_MACHINE\\HARDWARE\\DESCRIPTION\\SYSTEM\\CentralProcessor\\0",
                                          "ProcessorNameString", null);
                    if (reg != null)
                    {
                        cpuName = reg.ToString() ?? env;
                        cpuName = cpuName.Trim();
                    }
                    else cpuName = env;

                    return cpuName;
                }
                catch (Exception ex)
                {
                    Logger.LogWriteLine("Failed when trying to get CPU Name!\r\n" + ex, LogType.Error, true);
                    return "Unknown CPU";
                }
            }
        }

        private static List<(string GpuName, string DriverVersion)> GetGpuInfo
        {
            get
            {
                List<(string GpuName, string DriverVersion)> gpuInfoList = [];
                try
                {
                    using RegistryKey? baseKey =
                        Registry.LocalMachine
                                .OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\");
                    if (baseKey != null)
                    {
                        foreach (var subKeyName in baseKey.GetSubKeyNames())
                        {
                            if (!int.TryParse(subKeyName, out int subKeyInt) || subKeyInt < 0 || subKeyInt > 9999)
                                continue;

                            var gpuName       = "Unknown GPU";
                            var driverVersion = "Unknown Driver Version";
                            try
                            {
                                using var subKey = baseKey.OpenSubKey(subKeyName);
                                if (subKey == null) continue;

                                gpuName       = subKey.GetValue("DriverDesc") as string;
                                driverVersion = subKey.GetValue("DriverVersion") as string;
                                if (!string.IsNullOrEmpty(gpuName) && !string.IsNullOrEmpty(driverVersion))
                                {
                                    gpuInfoList.Add((subKeyName + gpuName, driverVersion));
                                }
                            }
                            catch (Exception)
                            {
                                gpuInfoList.Add((subKeyName + gpuName, driverVersion)!);
                                throw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWriteLine($"Failed to retrieve GPU info from registry: {ex.Message}", LogType.Error,
                                        true);
                }

                return gpuInfoList;
            }
        }

        private static Breadcrumb? _buildInfo;
        private static Breadcrumb BuildInfo
        {
            get => _buildInfo ??= new("Build Info", "commit", new Dictionary<string, string>
            {
                { "Branch", AppBuildBranch },
                { "Commit", AppBuildCommit },
                { "Repository", AppBuildRepo },
                { "IsPreview", IsPreview.ToString() }
            }, "BuildInfo");
        }
        
        private static Breadcrumb? _cpuInfo;
        private static Breadcrumb CpuInfo
        {
            get => _cpuInfo ??= new("CPU Info", "system.cpu", new Dictionary<string, string>
            {
                { "CPU Name", CpuName },
                { "Total Thread", CpuThreadsTotal.ToString() }
            }, "CPUInfo");
        }

        private static Breadcrumb? _gpuInfo;
        private static Breadcrumb GpuInfo
        {
            get => _gpuInfo ??= new("GPU Info", "system.gpu",
                                     GetGpuInfo.ToDictionary(item => item.GpuName,
                                                             item => item.DriverVersion),
                                     "GPUInfo");
        }
        
        private static Breadcrumb GameInfo =>
            new("Current Loaded Game Info", "game", new Dictionary<string, string>
            {
                { "Category", CurrentGameCategory },
                { "Region", CurrentGameRegion },
                { "Installed", CurrentGameInstalled.ToString() },
                { "Updated", CurrentGameUpdated.ToString() },
                { "HasPreload", CurrentGameHasPreload.ToString() },
                { "HasDelta", CurrentGameHasDelta.ToString() },
                { "Location", CurrentGameLocation },
                { "CdnOption", AppCdnOption }
            }, "GameInfo");

        #endregion
        
        private static Task ExceptionHandlerInner(Exception ex, ExceptionType exT = ExceptionType.Handled)
        {
            SentrySdk.AddBreadcrumb(BuildInfo);
            SentrySdk.AddBreadcrumb(GameInfo);
            SentrySdk.AddBreadcrumb(CpuInfo);
            SentrySdk.AddBreadcrumb(GpuInfo);

            var loadedModules = Process.GetCurrentProcess().Modules;
            var modulesInfo   = new ConcurrentDictionary<string, string>();

            Parallel.ForEach(loadedModules.Cast<ProcessModule>(), module =>
            {
                try
                {
                    var name = module.ModuleName;
                    var ver  = module.FileVersionInfo.FileVersion;
                    var path = module.FileName;
                    _ = modulesInfo.TryAdd(name, $"{ver} ({path})");
                }
                catch (Exception exI)
                {
                    Logger.LogWriteLine($"Failed to get module info: {exI.Message}", LogType.Error, true);
                }
            });

            var sbModulesInfo = new StringBuilder();
            foreach (var (key, value) in modulesInfo)
            {
                sbModulesInfo.AppendLine($"{key}: {value}");
            }
            
            ex.Data[Mechanism.HandledKey] ??= exT == ExceptionType.Handled;

            string? methodName = null;
            var    st = ex.StackTrace;
            if (st != null)
            {
                var          m   = ExceptionFrame().Match(st);
                methodName = m.Success ? m.Value : null;
            }

            ex.Data[Mechanism.MechanismKey] ??= exT switch
                                              {
                                                  ExceptionType.UnhandledXaml => "Application.XamlUnhandledException",
                                                  ExceptionType.UnhandledOther => methodName ?? ex.Source ?? "Application.UnhandledException",
                                                  _ => methodName ?? ex.Source ?? "Application.HandledException"
                                              };
            
        #pragma warning disable CS0162 // Unreachable code detected
            if (SentryUploadLog) // Upload log file if enabled
                // ReSharper disable once HeuristicUnreachableCode
            {
                if ((bool)(ex.Data[Mechanism.HandledKey] ?? false))
                    SentrySdk.CaptureException(ex);
                else
                    SentrySdk.CaptureException(ex, s =>
                                                   {
                                                       s.AddAttachment(LoggerBase.LogPath, AttachmentType.Default, "text/plain");
                                                       s.AddAttachment(new MemoryStream(Encoding.UTF8.GetBytes(sbModulesInfo.ToString())),
                                                                       "LoadedModules.txt", AttachmentType.Default,
                                                                       "text/plain");
                                                   });
            }
            else
            {
                SentrySdk.CaptureException(ex);
            }
        #pragma warning restore CS0162 // Unreachable code detected
            return Task.CompletedTask;
        }

        [GeneratedRegex(@"(?<=\bat\s)(CollapseLauncher|Hi3Helper)\.[^\s(]+", RegexOptions.Compiled)]
        private static partial Regex ExceptionFrame();
    }

        #endregion
}
