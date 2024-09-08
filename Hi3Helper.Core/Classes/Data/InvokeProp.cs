using System;
using System.Buffers;
using System.Collections.Generic;
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
            SM_CXSCREEN       = 0,
            SM_CYSCREEN       = 1,
            SM_CYCAPTION      = 4,
            SM_CYSIZEFRAME    = 33,
            SM_CXPADDEDBORDER = 92,
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

        [Flags]
        public enum GLOBAL_ALLOC_FLAGS : uint
        {
            GHND = 0x0042,
            GMEM_FIXED = 0x00000000,
            GMEM_MOVEABLE = 0x00000002,
            GMEM_ZEROINIT = 0x00000040,
            GPTR = 0x00000040,
        }

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
        
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint   wFunc;
            public string pFrom;
            public string pTo;
            public ushort fFlags;
            public int    fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }
        #endregion

        #region Kernel32
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool AllocConsole();

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, uint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, uint hTemplateFile);
        
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GlobalAlloc(GLOBAL_ALLOC_FLAGS uFlags, nuint uBytes);
        
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GlobalFree(IntPtr hMem);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool GlobalUnlock(IntPtr hMem);
        
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine handlerRoutine, bool add);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode )]
        public static extern bool CreateHardLink(
            string lpFileName,
            string lpExistingFileName,
            IntPtr lpSecurityAttributes
        );
        #endregion

        #region User32
        public struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int    x;
            public int    y;
            public int    cx;
            public int    cy;
            public uint   flags;
        }

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int GetSystemMetrics(SystemMetric nIndex);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

        [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool CloseClipboard();

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr SetClipboardData(uint uFormat, IntPtr data);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool EmptyClipboard();
        
        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "FindWindowExW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll", EntryPoint = "CallWindowProcW", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, UIntPtr wParam, IntPtr lParam);
        #endregion

        #region Shcore
        [DllImport("Shcore.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int GetDpiForMonitor(IntPtr hmonitor, Monitor_DPI_Type dpiType, out uint dpiX, out uint dpiY);
        #endregion

        #region Ntdll
        // https://github.com/dotnet/runtime/blob/f4d39134b8daefb5ab0db6750a203f980eecb4f0/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/ProcessManager.Win32.cs#L299
        // https://github.com/dotnet/runtime/blob/f4d39134b8daefb5ab0db6750a203f980eecb4f0/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/ProcessManager.Win32.cs#L346
        // https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/Interop/Windows/NtDll/Interop.NtQuerySystemInformation.cs#L11

        private const int SystemProcessInformation = 5;

        // https://msdn.microsoft.com/en-us/library/windows/desktop/aa380518.aspx
        // https://msdn.microsoft.com/en-us/library/windows/hardware/ff564879.aspx
        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct UNICODE_STRING
        {
            /// <summary>
            /// Length in bytes, not including the null terminator, if any.
            /// </summary>
            internal ushort Length;

            /// <summary>
            /// Max size of the buffer in bytes
            /// </summary>
            internal ushort MaximumLength;
            internal void* Buffer;
        }

        [DllImport("ntdll.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private unsafe static extern uint NtQuerySystemInformation(int SystemInformationClass, byte* SystemInformation, uint SystemInformationLength, out uint ReturnLength);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern nint OpenProcess(int dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
        
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
        public static unsafe extern bool QueryFullProcessImageName(nint hProcess, int dwFlags, char* lpExeName, ref int lpdwSize);

        public unsafe static bool IsProcessExist(ReadOnlySpan<char> processName, string checkForOriginPath = "")
        {
            // Initialize the first buffer to 512 KiB
            ArrayPool<byte> arrayPool = ArrayPool<byte>.Shared;
            byte[] NtQueryCachedBuffer = arrayPool.Rent(4 << 17);
            bool isReallocate = false;
            uint length = 0;

            // Get size of UNICODE_STRING struct
            int sizeOfUnicodeString = Marshal.SizeOf<UNICODE_STRING>();

        StartOver:
            try
            {
                // If the buffer request is more than 2 MiB, then return false
                if (length > (2 << 20))
                    return false;

                // If buffer reallocation is requested, then re-rent the buffer
                // from ArrayPool<T>.Shared
                if (isReallocate)
                    NtQueryCachedBuffer = arrayPool.Rent((int)length);

                // Get the pointer of the buffer
                fixed (byte* dataBufferPtr = &NtQueryCachedBuffer[0])
                {
                    // Get the query of the current running process and store it to the buffer
                    uint hNtQuerySystemInformationResult = NtQuerySystemInformation(SystemProcessInformation, dataBufferPtr, (uint)NtQueryCachedBuffer.Length, out length);

                    // If the required length of the data is exceeded than the current buffer,
                    // then try to reallocate and start over to the top.
                    const uint STATUS_INFO_LENGTH_MISMATCH = 0xC0000004;
                    if (hNtQuerySystemInformationResult == STATUS_INFO_LENGTH_MISMATCH || length > NtQueryCachedBuffer.Length)
                    {
                        LogWriteLine($"Buffer requested is insufficient! Requested: {length} > Capacity: {NtQueryCachedBuffer.Length}, Resizing the buffer...", LogType.Warning, true);
                        isReallocate = true;
                        goto StartOver;
                    }

                    // If other error has occurred, then return false as failed.
                    if (hNtQuerySystemInformationResult != 0)
                    {
                        LogWriteLine($"Error happened while operating NtQuerySystemInformation(): {Marshal.GetLastWin32Error()}", LogType.Error, true);
                        return false;
                    }

                    // Start reading data from the buffer
                    int currentOffset = 0;
                    bool isCommandPathEqual = false;
                ReadQueryData:
                    // Get the current position of the pointer based on its offset
                    byte* curPosPtr = dataBufferPtr + currentOffset;

                    // Get the increment of the next entry offset
                    // and get the struct from the given pointer offset + 56 bytes ahead
                    // to obtain the process name.
                    int nextEntryOffset = *(int*)curPosPtr;
                    UNICODE_STRING* unicodeString = (UNICODE_STRING*)(curPosPtr + 56);

                    // Use the struct buffer into the ReadOnlySpan<char> to be compared with
                    // the input from "processName" argument.
                    ReadOnlySpan<char> imageNameSpan = new ReadOnlySpan<char>(unicodeString->Buffer, unicodeString->Length / 2);
                    bool isMatchedExecutable = imageNameSpan.Equals(processName, StringComparison.OrdinalIgnoreCase);
                    if (isMatchedExecutable)
                    {
                        // If the origin path argument is null, then return as true.
                        if (string.IsNullOrEmpty(checkForOriginPath))
                            return true;

                        // If the string is not null, then check if the file path is exactly the same.
                        // START!!

                        // Move the offset of the current pointer and get the processId value
                        uint processId = *(uint*)(curPosPtr + 56 + sizeOfUnicodeString + 8);

                        // Try open the process and get the handle
                        const int QueryLimitedInformation = 0x1000;
                        nint processHandle = OpenProcess(QueryLimitedInformation, false, processId);

                        // If failed, then log the Win32 error and return false.
                        if (processHandle == nint.Zero)
                        {
                            LogWriteLine($"Error happened while operating OpenProcess(): {Marshal.GetLastWin32Error()}", LogType.Error, true);
                            return false;
                        }

                        // Try rent the new buffer to get the command line
                        int bufferProcessCmdLen = 1 << 10;
                        int bufferProcessCmdLenReturn = bufferProcessCmdLen;
                        char[] bufferProcessCmd = ArrayPool<char>.Shared.Rent(bufferProcessCmdLen);
                        try
                        {
                            // Cast processCmd buffer as pointer
                            fixed (char* bufferProcessCmdPtr = &bufferProcessCmd[0])
                            {
                                // Get the command line query of the process
                                bool hQueryFullProcessImageNameResult = QueryFullProcessImageName(processHandle, 0, bufferProcessCmdPtr, ref bufferProcessCmdLenReturn);
                                // If the query is unsuccessful, then log the Win32 error and return false.
                                if (!hQueryFullProcessImageNameResult)
                                {
                                    LogWriteLine($"Error happened while operating QueryFullProcessImageName(): {Marshal.GetLastWin32Error()}", LogType.Error, true);
                                    return false;
                                }

                                // If the requested return length is more than capacity (-2 for null terminator), then return false.
                                if (bufferProcessCmdLenReturn > bufferProcessCmdLen - 2)
                                {
                                    LogWriteLine($"The process command line length is more than requested length: {bufferProcessCmdLen - 2} < return {bufferProcessCmdLenReturn}", LogType.Error, true);
                                    return false;
                                }

                                // Get the command line query
                                ReadOnlySpan<char> processCmdLineSpan = new ReadOnlySpan<char>(bufferProcessCmdPtr, bufferProcessCmdLenReturn);

                                // Get the span of origin path to compare
                                ReadOnlySpan<char> checkForOriginPathDir = checkForOriginPath;

                                // Compare and return if any of result is equal
                                isCommandPathEqual = processCmdLineSpan.Equals(checkForOriginPathDir, StringComparison.OrdinalIgnoreCase);
                                if (isCommandPathEqual)
                                    return true;
                            }
                        }
                        finally
                        {
                            // Return the buffer
                            ArrayPool<char>.Shared.Return(bufferProcessCmd);
                        }
                    }

                    // Otherwise, if the next entry offset is not 0 (not ended), then read
                    // the next data and move forward based on the given offset.
                    currentOffset += nextEntryOffset;
                    if (nextEntryOffset != 0)
                        goto ReadQueryData;
                }
            }
            finally
            {
                // Return the buffer to the ArrayPool<T>.Shared
                arrayPool.Return(NtQueryCachedBuffer);
            }

            return false;
        }

#nullable enable
        public static unsafe string? GetProcessPathByProcessId(int processId)
        {
            // Try open the process and get the handle
            const int QueryLimitedInformation = 0x1000;
            nint processHandle = OpenProcess(QueryLimitedInformation, false, (uint)processId);

            // If failed, then log the Win32 error and return null.
            if (processHandle == nint.Zero)
            {
                LogWriteLine($"Error happened while operating OpenProcess(): {Marshal.GetLastWin32Error()}", LogType.Error, true);
                return null;
            }

            // Try rent the new buffer to get the command line
            int bufferProcessCmdLen = 1 << 10;
            int bufferProcessCmdLenReturn = bufferProcessCmdLen;
            char[] bufferProcessCmd = ArrayPool<char>.Shared.Rent(bufferProcessCmdLen);

            try
            {
                // Cast processCmd buffer as pointer
                fixed (char* bufferProcessCmdPtr = &bufferProcessCmd[0])
                {
                    // Get the command line query of the process
                    bool hQueryFullProcessImageNameResult = QueryFullProcessImageName(processHandle, 0, bufferProcessCmdPtr, ref bufferProcessCmdLenReturn);
                    // If the query is unsuccessful, then log the Win32 error and return false.
                    if (!hQueryFullProcessImageNameResult)
                    {
                        LogWriteLine($"Error happened while operating QueryFullProcessImageName(): {Marshal.GetLastWin32Error()}", LogType.Error, true);
                        return null;
                    }

                    // If the requested return length is more than capacity (-2 for null terminator), then return false.
                    if (bufferProcessCmdLenReturn > bufferProcessCmdLen - 2)
                    {
                        LogWriteLine($"The process command line length is more than requested length: {bufferProcessCmdLen - 2} < return {bufferProcessCmdLenReturn}", LogType.Error, true);
                        return null;
                    }

                    // Return string
                    return new string(bufferProcessCmdPtr, 0, bufferProcessCmdLenReturn);
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(bufferProcessCmd);
            }
        }
#nullable restore
        #endregion

        #region shell32
        [DllImport("shell32.dll", EntryPoint = "ExtractIconExW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        public static void SetWindowIcon(IntPtr hWnd, IntPtr hIconLarge, IntPtr hIconSmall)
        {
            const uint    WM_SETICON = 0x0080;
            const UIntPtr ICON_BIG   = 1;
            const UIntPtr ICON_SMALL = 0;
            SendMessage(hWnd, WM_SETICON, ICON_BIG,   hIconLarge);
            SendMessage(hWnd, WM_SETICON, ICON_SMALL, hIconSmall);
        }
        
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);
        #endregion
        
        public static void MoveFileToRecycleBin(IList<string> filePaths)
        { 
            uint   FO_DELETE          = 0x0003;
            ushort FOF_ALLOWUNDO      = 0x0040;
            ushort FOF_NOCONFIRMATION = 0x0010;

            var concat = string.Join('\0', filePaths) + '\0' + '\0';
            
            SHFILEOPSTRUCT fileOp = new SHFILEOPSTRUCT
            {
                wFunc  = FO_DELETE,
                pFrom  = concat,
                fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION)
            };

            SHFileOperation(ref fileOp);
        }

#nullable enable
        public static CancellationTokenSource? _preventSleepToken;
        private static bool _preventSleepRunning;

        public static async void RestoreSleep()
        {
            // Return early if token is disposed/already cancelled
            if (_preventSleepToken == null || _preventSleepToken.IsCancellationRequested)
                return;
            await (_preventSleepToken?.CancelAsync() ?? Task.CompletedTask);
        }

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
            catch (TaskCanceledException)
            {
                //do nothing, its cancelled :)
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

        [DllImport("dwmapi.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);
        #endregion

        #region uxtheme
        [DllImport("uxtheme.dll", EntryPoint = "#132", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return:MarshalAs(UnmanagedType.I1)]
        public static extern bool ShouldAppsUseDarkMode();

        // Note: Can only use "Default" and "AllowDark" to support Windows 10 1809
        [DllImport("uxtheme.dll", EntryPoint = "#135", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern PreferredAppMode SetPreferredAppMode(PreferredAppMode preferredAppMode);
        #endregion
    }
}
