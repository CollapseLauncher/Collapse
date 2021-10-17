using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Hi3HelperGUI.Data
{
    // Reference:
    // https://stackoverflow.com/questions/217902/reading-writing-an-ini-file
    [DebuggerStepThrough]
    public class IniParser
    {
        readonly string Path;
        readonly string EXE = Assembly.GetExecutingAssembly().GetName().Name;

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern long WritePrivateProfileString(string Section, string Key, string Value, string FilePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

        public IniParser(string IniPath = null) => Path = new FileInfo(IniPath ?? EXE + ".ini").FullName;

        public string Read(string Key, string Section = null)
        {
            var RetVal = new StringBuilder(255);
            GetPrivateProfileString(Section ?? EXE, Key, "", RetVal, 255, Path);
            return RetVal.ToString();
        }

        public void Write(string Key, string Value, string Section = null) => WritePrivateProfileString(Section ?? EXE, Key, Value, Path);

        public void DeleteKey(string Key, string Section = null) => Write(Key, null, Section ?? EXE);

        public void DeleteSection(string Section = null) => Write(null, null, Section ?? EXE);

        public bool KeyExists(string Key, string Section = null) => Read(Key, Section).Length > 0;
    }
}
