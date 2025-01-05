namespace Hi3Helper
{
#nullable enable
    public static class Logger
    {
        public static ILog? CurrentLogger { get; set; }
        public static void LogWriteLine() => CurrentLogger?.LogWriteLine();
        public static void LogWriteLine(string line, LogType type = LogType.Default, bool writeToLog = false) => CurrentLogger?.LogWriteLine(line, type, writeToLog);
        public static void LogWrite(string line, LogType type = LogType.Default, bool writeToLog = false, bool resetLinePosition = false) => CurrentLogger?.LogWrite(line, type, writeToLog, resetLinePosition);
        public static void WriteLog(string line, LogType type = LogType.Default) => CurrentLogger?.WriteLog(line, type);
    }
}
