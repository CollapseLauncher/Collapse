using Hi3Helper.Shared.Region;
using Microsoft.Win32;
using Sentry;
using Sentry.Protocol;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable CheckNamespace
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
            Handled,

            /// <summary>
            /// Use this enum if an unhandled exception is coming from plugin operations.
            /// </summary>
            PluginUnhandled,

            /// <summary>
            /// Use this enum if the exception is happened to be expected and handled by the launcher.
            /// </summary>
            PluginHandled
        }

        #endregion

        #region Enable/Disable Sentry

        public static bool IsDisableEnvVarDetected
        {
            get
            {
                string? envVar = Environment.GetEnvironmentVariable("DISABLE_SENTRY");
                return !string.IsNullOrEmpty(envVar) &&
                       ((int.TryParse(envVar, out int isDisabledFromInt) && isDisabledFromInt == 1) ||
                        (bool.TryParse(envVar, out bool isDisabledFromBool) && !isDisabledFromBool));
            }
        }

        private static bool? _isEnabled;

        public static bool IsEnabled
        {
            get
            {
                if (!IsDisableEnvVarDetected)
                {
                    return LauncherConfig.GetAppConfigValue("SendRemoteCrashData");
                }

                Logger.LogWriteLine("Detected 'DISABLE_SENTRY' environment variable! Disabling crash data reporter...");
                LauncherConfig.SetAndSaveConfigValue("SendRemoteCrashData", false);
                return false;
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

        public static bool IsPreview { get; set; }
        
        private static IDisposable? _sentryInstance;

        public static void InitializeSentrySdk()
        {
            _sentryInstance ??=
                SentrySdk.Init(o =>
                               {
                                   o.Dsn = SentryDsn;
                                   o.AddEventProcessor(new SentryEventProcessor());
                                   o.CacheDirectoryPath = LauncherConfig.AppDataFolder;
                                   o.Debug              = IsDebugSentry;
                                   o.DiagnosticLogger =
                                       new CollapseLogger(IsDebugSentry ? SentryLevel.Debug : SentryLevel.Warning);
                                   o.DiagnosticLevel     = IsDebugSentry ? SentryLevel.Debug : SentryLevel.Warning;
                                   o.AutoSessionTracking = true;
                                   o.StackTraceMode      = StackTraceMode.Enhanced;
                                   o.DisableSystemDiagnosticsMetricsIntegration();
                                   o.IsGlobalModeEnabled = true;
                                   o.DisableWinUiUnhandledExceptionIntegration(); // Use this for trimmed/NativeAOT published app
                                   o.StackTraceMode = StackTraceMode.Enhanced;
                                   o.SendDefaultPii = false;
                                   o.MaxAttachmentSize = SentryMaxAttachmentSize;
                                   o.DeduplicateMode = DeduplicateMode.All;
                                   o.Environment = Debugger.IsAttached ? "debug" : IsPreview ? "non-debug" : "stable";
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
                                 _sentryInstance = null;
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
            if (a.ExceptionObject is not Exception ex) return;

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
        public static Guid ExceptionHandler(Exception ex, ExceptionType exT = ExceptionType.Handled)
        {
            if (!IsEnabled) return Guid.Empty;
            if (ex is AggregateException && ex.InnerException != null) ex = ex.InnerException;
            if (ex is TaskCanceledException or OperationCanceledException)
            {
                Logger.LogWriteLine($"Caught TCE/OCE exception from: {ex.Source}. Exception will not be uploaded!\r\n{ex}",
                                    LogType.Sentry);
                return Guid.Empty;
            }

            SentryId id = ExceptionHandlerInner(ex, exT);
            return Guid.TryParse(id.ToString(), out Guid guid)
                ? guid
                : Guid.Empty;
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

        private static readonly SemaphoreSlim           AsyncSemaphore = new SemaphoreSlim(1, 1);
        private static          string?                 _lastExceptionKey;
        private static          CancellationTokenSource _loopToken = new();

        // ReSharper disable once AsyncVoidMethod
        /// <summary>
        /// Clean loop last exception data to be cleaned after 20s so the exception data will be sent to Dsn again.
        /// </summary>
        private static async void ExHLoopLastEx_AutoClean(CancellationToken ct)
        {
            // if (loopToken.Token.IsCancellationRequested) return;
            try
            {
                await Task.Delay(20000, ct); // 20s delay

                // Use atomic exchange. This has the same approach by using LockObject.
                Interlocked.Exchange(ref _lastExceptionKey, null);
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
        public static async Task<Guid> ExceptionHandler_ForLoopAsync(Exception     ex,
                                                                     ExceptionType exT = ExceptionType.Handled)
        {
            try
            {
                if (!IsEnabled) return Guid.Empty;
                if (ex is AggregateException && ex.InnerException != null) ex = ex.InnerException;
                if (ex is TaskCanceledException or OperationCanceledException)
                {
                    Logger.LogWriteLine($"Caught TCE/OCE exception from: {ex.Source}. Exception will not be uploaded!\r\n{ex}",
                                        LogType.Sentry);
                    return Guid.Empty;
                }

                string exKey = $"{ex.GetType().Name}:{ex.Message}:{ex.StackTrace?.GetHashCode()}";

                // Use semaphore to lock and wait until the previous operation is done on the same thread.
                // Note from neon:
                // The reason why I changed it with SemaphoreSlim is that Lock is synchronous and may cause deadlock.
                await AsyncSemaphore.WaitAsync();
                if (exKey == _lastExceptionKey) return Guid.Empty;

                CancellationTokenSource oldToken = _loopToken;

                _loopToken        = new CancellationTokenSource();
                _lastExceptionKey = exKey;
                ExHLoopLastEx_AutoClean(_loopToken.Token); // Start auto clean loop

                // Detach and create a new thread.
                _ = Task.Run(async () =>
                             {
                                 await oldToken.CancelAsync();
                                 oldToken.Dispose();
                             }, oldToken.Token);

                // ReSharper disable once MethodSupportsCancellation
                return await Task.Run(() =>
                                      {
                                          SentryId id = ExceptionHandlerInner(ex, exT);
                                          return Guid.TryParse(id.ToString(), out Guid sentryId)
                                              ? sentryId
                                              : Guid.Empty;
                                      });
            }
            catch (Exception err)
            {
                Logger.LogWriteLine($"[SentryHelper::ExceptionHandler_ForLoopAsync] Failed to send exception!\r\n{err}",
                                    LogType.Error, true);
                return Guid.Empty;
            }
            finally
            {
                // After the operation is done, release the semaphore.
                AsyncSemaphore.Release();
            }
        }

        #region Breadcrumbs Data
        public static Func<string>? AppCdnOptionGetter { get; set; } = null;

        public static string AppBuildCommit        { get; set; } = "";
        public static string AppBuildBranch        { get; set; } = "";
        public static string AppBuildRepo          { get; set; } = "";
        public static string AppCdnOption          { get => AppCdnOptionGetter?.Invoke() ?? field; } = "";
        public static string CurrentGameCategory   { get; set; } = "";
        public static string CurrentGameRegion     { get; set; } = "";
        public static string CurrentGameLocation   { get; set; } = "";
        public static bool   CurrentGameInstalled  { get; set; }
        public static bool   CurrentGameUpdated    { get; set; }
        public static bool   CurrentGameHasPreload { get; set; }
        public static bool   CurrentGameHasDelta   { get; set; }
        public static bool   CurrentGameIsPlugin   { get; set; }

        private static int CpuThreadsTotal => Environment.ProcessorCount;

        private static string CpuName
        {
            get
            {
                try
                {
                    string cpuName;
                    string    env = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU";
                    object? reg =
                        Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\SYSTEM\CentralProcessor\0",
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
                        foreach (string subKeyName in baseKey.GetSubKeyNames())
                        {
                            if (!int.TryParse(subKeyName, out int subKeyInt) || subKeyInt < 0 || subKeyInt > 9999)
                                continue;

                            string? gpuName       = "Unknown GPU";
                            string? driverVersion = "Unknown Driver Version";
                            try
                            {
                                using RegistryKey? subKey = baseKey.OpenSubKey(subKeyName);
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

        private static Breadcrumb BuildInfo =>
            _buildInfo ??= new Breadcrumb("Build Info", "commit", new Dictionary<string, string>
            {
                { "Branch", AppBuildBranch },
                { "Commit", AppBuildCommit },
                { "Repository", AppBuildRepo },
                { "IsPreview", IsPreview.ToString() }
            }, "BuildInfo");

        private static Breadcrumb? _cpuInfo;

        private static Breadcrumb CpuInfo =>
            _cpuInfo ??= new Breadcrumb("CPU Info", "system.cpu", new Dictionary<string, string>
            {
                { "CPU Name", CpuName },
                { "Total Thread", CpuThreadsTotal.ToString() }
            }, "CPUInfo");

        private static Breadcrumb? _gpuInfo;

        private static Breadcrumb GpuInfo =>
            _gpuInfo ??= new Breadcrumb("GPU Info", "system.gpu",
                                        GetGpuInfo.ToDictionary(item => item.GpuName,
                                                                item => item.DriverVersion),
                                        "GPUInfo");

        private static Breadcrumb GameInfo =>
            new("Current Loaded Game Info", "game", new Dictionary<string, string>
            {
                { "Category", CurrentGameCategory },
                { "Region", CurrentGameRegion },
                { "Installed", CurrentGameInstalled.ToString() },
                { "Updated", CurrentGameUpdated.ToString() },
                { "HasPreload", CurrentGameHasPreload.ToString() },
                { "HasDelta", CurrentGameHasDelta.ToString() },
                { "IsGameFromPlugin", CurrentGameIsPlugin.ToString() },
                { "Location", CurrentGameLocation },
                { "CdnOption", AppCdnOption }
            }, "GameInfo");

        #endregion

        private static SentryId ExceptionHandlerInner(Exception ex, ExceptionType exT = ExceptionType.Handled)
        {
            SentrySdk.AddBreadcrumb(BuildInfo);
            SentrySdk.AddBreadcrumb(GameInfo);
            SentrySdk.AddBreadcrumb(CpuInfo);
            SentrySdk.AddBreadcrumb(GpuInfo);

            ex.Data[Mechanism.HandledKey] ??= exT == ExceptionType.Handled;

            string? methodName = null;
            string? st         = ex.StackTrace;
            if (st != null)
            {
                Match m = ExceptionFrame().Match(st);
                methodName = m.Success ? m.Value : null;
            }

            ex.Data[Mechanism.MechanismKey] ??= exT switch
                                                {
                                                    ExceptionType.UnhandledXaml => "Application.XamlUnhandledException",
                                                    ExceptionType.UnhandledOther => methodName ??
                                                        ex.Source ?? "Application.UnhandledException",
                                                    _ => methodName ?? ex.Source ?? "Application.HandledException"
                                                };

        #pragma warning disable CS0162 // Unreachable code detected
            string? logPath = LoggerBase.LogPath;
            if (logPath != null && SentryUploadLog) // Upload log file if enabled
                // ReSharper disable once HeuristicUnreachableCode
            {
                if ((bool)(ex.Data[Mechanism.HandledKey] ?? false))
                    return SentrySdk.CaptureException(ex);
                else
                {
                    // Tail to the last 100 lines of log
                    MemoryStream logStream = new MemoryStream();
                    using FileStream logFileStream =
                        new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                    bool isSuccessTailing = TryTailLinesFromStream(logFileStream, logStream, 100);
                    if (!isSuccessTailing)
                    {
                        // Send only the exception without providing attachment if fails on tailing.
                        logStream.Dispose();
                        return SentrySdk.CaptureException(ex);
                    }
                    else
                    {
                        logStream.Position = 0; // Reset stream position to the beginning
                        return SentrySdk.CaptureException(ex,
                                                          s =>
                                                          {
                                                              s.AddAttachment(logStream,
                                                                              Path.GetFileName(logPath),
                                                                              AttachmentType.Default, "text/plain");
                                                          });
                    }
                }
            }
            else
            {
                return SentrySdk.CaptureException(ex);
            }
        #pragma warning restore CS0162 // Unreachable code detected
        }

        public static bool SendExceptionFeedback(Guid sentryId, string userEmail, string user, string feedback)
        {
            if (sentryId == Guid.Empty)
            {
                Logger.LogWriteLine("[SendExceptionFeedback] SentryId is empty, feedback will not be sent!",
                                    LogType.Error,
                                    true);
                return false;
            }

            if (!IsEnabled)
            {
                Logger.LogWriteLine("[SendExceptionFeedback] Sentry is disabled, feedback will not be sent!",
                                    LogType.Error, true);
                return false;
            }

            SentryId sId = new SentryId(sentryId);
            SentrySdk.CaptureFeedback(feedback, userEmail, user, null, null, sId);
            return true;
        }

        public static bool SendGenericFeedback(string feedback, string userEmail, string user)
        {
            if (!IsEnabled)
            {
                Logger.LogWriteLine("[SendGenericFeedback] Sentry is disabled, feedback will not be sent!",
                                    LogType.Error, true);
                return false;
            }

            SentrySdk.CaptureFeedback(feedback, userEmail, user);
            return true;
        }

        [GeneratedRegex(@"(?<=\bat\s)(CollapseLauncher|Hi3Helper)\.[^\s(]+", RegexOptions.Compiled)]
        private static partial Regex ExceptionFrame();

        #endregion

        #region Private Tools
        private static unsafe bool TryTailLinesFromStream(Stream sourceStream, Stream targetStream, int maxLines, int bufferSize = 8 << 10) // == 8K buffer
        {
            // This approach reads the stream from the end backwards to find the last `maxLines` lines.

            if (!sourceStream.CanSeek)
                throw new ArgumentException("Source stream must support seeking.", nameof(sourceStream));

            const byte returnByteChar   = (byte)'\r';
            const byte lineFeedByteChar = (byte)'\n';

            long currentPos        = sourceStream.Length;
            long endWriteFromPos   = currentPos;
            long startWriteFromPos = 0;

            bool isFirst = true;

            // Seek to the end
            sourceStream.Seek(0, SeekOrigin.End);

            // Automatic buffer rent or allocating from stack
            byte[]?           poolBuffer = bufferSize > 8 << 10 ? ArrayPool<byte>.Shared.Rent(bufferSize) : null;
            scoped Span<byte> buffer     = poolBuffer ?? stackalloc byte[bufferSize];

            try
            {
                // Do the do
                do
                {
                    // Get the minimum bytes to read, just in case if the file is smaller than buffer size
                    int toRead = (int)Math.Min(bufferSize, currentPos);
                    if (toRead == 0)
                    {
                        break;
                    }

                    // Seek to the position - buffer to read
                    sourceStream.Position = currentPos - toRead;
                    int readBytes = sourceStream.ReadAtLeast(buffer[..toRead], toRead, false);
                    currentPos -= readBytes;

                    Span<byte> readData = buffer[..readBytes];
                    int        offset   = readData.Length;

                    // Scan character backwards
                    while (offset > 0)
                    {
                        // Not new line feed? Skip
                        if (readData[--offset] != lineFeedByteChar)
                        {
                            continue;
                        }

                        // Try skip trailing chars (including return char)
                        if (isFirst)
                        {
                            --endWriteFromPos;
                            while (offset > 0 &&
                                   (readData[offset - 1] == returnByteChar ||
                                    readData[offset - 1] == lineFeedByteChar))
                            {
                                --endWriteFromPos;
                                --offset;
                            }

                            isFirst = false;
                            continue;
                        }

                        // Found a new line feed, count down
                        --maxLines;
                        if (maxLines != 0)
                        {
                            continue;
                        }

                        // Set the current stream position for the next read scan
                        currentPos = currentPos + offset + 1;
                        break;
                    }

                    // Set the position to write from
                    startWriteFromPos = currentPos;
                } while (maxLines > 0);

                sourceStream.Position = startWriteFromPos;

                // Now copy the data to the target stream.
                long remainedBytes = endWriteFromPos - startWriteFromPos;
                while (remainedBytes != 0)
                {
                    int toWrite = (int)Math.Min(bufferSize, remainedBytes);
                    int read    = sourceStream.Read(buffer[..toWrite]);
                    if (read == 0) break;

                    targetStream.Write(buffer[..read]);
                    remainedBytes -= read;
                }

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (poolBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(poolBuffer);
                }
            }
        }
        #endregion
    }
}
