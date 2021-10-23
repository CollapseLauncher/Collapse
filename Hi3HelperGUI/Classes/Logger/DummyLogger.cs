using System.IO;

namespace Hi3HelperGUI
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
            using (Logger.logstream = new(Path.Combine(Logger.logdir, Logger.filename), true))
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
}
