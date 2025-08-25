using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.LibraryImport;
using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if !APPLYUPDATE
using static Hi3Helper.Shared.Region.LauncherConfig;
#endif

// ReSharper disable AsyncVoidMethod
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace
#pragma warning disable CA2211
#nullable enable
namespace Hi3Helper;

public class LoggerConsole : LoggerBase
{
    public static   nint ConsoleHandle;
    public static   nint ConsoleWindow => PInvoke.GetConsoleWindow();
    internal static bool VirtualTerminal;

    private readonly SimpleConsoleWin32OutStream StdOutStream;

    public LoggerConsole(string folderPath, Encoding? encoding)
        : base(folderPath, encoding)
    {
        AllocateConsole();
        StdOutStream = new SimpleConsoleWin32OutStream(ConsoleHandle);
    }

    #region Console Allocation Methods
    public static void DisposeConsole()
    {
        if (ConsoleHandle == nint.Zero)
        {
            return;
        }

        nint consoleWindow = ConsoleWindow;

        if (ConsoleWindow == nint.Zero)
        {
            return;
        }
        PInvoke.ShowWindow(consoleWindow, 0);
    }

    public static void AllocateConsole(bool isConsoleApp = false)
    {
        if (ConsoleWindow == nint.Zero)
            isConsoleApp = true;

        if (isConsoleApp && !PInvoke.AttachConsole(0xFFFFFFFF))
        {
            if (!PInvoke.AllocConsole())
            {
                throw new ContextMarshalException($"Failed to attach or allocate console with error code: {Win32Error.GetLastWin32ErrorMessage()}");
            }
        }

        const uint GENERIC_READ     = 0x80000000;
        const uint GENERIC_WRITE    = 0x40000000;
        const uint FILE_SHARE_WRITE = 2;
        const uint OPEN_EXISTING    = 3;
        ConsoleHandle = PInvoke.CreateFile("CONOUT$", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_WRITE, (uint)0, OPEN_EXISTING, 0, 0);

        const int STD_OUTPUT_HANDLE = -11;
        PInvoke.SetStdHandle(STD_OUTPUT_HANDLE, ConsoleHandle);

        Console.OutputEncoding = Encoding.UTF8;

        if (PInvoke.GetConsoleMode(ConsoleHandle, out uint mode))
        {
            const uint ENABLE_PROCESSED_OUTPUT = 1;
            const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 4;
            const uint DISABLE_NEWLINE_AUTO_RETURN = 8;
            mode |= ENABLE_PROCESSED_OUTPUT | ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
            if (PInvoke.SetConsoleMode(ConsoleHandle, mode))
            {
                VirtualTerminal = true;
            }
        }

        try
        {
            if (ConsoleHandle != nint.Zero)
            {
                PInvoke.ShowWindow(ConsoleWindow, 5);
            }

            string instanceIndicator = "";
            int    instanceCount     = ProcessChecker.EnumerateInstances(ILoggerHelper.GetILogger());

            if (instanceCount > 1) instanceIndicator = $" - #{instanceCount}";
            Console.Title = $"Collapse Console{instanceIndicator}";

#if !APPLYUPDATE
            Windowing.SetWindowIcon(PInvoke.GetConsoleWindow(), AppIconLarge, AppIconSmall);
#endif
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to set console title or icon: \r\n{ex}");
        }
    }
    #endregion

    #region Logging Methods

    [MethodImpl(MethodImplOptions.NoInlining)]
    public sealed override void LogWriteLine()
        => Console.WriteLine();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public sealed override void LogWriteLine(scoped ReadOnlySpan<char> line,
                                             LogType                   type                    = LogType.Info,
                                             bool                      writeToLogFile          = false,
                                             bool                      writeTimestampOnLogFile = true)
    {
        WriteLineToStreamCore(StdOutStream, line, type);

        if (!writeToLogFile)
        {
            return;
        }

        WriteLineToStreamCore(LogWriter.BaseStream,
                              line,
                              type,
                              isWriteColor: false,
                              isWriteTimestamp: writeTimestampOnLogFile);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public sealed override void LogWriteLine(ref DefaultInterpolatedStringHandler interpolatedLine,
                                             LogType                              type = LogType.Info,
                                             bool                                 writeToLogFile = false,
                                             bool                                 writeTimestampOnLogFile = true)
    {
        ReadOnlySpan<char> line = GetInterpolateStringSpan(ref interpolatedLine);

        try
        {
            LogWriteLine(line, type, writeToLogFile, writeTimestampOnLogFile);
        }
        finally
        {
            ClearInterpolateString(ref interpolatedLine);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public sealed override void LogWrite(ReadOnlySpan<char> line,
                                         LogType            type                    = LogType.Info,
                                         bool               appendNewLine           = false,
                                         bool               writeToLogFile          = false,
                                         bool               writeTypeTag            = false,
                                         bool               writeTimestampOnLogFile = true)
    {
        WriteLineToStreamCore(StdOutStream,
                              line,
                              type,
                              appendNewLine: appendNewLine,
                              isWriteColor: true,
                              isWriteTagType: writeTypeTag,
                              isWriteTimestamp: false);

        if (!writeToLogFile)
        {
            return;
        }

        WriteLineToStreamCore(LogWriter.BaseStream,
                              line,
                              type,
                              appendNewLine: appendNewLine,
                              isWriteColor: false,
                              isWriteTagType: writeTimestampOnLogFile,
                              isWriteTimestamp: writeTimestampOnLogFile);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public sealed override void LogWrite(ref DefaultInterpolatedStringHandler interpolatedLine,
                                         LogType                              type                    = LogType.Info,
                                         bool                                 appendNewLine           = false,
                                         bool                                 writeToLogFile          = false,
                                         bool                                 writeTypeTag            = false,
                                         bool                                 writeTimestampOnLogFile = true)
    {
        ReadOnlySpan<char> line = GetInterpolateStringSpan(ref interpolatedLine);

        try
        {
            LogWrite(line, type, appendNewLine, writeToLogFile, writeTypeTag, writeTimestampOnLogFile);
        }
        finally
        {
            ClearInterpolateString(ref interpolatedLine);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public sealed override Task LogWriteLineAsync(CancellationToken token = default)
        => Console.Out.WriteLineAsync();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public sealed override async Task LogWriteLineAsync(string            line,
                                                        LogType           type                    = LogType.Info,
                                                        bool              writeToLogFile          = false,
                                                        bool              writeTimestampOnLogFile = true,
                                                        CancellationToken token                   = default)
    {
        await WriteLineToStreamCoreAsync(StdOutStream,
                                         line,
                                         type,
                                         token: token).ConfigureAwait(false);

        if (!writeToLogFile)
        {
            return;
        }
        await WriteLineToStreamCoreAsync(LogWriter.BaseStream,
                                         line,
                                         type,
                                         isWriteColor: false,
                                         isWriteTimestamp: writeTimestampOnLogFile,
                                         token: token).ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public sealed override async Task LogWriteAsync(string            line,
                                                    LogType           type                    = LogType.Info,
                                                    bool              appendNewLine           = false,
                                                    bool              writeToLogFile          = false,
                                                    bool              writeTypeTag            = false,
                                                    bool              writeTimestampOnLogFile = true,
                                                    CancellationToken token                   = default)
    {
        await WriteLineToStreamCoreAsync(StdOutStream,
                                         line,
                                         type,
                                         appendNewLine: appendNewLine,
                                         isWriteColor: true,
                                         isWriteTagType: writeTypeTag,
                                         isWriteTimestamp: false,
                                         token: token).ConfigureAwait(false);

        if (!writeToLogFile)
        {
            return;
        }
        await WriteLineToStreamCoreAsync(LogWriter.BaseStream,
                                         line,
                                         type,
                                         appendNewLine: appendNewLine,
                                         isWriteColor: false,
                                         isWriteTagType: writeTimestampOnLogFile,
                                         isWriteTimestamp: writeTimestampOnLogFile,
                                         token: token).ConfigureAwait(false);
    }
    #endregion

    protected override void DisposeCore(bool onlyReset = false)
    {
        // Dispose console and base if requested.
        if (!onlyReset)
        {
            DisposeConsole();
        }
        base.DisposeCore(onlyReset);
    }
}