using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Logger;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable MemberCanBePrivate.Global

namespace Hi3Helper
{
    public static class InvokeProp
    {
        #region Enums
        // Reference:
        // https://pinvoke.net/default.aspx/Enums.SystemMetric
        public enum SystemMetric
        {
            SM_CXSCREEN = 0,
            SM_CYSCREEN = 1
        }

        // Reference:
        // https://pinvoke.net/default.aspx/Enums/SetWindowPosFlags.html
        [Flags]
        public enum SetWindowPosFlags : uint
        {
            SWP_NOSIZE       = 1,
            SWP_NOMOVE       = 2,
            SWP_NOZORDER     = 4,
            SWP_FRAMECHANGED = 0x20,
            SWP_SHOWWINDOW   = 0x40,
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

        public enum Monitor_DPI_Type
        {
            MDT_Effective_DPI = 0,
            MDT_Angular_DPI = 1,
            MDT_Raw_DPI = 2,
            MDT_Default = MDT_Effective_DPI
        }

        public enum PreferredAppMode
        {
            Default,
            AllowDark,
            ForceDark,
            ForceLight,
            Max
        };

        [Flags]
        private enum ExecutionState : uint
        {
            EsAwaymodeRequired = 0x00000040,
            EsContinuous = 0x80000000,
            EsDisplayRequired = 0x00000002,
            EsSystemRequired = 0x00000001
        }
        #endregion

        #region Kernel32
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
        
        [DllImport("kernel32.dll")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine handlerRoutine, bool add);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

        #endregion

        #region User32

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
        
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, ref WindowRect rectangle);

        [DllImport("user32.dll")]
        public static extern uint GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint SetWindowLong(IntPtr hwnd, int index, uint value);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        public static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hwnd, uint msg, UIntPtr wParam, IntPtr lParam);
        #endregion

        #region Shcore
        [DllImport("Shcore.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int GetDpiForMonitor(IntPtr hmonitor, Monitor_DPI_Type dpiType, out uint dpiX, out uint dpiY);
        #endregion

#nullable enable
        public static CancellationTokenSource? _preventSleepToken;
        private static bool _preventSleepRunning;

        public static async void RestoreSleep() => await (_preventSleepToken?.CancelAsync() ?? Task.CompletedTask);

        public static async void PreventSleep()
        {
            // Only run this loop once
            if (_preventSleepRunning) return;

            // Initialize instance if it's still null
            _preventSleepToken ??= new CancellationTokenSource();

            // If the instance cancellation has been requested, return
            if (_preventSleepToken.IsCancellationRequested) return;

            // Set flag
            _preventSleepRunning = true;

            try
            {
                LogWriteLine("[InvokeProp::PreventSleep()] Starting to prevent sleep!", LogType.Warning, true);
                while (!_preventSleepToken.IsCancellationRequested)
                {
                    // Set ES to SystemRequired every 60s
                    SetThreadExecutionState(ExecutionState.EsContinuous | ExecutionState.EsSystemRequired);
                    await Task.Delay(60000, _preventSleepToken.Token);
                }
            }
            catch (Exception e)
            {
                LogWriteLine($"[InvokeProp::PreventSleep()] Errors while preventing sleep!\r\n{e}",
                             LogType.Error, true);
            }
            finally
            {
                // Reset flag and ES 
                _preventSleepRunning = false;
                SetThreadExecutionState(ExecutionState.EsContinuous);
                LogWriteLine("[InvokeProp::PreventSleep()] Stopped preventing sleep!", LogType.Warning, true);
                
                // Null the token for the next time method is called
                _preventSleepToken = null;
            }
        }
#nullable restore

        public static unsafe void CopyStringToClipboard(string inputString)
        {
            // Initialize the memory pointer
            // ReSharper disable RedundantAssignment
            IntPtr stringBufferPtr = IntPtr.Zero;
            // ReSharper restore RedundantAssignment
            IntPtr hMem = IntPtr.Zero;

            // Set the Clipboard bool status
            bool isOpenClipboardSuccess = false;

            try
            {
                // If inputString is null or empty, then return
                if (string.IsNullOrEmpty(inputString))
                {
                    LogWriteLine($"[InvokeProp::CopyStringToClipboard()] inputString cannot be empty! Clipboard will not be set!", LogType.Warning, true);
                    return;
                }

                // Try open the Clipboard
                if (!(isOpenClipboardSuccess = OpenClipboard(IntPtr.Zero)))
                    LogWriteLine($"[InvokeProp::CopyStringToClipboard()] Error has occurred while opening clipboard buffer! Error: {Marshal.GetLastPInvokeErrorMessage()}", LogType.Error, true);

                // Set the bufferSize + 1, the additional 1 byte will be used to interpret the null byte
                int bufferSize = (inputString.Length + 1);

                // Allocate the Global-Movable buffer to the kernel with given size and lock the buffer
                hMem = GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, (nuint)bufferSize);
                stringBufferPtr = GlobalLock(hMem);

                // Write the inputString as a UTF-8 bytes into the string buffer
                if (!Encoding.UTF8.TryGetBytes(inputString, new Span<byte>((byte*)stringBufferPtr, inputString.Length), out int bufferWritten))
                    LogWriteLine($"[InvokeProp::CopyStringToClipboard()] Loading inputString into buffer has failed! Clipboard will not be set!", LogType.Error, true);

                // Always set the null byte at the end of the buffer
                ((byte*)stringBufferPtr!)![bufferWritten] = 0x00; // Write the null (terminator) byte

                // Unlock the buffer
                GlobalUnlock(hMem);

                // Empty the previous Clipboard and set to the new one from this buffer. If
                // the clearance is failed, then clear the buffer at "finally" block
                if (!EmptyClipboard() || SetClipboardData(1, hMem) == IntPtr.Zero)
                {
                    LogWriteLine($"[InvokeProp::CopyStringToClipboard()] Error has occurred while clearing and set clipboard buffer! Error: {Marshal.GetLastPInvokeErrorMessage()}", LogType.Error, true);
                    return;
                }

                LogWriteLine($"[InvokeProp::CopyStringToClipboard()] Content has been set to Clipboard buffer with size: {bufferSize} bytes", LogType.Debug, true);
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
            // ReSharper disable UnusedAutoPropertyAccessor.Global
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
            // ReSharper restore UnusedAutoPropertyAccessor.Global
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

        #region shell32
        [DllImport("shell32.dll", SetLastError = true)]
        public static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        public static void SetWindowIcon(IntPtr hWnd, IntPtr hIconLarge, IntPtr hIconSmall)
        {
            const uint WM_SETICON = 0x0080;
            const UIntPtr ICON_BIG = 1;
            const UIntPtr ICON_SMALL = 0;
            SendMessage(hWnd, WM_SETICON, ICON_BIG, hIconLarge);
            SendMessage(hWnd, WM_SETICON, ICON_SMALL, hIconSmall);
        }
        #endregion

        public delegate bool HandlerRoutine(uint dwCtrlType);

        public static Process[] GetInstanceProcesses()
        {
            var currentProcess = Process.GetCurrentProcess();
            var processes      = Process.GetProcessesByName(currentProcess.ProcessName);
            
            return processes;
        }

        public static int EnumerateInstances()
        {
            var instanceProc  = GetInstanceProcesses();
            var instanceCount = instanceProc.Length;

            var finalInstanceCount = 0;
            
            if (instanceCount > 1)
            {
                var curPId = Process.GetCurrentProcess().Id;
                LogWriteLine($"Detected {instanceCount} instances! Current PID: {curPId}", LogType.Default, true);
                LogWriteLine($"Enumerating instances...");
                foreach (Process p in instanceProc)
                {
                    if (p == null) continue;
                    try
                    {
                        if (p.MainWindowHandle == IntPtr.Zero)
                        {
                            LogWriteLine("Process does not have window, skipping...", LogType.NoTag, true);
                            continue;
                        }
                            
                        LogWriteLine($"Name: {p.ProcessName}",                LogType.NoTag, true);
                        LogWriteLine($"MainModule: {p.MainModule?.FileName}", LogType.NoTag, true);
                        LogWriteLine($"PID: {p.Id}",                          LogType.NoTag, true);
                            
                        finalInstanceCount++;
                    }
                    catch (Exception ex)
                    {
                        LogWriteLine($"Failed when trying to fetch an instance information! " +
                                     $"InstanceCount is not incremented.\r\n{ex}",
                                     LogType.Error, true);
                    }
                }

                LogWriteLine($"Multiple instances found! This is instance #{finalInstanceCount}",
                             LogType.Scheme, true);
            }
            else finalInstanceCount = 1;

            return finalInstanceCount;
        }

        #region dwmapi
        public struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [DllImport("dwmapi.dll")]
        public static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);
        #endregion

        #region uxtheme
        [DllImport("uxtheme.dll", EntryPoint = "#132")]
        [return:MarshalAs(UnmanagedType.I1)]
        public static extern bool ShouldAppsUseDarkMode();

        // Note: Can only use "Default" and "AllowDark" to support Windows 10 1809
        [DllImport("uxtheme.dll", EntryPoint = "#135")]
        public static extern PreferredAppMode SetPreferredAppMode(PreferredAppMode preferredAppMode);
        #endregion
    }
}
