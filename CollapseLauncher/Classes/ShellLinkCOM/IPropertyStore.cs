using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace CollapseLauncher.ShellLinkCOM
{
    [Guid(CLSIDGuid.Id_IPropertyStoreIGuid)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [GeneratedComInterface]
    internal partial interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint cProps);
        [PreserveSig]
        int GetAt(in uint iProp, out PropertyKey pkey);
        [PreserveSig]
        int GetValue(ref PropertyKey key, out PropVariant pv);
        [PreserveSig]
        int SetValue(ref PropertyKey key, ref PropVariant pv);
        [PreserveSig]
        int Commit();
    }
}
