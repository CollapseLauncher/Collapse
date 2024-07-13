using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace CollapseLauncher.FileDialogCOM
{
    [Guid(IIDGuid.IShellItemArray)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [GeneratedComInterface]
    internal partial interface IShellItemArray
    {
        // Not supported: IBindCtx
        void BindToHandler(IntPtr pbc, ref Guid rbhid, ref Guid riid, out IntPtr ppvOut);

        void GetPropertyStore(int Flags, ref Guid riid, out IntPtr ppv);

        void GetPropertyDescriptionList(ref PROPERTYKEY keyType, ref Guid riid, out IntPtr ppv);

        void GetAttributes(SIATTRIBFLAGS dwAttribFlags, uint sfgaoMask, out uint psfgaoAttribs);

        void GetCount(out uint pdwNumItems);

        void GetItemAt(uint dwIndex, out IShellItem ppsi);

        void EnumItems(out IntPtr ppenumShellItems);
    }

    [Guid(IIDGuid.IFileOpenDialog)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [GeneratedComInterface]
    internal partial interface IFileOpenDialog
    {
        [PreserveSig]
        int Show(IntPtr parent);

        void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);

        void SetFileTypeIndex(uint iFileType);

        void GetFileTypeIndex(out uint piFileType);

        void Advise(IFileDialogEvents pfde, out uint pdwCookie);

        void Unadvise(uint dwCookie);

        void SetOptions(FOS fos);

        void GetOptions(out FOS pfos);

        void SetDefaultFolder(IShellItem psi);

        void SetFolder(IShellItem psi);

        void GetFolder(out IShellItem ppsi);

        void GetCurrentSelection(out IShellItem ppsi);

        void SetFileName(IntPtr pszName);

        void GetFileName(out IntPtr pszName);

        void SetTitle(IntPtr pszTitle);

        void SetOkButtonLabel(IntPtr pszText);

        void SetFileNameLabel(IntPtr pszLabel);

        void GetResult(out IShellItem ppsi);

        // Argument: IShellItem psi, FileDialogCustomPlace (no longer available) fdcp
        void AddPlace(IShellItem psi, IntPtr fdcp);

        void SetDefaultExtension(IntPtr pszDefaultExtension);

        void Close(int hr);

        void SetClientGuid(ref Guid guid);

        void ClearClientData();

        void SetFilter(IntPtr pFilter);

        void GetResults(out IShellItemArray ppenum);

        void GetSelectedItems(out IShellItemArray ppsai);
    }

    [Guid(IIDGuid.IFileSaveDialog)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [GeneratedComInterface(Options = ComInterfaceOptions.ComObjectWrapper)]
    internal partial interface IFileSaveDialog
    {
        [PreserveSig]
        int Show(IntPtr parent);

        void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);

        void SetFileTypeIndex(uint iFileType);

        void GetFileTypeIndex(out uint piFileType);

        void Advise(IFileDialogEvents pfde, out uint pdwCookie);

        void Unadvise(uint dwCookie);

        void SetOptions(FOS fos);

        void GetOptions(out FOS pfos);

        void SetDefaultFolder(IShellItem psi);

        void SetFolder(IShellItem psi);

        void GetFolder(out IShellItem ppsi);

        void GetCurrentSelection(out IShellItem ppsi);

        void SetFileName(IntPtr pszName);

        void GetFileName(out IntPtr pszName);

        void SetTitle(IntPtr pszTitle);

        void SetOkButtonLabel(IntPtr pszText);

        void SetFileNameLabel(IntPtr pszLabel);

        void GetResult(out IShellItem ppsi);

        // Argument: IShellItem psi, FileDialogCustomPlace (no longer available) fdcp
        void AddPlace(IShellItem psi, IntPtr fdcp);

        void SetDefaultExtension(IntPtr pszDefaultExtension);

        void Close([MarshalAs(UnmanagedType.Error)] int hr);

        void SetClientGuid(ref Guid guid);

        void ClearClientData();

        void SetFilter(IntPtr pFilter);

        void SetSaveAsItem(IShellItem psi);

        void SetProperties(IntPtr pStore);

        void SetCollectedProperties(IntPtr pList, int fAppendDefault);

        void GetProperties(out IntPtr ppStore);

        void ApplyProperties(IShellItem psi, IntPtr pStore, ref IntPtr hwnd, IntPtr pSink);
    }

    [Guid(IIDGuid.IFileDialogEvents)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [GeneratedComInterface]
    internal partial interface IFileDialogEvents; // This dialog is no longer being used

    [Guid(IIDGuid.IShellItem)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [GeneratedComInterface]
    internal partial interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);

        void GetParent(out IShellItem ppsi);

        void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);

        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

        void Compare(IShellItem psi, uint hint, out int piOrder);
    }
}
