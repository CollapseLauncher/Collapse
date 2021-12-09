using System;
using System.Runtime.InteropServices;
using Hi3Helper;

using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    public static class InvokeProp
    {
        public static IntPtr m_windowHandle;
        public static IntPtr m_consoleHandle;

        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

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

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        public static void HideConsoleWindow()
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);
            DisableConsole = true;
            WriteLog($"Console toggle: Hidden", LogType.Default);
        }

        public static void ShowConsoleWindow()
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_SHOW);
            DisableConsole = false;
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

    }
}
