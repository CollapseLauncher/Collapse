using Sentry;
using Sentry.Infrastructure;
using System;
using System.Threading.Tasks;

namespace Hi3Helper.Core.Classes.Remote
{
    public class GlobalSentryHandler
    {
        public static void InitializeExceptionRedirector()
        {
            // Handle any unhandled errors in app domain
            // https://learn.microsoft.com/en-us/dotnet/api/system.appdomain?view=net-9.0
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
              var ex = args.ExceptionObject as Exception;
              SentrySdk.CaptureException(ex);
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
             SentrySdk.CaptureException(args.Exception);
             args.SetObserved();
            };
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
                    o.DisableWinUiUnhandledExceptionIntegration();
                    o.StackTraceMode = StackTraceMode.Enhanced;
                    
#if DEBUG
                    o.Distribution = "Debug";
#else
                    o.Distribution = IsPreview ? "Preview" : "Stable";
#endif
                    
                    o.MaxAttachmentSize = 5 * 1024 * 1024; // 5 MB
                });
        }
    }
}