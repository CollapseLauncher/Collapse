using CollapseLauncher.Interfaces.Class;
using CollapseLauncher.Plugins;
using Hi3Helper;
using Hi3Helper.Http;
using Hi3Helper.Shared.Region;
using Hi3Helper.Sophon;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using WinRT;
// ReSharper disable CheckNamespace
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper;

/// <summary>
/// Provides a helper for using Speed Limiter Service.
/// </summary>
/// <remarks>
/// You can call <see cref="AddBytesOrWaitAsync"/> method to pass the read bytes to the service and then throttle the speed for you.
/// <br/><br/>
/// Usage example on your code:
/// <code>
/// async Task ReadDataAsync(Stream inputStream, Stream outputStream, CancellationToken token)
/// {
///     byte[] buffer = new byte[8192];
///     int read;
///
///     nint context = SpeedLimiterService.CreateServiceContext();
///     while ((read = await inputStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
///     {
///         // Do anything...
///         ...
///
///         // Pass the read bytes to the speed limiter service, and wait if necessary until the service allows more bytes to be processed.
///         await SpeedLimiterService.AddBytesOrWaitAsync(context, read, token);
///     }
/// }
/// </code>
/// </remarks>
[GeneratedBindableCustomProperty]
public partial class SpeedLimiterService : NotifyPropertyChanged
{
    private const string LibName                          = @"Lib\Hi3Helper.Throttle.dll";
    private const string SetSharedThrottleBytesExportName = "ThrottleServiceSetSharedThrottleBytes";
    private const string GetSharedThrottleBytesExportName = "ThrottleServiceGetSharedThrottleBytes";
    private const string AddBytesOrWaitAsyncExportName    = "ThrottleServiceAddBytesOrWaitAsync";

    [LibraryImport(LibName, EntryPoint = SetSharedThrottleBytesExportName)]
    [return: MarshalAs(UnmanagedType.Error)]
    private static partial int ThrottleServiceSetSharedThrottleBytes(long bytesPerSecond, in long burstBytes);

    [LibraryImport(LibName, EntryPoint = GetSharedThrottleBytesExportName)]
    private static partial void ThrottleServiceGetSharedThrottleBytes(ref long bytesPerSecond, ref long burstBytes);

    [LibraryImport(LibName, EntryPoint = AddBytesOrWaitAsyncExportName)]
    [return: MarshalAs(UnmanagedType.Error)]
    private static partial int ThrottleServiceAddBytesOrWaitAsync(
        nint     context,
        long     readBytes,
        nint     tokenHandle,
        out nint asyncWaitHandle);

    public static SpeedLimiterService Shared { get; } = new();

    public static readonly nint AddBytesOrWaitAsyncDelegatePtr;
    public static readonly nint SetSharedThrottleBytesDelegatePtr;
    public static readonly nint GetSharedThrottleBytesDelegatePtr;

    static SpeedLimiterService()
    {
        if (!NativeLibrary.TryLoad(LibName, out nint libHandle))
        {
            throw new DllNotFoundException($"Failed to load {LibName} library!");
        }

        LoadExport(libHandle, AddBytesOrWaitAsyncExportName,    out AddBytesOrWaitAsyncDelegatePtr);
        LoadExport(libHandle, SetSharedThrottleBytesExportName, out SetSharedThrottleBytesDelegatePtr);
        LoadExport(libHandle, GetSharedThrottleBytesExportName, out GetSharedThrottleBytesDelegatePtr);

        if (Shared.IsEnabled)
        {
            ToggleSophonService(true);
            TogglePluginService(true);
            ToggleHi3HelperHttpService(true);
        }

        UpdateChanges();
        return;

        static void LoadExport(nint handle, string name, out nint exportPtr)
        {
            if (!NativeLibrary.TryGetExport(handle, name, out exportPtr))
            {
                throw new
                    EntryPointNotFoundException($"Cannot find {AddBytesOrWaitAsyncExportName} export from library!");
            }
        }
    }

    public bool IsEnabled
    {
        get => LauncherConfig.IsUseDownloadSpeedLimiter;
        set
        {
            LauncherConfig.IsUseDownloadSpeedLimiter = value;

            UpdateChanges();
            ToggleSophonService(value);
            TogglePluginService(value);
            ToggleHi3HelperHttpService(value);
            OnPropertyChanged();
        }
    }

    public double CurrentThrottleMebiBytes
    {
        get => (double)LauncherConfig.DownloadSpeedLimit / (1 << 20);
        set
        {
            LauncherConfig.DownloadSpeedLimit = (long)(value * (1 << 20));
            OnPropertyChanged();
            UpdateChanges();
        }
    }

    private static void UpdateChanges()
    {
        const long burstMultiply = 2;

        bool isEnabled       = Shared.IsEnabled;
        long currentSetBytes = (long)(Shared.CurrentThrottleMebiBytes * (1 << 20));

        // Override throttle bytes to 0 if disabled.
        if (!isEnabled || currentSetBytes < 0)
        {
            currentSetBytes = 0;
        }

        int hr = ThrottleServiceSetSharedThrottleBytes(currentSetBytes, currentSetBytes * burstMultiply);

        long bytesRead  = 0;
        long burstBytes = 0;
        ThrottleServiceGetSharedThrottleBytes(ref bytesRead, ref burstBytes);

        if (Marshal.GetExceptionForHR(hr) is {} exception)
        {
            Logger.LogWriteLine($"Failed while trying to set throttle bytes value to: {currentSetBytes} bytes.\r\n{exception}",
                                LogType.Error,
                                true);
        }
    }

    private static void TogglePluginService(bool isEnabled)
    {
        foreach (KeyValuePair<string, PluginInfo> plugin in PluginManager.PluginInstances)
        {
            plugin.Value.ToggleSpeedLimiterService(isEnabled);
        }
    }

    private static void ToggleSophonService(bool isEnabled)
    {
        SophonDownloadSpeedLimiter.AddBytesOrWaitAsyncDelegate = isEnabled
            ? AddBytesOrWaitAsync
            : null;
    }

    private static void ToggleHi3HelperHttpService(bool isEnabled)
    {
        DownloadSpeedLimiter.AddBytesOrWaitAsyncDelegate = isEnabled
            ? AddBytesOrWaitAsync
            : null;
    }

    /// <summary>
    /// Creates a context to be used for the speed limiter service. This context can be used into multiple instances or threads of your downloader.
    /// </summary>
    public static unsafe nint CreateServiceContext()
    {
        long bytesPerSecond = 0;
        long burstBytes     = 0;
        ThrottleServiceGetSharedThrottleBytes(ref bytesPerSecond, ref burstBytes);

        nint ptr = (nint)NativeMemory.Alloc(2, 8); // Context struct is 16 bytes in size.

        // Preallocate tokens and last timestamp
        ThrottleServiceContext* ptrContext = (ThrottleServiceContext*)ptr;
        ptrContext->AvailableTokens = burstBytes < bytesPerSecond ? bytesPerSecond * 2 : burstBytes;
        ptrContext->LastTimestamp   = Environment.TickCount64;

        return ptr;
    }

    /// <summary>
    /// Free the speed limiter service context.
    /// </summary>
    /// <param name="context"></param>
    public static unsafe void FreeServiceContext(nint context)
    {
        if (context != nint.Zero)
        {
            NativeMemory.Free((void*)context);
        }
    }

    /// <summary>
    /// Adds-up counter of the consumed bytes into the service, and await (throttle) if the target speed is already reached.<br/>
    /// If the service is not registered or the callback is not set, this method will simply return immediately without awaiting.
    /// </summary>
    /// <param name="context">The pointer of the service context.</param>
    /// <param name="readBytes">How many bytes consumed on current operation.</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    [SkipLocalsInit]
    public static ValueTask AddBytesOrWaitAsync(
        nint              context,
        long              readBytes,
        CancellationToken token = default)
    {
        if (context == nint.Zero)
            return ValueTask.CompletedTask;

        int hr = ThrottleServiceAddBytesOrWaitAsync(context,
                                                    readBytes,
                                                    nint.Zero,
                                                    out nint completionHandle);

        if (Marshal.GetExceptionForHR(hr) is { } ex)
            return ValueTask.FromException(ex);

        NativeThrottleOperation op = new();
        op.Initialize(completionHandle, token);

        return op.AsValueTask();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)] // Pack to 8 bytes to ensure aligning
    private struct ThrottleServiceContext
    {
        public long AvailableTokens;
        public long LastTimestamp;
    }

    internal sealed class NativeThrottleOperation : IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<bool> _core = new()
        {
            RunContinuationsAsynchronously = true
        };

        private int                           _isCompleted;
        private EventWaitHandle?              _completionWait;
        private SafeWaitHandle?               _completionSafe;
        private RegisteredWaitHandle?         _registeredWait;
        private CancellationTokenRegistration _ctr;

        public ValueTask AsValueTask()
            => new(this, _core.Version);

        public void Initialize(
            nint              completionHandle,
            CancellationToken token)
        {
            _completionSafe = new SafeWaitHandle(completionHandle, true);
            _completionWait = new EventWaitHandle(false, EventResetMode.ManualReset)
            {
                SafeWaitHandle = _completionSafe
            };

            _registeredWait =
                ThreadPool.RegisterWaitForSingleObject(_completionWait,
                                                       OnWaitSingleCompleted,
                                                       this,
                                                       -1,
                                                       true);

            if (token.CanBeCanceled)
            {
                _ctr = token.Register(OnCancellationRequested, this);
            }
        }

        private static void OnWaitSingleCompleted(object? state, bool isTimedOut)
        {
            NativeThrottleOperation op = (NativeThrottleOperation)state!;
            op.Complete();
        }

        private static void OnCancellationRequested(object? state)
        {
            NativeThrottleOperation op = (NativeThrottleOperation)state!;
            op.Cancel();
        }

        private void Complete()
        {
            if (Interlocked.Exchange(ref _isCompleted, 1) == 1)
            {
                return;
            }

            Cleanup();
            _core.SetResult(true);
        }

        private void Cancel()
        {
            if (Interlocked.Exchange(ref _isCompleted, 1) == 1)
            {
                return;
            }

            Cleanup();
            _core.SetException(new OperationCanceledException());
        }

        private void Cleanup()
        {
            _registeredWait?.Unregister(null);
            _registeredWait = null;

            _ctr.Dispose();

            _completionWait?.Dispose();
            _completionWait = null;
            _completionSafe = null;
        }

        public void GetResult(short token)
            => _core.GetResult(token);

        public ValueTaskSourceStatus GetStatus(short token)
            => _core.GetStatus(token);

        public void OnCompleted(
            Action<object?> continuation,
            object? state,
            short token,
            ValueTaskSourceOnCompletedFlags flags)
            => _core.OnCompleted(continuation, state, token, flags);
    }
}
