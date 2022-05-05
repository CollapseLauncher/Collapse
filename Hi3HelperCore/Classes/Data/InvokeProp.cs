using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

using static Hi3Helper.Logger;

namespace Hi3Helper
{
    public static class InvokeProp
    {
        public static IntPtr m_windowHandle;
        public static IntPtr m_consoleHandle;

        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;
        public enum HandleEnum
        {
            SW_HIDE = 0,
            SW_SHOWNORMAL = 1,
            SW_SHOWMINIMIZED = 2,
            SW_SHOWMAXIMIZED = 3,
            SW_SHOW = 5,
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

        [DllImport("Kernel32")]
        public static extern void AllocConsole();

        [DllImport("Kernel32")]
        public static extern void FreeConsole();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport(@"Lib\HPatchZ.dll", EntryPoint = "hpatch", CallingConvention = CallingConvention.Cdecl)]
        public static extern int HPatch(string oldFileName, string diffFileName, string outNewFileName,
            bool isLoadOldAll, UIntPtr patchCacheSize, long diffDataOffert, long diffDataSize);

        [DllImport(@"Lib\HPatchZ.dll", EntryPoint = "hpatch_cmd_line", CallingConvention = CallingConvention.Cdecl)]
        public static extern int HPatchCommand(int argc, string[] argv);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

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
