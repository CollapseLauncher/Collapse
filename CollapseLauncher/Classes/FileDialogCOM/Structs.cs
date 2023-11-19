using System;
using System.Runtime.InteropServices;

namespace CollapseLauncher.FileDialogCOM
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal partial struct PROPERTYKEY
    {
        internal Guid fmtid;
        internal uint pid;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 4)]
    internal partial struct COMDLG_FILTERSPEC
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        internal string pszName;
        [MarshalAs(UnmanagedType.LPWStr)]
        internal string pszSpec;
    }
}
