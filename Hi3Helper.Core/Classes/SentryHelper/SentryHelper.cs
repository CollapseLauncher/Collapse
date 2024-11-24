using Sentry;
using System;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Shared.Region;
#if DEBUG
using Sentry.Infrastructure;
#endif
using Sentry.Protocol;
using System.Collections.Generic;
using System.Diagnostics;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable HeuristicUnreachableCode
// ReSharper disable RedundantIfElseBlock
#nullable enable
namespace Hi3Helper.SentryHelper
{
    public static class SentryHelper
    {
        #region Sentry Configuration

        /// <summary>
        /// DSN (Data Source Name a.k.a. upstream server) to be used for error reporting.
        /// </summary>
        private const string SentryDsn = "https://38df0c6a1a779a4d663def402084187f@o4508297437839360.ingest.de.sentry.io/4508302961410128";
        
        /// <summary>
        /// <inheritdoc cref="SentryOptions.MaxAttachmentSize"/>
        /// </summary>
        private const long SentryMaxAttachmentSize = 2 * 1024 * 1024; // 2 MB

        /// <summary>
        /// Whether to upload log file when exception is caught.
        /// </summary>
        private const bool SentryUploadLog = true;

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

        public static bool IsDisableEnvVarDetected => Convert.ToBoolean(Environment.GetEnvironmentVariable("DISABLE_SENTRY"));
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
                    InitializeSentrySdk();
                    InitializeExceptionRedirect();
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
            _sentryInstance = 
                SentrySdk.Init(o =>
                             {
                                 o.Dsn = SentryDsn;
                                 o.AddEventProcessor(new SentryEventProcessor());
                                 o.CacheDirectoryPath = LauncherConfig.AppDataFolder;
                             #if DEBUG
                                o.Debug = true;
                                o.DiagnosticLogger = new ConsoleAndTraceDiagnosticLogger(SentryLevel.Debug);
                                o.DiagnosticLevel = SentryLevel.Debug;
                                o.Distribution = "Debug";
                                // Set TracesSampleRate to 1.0 to capture 100%
                                // of transactions for tracing.
                                // We recommend adjusting this value in production.
                                o.TracesSampleRate = 1.0;

                                // Sample rate for profiling, applied on top of other TracesSampleRate,
                                // e.g. 0.2 means we want to profile 20 % of the captured transactions.
                                // We recommend adjusting this value in production.
                                o.ProfilesSampleRate = 1.0;
                             #else
                                 o.Debug              = false;
                                 o.DiagnosticLevel    = SentryLevel.Error;
                                 o.Distribution       = IsPreview ? "Preview" : "Stable";
                                 o.TracesSampleRate   = 0.4;
                                 o.ProfilesSampleRate = 0.2;
                             #endif
                                 o.AutoSessionTracking = true;
                                 o.StackTraceMode      = StackTraceMode.Enhanced;
                                 o.DisableSystemDiagnosticsMetricsIntegration();
                                 o.IsGlobalModeEnabled      = true;
                                 o.DisableWinUiUnhandledExceptionIntegration(); // Use this for trimmed/NativeAOT published app
                                 o.StackTraceMode    = StackTraceMode.Enhanced;
                                 o.SendDefaultPii    = false;
                                 o.MaxAttachmentSize = SentryMaxAttachmentSize;
                                 o.DeduplicateMode   = DeduplicateMode.All;
                                 o.Release           = LauncherConfig.AppCurrentVersionString;
                                 o.Environment       = Debugger.IsAttached ? "debug" : "non-debug";
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
        }

        /// <summary>
        /// Flush, ends, and dispose SentrySdk instance.
        /// </summary>
        public static void StopSentrySdk()
        {
            try
            {
                SentrySdk.Flush(TimeSpan.FromSeconds(5));
                SentrySdk.EndSession();
                ReleaseExceptionRedirect();
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"Failed when preparing to stop SentryInstance, Dispose will still be invoked!\r\n{ex}"
                                  , LogType.Error, true);
            }
            finally
            {
                _sentryInstance?.Dispose();
            }
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
            ExceptionHandler(ex, exT);

            await SentrySdk.FlushAsync(TimeSpan.FromSeconds(10));
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
            ExceptionHandlerInner(ex, exT);
        }

        
        public static string AppBuildCommit        { get; set; } = "";
        public static string AppBuildBranch        { get; set; } = "";
        public static string AppBuildRepo          { get; set; } = "";
        public static string CurrentGameCategory   { get; set; } = "";
        public static string CurrentGameRegion     { get; set; } = "";
        public static bool   CurrentGameInstalled  { get; set; }
        public static bool   CurrentGameUpdated    { get; set; }
        public static bool   CurrentGameHasPreload { get; set; }
        public static bool   CurrentGameHasDelta   { get; set; }
        
        private static void ExceptionHandlerInner(Exception ex, ExceptionType exT = ExceptionType.Handled)
        {
            // Breadcrumbs
            var buildInfo = new Breadcrumb("Build Info", "commit", 
                                         new Dictionary<string, string>
                                         {
                                                        { "Branch", AppBuildBranch },
                                                        { "Commit", AppBuildCommit },
                                                        { "Repository", AppBuildRepo }
                                         }, "BuildInfo");
            var gameInfo = new Breadcrumb("Current Loaded Game Info", "game",
                                                   new Dictionary<string, string>
                                                   {
                                                        { "Category", CurrentGameCategory },
                                                        { "Region", CurrentGameRegion },
                                                        { "Installed", CurrentGameInstalled.ToString() },
                                                        { "Updated", CurrentGameUpdated.ToString()},
                                                        { "HasPreload", CurrentGameHasPreload.ToString()},
                                                        { "HasDelta", CurrentGameHasDelta.ToString()}
                                                   }, "GameInfo");
            SentrySdk.AddBreadcrumb(buildInfo);
            SentrySdk.AddBreadcrumb(gameInfo);
            
            ex.Data[Mechanism.HandledKey] ??= exT == ExceptionType.Handled;
            ex.Data[Mechanism.MechanismKey] = exT switch
                                              {
                                                  ExceptionType.UnhandledXaml => "Application.XamlUnhandledException",
                                                  ExceptionType.UnhandledOther => "Application.UnhandledException",
                                                  _ => ex.Data[Mechanism.MechanismKey]
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
                                                   });
            }
            else
            {
                SentrySdk.CaptureException(ex);
            }
        #pragma warning restore CS0162 // Unreachable code detected
        }
    }

        #endregion
}