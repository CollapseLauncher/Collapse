using System.Runtime.InteropServices;
#pragma warning disable SYSLIB1054

namespace Hi3Helper.Log;

internal static partial class PInvoke
{
    internal static class User32
    {
        private const string LibName = "User32";

        [DllImport(LibName, EntryPoint = "ShowWindow")]
        public static extern int ShowWindow(nint windowHandle, int nCmdShow);
    }
}
