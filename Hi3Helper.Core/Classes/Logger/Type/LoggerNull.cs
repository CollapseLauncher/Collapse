using System.Text;

namespace Hi3Helper
{
    public class LoggerNull : LoggerBase, ILog
    {
        public LoggerNull(string folderPath, Encoding encoding) : base(folderPath, encoding) { }

        ~LoggerNull() => DisposeBase();

        #region Methods
        public void Dispose() => DisposeBase();

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async void LogWriteLine(string line, LogType type, bool writeToLog)
        {
            if (writeToLog) WriteLog(line, type);
        }

        public override async void LogWrite(string line, LogType type, bool writeToLog, bool resetLinePosition)
        {
            if (writeToLog) WriteLog(line, type);
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        #endregion
    }
}
