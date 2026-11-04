using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable CheckNamespace
// ReSharper disable UnusedMember.Global

#pragma warning disable CA2211
namespace Hi3Helper.Log;

public static class Logger
{
    public static readonly LoggerBase   Null = new LoggerNull();
    public static          ILog?        CurrentLogger;
    public static          StreamWriter LogFileWriter => CurrentLogger?.LogWriter ?? StreamWriter.Null;

    public static void UseConsoleLog(bool isEnable, string? logPath = null)
    {
        switch (isEnable)
        {
            case false when CurrentLogger is LoggerNull:
            case true when CurrentLogger is LoggerConsole:
                return;
        }

        logPath ??= Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? "", "logs");
        Interlocked.Exchange(ref CurrentLogger,
                             isEnable
                                 ? new LoggerConsole(logPath, Encoding.UTF8)
                                 : new LoggerNull(logPath, Encoding.UTF8))?.Dispose();
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteLine()
        => CurrentLogger?.WriteLine();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteLine(ReadOnlySpan<char> line,
                                 LogType            type                    = LogType.Info,
                                 bool               writeToLog              = false,
                                 bool               writeTimestampOnLogFile = true)
        => CurrentLogger?.WriteLine(line, type, writeToLog, writeTimestampOnLogFile);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteLine(
        [InterpolatedStringHandlerArgument] ref DefaultInterpolatedStringHandler interpolatedLine,
        LogType                                                                  type                    = LogType.Info,
        bool                                                                     writeToLog              = false,
        bool                                                                     writeTimestampOnLogFile = true)
        => CurrentLogger?.WriteLine(ref interpolatedLine, type, writeToLog, writeTimestampOnLogFile);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ReadOnlySpan<char> line,
                             LogType            type                    = LogType.Info,
                             bool               appendNewLine           = false,
                             bool               writeToLog              = false,
                             bool               writeTypeTag            = false,
                             bool               writeTimestampOnLogFile = true)
        => CurrentLogger?.Write(line, type, appendNewLine, writeToLog, writeTypeTag, writeTimestampOnLogFile);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(
        [InterpolatedStringHandlerArgument] ref DefaultInterpolatedStringHandler interpolatedLine,
        LogType                                                                  type                    = LogType.Info,
        bool                                                                     appendNewLine           = false,
        bool                                                                     writeToLog              = false,
        bool                                                                     writeTypeTag            = false,
        bool                                                                     writeTimestampOnLogFile = true)
        => CurrentLogger?.Write(ref interpolatedLine, type, appendNewLine, writeToLog, writeTypeTag, writeTimestampOnLogFile);

    public static ValueTask WriteLineAsync(CancellationToken token = default)
        => CurrentLogger?.WriteLineAsync(token) ?? ValueTask.CompletedTask;

    public static ValueTask WriteLineAsync(string            line,
                                           LogType           type                    = LogType.Info,
                                           bool              writeToLog              = false,
                                           bool              writeTimestampOnLogFile = true,
                                           CancellationToken token                   = default)
        => CurrentLogger?.WriteLineAsync(line, type, writeToLog, writeTimestampOnLogFile, token) ?? ValueTask.CompletedTask;

    public static ValueTask WriteAsync(string            line,
                                       LogType           type                    = LogType.Info,
                                       bool              appendNewLine           = false,
                                       bool              writeToLog              = false,
                                       bool              writeTypeTag            = false,
                                       bool              writeTimestampOnLogFile = true,
                                       CancellationToken token                   = default)
        => CurrentLogger?.WriteAsync(line, type, appendNewLine, writeToLog, writeTypeTag, writeTimestampOnLogFile, token) ?? ValueTask.CompletedTask;
}