using System;
using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
using System.Reflection;
using System.IO;
using System.Windows.Media;
using System.Windows.Controls;

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

    public class DummyLogger : ILogger
    {
        public void LogWriteLine(string i = "", LogType a = LogType.Default, bool writeToLog = false)
        {
            if (writeToLog)
                WriteLog(i, a);
        }
        public void LogWrite(string i = "", LogType a = LogType.Default, bool writeToLog = false, bool overwriteCurLine = false)
        {
            if (writeToLog)
                WriteLog(i, a);
        }

        public void WriteLog(string i, LogType a = LogType.Default)
        {
            using (Logger.logstream = new StreamWriter(Path.Combine(Logger.logdir, Logger.filename), true))
                Logger.logstream.WriteLine(GetLog(i, a));
        }

        string GetLog(string i, LogType a)
        {
            i = a switch
            {
                LogType.Error => $"[Erro]\t{i}",
                LogType.Warning => $"[Warn]\t{i}",
                LogType.Default => $"[Info]\t{i}",
                LogType.Scheme => $"[Schm]\t{i}",
                _ => $"\t\t{i}",
            };
            return $"[{Logger.GetCurrentTime("HH:mm:ss.fff")}] {i.Replace("\n", $"{new string(' ', 22)}\t")}";
        }
    }

    public class ConsoleLogger : ILogger
    {

        private protected string ColorizePrint(string i, LogType a)
        {
            switch (a)
            {
                case LogType.Error:
                    i = $"\u001b[31;1m[Erro]\u001b[0m\t{i}";
                    break;
                case LogType.Warning:
                    i = $"\u001b[33;1m[Warn]\u001b[0m\t{i}";
                    break;
                default:
                case LogType.Default:
                    i = $"\u001b[32;1m[Info]\u001b[0m\t{i}";
                    break;
                case LogType.Scheme:
                    i = $"\u001b[34;1m[Schm]\u001b[0m\t{i}";
                    break;
                case LogType.NoTag:
                    i = $"\t{i}";
                    break;
                case LogType.Empty:
                    break;
            }
            return i;
        }

        private protected void PrintLine(string i, LogType a) => Console.WriteLine(ColorizePrint(i, a));
        private protected void Print(string i, LogType a) => Console.Write(ColorizePrint(i, a));

        public void SetLabelAttrib(out Label i, string s, SolidColorBrush a)
        {
            i = new Label
            {
                Foreground = a,
                Content = s
            };
        }

        public void LogWriteLine(string i = "", LogType a = LogType.Default, bool writeToLog = false)
        {
            if (writeToLog)
                WriteLog(i, a);
            if (!Logger.DisableConsole)
                PrintLine(i, a);
        }

        public void LogWrite(string i = "", LogType a = LogType.Default, bool writeToLog = false, bool overwriteCurLine = false)
        {
            if (writeToLog)
                WriteLog(i, a);

            if (!Logger.DisableConsole)
            {
                if (overwriteCurLine)
                    Console.SetCursorPosition(0, Console.CursorTop);
                Print(i, a);
            }
        }

        public void WriteLog(string i = "", LogType a = LogType.Default)
        {
            using (Logger.logstream = new StreamWriter(Path.Combine(Logger.logdir, Logger.filename), true))
                Logger.logstream.WriteLine(GetLog(i, a));
        }

        private protected string GetLog(string i, LogType a)
        {
            i = a switch
            {
                LogType.Error => $"[Erro]\t{i}",
                LogType.Warning => $"[Warn]\t{i}",
                LogType.Default => $"[Info]\t{i}",
                LogType.Scheme => $"[Schm]\t{i}",
                _ => $"\t\t{i}",
            };
            return $"[{Logger.GetCurrentTime("HH:mm:ss.fff")}] {i.Replace("\n", $"{new string(' ', 22)}\t")}";
        }
    }
}
