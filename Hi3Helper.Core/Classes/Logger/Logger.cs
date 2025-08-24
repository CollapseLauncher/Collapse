using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable UnusedMember.Global

// ReSharper disable CheckNamespace

#nullable enable
namespace Hi3Helper;

public static class Logger
{
    public static readonly LoggerBase Null = new LoggerNull();

    public static ILog CurrentLogger { get; set; } = Null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogWriteLine()
        => CurrentLogger.LogWriteLine();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogWriteLine(ReadOnlySpan<char> line,
                                    LogType            type                    = LogType.Default,
                                    bool               writeToLog              = false,
                                    bool               writeTimestampOnLogFile = true)
        => CurrentLogger.LogWriteLine(line, type, writeToLog, writeTimestampOnLogFile);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogWriteLine([InterpolatedStringHandlerArgument] ref DefaultInterpolatedStringHandler interpolatedLine,
                                    LogType                              type                    = LogType.Default,
                                    bool                                 writeToLog              = false,
                                    bool                                 writeTimestampOnLogFile = true)
        => CurrentLogger.LogWriteLine(ref interpolatedLine, type, writeToLog, writeTimestampOnLogFile);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogWrite(ReadOnlySpan<char> line,
                                LogType            type                    = LogType.Default,
                                bool               appendNewLine           = false,
                                bool               writeToLog              = false,
                                bool               writeTypeTag            = false,
                                bool               writeTimestampOnLogFile = true)
        => CurrentLogger.LogWrite(line, type, appendNewLine, writeToLog, writeTypeTag, writeTimestampOnLogFile);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogWrite([InterpolatedStringHandlerArgument] ref DefaultInterpolatedStringHandler interpolatedLine,
                                LogType                              type                    = LogType.Default,
                                bool                                 appendNewLine           = false,
                                bool                                 writeToLog              = false,
                                bool                                 writeTypeTag            = false,
                                bool                                 writeTimestampOnLogFile = true)
        => CurrentLogger.LogWrite(ref interpolatedLine, type, appendNewLine, writeToLog, writeTypeTag, writeTimestampOnLogFile);

    public static Task LogWriteLineAsync(CancellationToken token = default)
        => CurrentLogger.LogWriteLineAsync(token);

    public static Task LogWriteLineAsync(string            line,
                                         LogType           type                    = LogType.Default,
                                         bool              writeToLog              = false,
                                         bool              writeTimestampOnLogFile = true,
                                         CancellationToken token                   = default)
        => CurrentLogger.LogWriteLineAsync(line, type, writeToLog, writeTimestampOnLogFile, token);

    public static Task LogWriteAsync(string            line,
                                     LogType           type                    = LogType.Default,
                                     bool              appendNewLine           = false,
                                     bool              writeToLog              = false,
                                     bool              writeTypeTag            = false,
                                     bool              writeTimestampOnLogFile = true,
                                     CancellationToken token                   = default)
        => CurrentLogger.LogWriteAsync(line, type, appendNewLine, writeToLog, writeTypeTag, writeTimestampOnLogFile, token);
}