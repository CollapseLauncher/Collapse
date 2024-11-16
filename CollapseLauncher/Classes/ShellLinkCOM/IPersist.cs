using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace CollapseLauncher.ShellLinkCOM
{
    [Guid(CLSIDGuid.Id_IPersistIGuid)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [GeneratedComInterface]
    internal partial interface IPersist
    {
        [PreserveSig]
        //[helpstring("Returns the class identifier for the component object")]
        void GetClassID(out Guid pClassID);
    }
}
