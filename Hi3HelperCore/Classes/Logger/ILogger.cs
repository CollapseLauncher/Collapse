namespace Hi3Helper
{
    interface ILogger
    {
        void LogWriteLine(string i = "", LogType a = LogType.Default, bool writeToLog = false);
        void LogWrite(string i = "", LogType a = LogType.Default, bool writeToLog = false, bool overwriteCurLine = false);
        void WriteLog(string i = "", LogType a = LogType.Default);
    }
}
