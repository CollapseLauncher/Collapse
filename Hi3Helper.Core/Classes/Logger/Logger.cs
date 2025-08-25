using Hi3Helper.Shared.Region;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable CheckNamespace
// ReSharper disable UnusedMember.Global

#pragma warning disable CA2211
#nullable enable
namespace Hi3Helper;

public static class Logger
{
    public static readonly LoggerBase   Null           = new LoggerNull();
    public static          ILog?        CurrentLogger;
    public static          StreamWriter LogFileWriter => CurrentLogger?.LogWriter ?? StreamWriter.Null;

    public static void UseConsoleLog(bool isEnable)
    {
        using (LoggerBase.LockObject.EnterScope())
        {
            switch (isEnable)
            {
                case false when CurrentLogger is LoggerNull:
                case true when CurrentLogger is LoggerConsole:
                    return;
            }

            CurrentLogger?.Dispose();

            string logPath = LauncherConfig.AppGameLogsFolder;
            Interlocked.Exchange(ref CurrentLogger,
                                 isEnable
                                     ? new LoggerConsole(logPath, Encoding.UTF8)
                                     : new LoggerNull(logPath, Encoding.UTF8));
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogWriteLine()
        => CurrentLogger?.LogWriteLine();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogWriteLine(ReadOnlySpan<char> line,
                                    LogType            type                    = LogType.Info,
                                    bool               writeToLog              = false,
                                    bool               writeTimestampOnLogFile = true)
        => CurrentLogger?.LogWriteLine(line, type, writeToLog, writeTimestampOnLogFile);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogWriteLine([InterpolatedStringHandlerArgument] ref DefaultInterpolatedStringHandler interpolatedLine,
                                    LogType                              type                    = LogType.Info,
                                    bool                                 writeToLog              = false,
                                    bool                                 writeTimestampOnLogFile = true)
        => CurrentLogger?.LogWriteLine(ref interpolatedLine, type, writeToLog, writeTimestampOnLogFile);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogWrite(ReadOnlySpan<char> line,
                                LogType            type                    = LogType.Info,
                                bool               appendNewLine           = false,
                                bool               writeToLog              = false,
                                bool               writeTypeTag            = false,
                                bool               writeTimestampOnLogFile = true)
        => CurrentLogger?.LogWrite(line, type, appendNewLine, writeToLog, writeTypeTag, writeTimestampOnLogFile);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogWrite([InterpolatedStringHandlerArgument] ref DefaultInterpolatedStringHandler interpolatedLine,
                                LogType                              type                    = LogType.Info,
                                bool                                 appendNewLine           = false,
                                bool                                 writeToLog              = false,
                                bool                                 writeTypeTag            = false,
                                bool                                 writeTimestampOnLogFile = true)
        => CurrentLogger?.LogWrite(ref interpolatedLine, type, appendNewLine, writeToLog, writeTypeTag, writeTimestampOnLogFile);

    public static Task LogWriteLineAsync(CancellationToken token = default)
        => CurrentLogger?.LogWriteLineAsync(token) ?? Task.CompletedTask;

    public static Task LogWriteLineAsync(string            line,
                                         LogType           type                    = LogType.Info,
                                         bool              writeToLog              = false,
                                         bool              writeTimestampOnLogFile = true,
                                         CancellationToken token                   = default)
        => CurrentLogger?.LogWriteLineAsync(line, type, writeToLog, writeTimestampOnLogFile, token) ?? Task.CompletedTask;

    public static Task LogWriteAsync(string            line,
                                     LogType           type                    = LogType.Info,
                                     bool              appendNewLine           = false,
                                     bool              writeToLog              = false,
                                     bool              writeTypeTag            = false,
                                     bool              writeTimestampOnLogFile = true,
                                     CancellationToken token                   = default)
        => CurrentLogger?.LogWriteAsync(line, type, appendNewLine, writeToLog, writeTypeTag, writeTimestampOnLogFile, token) ?? Task.CompletedTask;
}