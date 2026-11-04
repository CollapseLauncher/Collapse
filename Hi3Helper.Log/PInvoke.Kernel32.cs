using System.Runtime.InteropServices;

namespace Hi3Helper.Log;

internal static partial class PInvoke
{
    internal static partial class Kernel32
    {
        private const string LibName = "Kernel32";

        [LibraryImport(LibName, EntryPoint = "GetConsoleWindow")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static partial nint GetConsoleWindow();
        
        [LibraryImport(LibName, EntryPoint = "GetConsoleMode", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);

        [LibraryImport(LibName, EntryPoint = "SetConsoleMode", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetConsoleMode(nint hConsoleHandle, uint dwMode);

        [LibraryImport(LibName, EntryPoint = "AttachConsole", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool AttachConsole(uint dwProcessId);

        [LibraryImport(LibName, EntryPoint = "AllocConsole", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool AllocConsole();

        [LibraryImport(LibName, EntryPoint = "CreateFileW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static partial nint CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, uint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, uint hTemplateFile);

        [LibraryImport(LibName, EntryPoint = "SetStdHandle", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetStdHandle(int nStdHandle, nint hHandle);
    }
}
