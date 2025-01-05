using System.Text;

namespace Hi3Helper
{
    public class LoggerNull(string folderPath, Encoding encoding) : LoggerBase(folderPath, encoding), ILog
    {
        ~LoggerNull() => DisposeBase();

        #region Methods
        public void Dispose() => DisposeBase();
        public override void LogWriteLine() { }
        public override void LogWriteLine(string _ = null) { }
        public override void LogWriteLine(string line, LogType type) { }
        public override void LogWriteLine(string line, LogType type, bool writeToLog)
        {
            if (writeToLog) WriteLog(line, type);
        }

        public override void LogWrite(string line, LogType type, bool writeToLog, bool resetLinePosition)
        {
            if (writeToLog) WriteLog(line, type);
        }
        #endregion
    }
}
