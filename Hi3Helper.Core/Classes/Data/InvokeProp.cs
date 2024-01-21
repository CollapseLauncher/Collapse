using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

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
            SWP_SHOWWINDOW = 0x40,
        }

        public enum SpecialWindowHandles
        {
            HWND_TOP = 0,
            HWND_BOTTOM = 1,
            HWND_TOPMOST = -1,
            HWND_NOTOPMOST = -2
        }

        [Flags]
        public enum GLOBAL_ALLOC_FLAGS : uint
        {
            GHND = 0x0042,
            GMEM_FIXED = 0x00000000,
            GMEM_MOVEABLE = 0x00000002,
            GMEM_ZEROINIT = 0x00000040,
            GPTR = 0x00000040,
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
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint GetLastError();

        [DllImport("Kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool AllocConsole();

        [DllImport("Kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool FreeConsole();

        [DllImport("Kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, uint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, uint hTemplateFile);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int GetDpiForWindow(IntPtr hWnd);

        [DllImport("Shcore.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int GetDpiForMonitor(IntPtr hmonitor, Monitor_DPI_Type dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", SetLastError = true, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr SetClipboardData(uint uFormat, IntPtr data);

        [DllImport("user32.dll", SetLastError = true, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EmptyClipboard();

        [DllImport("KERNEL32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GlobalAlloc(GLOBAL_ALLOC_FLAGS uFlags, nuint uBytes);
        
        [DllImport("KERNEL32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GlobalFree(IntPtr hMem);

        [DllImport("KERNEL32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("KERNEL32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalUnlock(IntPtr hMem);

        public unsafe static void CopyStringToClipboard(string inputString)
        {
            // Initialize the memory pointer
            IntPtr stringBufferPtr = IntPtr.Zero;
            IntPtr hMem = IntPtr.Zero;

            // Set the Clipboard bool status
            bool isOpenClipboardSuccess = false;

            try
            {
                // If inputString is null or empty, then return
                if (string.IsNullOrEmpty(inputString))
                {
                    Logger.LogWriteLine($"[InvokeProp::CopyStringToClipboard()] inputString cannot be empty! Clipboard will not be set!", LogType.Warning, true);
                    return;
                }

                // Try open the Clipboard
                if (!(isOpenClipboardSuccess = OpenClipboard(IntPtr.Zero)))
                    Logger.LogWriteLine($"[InvokeProp::CopyStringToClipboard()] Error has occurred while opening clipboard buffer! Error: {Marshal.GetLastPInvokeErrorMessage()}", LogType.Error, true);

                // Set the bufferSize + 1, the additional 1 byte will be used to interpret the null byte
                int bufferSize = (inputString.Length + 1);

                // Allocate the Global-Moveable buffer to the kernel with given size and lock the buffer
                hMem = GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, (nuint)bufferSize);
                stringBufferPtr = GlobalLock(hMem);

                // Write the inputString as a UTF-8 bytes into the string buffer
                if (!Encoding.UTF8.TryGetBytes(inputString, new Span<byte>((byte*)stringBufferPtr, inputString.Length), out int bufferWritten))
                    Logger.LogWriteLine($"[InvokeProp::CopyStringToClipboard()] Loading inputString into buffer has failed! Clipboard will not be set!", LogType.Error, true);

                // Always set the null byte at the end of the buffer
                ((byte*)stringBufferPtr)[bufferWritten] = 0x00; // Write the null (terminator) byte

                // Unlock the buffer
                GlobalUnlock(hMem);

                // Empty the previous Clipboard and set to the new one from this buffer. If
                // the clearance is failed, then clear the buffer at "finally" block
                if (!EmptyClipboard() || SetClipboardData(1, hMem) == IntPtr.Zero)
                {
                    Logger.LogWriteLine($"[InvokeProp::CopyStringToClipboard()] Error has occurred while clearing and set clipboard buffer! Error: {Marshal.GetLastPInvokeErrorMessage()}", LogType.Error, true);
                    return;
                }
            }
            finally
            {
                // If the buffer is allocated (not zero), then free it.
                if (hMem != IntPtr.Zero) GlobalFree(hMem);

                // Close the buffer if the clipboard is successfully opened.
                if (isOpenClipboardSuccess) CloseClipboard();
            }
        }

        public struct WindowRect
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
        }

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, ref WindowRect rectangle);

        [DllImport("user32.dll")]
        public extern static uint GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        public extern static uint SetWindowLong(IntPtr hwnd, int index, uint value);

        [DllImport("user32.dll")]
        public extern static IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        public static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hwnd, uint msg, UIntPtr wParam, IntPtr lParam);

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
