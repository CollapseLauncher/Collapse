using System;
using System.IO;

namespace Hi3Helper
{
    public enum LogType { Error, Warning, Default, Scheme, Empty, NoTag, Game }
    public static class Logger
    {
        public static StreamWriter logstream;
        internal static string logdir,
                               filename;
        public static bool EnableConsole = true;
        private static ILogger logger;
        public static string GetCurrentTime(string format) => DateTime.Now.ToLocalTime().ToString(format);

        public static void InitLog(bool enableLog = true, string defaultLogLocation = null)
        {
            if (!EnableConsole)
                logger = new DummyLogger();
            else
                logger = new ConsoleLogger();

            if (enableLog)
            {
                logdir = string.IsNullOrEmpty(defaultLogLocation) ?
                        Directory.GetCurrentDirectory()
                      : defaultLogLocation;

                if (!Directory.Exists(logdir))
                    Directory.CreateDirectory(logdir);
                filename = $"log-{GetCurrentTime("yyyy-MM-dd")}.log";

                // if (logstream == null)
                //    logstream = new StreamWriter(Path.Combine(logdir, filename), true);
            }
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
