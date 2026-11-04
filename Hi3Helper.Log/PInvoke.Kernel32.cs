using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

#pragma warning disable SYSLIB1054

namespace Hi3Helper.Log;

internal static partial class PInvoke
{
    internal static class Kernel32
    {
        private const string LibName = "Kernel32";

        [DllImport(LibName, EntryPoint = "GetConsoleWindow")]
        public static extern nint GetConsoleWindow();
        
        [DllImport(LibName, EntryPoint = "GetConsoleMode", SetLastError = true)]
        public static extern int GetConsoleMode(nint hConsoleHandle, out uint lpMode);

        [DllImport(LibName, EntryPoint = "SetConsoleMode", SetLastError = true)]
        public static extern int SetConsoleMode(nint hConsoleHandle, uint dwMode);

        [DllImport(LibName, EntryPoint = "AttachConsole", SetLastError = true)]
        public static extern int AttachConsole(uint dwProcessId);

        [DllImport(LibName, EntryPoint = "AllocConsole", SetLastError = true)]
        public static extern int AllocConsole();

        [DllImport(LibName, EntryPoint = "CreateFileW", SetLastError = true)]
        public static extern nint CreateFile(nint lpFileName,
                                             uint dwDesiredAccess,
                                             uint dwShareMode,
                                             uint lpSecurityAttributes,
                                             uint dwCreationDisposition,
                                             uint dwFlagsAndAttributes,
                                             uint hTemplateFile);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SkipLocalsInit]
        public static unsafe nint CreateFile(string? lpFileName,
                                             uint    dwDesiredAccess,
                                             uint    dwShareMode,
                                             uint    lpSecurityAttributes,
                                             uint    dwCreationDisposition,
                                             uint    dwFlagsAndAttributes,
                                             uint    hTemplateFile)
        {
            nint lpFileNameP = (nint)Unsafe.AsPointer(in Utf16StringMarshaller.GetPinnableReference(lpFileName));
            return CreateFile(lpFileNameP,
                              dwDesiredAccess,
                              dwShareMode,
                              lpSecurityAttributes,
                              dwCreationDisposition,
                              dwFlagsAndAttributes,
                              hTemplateFile);
        }

        [DllImport(LibName, EntryPoint = "SetStdHandle", SetLastError = true)]
        public static extern int SetStdHandle(int nStdHandle, nint hHandle);
    }
}
