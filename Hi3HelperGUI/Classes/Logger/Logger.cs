using System;
using System.Reflection;
using System.IO;

namespace Hi3HelperGUI
{
    public enum LogType { Error, Warning, Default, Scheme, Empty, NoTag }
    public static class Logger
    {
        internal static StreamWriter logstream;
        internal static string logdir,
                               filename;
        public static bool DisableConsole = false;
        private static ILogger logger;
        public static string GetCurrentTime(string format) => DateTime.Now.ToLocalTime().ToString(format);
        public static Version GetRunningVersion() => Assembly.GetExecutingAssembly().GetName().Version;

        public static void InitLog()
        {
            logger = DisableConsole ? new DummyLogger() : new ConsoleLogger();

            logdir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            if (!Directory.Exists(logdir))
                Directory.CreateDirectory(logdir);
            filename = $"log-{GetCurrentTime("yyyy-MM-dd")}.log";
            // LogWriteLine($"App started! v{GetRunningVersion()}", LogType.Scheme, true);
        }

        public static void LogWriteLine() => logger.LogWriteLine(string.Empty, LogType.Empty);
        public static void LogWriteLine(
            string i = "",
            LogType a = LogType.Default,
            bool writeToLog = false) =>
            logger.LogWriteLine(i, a, writeToLog);

        public static void LogWrite(
            string i = "",
            LogType a = LogType.Default,
            bool writeToLog = false,
            bool overwriteCurLine = false) =>
            logger.LogWrite(i, a, writeToLog, overwriteCurLine);

        public static void WriteLog(string i = "", LogType a = LogType.Default) =>
            logger.WriteLog(i, a);
    }
}
