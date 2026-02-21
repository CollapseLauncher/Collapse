using CollapseLauncher.Interfaces.Class;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.Native.LibraryImport;
using Microsoft.Win32.SafeHandles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
    [LibraryImport("Lib\\Hi3Helper.Throttle.dll", EntryPoint = "ThrottleServiceSetSharedThrottleBytes")]
    [return: MarshalAs(UnmanagedType.Error)]
    private static partial int ThrottleServiceSetSharedThrottleBytes(long bytesPerSecond, in long burstBytes);

    [LibraryImport("Lib\\Hi3Helper.Throttle.dll", EntryPoint = "ThrottleServiceAddBytesOrWaitAsync")]
    [return: MarshalAs(UnmanagedType.Error)]
    private static partial int ThrottleServiceAddBytesOrWaitAsync(
        nint     context,
        long     readBytes,
        nint     tokenHandle,
        out nint asyncWaitHandle);

    public static SpeedLimiterService Shared { get; } = new();

    static SpeedLimiterService() => UpdateChanges();

    public bool IsEnabled
    {
        get => LauncherConfig.IsUseDownloadSpeedLimiter;
        set
        {
            LauncherConfig.IsUseDownloadSpeedLimiter = value;
            OnPropertyChanged();
            UpdateChanges();
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
        if (Marshal.GetExceptionForHR(hr) is {} exception)
        {
            Logger.LogWriteLine($"Failed while trying to set throttle bytes value to: {currentSetBytes} bytes.\r\n{exception}",
                                LogType.Error,
                                true);
        }
    }

    /// <summary>
    /// Creates a context to be used for the speed limiter service. This context can be used into multiple instances or threads of your downloader.
    /// </summary>
    /// <returns></returns>
    public static unsafe nint CreateServiceContext()
        => (nint)NativeMemory.Alloc(2, 8); // Context struct is 16 bytes in size.

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
        nint tokenHandle = token.WaitHandle.SafeWaitHandle.DangerousGetHandle();
        int hr = ThrottleServiceAddBytesOrWaitAsync(context,
                                                    readBytes,
                                                    tokenHandle,
                                                    out nint asyncWaitHandle);

        AsyncValueTaskMethodBuilder valueTaskCs = new();
        if (Marshal.GetExceptionForHR(hr) is { } exception)
        {
            valueTaskCs.SetException(exception);
            return valueTaskCs.Task;
        }

        SafeWaitHandle safeHandle = new(asyncWaitHandle, false);
        WaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset)
        {
            SafeWaitHandle = safeHandle
        };

        ThreadPool.UnsafeRegisterWaitForSingleObject(waitHandle,
            DisposeWaitHandleCallback,
            null,
            -1,
            true);

        return valueTaskCs.Task;

        void DisposeWaitHandleCallback(object? state, bool isTimedOut)
        {
            safeHandle.Dispose();
            waitHandle.Dispose();

            if (asyncWaitHandle != nint.Zero)
            {
                _ = PInvoke.CloseHandle(asyncWaitHandle);
            }

            valueTaskCs.SetResult();
        }
    }
}
