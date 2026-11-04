using System.Runtime.InteropServices;

namespace Hi3Helper.Log;

internal static partial class PInvoke
{
    internal static partial class User32
    {
        private const string LibName = "User32";

        [LibraryImport(LibName, EntryPoint = "ShowWindow")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ShowWindow(nint windowHandle, int nCmdShow);
    }
}
