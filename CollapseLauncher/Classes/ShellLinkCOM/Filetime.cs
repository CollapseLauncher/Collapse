using System.Runtime.InteropServices;

namespace CollapseLauncher.ShellLinkCOM
{
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 0)]
    public partial struct Filetime
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }
}
