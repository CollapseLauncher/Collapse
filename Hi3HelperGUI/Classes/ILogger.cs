using System.Windows.Media;
using System.Windows.Controls;

namespace Hi3HelperGUI
{
    interface ILogger
    {
        //void SetLabelAttrib(out Label i, string s, SolidColorBrush a);
        void LogWriteLine(string i = "", LogType a = LogType.Default, bool writeToLog = false);
        void LogWrite(string i = "", LogType a = LogType.Default, bool writeToLog = false, bool overwriteCurLine = false);
        void WriteLog(string i = "", LogType a = LogType.Default);
    }
}
