using System.Runtime.InteropServices;

namespace CollapseLauncher.ShellLinkCOM
{
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 0)]
    public partial struct Win32FindDataW
    {
        public uint dwFileAttributes; // 4
        public Filetime ftCreationTime; // 12
        public Filetime ftLastAccessTime; // 20
        public Filetime ftLastWriteTime; // 28
        public uint nFileSizeHigh; // 32
        public uint nFileSizeLow; // 36
        public uint dwReserved0; // 40
        public uint dwReserved1; // 44

        [MarshalAs(UnmanagedType.ByValArray,
            // SizeConst = 260
            SizeConst = 520
            )]
        public char[] cFileName;

        [MarshalAs(UnmanagedType.ByValArray,
            // SizeConst = 14
            SizeConst = 28
            )]
        public char[] cAlternateFileName;

        public uint dwFileType;
        public uint dwCreatorType;
        public uint wFinderFlags;
    }
}
