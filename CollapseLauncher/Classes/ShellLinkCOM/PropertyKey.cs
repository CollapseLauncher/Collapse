using System;
using System.Runtime.InteropServices;

namespace CollapseLauncher.ShellLinkCOM
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PropertyKey
    {
        public Guid fmtid;
        public UIntPtr pid;

        public static PropertyKey PKEY_AppUserModel_ID
        {
            get
            {
                return new PropertyKey()
                {
                    fmtid = Guid.ParseExact("{9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}", "B"),
                    pid = new UIntPtr(5),
                };
            }
        }

        public static PropertyKey PKEY_AppUserModel_ToastActivatorCLSID
        {
            get
            {
                return new PropertyKey()
                {
                    fmtid = Guid.ParseExact("{9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}", "B"),
                    pid = new UIntPtr(26),
                };
            }
        }
    }
}
