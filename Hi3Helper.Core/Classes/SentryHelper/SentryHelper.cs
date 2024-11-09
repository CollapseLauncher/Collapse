using Sentry;
using Sentry.Infrastructure;
using System;
using System.Threading.Tasks;
using Hi3Helper.Shared.Region;
using Sentry.Protocol;

#nullable enable
namespace Hi3Helper.SentryHelper
{
    public static class SentryHelper
    {
        public enum ExceptionType
        {
            UnhandledXaml,
            UnhandledOther,
            Handled
        }

        private static bool? _isEnabled;
        public static bool IsEnabled
        {
            get => _isEnabled ??= LauncherConfig.GetAppConfigValue("SendRemoteCrashData").ToBool();
            set
            {
                if (value == _isEnabled) return;

                if (value)
                {
                    InitializeSentrySdk();
                }
                else
                {
                    StopSentrySdk();
                }
                
                _isEnabled = value;
                LauncherConfig.SetAndSaveConfigValue("SendRemoteCrashData", value);
            }
        }
        
        
        public static void InitializeSentrySdk()
        {
            SentrySdk.Init(o =>
            {
                o.Dsn = "https://2acc39f86f2b4f5a99bac09494af13c6@bugsink.bagelnl.my.id/1";

#if DEBUG
                o.Debug = true;
                o.DiagnosticLogger = new ConsoleDiagnosticLogger(SentryLevel.Debug);
                o.DiagnosticLevel = SentryLevel.Debug;
#else
                o.Debug = false;
#endif
                o.TracesSampleRate = 1.0;
                o.IsGlobalModeEnabled = true;
                o.DisableWinUiUnhandledExceptionIntegration(); // Use this for trimmed/NativeAOT published app
                o.StackTraceMode = StackTraceMode.Enhanced;
                o.SendDefaultPii = false;
#if DEBUG
                o.Distribution = "Debug";
#else
                o.Distribution = IsPreview ? "Preview" : "Stable";
#endif
                o.MaxAttachmentSize = 5 * 1024 * 1024; // 5 MB
            });
        }

        private static void StopSentrySdk() => SentrySdk.EndSession();

        public static void InitializeExceptionRedirect()
        {
            // Handle any unhandled errors in app domain
            // https://learn.microsoft.com/en-us/dotnet/api/system.appdomain?view=net-9.0
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                if (ex == null) return;
                ex.Data[Mechanism.HandledKey] = false;
                ex.Data[Mechanism.MechanismKey] = "Application.UnhandledException";
                SentrySdk.CaptureException(ex);
                SentrySdk.FlushAsync(TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                SentrySdk.CaptureException(args.Exception);
                SentrySdk.FlushAsync(TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
                args.SetObserved();
            };
        }

        /// <summary>
        /// Sends exception to Sentry's set DSN
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="exT"></param>
        public static void ExceptionHandler(Exception ex, ExceptionType exT = ExceptionType.UnhandledOther)
        {
            if (!IsEnabled) return;
            ex.Data[Mechanism.HandledKey] = exT == ExceptionType.Handled;
            if (exT == ExceptionType.UnhandledXaml) 
                ex.Data[Mechanism.MechanismKey] = "Application.XamlUnhandledException";
            SentrySdk.CaptureException(ex);
            SentrySdk.FlushAsync(TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
        }

        public static async Task ExceptionHandlerAsync(Exception ex, ExceptionType exT = ExceptionType.UnhandledOther)
        {
            if (!IsEnabled) return;
            ex.Data[Mechanism.HandledKey] = exT == ExceptionType.Handled;
            if (exT == ExceptionType.UnhandledXaml) 
                ex.Data[Mechanism.MechanismKey] = "Application.XamlUnhandledException";
            SentrySdk.CaptureException(ex);
            await SentrySdk.FlushAsync(TimeSpan.FromSeconds(1));
        }
        
        private static Exception? _exHLoopLastEx;

        // ReSharper disable once AsyncVoidMethod
        /// <summary>
        /// Clean loop last exception data to be cleaned after 20s so the exception data will be sent to Dsn again.
        /// </summary>
        private static async void ExHLoopLastEx_AutoClean()
        {
            await Task.Delay(20000);
            if (_exHLoopLastEx == null) return;
            lock (_exHLoopLastEx)
            {
                _exHLoopLastEx = null;
            }
        }
        
        public static void ExceptionHandler_ForLoop(Exception ex, ExceptionType exT = ExceptionType.UnhandledOther)
        {
            if (!IsEnabled) return;
            if (ex == _exHLoopLastEx) return;
            _exHLoopLastEx = ex;
            ExHLoopLastEx_AutoClean(); 
            
            ex.Data[Mechanism.HandledKey] = exT == ExceptionType.Handled;
            SentrySdk.CaptureException(ex);
            SentrySdk.FlushAsync(TimeSpan.FromSeconds(1));
        }
        
    }
}