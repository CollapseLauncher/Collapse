using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.LibraryImport;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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
    internal static bool VirtualTerminal;

    [field: AllowNull, MaybeNull]
    private static Stream StdOutStream => field ??= Console.OpenStandardOutput();

    [field: AllowNull, MaybeNull]
    private static Stream StdErrStream => field ??= Console.OpenStandardError();

    public LoggerConsole(string folderPath, Encoding? encoding, bool isConsoleApp = false)
        : base(folderPath, encoding)
#if !APPLYUPDATE
        => AllocateConsole(isConsoleApp);
#else
        ;
#endif

    #region Console Allocation Methods
    public static void DisposeConsole()
    {
        if (ConsoleHandle == nint.Zero)
        {
            return;
        }

        nint consoleWindow = PInvoke.GetConsoleWindow();
        PInvoke.ShowWindow(consoleWindow, 0);
    }

    public static void AllocateConsole(bool isConsoleApp = false)
    {
        nint consoleWindow = PInvoke.GetConsoleWindow();

        if (ConsoleHandle != nint.Zero)
        {
            PInvoke.ShowWindow(consoleWindow, 5);
            return;
        }

        if (consoleWindow != nint.Zero)
            isConsoleApp = true;

        if (!isConsoleApp && !PInvoke.AttachConsole(0xFFFFFFFF))
        {
            if (!PInvoke.AllocConsole())
            {
                throw new ContextMarshalException($"Failed to attach or allocate console with error code: {Win32Error.GetLastWin32ErrorMessage()}");
            }
        }

        const uint GENERIC_READ = 0x80000000;
        const uint GENERIC_WRITE = 0x40000000;
        const uint FILE_SHARE_WRITE = 2;
        const uint OPEN_EXISTING = 3;
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

    #region Util Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Stream GetConsoleTextStreamFromType(LogType type)
        => type switch
           {
               LogType.Error => StdErrStream,
               _ => StdOutStream
           };
    #endregion

    #region Logging Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sealed override void LogWriteLine()
        => Console.WriteLine();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sealed override void LogWriteLine(ReadOnlySpan<char> line,
                                             LogType            type                    = LogType.Default,
                                             bool               writeToLogFile          = false,
                                             bool               writeTimestampOnLogFile = true)
    {
        Stream consoleStream = GetConsoleTextStreamFromType(type);

        WriteLineToStreamCore(consoleStream, line, type);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sealed override void LogWriteLine(ref DefaultInterpolatedStringHandler interpolatedLine,
                                             LogType                              type = LogType.Default,
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sealed override void LogWrite(ReadOnlySpan<char> line,
                                         LogType            type                    = LogType.Default,
                                         bool               appendNewLine           = false,
                                         bool               writeToLogFile          = false,
                                         bool               writeTypeTag            = false,
                                         bool               writeTimestampOnLogFile = true)
    {
        Stream consoleStream = GetConsoleTextStreamFromType(type);

        WriteLineToStreamCore(consoleStream,
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sealed override void LogWrite(ref DefaultInterpolatedStringHandler interpolatedLine,
                                         LogType                              type                    = LogType.Default,
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
                                                        LogType           type                    = LogType.Default,
                                                        bool              writeToLogFile          = false,
                                                        bool              writeTimestampOnLogFile = true,
                                                        CancellationToken token                   = default)
    {
        Stream consoleStream = GetConsoleTextStreamFromType(type);
        await WriteLineToStreamCoreAsync(consoleStream,
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
                                                    LogType           type                    = LogType.Default,
                                                    bool              appendNewLine           = false,
                                                    bool              writeToLogFile          = false,
                                                    bool              writeTypeTag            = false,
                                                    bool              writeTimestampOnLogFile = true,
                                                    CancellationToken token                   = default)
    {
        Stream consoleStream = GetConsoleTextStreamFromType(type);

        await WriteLineToStreamCoreAsync(consoleStream,
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

    public sealed override void Dispose()
    {
        // Dispose console and base if requested.
        DisposeConsole();
        base.Dispose();

        GC.SuppressFinalize(this);
    }
}