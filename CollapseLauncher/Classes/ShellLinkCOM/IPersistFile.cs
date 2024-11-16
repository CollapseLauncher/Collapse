using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace CollapseLauncher.ShellLinkCOM
{
    [Guid(CLSIDGuid.Id_IPersistFileIGuid)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
    internal partial interface IPersistFile
    {
        // can't get this to go if I extend IPersist, so put it here:
        [PreserveSig]
        void GetClassID(out Guid pClassID);

        //[helpstring("Checks for changes since last file write")]      
        void IsDirty();

        //[helpstring("Opens the specified file and initializes the object from its contents")]      
        void Load(
            [MarshalAs(UnmanagedType.LPWStr)] string pszFileName,
            uint dwMode);

        //[helpstring("Saves the object into the specified file")]      
        void Save(
            [MarshalAs(UnmanagedType.LPWStr)] string pszFileName,
            [MarshalAs(UnmanagedType.Bool)] bool fRemember);

        //[helpstring("Notifies the object that save is completed")]      
        void SaveCompleted(
            [MarshalAs(UnmanagedType.LPWStr)] string pszFileName);

        //[helpstring("Gets the current name of the file associated with the object")]      
        void GetCurFile(
            [MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
