using System;
using System.IO;

namespace Hi3Helper
{
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
                case LogType.Game:
                    i = $"\u001b[35;1m[Game]\u001b[0m\t{i}";
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

        public void LogWriteLine(string i = "", LogType a = LogType.Default, bool writeToLog = false)
        {
            if (writeToLog)
                WriteLog(i, a);
            if (Logger.EnableConsole)
                PrintLine(i, a);
        }

        public void LogWrite(string i = "", LogType a = LogType.Default, bool writeToLog = false, bool overwriteCurLine = false)
        {
            if (writeToLog)
                WriteLog(i, a);

            if (Logger.EnableConsole)
            {
                if (overwriteCurLine)
                    Console.SetCursorPosition(0, Console.CursorTop);
                Print(i, a);
            }
        }

        public void WriteLog(string i = "", LogType a = LogType.Default)
        {
            // if (Logger.logstream != null)
            using (Logger.logstream = new StreamWriter(Path.Combine(Logger.logdir, Logger.filename), true))
                Logger.logstream.WriteLine(GetLog(i, a));
        }

        private protected string GetLog(string i, LogType a)
        {
            switch (a)
            {
                case LogType.Error:
                    i = $"[Erro]\t{i}";
                    break;
                case LogType.Warning:
                    i = $"[Warn]\t{i}";
                    break;
                case LogType.Default:
                    i = $"[Info]\t{i}";
                    break;
                case LogType.Scheme:
                    i = $"[Schm]\t{i}";
                    break;
                case LogType.Game:
                    i = $"[Game]\t{i}";
                    break;
                default:
                    i = $"\t\t{i}";
                    break;
            }

            return $"[{Logger.GetCurrentTime("HH:mm:ss.fff")}] {i.Replace("\n", $"{new string(' ', 22)}\t")}";
        }
    }
}
