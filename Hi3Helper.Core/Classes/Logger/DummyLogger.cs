namespace Hi3Helper
{
    public class DummyLogger : ConsoleLogger, ILogger
    {
        public override void LogWriteLine(string i = "", LogType a = LogType.Default, bool writeToLog = false)
        {
            if (writeToLog)
                WriteLog(i, a);
        }

        public override void LogWrite(string i = "", LogType a = LogType.Default, bool writeToLog = false, bool overwriteCurLine = false)
        {
            if (writeToLog)
                WriteLog(i, a);
        }
    }
}
