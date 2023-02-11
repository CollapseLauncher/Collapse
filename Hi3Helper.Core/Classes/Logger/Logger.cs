using System;
using System.IO;

namespace Hi3Helper
{
#nullable enable
    public static class Logger
    {
        public static ILog? _log { get; set; }
        public static void LogWriteLine() => _log?.LogWriteLine();
        public static void LogWriteLine(string line, LogType type = LogType.Default, bool writeToLog = false) => _log?.LogWriteLine(line, type, writeToLog);
        public static void LogWrite(string line, LogType type = LogType.Default, bool writeToLog = false, bool resetLinePosition = false) => _log?.LogWrite(line, type, writeToLog, resetLinePosition);
        public static void WriteLog(string line, LogType type = LogType.Default) => _log?.WriteLog(line, type);
    }
}
