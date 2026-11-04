using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable AsyncVoidMethod
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace
#pragma warning disable IDE0290
#pragma warning disable CA2211
namespace Hi3Helper.Log;

public sealed class LoggerConsole : LoggerBase
{
    public static bool VirtualTerminal;
    public static nint ConsoleHandle;
    public static nint ConsoleWindow => PInvoke.Kernel32.GetConsoleWindow();

    private readonly SimpleConsoleWin32OutStream StdOutStream;

    public LoggerConsole(string folderPath, Encoding? encoding, string? titleName = null)
        : base(folderPath, encoding, () => AllocateConsole(titleName))
    {
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
        _ = PInvoke.User32.ShowWindow(consoleWindow, 0);
    }

    public static void AllocateConsole(string? titleName = null)
    {
        bool isConsoleApp = ConsoleWindow != nint.Zero;
        if (!isConsoleApp && PInvoke.Kernel32.AttachConsole(0xFFFFFFFF) == 0)
        {
            if (PInvoke.Kernel32.AllocConsole() == 0)
            {
                throw new ContextMarshalException($"Failed to attach or allocate console with error code: {Marshal.GetLastPInvokeErrorMessage()}");
            }
        }

        const uint GENERIC_READ     = 0x80000000;
        const uint GENERIC_WRITE    = 0x40000000;
        const uint FILE_SHARE_WRITE = 2;
        const uint OPEN_EXISTING    = 3;
        ConsoleHandle = PInvoke.Kernel32.CreateFile("CONOUT$", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_WRITE, 0, OPEN_EXISTING, 0, 0);

        const int STD_OUTPUT_HANDLE = -11;
        _ = PInvoke.Kernel32.SetStdHandle(STD_OUTPUT_HANDLE, ConsoleHandle);

        Console.OutputEncoding = Encoding.UTF8;

        if (PInvoke.Kernel32.GetConsoleMode(ConsoleHandle, out uint mode) == 1)
        {
            const uint ENABLE_PROCESSED_OUTPUT            = 1;
            const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 4;
            mode |= ENABLE_PROCESSED_OUTPUT | ENABLE_VIRTUAL_TERMINAL_PROCESSING;
            if (PInvoke.Kernel32.SetConsoleMode(ConsoleHandle, mode) == 1)
            {
                VirtualTerminal = true;
            }
        }

        try
        {
            if (ConsoleHandle != nint.Zero)
            {
                _ = PInvoke.User32.ShowWindow(ConsoleWindow, 5);
            }

            if (!string.IsNullOrEmpty(titleName))
                Console.Title = titleName;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to set console title or icon: \r\n{ex}");
        }
    }
    #endregion

    #region Logging Methods

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override void WriteLine(bool writeToLogFile          = false,
                                   bool writeTimestampOnLogFile = true)
    {
        Console.WriteLine();

        if (!writeToLogFile)
        {
            return;
        }

        base.WriteLine(false, writeTimestampOnLogFile);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override void WriteLine(ReadOnlySpan<char> line,
                                   LogType            type                    = LogType.Info,
                                   bool               writeToLogFile          = false,
                                   bool               writeTimestampOnLogFile = true)
    {
        WriteLineToStreamCore(StdOutStream, line, type, isBeginWithCarriageReturn: true);

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
    public override void WriteLine(
        [InterpolatedStringHandlerArgument] ref DefaultInterpolatedStringHandler interpolatedLine,
        LogType                                                                  type                    = LogType.Info,
        bool                                                                     writeToLogFile          = false,
        bool                                                                     writeTimestampOnLogFile = true)
    {
#if NET10_0_OR_GREATER
        try
        {
            WriteLine(interpolatedLine.Text,
                      type,
                      writeToLogFile,
                      writeTimestampOnLogFile);
        }
        finally
        {
            interpolatedLine.Clear();
        }
#else
        ReadOnlySpan<char> line = GetInterpolateStringSpan(ref interpolatedLine);

        try
        {
            LogWriteLine(line, type, writeToLogFile, writeTimestampOnLogFile);
        }
        finally
        {
            ClearInterpolateString(ref interpolatedLine);
        }
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override void Write(ReadOnlySpan<char> line,
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
    public override void Write(
        [InterpolatedStringHandlerArgument] ref DefaultInterpolatedStringHandler interpolatedLine,
        LogType                                                                  type                    = LogType.Info,
        bool                                                                     appendNewLine           = false,
        bool                                                                     writeToLogFile          = false,
        bool                                                                     writeTypeTag            = false,
        bool                                                                     writeTimestampOnLogFile = true)
    {
#if NET10_0_OR_GREATER
        try
        {
            Write(interpolatedLine.Text,
                  type,
                  appendNewLine,
                  writeToLogFile,
                  writeTypeTag,
                  writeTimestampOnLogFile);
        }
        finally
        {
            interpolatedLine.Clear();
        }
#else
        ReadOnlySpan<char> line = GetInterpolateStringSpan(ref interpolatedLine);

        try
        {
            LogWrite(line, type, appendNewLine, writeToLogFile, writeTypeTag, writeTimestampOnLogFile);
        }
        finally
        {
            ClearInterpolateString(ref interpolatedLine);
        }
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override ValueTask WriteLineAsync(CancellationToken token = default)
        => new(Console.Out.WriteLineAsync());

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override async ValueTask WriteLineAsync(string            line,
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
    public override async ValueTask WriteAsync(string            line,
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