using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using static Hi3Helper.Logger;

namespace Hi3Helper
{
    public static class InvokeProp
    {
        // Reference:
        // https://pinvoke.net/default.aspx/Enums.SystemMetric
        public enum SystemMetric : int
        {
            SM_CXSCREEN = 0,
            SM_CYSCREEN = 1
        }

        // Reference:
        // https://pinvoke.net/default.aspx/Enums/SetWindowPosFlags.html
        public enum SetWindowPosFlags : uint
        {
            SWP_NOMOVE = 2,
        }
        public enum SpecialWindowHandles
        {
            HWND_TOP = 0,
            HWND_BOTTOM = 1,
            HWND_TOPMOST = -1,
            HWND_NOTOPMOST = -2
        }

        public static IntPtr m_consoleHandle;

        public enum HandleEnum
        {
            SW_HIDE = 0,
            SW_SHOWNORMAL = 1,
            SW_SHOWMINIMIZED = 2,
            SW_SHOWMAXIMIZED = 3,
            SW_SHOW = 5,
        }

        public enum Monitor_DPI_Type : int
        {
            MDT_Effective_DPI = 0,
            MDT_Angular_DPI = 1,
            MDT_Raw_DPI = 2,
            MDT_Default = MDT_Effective_DPI
        }

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        [DllImport("Kernel32.dll")]
        public static extern void AllocConsole();

        [DllImport("Kernel32.dll")]
        public static extern void FreeConsole();

        [DllImport("Kernel32", CharSet = CharSet.Unicode)]
        public static extern long WritePrivateProfileString(string Section, string Key, string Value, string FilePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        public static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

        [DllImport("user32.dll")]
        public static extern int GetDpiForWindow(IntPtr hWnd);

        [DllImport("Shcore.dll", SetLastError = true)]
        public static extern int GetDpiForMonitor(IntPtr hmonitor, Monitor_DPI_Type dpiType, out uint dpiX, out uint dpiY);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport(@"Lib\HPatchZ.dll", EntryPoint = "hpatch", CallingConvention = CallingConvention.Cdecl)]
        public static extern int HPatch(string oldFileName, string diffFileName, string outNewFileName,
            bool isLoadOldAll, UIntPtr patchCacheSize, long diffDataOffert, long diffDataSize);

        [DllImport(@"Lib\HPatchZ.dll", EntryPoint = "hpatch_cmd_line", CallingConvention = CallingConvention.Cdecl)]
        public static extern int HPatchCommand(int argc, string[] argv);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        public static void HideConsoleWindow()
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);
            EnableConsole = false;
            WriteLog($"Console toggle: Hidden", LogType.Default);
        }

        public static void ShowConsoleWindow()
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_SHOW);
            EnableConsole = true;
            WriteLog($"Console toggle: Show", LogType.Default);
        }

        public static void InitializeConsole(bool writeToLog = true, string defaultLogLocation = null)
        {
            AllocConsole();
            m_consoleHandle = GetStdHandle(-11);
            if (!GetConsoleMode(m_consoleHandle, out var lpMode))
            {
                LogWriteLine("failed to get output console mode", LogType.Error);
                Console.ReadKey();
                return;
            }
            lpMode |= 0xCu;
            if (!SetConsoleMode(m_consoleHandle, lpMode))
            {
                LogWriteLine($"failed to set output console mode, error code {GetLastError()}", LogType.Error);
                Console.ReadKey();
            }
            else
            {
                InitLog(writeToLog, defaultLogLocation);
            }
        }

        public static IntPtr GetProcessWindowHandle(string ProcName) => Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ProcName), ".")[0].MainWindowHandle;

        public class InvokePresence
        {
            IntPtr m_WindowPtr;
            public InvokePresence(IntPtr windowPtr)
            {
                m_WindowPtr = windowPtr;
            }

            public void ShowWindow() => ShowWindowAsync(m_WindowPtr, (int)HandleEnum.SW_SHOWNORMAL);
            public void ShowWindowMaximized() => ShowWindowAsync(m_WindowPtr, (int)HandleEnum.SW_SHOWMAXIMIZED);
            public void HideWindow() => ShowWindowAsync(m_WindowPtr, (int)HandleEnum.SW_SHOWMINIMIZED);
        }
    }
}
