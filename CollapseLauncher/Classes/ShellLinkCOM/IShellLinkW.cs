using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace CollapseLauncher.ShellLinkCOM
{
    [Guid(CLSIDGuid.Id_ShellLinkIGuid)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
    internal partial interface IShellLinkW
    {
        //[helpstring("Retrieves the path and filename of a shell link object")]
        unsafe void GetPath(
            char* pszFile,
            int cchMaxPath,
            nint pfd,
            uint fFlags);

        //[helpstring("Retrieves the list of shell link item identifiers")]
        void GetIDList(out IntPtr ppidl);

        //[helpstring("Sets the list of shell link item identifiers")]
        void SetIDList(IntPtr pidl);

        //[helpstring("Retrieves the shell link description string")]
        unsafe void GetDescription(
            char* pszFile,
            int cchMaxName);

        //[helpstring("Sets the shell link description string")]
        void SetDescription(
            [MarshalAs(UnmanagedType.LPWStr)] string pszName);

        //[helpstring("Retrieves the name of the shell link working directory")]
        unsafe void GetWorkingDirectory(
            char* pszDir,
            int cchMaxPath);

        //[helpstring("Sets the name of the shell link working directory")]
        void SetWorkingDirectory(
            [MarshalAs(UnmanagedType.LPWStr)] string pszDir);

        //[helpstring("Retrieves the shell link command-line arguments")]
        unsafe void GetArguments(
            char* pszArgs,
            int cchMaxPath);

        //[helpstring("Sets the shell link command-line arguments")]
        void SetArguments(
            [MarshalAs(UnmanagedType.LPWStr)] string pszArgs);

        //[propget, helpstring("Retrieves or sets the shell link hot key")]
        void GetHotkey(out short pwHotkey);
        //[propput, helpstring("Retrieves or sets the shell link hot key")]
        void SetHotkey(short pwHotkey);

        //[propget, helpstring("Retrieves or sets the shell link show command")]
        void GetShowCmd(out uint piShowCmd);
        //[propput, helpstring("Retrieves or sets the shell link show command")]
        void SetShowCmd(uint piShowCmd);

        //[helpstring("Retrieves the location (path and index) of the shell link icon")]
        unsafe void GetIconLocation(
            char* pszIconPath,
            int cchIconPath,
            out int piIcon);

        //[helpstring("Sets the location (path and index) of the shell link icon")]
        void SetIconLocation(
            [MarshalAs(UnmanagedType.LPWStr)] string pszIconPath,
            int iIcon);

        //[helpstring("Sets the shell link relative path")]
        void SetRelativePath(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPathRel,
            uint dwReserved);

        //[helpstring("Resolves a shell link. The system searches for the shell link object and updates the shell link path and its list of identifiers (if necessary)")]
        void Resolve(
            IntPtr hWnd,
            uint fFlags);

        //[helpstring("Sets the shell link path and filename")]
        void SetPath(
            [MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
