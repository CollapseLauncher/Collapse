using System;
using System.IO;
using System.Text;

namespace Hi3Helper
{
    public class ConsoleLogger : ILogger
    {
        StringBuilder _logBuilder = new StringBuilder();
        private protected void ColorizePrint(string i, LogType a)
        {
            _logBuilder.Clear();
            switch (a)
            {
                case LogType.Error:
                    _logBuilder.Append("\u001b[31;1m[Erro]\u001b[0m\t");
                    break;
                case LogType.Warning:
                    _logBuilder.Append("\u001b[33;1m[Warn]\u001b[0m\t");
                    break;
                default:
                case LogType.Default:
                    _logBuilder.Append("\u001b[32;1m[Info]\u001b[0m\t");
                    break;
                case LogType.Scheme:
                    _logBuilder.Append("\u001b[34;1m[Schm]\u001b[0m\t");
                    break;
                case LogType.Game:
                    _logBuilder.Append("\u001b[35;1m[Game]\u001b[0m\t");
                    break;
                case LogType.Empty:
                    break;
                case LogType.NoTag:
                    _logBuilder.Append("\t");
                    break;
            }
            _logBuilder.Append(i);
        }

        private protected void PrintLine(string i, LogType a)
        {
            ColorizePrint(i, a);
            Console.WriteLine(_logBuilder.ToString());
        }
        private protected void Print(string i, LogType a)
        {
            ColorizePrint(i, a);
            Console.Write(_logBuilder.ToString());
        }

        public virtual void LogWriteLine(string i = "", LogType a = LogType.Default, bool writeToLog = false)
        {
            if (writeToLog)
                WriteLog(i, a);
            if (Logger.EnableConsole)
                PrintLine(i, a);
        }

        public virtual void LogWrite(string i = "", LogType a = LogType.Default, bool writeToLog = false, bool overwriteCurLine = false)
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
            //Logger.logstream = new StreamWriter(Path.Combine(Logger.logdir, Logger.filename), true)
            GetLog(i, a);
            Logger.logstream.WriteLine(_logBuilder.ToString());
        }

        private protected void GetLog(string i, LogType a)
        {
            _logBuilder.Clear();
            switch (a)
            {
                case LogType.Error:
                    _logBuilder.Append("[Erro] ");
                    break;
                case LogType.Warning:
                    _logBuilder.Append("[Warn] ");
                    break;
                case LogType.Default:
                    _logBuilder.Append("[Info] ");
                    break;
                case LogType.Scheme:
                    _logBuilder.Append("[Schm] ");
                    break;
                case LogType.Game:
                    _logBuilder.Append("[Game] ");
                    break;
                default:
                    _logBuilder.Append("\t\t");
                    break;
            }

            _logBuilder.Append('[');
            _logBuilder.Append(Logger.GetCurrentTime("HH:mm:ss.fff"));
            _logBuilder.Append(']');
            _logBuilder.Append('\t');
            _logBuilder.Append(i.Replace("\n", $"{new string(' ', 22)}\t"));
        }
    }
}
