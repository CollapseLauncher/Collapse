using System;
//using System.Collections.Generic;
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
    public partial class Logger : Window
    {
        protected internal static StreamWriter logstream;
        protected internal static string logdir,
                                         filename;
        public static bool DisableConsole = false;
        public enum LogType { Error, Warning, Default, Scheme, Empty, NoTag }

        public static Version GetRunningVersion() => Assembly.GetExecutingAssembly().GetName().Version;

        public static void InitLog()
        {
            logdir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            if (!Directory.Exists(logdir))
                Directory.CreateDirectory(logdir);
            filename = $"log-{GetCurrentTime("yyyy-MM-dd")}.log";
            LogWriteLine($"App started! v{GetRunningVersion()}", LogType.Scheme, true);
        }

        private protected static string ColorizePrint(string i, LogType a)
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

        private protected static void PrintLine(string i, LogType a) => Console.WriteLine(ColorizePrint(i, a));
        private protected static void Print(string i, LogType a) => Console.Write(ColorizePrint(i, a));

        public static void SetLabelAttrib(out Label i, string s, SolidColorBrush a)
        {
            i = new Label
            {
                Foreground = a,
                Content = s
            };
        }

        public static void LogWriteLine(string i, LogType a = LogType.Default, bool writeToLog = false)
        {
            if (writeToLog)
                WriteLog(i, a);
            if (!DisableConsole)
                PrintLine(i, a);
        }

        public static void LogWrite(string i, LogType a = LogType.Default, bool writeToLog = false, bool overwriteCurLine = false)
        {
            if (writeToLog)
                WriteLog(i, a);

            if (!DisableConsole)
            {
                if (overwriteCurLine)
                    Console.SetCursorPosition(0, Console.CursorTop);
                Print(i, a);
            }
        }

        public static void WriteLog(string i, LogType a = LogType.Default)
        {
            using (logstream = new StreamWriter(Path.Combine(logdir, filename), true))
                logstream.WriteLine(GetLog(i, a));
        }

        private protected static string GetCurrentTime(string format) => DateTime.Now.ToLocalTime().ToString(format);

        private protected static string GetLog(string i, LogType a)
        {
            switch (a)
            {
                case LogType.Error:
                    i = $"[Erro]\t{i}";
                    break;
                case LogType.Warning:
                    i = $"[Warn]\t{i}";
                    break;
                default:
                    i = $"\t\t{i}";
                    break;
                case LogType.Default:
                    i = $"[Info]\t{i}";
                    break;
                case LogType.Scheme:
                    i = $"[Schm]\t{i}";
                    break;
            }
            return $"[{GetCurrentTime("HH:mm:ss.fff")}] {i}";
        }
    }
}
