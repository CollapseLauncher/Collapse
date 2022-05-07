using System.IO;

namespace Hi3Helper
{
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
            // if (Logger.logstream != null)
            using (Logger.logstream = new StreamWriter(Path.Combine(Logger.logdir, Logger.filename), true))
                Logger.logstream.WriteLine(GetLog(i, a));
        }

        string GetLog(string i, LogType a)
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
